using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;
using Login_Administracion_de_Modulos.Areas.Modulo01.Services;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Controllers;

/// <summary>
/// Portada del Módulo 01 (patrón del Módulo 02): UNA sola entrada en el
/// menú global → dashboard con métricas, secciones y accesos a las
/// sub-vistas del equipo (Mi Matrícula, Panel de trabajo, Vacantes).
/// </summary>
[Area("Modulo01")]
[Route("Modulo01")]
public class Modulo01Controller : ModuloBaseController
{
    private readonly IMatriculaServicio _servicioMatricula;
    private readonly IMatriculaturaServicio _servicioMatriculatura;

    public Modulo01Controller(IMatriculaServicio servicioMatricula,
        IMatriculaturaServicio servicioMatriculatura)
    {
        _servicioMatricula = servicioMatricula;
        _servicioMatriculatura = servicioMatriculatura;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        // Puerta por rol: fuera de Alumno/Secretaría/Tesorería/Director/Admin no hay
        // funciones aquí — antes un Docente por URL directa veía el panel con datos
        // de matrículas de terceros (fuga E2E). Ahora recibe la página SinAcceso.
        if (!AccedeAlModulo) return View("SinAcceso");

        ViewData["Title"] = "01. Matrícula Académica";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        ViewData["UsuarioRol"] = UsuarioActualRol;
        // Fix multi-rol: el filtro de tabs usa el ROL ACTIVO (el modo elegido con el selector
        // se guarda en el claim "ActiveRole"), no la lista completa de roles — si no, un
        // multi-rol (p. ej. Director+Alumno) ve pestañas de staff estando en Modo Alumno.
        ViewData["RolesUsuario"] = User.FindFirst("ActiveRole")?.Value ?? UsuarioActualRol;

        var esAlumno = EsAlumno; // ActiveRole-aware: demo 87654321 multi-rol en Modo Alumno

        var vm = new ModeloPanelModulo01Vista
        {
            EsAlumno = esAlumno,
            Resumen = await _servicioMatricula.ObtenerResumenAsync(),
            Ultimas = (await _servicioMatricula.ListarPorPeriodoAsync(null)).Take(5),
            VacantesCriticas = (await _servicioMatricula.ListarVacantesAsync())
                .OrderBy(v => v.Disponibles).Take(4),
            NombreUsuario = UsuarioActualNombre,
            RolUsuario = UsuarioActualRol
        };
        // Mini-dashboard personal del alumno: historial, promedio y estado de SU flujo.
        if (esAlumno)
        {
            var fabrica = HttpContext.RequestServices
                .GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
            using var conn = fabrica.CreateConnection("01");
            var estudianteId = await conn.ExecuteScalarAsync<int?>(
                "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
                new { PersonaId = PersonaActualId ?? 0 }) ?? 0;
            vm.Panel = await _servicioMatriculatura.PanelAlumnoAsync(estudianteId);
        }

        // Resumen POR PUESTO (rol activo) — anti-confusión Tesorería/Secretaría:
        // Tesorería ve el estado económico de las reservas (vouchers CT13),
        // Secretaría ve SU trabajo (cierres de matrícula). Jefaturas: vista general.
        var rolActivo = ViewData["RolesUsuario"]?.ToString() ?? "";
        if (rolActivo.Equals("Tesoreria", StringComparison.OrdinalIgnoreCase))
            vm.ResumenTesoreria = await _servicioMatriculatura.ResumenTesoreriaAsync();
        if (rolActivo.Equals("Secretaria", StringComparison.OrdinalIgnoreCase))
            vm.ResumenSecretaria = await _servicioMatriculatura.ResumenSecretariaAsync();
        return View(vm);
    }
}

/// <summary>ViewModel de la portada del módulo.</summary>
public class ModeloPanelModulo01Vista
{
    public bool EsAlumno { get; set; }
    public ModeloResumenMatricula Resumen { get; set; } = new();
    public IEnumerable<ModeloMatriculaLista> Ultimas { get; set; } = [];
    public IEnumerable<ModeloVacante> VacantesCriticas { get; set; } = [];
    public string NombreUsuario { get; set; } = "";
    public string RolUsuario { get; set; } = "";
    /// <summary>Mini-dashboard personal (solo rol activo Alumno).</summary>
    public ModeloPanelAlumno? Panel { get; set; }
    /// <summary>Panel de vouchers de reserva (solo rol activo Tesorería).</summary>
    public ModeloResumenTesoreriaMatricula? ResumenTesoreria { get; set; }
    /// <summary>Panel de cierre de matrículas (solo rol activo Secretaría).</summary>
    public ModeloResumenSecretariaMatricula? ResumenSecretaria { get; set; }
}
