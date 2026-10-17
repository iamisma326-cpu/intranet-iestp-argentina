using System.Text.Json;
using System.IO;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo09.Models;
using Login_Administracion_de_Modulos.Areas.Modulo09.Services;

namespace Login_Administracion_de_Modulos.Areas.Modulo09.Controllers;

/// <summary>
/// Trámites TUPA del Módulo 09 (Equipo 09).
/// Vistas duales según rol (patrón del prototipo WebForms):
///   Alumno    → "Mis trámites" + nuevo trámite con requisitos dinámicos
///   Secretaría → "Mesa de trámites" (seguimiento y avance del flujo)
/// </summary>
[Area("Modulo09")]
[Route("Modulo09/[controller]")]
public class TramitesController : ModuloBaseController
{
    private readonly ITramiteServicio _servicioTramite;

    public TramitesController(ITramiteServicio servicioTramite)
    {
        _servicioTramite = servicioTramite;
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

    // Mis trámites (alumno) / Mesa de trámites (personal)
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? estado)
    {
        // Puerta por rol (misma regla de la portada): sin funciones aquí → SinAcceso.
        if (!AccedeAlModulo) return View("SinAcceso");

        ViewData["Title"] = "09. Trámites TUPA";
        ViewData["TeamName"] = "Equipo 09";
        bool esParcial = Request.Query.ContainsKey("partial") || Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        ViewData["UsuarioRol"] = UsuarioActualRol;

        // el alumno es un estudiante: obtener su id vía persona → estudiantes
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            var mios = await _servicioTramite.ListarPorEstudianteAsync(estudianteId, estado);
            return VistaModulo("Mis", new ModeloMisTramitesVista
            {
                Tramites = mios,
                Tipos = await _servicioTramite.ListarTiposAsync(),
                FiltroEstado = estado
            });
        }

        var mesa = await _servicioTramite.ListarMesaAsync(estado);
        return View("Mesa", new ModeloMesaTramitesVista
        {
            Tramites = mesa,
            FiltroEstado = estado,
            Pendientes = await _servicioTramite.ContarPendientesAsync()
        });
    }

    // Requisitos dinámicos por tipo (el AutoPostBack del prototipo):
    // devuelve JSON para el select del formulario de nuevo trámite.
    [HttpGet("Requisitos/{tipoCodigo}")]
    public async Task<IActionResult> Requisitos(string tipoCodigo)
    {
        var tipo = await _servicioTramite.ObtenerTipoAsync(tipoCodigo);
        if (tipo == null) return NotFound();
        var requisitos = await _servicioTramite.ListarRequisitosDeTipoAsync(tipoCodigo);
        return Json(new { tipo, requisitos });
    }

    // Ficha de un trámite
    [HttpGet("Detalle/{id}")]
    public async Task<IActionResult> Detalle(int id)
    {
        var t = await _servicioTramite.ObtenerDetalleAsync(id);
        if (t == null) return NotFound();

        // Zero-Blast-Radius de datos: el alumno SOLO puede abrir SU ficha. Antes el id
        // era suficiente y cualquier estudiante veía el trámite (y los requisitos) de otro.
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            if (estudianteId == 0 || t.CodigoEstudiante != await ObtenerCodigoEstudianteAsync(estudianteId))
                return Forbid();
        }
        return View(t);
    }

    /// <summary>Código del estudiante (mod09.tramites.c_estudiante) para cotejar autoría.</summary>
    private async Task<string> ObtenerCodigoEstudianteAsync(int estudianteId)
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        return await conn.ExecuteScalarAsync<string>(
            "SELECT e.codigo_estudiante FROM core.estudiantes e WHERE e.id = @Id;",
            new { Id = estudianteId }) ?? "";
    }

    // ALUMNO: crear trámite (los archivos se adjuntan en el form;
    // en esta fase se registran los requisitos como presentados)
    [HttpPost("Crear")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)] // 10 MB total: PDFs ≤2MB por requisito (regla del prototipo)
    public async Task<IActionResult> Crear(string tipoTramite, string? observaciones)
    {
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            var periodoId = await ObtenerPeriodoActivoIdAsync();

            // archivos por requisito: inputs archivoAdjunto llamados req_<catalogoId> (PDF/JPG/PNG, máx 2MB)
            var archivos = new Dictionary<int, (string Nombre, string Tipo, byte[] Contenido)>();
            foreach (var archivoAdjunto in Request.Form.Files)
            {
                if (archivoAdjunto.Length == 0 || !archivoAdjunto.Name.StartsWith("req_")) continue;
                if (!int.TryParse(archivoAdjunto.Name["req_".Length..], out var reqId)) continue;
                if (archivoAdjunto.Length > 2_097_152)
                {
                    AlertaError($"El archivo '{archivoAdjunto.FileName}' supera los 2 MB permitidos.");
                    return RedirectToAction(nameof(Index));
                }
                using var ms = new MemoryStream();
                await archivoAdjunto.CopyToAsync(ms);
                archivos[reqId] = (archivoAdjunto.FileName, archivoAdjunto.ContentType, ms.ToArray());
            }

            string? datos = null;
            if (!string.IsNullOrWhiteSpace(observaciones))
                datos = JsonSerializer.Serialize(new { observaciones });

            var (ok, mensaje, codigo) = await _servicioTramite.CrearTramiteAsync(
                estudianteId, periodoId, tipoTramite, datos, archivos);

            if (ok) AlertaExito(mensaje);
            else AlertaError(mensaje);
        }
        return RedirectToAction(nameof(Index));
    }

    // ALUMNO: corregir requisito observado (sin crear trámite nuevo)
    [HttpPost("Corregir/{id}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> Corregir(int id, int requisitoId, string? nota)
    {
        // El alumno solo puede corregir requisitos de SU trámite (mismo control que Detalle).
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            var t = await _servicioTramite.ObtenerDetalleAsync(id);
            if (t == null) return NotFound();
            if (estudianteId == 0 || t.CodigoEstudiante != await ObtenerCodigoEstudianteAsync(estudianteId))
                return Forbid();
        }

        // el alumno re-sube el archivo del requisito observado (PDF obligatorio en el prototipo)
        (string Nombre, string Tipo, byte[] Contenido)? archivo = null;
        var archivoAdjunto = Request.Form.Files.FirstOrDefault(f => f.Name == "req_" + requisitoId && f.Length > 0);
        if (archivoAdjunto != null)
        {
            using var ms = new MemoryStream();
            await archivoAdjunto.CopyToAsync(ms);
            archivo = (archivoAdjunto.FileName, archivoAdjunto.ContentType, ms.ToArray());
        }

        var (ok, mensaje) = await _servicioTramite.CorregirRequisitoAsync(
            id, requisitoId, nota ?? "Corregido por el estudiante", archivo);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // PERSONAL (Mesa): ver el archivo adjunto de un requisito del trámite
    // (evidencia para validar/observar). El alumno NO accede por aquí.
    [HttpGet("Archivo/{id}/{requisitoId}")]
    public async Task<IActionResult> Archivo(int id, int requisitoId)
    {
        if (EsAlumno) return Forbid();
        var a = await _servicioTramite.ObtenerArchivoRequisitoAsync(id, requisitoId);
        if (a == null) return NotFound();
        return File(a.Contenido, a.Tipo, a.Nombre);
    }

    // MESA (Secretaría): avanzar el flujo del trámite
    [HttpPost("Avanzar/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avanzar(int id, string nuevoEstado, string? resolucion)
    {
        if (!EsSecretaria && !EsDirector)
            return Forbid();

        var (ok, mensaje) = await _servicioTramite.AvanzarEstadoAsync(
            id, nuevoEstado, resolucion, UsuarioActualId ?? 0);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // Helpers de contexto
    private async Task<int> ObtenerEstudianteIdAsync()
    {
        // claim PersonaId → core.estudiantes (vía la connection fabrica del módulo 09)
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
            new { PersonaId = PersonaActualId ?? 0 });
        return id ?? 0;
    }

    private async Task<int> ObtenerPeriodoActivoIdAsync()
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        return await conn.ExecuteScalarAsync<int>(
            "SELECT id FROM core.periodos_academicos WHERE es_activo ORDER BY id DESC LIMIT 1;");
    }
}

// ViewModels
public class ModeloMisTramitesVista
{
    public IEnumerable<ModeloTramiteLista> Tramites { get; set; } = [];
    public IEnumerable<ModeloTipoTramite> Tipos { get; set; } = [];
    public string? FiltroEstado { get; set; }
}

public class ModeloMesaTramitesVista
{
    public IEnumerable<ModeloTramiteMesa> Tramites { get; set; } = [];
    public string? FiltroEstado { get; set; }
    public int Pendientes { get; set; }
}
