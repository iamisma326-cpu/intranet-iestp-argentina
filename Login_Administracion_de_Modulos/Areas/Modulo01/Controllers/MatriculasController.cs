using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;
using Login_Administracion_de_Modulos.Areas.Modulo01.Services;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Controllers;

/// <summary>
/// Matrícula Académica — proceso "Cero Filas" del Equipo 01.
/// Vistas duales según rol (patrón del prototipo WebForms):
///   Alumno → "Mi matrícula" (avance del proceso)
///   Secretaría/Admin → puesto de trabajo con las 4 zonas
/// </summary>
[Area("Modulo01")]
[Route("Modulo01/[controller]")]
public class MatriculasController : ModuloBaseController
{
    private readonly IMatriculaServicio _servicioMatricula;

    public MatriculasController(IMatriculaServicio servicioMatricula)
    {
        _servicioMatricula = servicioMatricula;
    }

    /// <summary>Expone los roles del usuario a las vistas (tabs rol-aware).</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        // Fix multi-rol: el filtro de tabs usa el ROL ACTIVO (el modo elegido con el selector
        // se guarda en el claim "ActiveRole"), no la lista completa de roles — si no, un
        // multi-rol (p. ej. Director+Alumno) ve pestañas de staff estando en Modo Alumno.
        ViewData["RolesUsuario"] = User.FindFirst("ActiveRole")?.Value ?? UsuarioActualRol;
        base.OnActionExecuting(context);
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? estado)
    {
        // Puerta por rol (misma regla de la portada): sin funciones aquí → SinAcceso.
        if (!AccedeAlModulo) return View("SinAcceso");

        ViewData["Title"] = "01. Matrícula Académica";
        ViewData["TeamName"] = "Equipo 01";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        ViewData["UsuarioRol"] = UsuarioActualRol;

        if (EsAlumno)
        {
            var activa = await _servicioMatricula.ObtenerMatriculaActivaAsync(await ObtenerEstudianteIdAsync());
            return VistaModulo("MiMatricula", activa);
        }

        // Vista personal (Secretaría / Admin / Tesorería)
        var modelo = new ModeloPanelMatriculasVista
        {
            Resumen = await _servicioMatricula.ObtenerResumenAsync(),
            ListasParaRegistrar = await _servicioMatricula.ListarListasParaRegistrarAsync(),
            EnEspera = await _servicioMatricula.ListarEnEsperaAsync(),
            PorLiberar = await _servicioMatricula.ListarVacantesPorLiberarAsync(),
            Todas = await _servicioMatricula.ListarPorPeriodoAsync(estado),
            FiltroEstado = estado
        };
        return VistaModulo("Panel", modelo);
    }

    /// <summary>Ruta explícita de "Mi Matrícula" (tab del alumno).</summary>
    [HttpGet("MiMatricula")]
    public async Task<IActionResult> MiMatricula()
    {
        // Ruta del alumno: el personal no tiene una "mi matrícula" propia. Antes cualquier
        // rol autenticado caía aquí y veía el registro de un estudiante arbitrario.
        if (!EsAlumno) return NotFound();

        ViewData["Title"] = "Mi Matrícula";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        var activa = await _servicioMatricula.ObtenerMatriculaActivaAsync(await ObtenerEstudianteIdAsync());
        return VistaModulo("MiMatricula", activa);
    }

    // Validaciones (Bandejas)
    [HttpPost("ValidarVoucher/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarVoucher(int id, bool aprobar = true)
    {
        if (!EsTesoreria && !EsDirector)
            return Forbid();

        var ok = await _servicioMatricula.ValidarVoucherAsync(id, aprobar);
        if (ok) AlertaExito(aprobar
            ? "Voucher validado. Si la foto ya está validada, la matrícula pasa a conformidad."
            : "Voucher rechazado: queda pendiente de corrección del estudiante.");
        else AlertaError("No se pudo actualizar el voucher (¿matrícula ya cerrada?).");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ValidarFoto/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidarFoto(int id, bool aprobar = true)
    {
        if (!EsSecretaria && !EsDirector)
            return Forbid();

        var ok = await _servicioMatricula.ValidarFotoAsync(id, aprobar);
        if (ok) AlertaExito(aprobar
            ? "Foto validada. Si el voucher ya está validado, la matrícula pasa a conformidad."
            : "Foto rechazada: queda pendiente de que el estudiante la corrija.");
        else AlertaError("No se pudo actualizar la foto (¿matrícula ya cerrada?).");
        return RedirectToAction(nameof(Index));
    }

    // Conformidad: registrar matrícula (consume vacante, atómico)
    [HttpPost("Registrar/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(int id)
    {
        if (!EsSecretaria && !EsAdmin)
            return Forbid();

        var (ok, mensaje) = await _servicioMatricula.RegistrarMatriculaAsync(id, UsuarioActualId ?? 0);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // Tablero de vacantes
    [HttpGet("Vacantes")]
    public async Task<IActionResult> Vacantes()
    {
        // Ruta del personal: el alumno no ve cifras globales de vacantes (regla del módulo).
        // Mismo tratamiento que Index: se le muestra su propia matrícula.
        if (EsAlumno)
        {
            var activa = await _servicioMatricula.ObtenerMatriculaActivaAsync(await ObtenerEstudianteIdAsync());
            return VistaModulo("MiMatricula", activa);
        }

        ViewData["Title"] = "01. Vacantes por Carrera";
        var vacantes = await _servicioMatricula.ListarVacantesAsync();
        return View(vacantes);
    }

    // Helpers de contexto
    private async Task<int> ObtenerEstudianteIdAsync()
    {
        // claim PersonaId → core.estudiantes (UsuarioActualId es id de USUARIO, no de estudiante)
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("01");
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
            new { PersonaId = PersonaActualId ?? 0 });
        return id ?? 0;
    }
}

/// <summary>ViewModel del puesto de trabajo del personal.</summary>
public class ModeloPanelMatriculasVista
{
    public ModeloResumenMatricula Resumen { get; set; } = new();
    public IEnumerable<ModeloMatriculaLista> ListasParaRegistrar { get; set; } = [];
    public IEnumerable<ModeloMatriculaLista> EnEspera { get; set; } = [];
    public IEnumerable<ModeloMatriculaPorLiberar> PorLiberar { get; set; } = [];
    public IEnumerable<ModeloMatriculaLista> Todas { get; set; } = [];
    public string? FiltroEstado { get; set; }
}
