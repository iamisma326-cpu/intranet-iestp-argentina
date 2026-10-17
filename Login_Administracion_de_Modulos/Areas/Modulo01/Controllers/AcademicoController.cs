using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Services;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Controllers;

/// <summary>
/// Pestaña "Académico" del alumno (Equipo 01): rendimiento por ciclo,
/// record académico por período (formato SIGA) y resumen global.
/// Ruta EXCLUSIVA del alumno — el personal tiene su herramienta en Matriculatura
/// (expediente con historial completo por DNI).
/// </summary>
[Area("Modulo01")]
[Route("Modulo01/[controller]")]
public class AcademicoController : ModuloBaseController
{
    private readonly AcademicoServicio _servicio;

    public AcademicoController(AcademicoServicio servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Expone el rol activo a las vistas (tabs rol-aware, fix multi-rol).</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        ViewData["RolesUsuario"] = User.FindFirst("ActiveRole")?.Value ?? UsuarioActualRol;
        base.OnActionExecuting(context);
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? ciclo)
    {
        // Puerta por rol (misma regla de la portada).
        if (!AccedeAlModulo) return View("SinAcceso");

        // Ruta del alumno: el personal no tiene "mi académico" propio (Matriculatura ya
        // trae el historial completo por DNI). Antes de este guard, cualquier rol
        // autenticado caía aquí y veía el record de un estudiante arbitrario.
        if (!EsAlumno) return NotFound();

        ViewData["Title"] = "Mi Académico";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        ViewData["UsuarioRol"] = UsuarioActualRol;

        // claim PersonaId → core.estudiantes (UsuarioActualId es id de USUARIO, no de estudiante)
        // Patrón probado del módulo (MatriculasController.MiMatricula): PersonaActualId del base.
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using (var conn = fabrica.CreateConnection("01"))
        {
            var estudianteId = await conn.ExecuteScalarAsync<int?>(
                "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
                new { PersonaId = PersonaActualId ?? 0 }) ?? 0;

            var record = await _servicio.RecordPorCicloAsync(estudianteId);

            // Filtro por ciclo: el pedido si existe en el record, si no el último cursado.
            CicloAcademico seleccionado;
            if (!string.IsNullOrEmpty(ciclo) && record.Any(c => c.Ciclo == ciclo))
            {
                seleccionado = record.First(c => c.Ciclo == ciclo);
            }
            else
            {
                seleccionado = record.LastOrDefault() ?? new CicloAcademico();
            }

            var modelo = new AcademicoVista
            {
                Resumen = await _servicio.ResumenAcademicoAsync(estudianteId),
                Rendimiento = await _servicio.RendimientoPorCicloAsync(estudianteId),
                Record = record,
                CicloSeleccionado = seleccionado,
                EstudianteNombre = UsuarioActualNombre
            };
            return VistaModulo("Academico", modelo);
        }
    }
}
