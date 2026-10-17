using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Login_Administracion_de_Modulos.Areas.Modulo00;

/// <summary>
/// Base de los módulos portados (01 y 09): decide por el ROL ACTIVO (claim
/// "RolActivo" de la cookie del login), no por la lista completa de roles.
/// Administra el sombrero como superusuario. Nombres de rol según el catálogo
/// real de core.roles de esta propuesta ("Secretaría Académica", "Tesorería", ...).
/// </summary>
public abstract class ModuloBaseController : Controller
{
    /// <summary>Rol activo (sombrero). Fuente única para vistas y guards.</summary>
    public string UsuarioActualRol =>
        User.FindFirst("rol")?.Value
        ?? User.FindFirst("rol")?.Value ?? User.FindFirst("RolActivo")?.Value
        ?? User.FindFirst(ClaimTypes.Role)?.Value ?? "Alumno";

    public int? UsuarioActualId =>
        int.TryParse(User.FindFirst("uid")?.Value ?? User.FindFirst("UsuarioId")?.Value, out var u) ? u : null;

    public int? PersonaActualId =>
        int.TryParse(User.FindFirst("pid")?.Value ?? User.FindFirst("PersonaId")?.Value, out var p) ? p : null;

    public string UsuarioActualNombre =>
        User.FindFirst("n")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Invitado";

    // Sombrero manda: SOLO el rol activo decide. Administrador = superusuario.
    public bool EsAdmin => UsuarioActualRol.Equals("Administrador", StringComparison.OrdinalIgnoreCase)
                        || UsuarioActualRol.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool EsDirector => EsAdmin || UsuarioActualRol.Equals("Director", StringComparison.OrdinalIgnoreCase);
    public bool EsSecretaria => EsAdmin || UsuarioActualRol.Contains("Secretar", StringComparison.OrdinalIgnoreCase);
    public bool EsTesoreria => EsAdmin || UsuarioActualRol.Contains("Tesorer", StringComparison.OrdinalIgnoreCase);
    public bool EsAlumno => UsuarioActualRol.Equals("Alumno", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿El rol ACTIVO tiene funciones en este módulo? Un rol fuera de la lista
    /// recibe 403 (API) o la vista SinAcceso (navegación).
    /// </summary>
    public bool AccedeAlModulo => EsAlumno || EsSecretaria || EsTesoreria || EsDirector || EsAdmin;

    /// <summary>
    /// Devuelve la vista como parcial (fetch de los paneles) o completa (navegación
    /// directa con el layout mínimo). El nombre de vista es relativo al controller.
    /// </summary>
    protected IActionResult VistaModulo(string vista, object modelo)
    {
        bool parcial = Request.Query.ContainsKey("partial")
            || Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        return parcial ? PartialView(vista, modelo) : View(vista, modelo);
    }

    // Sustitutos de MostrarAlerta* del core original: guardan en TempData; el panel
    // actual no renderiza toasts, así que son inofensivos.
    protected void AlertaExito(string m) => TempData["AlertaExito"] = m;
    protected void AlertaError(string m) => TempData["AlertaError"] = m;
    protected void AlertaInfo(string m) => TempData["AlertaInfo"] = m;

    /// <summary>ID de estudiante del usuario activo (claim PersonaId → core.estudiantes).</summary>
    public async Task<int> ObtenerEstudianteIdAsync(IModuleDbConnectionFactory fabrica)
    {
        if (PersonaActualId is not int pid) return 0;
        using var conn = fabrica.CreateConnection("01");
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
            new { PersonaId = pid });
        return id ?? 0;
    }
}
