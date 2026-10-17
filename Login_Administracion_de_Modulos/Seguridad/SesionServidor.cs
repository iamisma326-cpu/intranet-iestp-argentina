using System.Security.Claims;
using Login_Administracion_de_Modulos.Seguridad;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Login_Administracion_de_Modulos.Seguridad;

/// <summary>
/// Sesión server-side con cookie CORTA "sid" (32 chars).
///
/// Motivo: el reloj del frontal de MonsterASP está ~1 mes adelantado y su proxy descarta
/// hacia el navegador los Set-Cookie que llevan 'expires' (los ve como cookies vencidas:
/// expires hoy+8h queda en el pasado de SU reloj). Solo pasan las cookies de sesión SIN
/// expires — como la antiforgery. Por eso la autenticación por cookie persistente de
/// ASP.NET Core jamás llegaba al navegador. Aquí la cookie es de sesión (sin expires) y
/// la validez real vive en core.sesiones.expira_en (BD), inmune al reloj del hosting.
///
/// Flujo:
///   - IniciarSesionAsync: persiste los claims en core.sesiones y setea cookie "sid".
///   - CargarSesionAsync: middleware en cada request — lee "sid", busca el payload
///     y reconstruye el ClaimsPrincipal (rol activo, nombre, ids).
///   - CerrarSesionAsync: borra la fila + cookie.
/// </summary>
public static class SesionServidor
{
    private const string CookieName = ".AspNetCore.Sid";
    private const int HorasValidez = 8;

    /// <summary>Claims que se persisten: n=nombre, uid, pid, rol activo.</summary>
    public sealed record SesionPayload(int UsuarioId, int PersonaId, string Nombre, string RolActivo);

    public static async Task IniciarSesionAsync(HttpContext context, IConfiguration configuration,
        SesionPayload payload)
    {
        string sid = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=').Replace('+', '-').Replace('/', '_')
            + Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace('+', '-').Replace('/', '_')[..8];

        await using var conn = new NpgsqlConnection(ConfiguracionSegura.ObtenerCadenaConexion(configuration));
        await conn.OpenAsync();
        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO core.sesiones (sid, payload, expira_en)
            VALUES (@sid, @payload::jsonb, now() + interval '8 hours');
            DELETE FROM core.sesiones WHERE expira_en < now();", conn))
        {
            cmd.Parameters.AddWithValue("sid", sid);
            cmd.Parameters.AddWithValue("payload",
                $$"""{"uid": {{payload.UsuarioId}}, "pid": {{payload.PersonaId}}, "n": {{System.Text.Json.JsonSerializer.Serialize(payload.Nombre)}}, "rol": {{System.Text.Json.JsonSerializer.Serialize(payload.RolActivo)}}}""");
            await cmd.ExecuteNonQueryAsync();
        }

        // Cookie corta y SIN 'expires': es una cookie de sesión del navegador (vive hasta
        // cerrar la pestaña). La validez REAL la controla core.sesiones.expira_en (BD).
        // Motivo: el reloj del frontal de MonsterASP está ~1 mes adelantado y su proxy
        // descarta los Set-Cookie cuyo 'expires' queda en el pasado según SU reloj —
        // por eso ninguna cookie persistente llegaba al navegador y solo la antiforgery
        // (cookie de sesión, sin expires) pasaba.
        context.Response.Cookies.Append(CookieName, sid, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Path = "/",
            IsEssential = true
        });
    }

    /// <summary>Reconstruye el ClaimsPrincipal desde la cookie "sid". Null si no hay sesión válida.</summary>
    public static async Task<ClaimsPrincipal?> CargarSesionAsync(HttpContext context, IConfiguration configuration)
    {
        string? sid = context.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(sid)) return null;

        try
        {
            await using var conn = new NpgsqlConnection(ConfiguracionSegura.ObtenerCadenaConexion(configuration));
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT payload::text FROM core.sesiones WHERE sid = @sid AND expira_en > now()", conn);
            cmd.Parameters.AddWithValue("sid", sid);
            var raw = await cmd.ExecuteScalarAsync() as string;
            if (string.IsNullOrWhiteSpace(raw)) return null;

            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var root = doc.RootElement;

            int uid = root.GetProperty("uid").GetInt32();
            int pid = root.GetProperty("pid").GetInt32();
            string nombre = root.GetProperty("n").GetString() ?? "Usuario Institucional";
            string rol = root.GetProperty("rol").GetString() ?? "Alumno";

            var claims = new List<Claim>
            {
                new("n", nombre),
                new("uid", uid.ToString()),
                new("pid", pid.ToString()),
                new("rol", rol),
                new("r", rol)
            };
            var identity = new ClaimsIdentity(claims, "SesionServidor", nameType: "n", roleType: "r");
            return new ClaimsPrincipal(identity);
        }
        catch
        {
            return null; // BD caída o payload corrupto: sin sesión
        }
    }

    public static async Task CerrarSesionAsync(HttpContext context, IConfiguration configuration)
    {
        string? sid = context.Request.Cookies[CookieName];
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        if (string.IsNullOrWhiteSpace(sid)) return;

        try
        {
            await using var conn = new NpgsqlConnection(ConfiguracionSegura.ObtenerCadenaConexion(configuration));
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM core.sesiones WHERE sid = @sid", conn);
            cmd.Parameters.AddWithValue("sid", sid);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* mejor esfuerzo */ }
    }
}
