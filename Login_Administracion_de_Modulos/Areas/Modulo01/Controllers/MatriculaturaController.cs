using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;
using Login_Administracion_de_Modulos.Areas.Modulo01.Services;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Controllers;

/// <summary>
/// Matriculatura de Secretaría — cierre del flujo real "Reserva de Matrícula":
/// el alumno inició el trámite TM05, pagó el CT13 y Tesorería validó su voucher.
/// Aquí Secretaría busca al alumno por DNI, revisa su expediente (carrera,
/// historial, condición), elige las UDs del nuevo ciclo, cierra la matrícula
/// (consume vacante) y emite la ficha PDF que se envía al correo del estudiante.
/// </summary>
[Area("Modulo01")]
[Route("Modulo01/[controller]")]
public class MatriculaturaController : ModuloBaseController
{
    private readonly IMatriculaturaServicio _matriculatura;
    private readonly IServicioCorreoFicha _emailFicha;

    public MatriculaturaController(IMatriculaturaServicio matriculatura, IServicioCorreoFicha emailFicha)
    {
        _matriculatura = matriculatura;
        _emailFicha = emailFicha;
    }

    /// <summary>Expone los roles del usuario a las vistas (tabs rol-aware).</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        ViewData["RolesUsuario"] = User.FindFirst("ActiveRole")?.Value ?? UsuarioActualRol;
        base.OnActionExecuting(context);
    }

    // Puesto de trabajo: búsqueda por DNI
    [HttpGet("")]
    [HttpGet("Index")]
    public IActionResult Index(string? dni)
    {
        // Puerta por rol (misma regla de la portada): sin funciones aquí → SinAcceso.
        if (!AccedeAlModulo) return View("SinAcceso");
        if (!EsSecretaria && !EsDirector) return Forbid();
        ViewData["Title"] = "01. Matriculatura (Secretaría)";
        return VistaModulo("Buscar", new ModeloBusquedaMatriculaturaVista { Dni = dni ?? "" });
    }

    /// <summary>Busca el expediente del alumno por DNI y muestra su situación.</summary>
    [HttpGet("Expediente")]
    public async Task<IActionResult> Expediente(string dni)
    {
        if (!EsSecretaria && !EsDirector) return Forbid();
        if (string.IsNullOrWhiteSpace(dni))
        {
            AlertaError("Ingresa un DNI para buscar.");
            return RedirectToAction(nameof(Index));
        }

        var expediente = await _matriculatura.BuscarPorDniAsync(dni);
        if (expediente == null)
        {
            AlertaError($"No se encontró estudiante con DNI {dni}.");
            return RedirectToAction(nameof(Index));
        }

        var turnosTipos = await ObtenerTurnosTiposAsync();
        var vm = new ModeloExpedienteMatriculaturaVista
        {
            Expediente = expediente,
            Turnos = turnosTipos.Turnos,
            TiposMatricula = turnosTipos.Tipos
        };
        return VistaModulo("Expediente", vm);
    }

    // Cierre de la matrícula (transaccional: consume vacante)
    [HttpPost("Matricular")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Matricular(ModeloFormularioMatricularVista form)
    {
        if (!EsSecretaria && !EsDirector) return Forbid();

        if (form.UnidadesDidacticasIds is not { Count: > 0 })
        {
            AlertaError("Selecciona al menos una unidad didáctica.");
            return RedirectToAction(nameof(Expediente), new { dni = form.Dni });
        }

        // La reserva debe pertenecer al estudiante buscado (anti-suplantación)
        var expedienteChk = await _matriculatura.BuscarPorDniAsync(form.Dni);
        if (expedienteChk?.Reserva == null
            || (form.MatriculaId != null && expedienteChk.Reserva.MatriculaId != form.MatriculaId)
            || (form.TramiteId != null && expedienteChk.Reserva.TramiteId != form.TramiteId))
        {
            AlertaError("La reserva indicada no corresponde al estudiante.");
            return RedirectToAction(nameof(Expediente), new { dni = form.Dni });
        }

        var cmd = new MatricularComando(
            form.Dni, form.MatriculaId, form.TramiteId, form.CicloProximo ?? "",
            form.TurnoId, form.TipoMatriculaId,
            form.Condicion ?? "", form.CursosDesaprobadosNombres ?? "",
            form.UnidadesDidacticasIds);
        var (ok, mensaje, matriculaId) = await _matriculatura.MatricularAsync(cmd, UsuarioActualId ?? 0);
        if (!ok)
        {
            AlertaError(mensaje);
            return RedirectToAction(nameof(Expediente), new { dni = form.Dni });
        }

        // Ficha PDF + correo al estudiante (bitácora en fichas_enviadas)
        var ficha = await _matriculatura.ObtenerFichaAsync(matriculaId);
        string? enviadoMsg = null;
        if (ficha != null)
        {
            var (emailInst, emailPers) = await ObtenerCorreosAsync(ficha.CodigoEstudiante);
            var pdf = FichaMatriculaPdf.Generar(ficha);
            var (envOk, envMsg) = await _emailFicha.EnviarFichaAsync(
                matriculaId, emailInst, emailPers, pdf, ficha.CodigoMatricula, UsuarioActualId ?? 0);
            enviadoMsg = envMsg;
        }

        AlertaExito(mensaje + " " + (enviadoMsg ?? ""));
        return RedirectToAction(nameof(Expediente), new { dni = form.Dni });
    }

    /// <summary>Descarga la ficha PDF de una matrícula cerrada.</summary>
    [HttpGet("Ficha/{id}")]
    public async Task<IActionResult> Ficha(int id)
    {
        if (!EsSecretaria && !EsDirector) return Forbid();
        var ficha = await _matriculatura.ObtenerFichaAsync(id);
        if (ficha == null) return NotFound();
        var pdf = FichaMatriculaPdf.Generar(ficha);
        return File(pdf, "application/pdf", $"Ficha-{ficha.CodigoMatricula}.pdf");
    }

    /// <summary>Reenvía la ficha al correo del estudiante (ya cerrada).</summary>
    [HttpPost("Reenviar/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reenviar(int id)
    {
        if (!EsSecretaria && !EsDirector) return Forbid();
        var ficha = await _matriculatura.ObtenerFichaAsync(id);
        if (ficha == null) return NotFound();

        var (emailInst, emailPers) = await ObtenerCorreosAsync(ficha.CodigoEstudiante);
        var pdf = FichaMatriculaPdf.Generar(ficha);
        var (ok, mensaje) = await _emailFicha.EnviarFichaAsync(
            id, emailInst, emailPers, pdf, ficha.CodigoMatricula, UsuarioActualId ?? 0);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Expediente), new { dni = ficha.Dni });
    }

    // Helpers de contexto
    private async Task<(IEnumerable<ModeloTurno> Turnos, IEnumerable<ModeloTipoMatricula> Tipos)> ObtenerTurnosTiposAsync()
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("01");
        var turnos = await conn.QueryAsync<ModeloTurno>(
            "SELECT id AS Id, nombre AS Nombre FROM turnos ORDER BY id;");
        var tipos = await conn.QueryAsync<ModeloTipoMatricula>(
            "SELECT id AS Id, nombre AS Nombre FROM tipos_matricula ORDER BY id;");
        return (turnos, tipos);
    }

    private async Task<(string? Inst, string? Pers)> ObtenerCorreosAsync(string codigoEstudiante)
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("01");
        var row = await conn.QueryFirstOrDefaultAsync<(string?, string?)>(
            """
            SELECT u.email, p.email_personal
            FROM estudiantes e
            JOIN personas p ON p.id = e.persona_id
            LEFT JOIN usuarios u ON u.persona_id = p.id
            WHERE e.codigo_estudiante = @Cod;
            """, new { Cod = codigoEstudiante });
        return row;
    }
}

// ViewModels de las vistas de Secretaría

/// <summary>Pantalla de búsqueda por DNI.</summary>
public class ModeloBusquedaMatriculaturaVista
{
    public string Dni { get; set; } = "";
}

/// <summary>Expediente del alumno listo para cerrar su matrícula.</summary>
public class ModeloExpedienteMatriculaturaVista
{
    public ModeloExpedienteMatricula Expediente { get; set; } = new();
    public IEnumerable<ModeloTurno> Turnos { get; set; } = [];
    public IEnumerable<ModeloTipoMatricula> TiposMatricula { get; set; } = [];
}

/// <summary>Form del cierre de matrícula (checkboxes de UDs).</summary>
public class ModeloFormularioMatricularVista
{
    public string Dni { get; set; } = "";
    public int? MatriculaId { get; set; }
    public int? TramiteId { get; set; }
    public string? CicloProximo { get; set; }
    public int TurnoId { get; set; }
    public int TipoMatriculaId { get; set; }
    public string? Condicion { get; set; }
    public string? CursosDesaprobadosNombres { get; set; }
    public List<string>? UnidadesDidacticasIds { get; set; }
}

/// <summary>Turno para el select de la vista.</summary>
public class ModeloTurno
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}

/// <summary>Tipo de matrícula para el select de la vista.</summary>
public class ModeloTipoMatricula
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
}
