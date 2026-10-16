using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Login_Administracion_de_Modulos.Seguridad;

namespace Login_Administracion_de_Modulos.Controllers
{
    /// <summary>
    /// Autenticación institucional por DNI + contraseña, con soporte de Google SSO.
    ///
    /// CONTRATO DE SESIÓN (válido para TODOS los módulos):
    ///   - La sesión se firma con cookies de claims (SignInAsync). HttpContext.Session
    ///     NO autentica a nadie; queda solo para datos de presentación.
    ///   - Claims disponibles en toda la app:
    ///       UsuarioId          → id de core.usuarios
    ///       PersonaId          → id de core.personas (la FK que usan los módulos)
    ///       RolActivo          → el rol con el que está operando (el "sombrero")
    ///       ClaimTypes.Role    → TODOS los roles del usuario
    ///   - Contraseñas: SOLO hash PBKDF2$... o BCrypt ($2a/$2b/$2y).
    ///     El texto plano de desarrollo ya NO es aceptado.
    /// </summary>
    public class LoginController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<LoginController> _logger;
        private readonly IWebHostEnvironment _env;

        public LoginController(IConfiguration configuration, ILogger<LoginController> logger, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Si ya hay sesión activa, directo al panel de su rol
            if (User.Identity?.IsAuthenticated == true)
            {
                return Redirect(ObtenerRutaDashboardPorRol(User.FindFirst("RolActivo")?.Value ?? string.Empty));
            }
            return View("~/Views/Index.cshtml");
        }

        [HttpGet("Error")]
        public IActionResult Error()
        {
            ViewBag.Error = "Ocurrió un error inesperado al procesar la solicitud.";
            return View("~/Views/Index.cshtml");
        }

        // ==========================================
        // RUTA 1: Formulario Clásico por DNI
        // ==========================================
        [HttpPost("login-dni")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginDni(string dni, string clave)
        {
            string connectionString = ConfiguracionSegura.ObtenerCadenaConexion(_configuration);

            if (string.IsNullOrEmpty(connectionString))
            {
                ViewBag.Error = "La base de datos no está configurada. Cree el archivo .env con DB_CONNECTION_STRING.";
                return View("~/Views/Index.cshtml");
            }

            if (string.IsNullOrWhiteSpace(dni) || string.IsNullOrWhiteSpace(clave))
            {
                ViewBag.Error = "Debe ingresar su DNI y contraseña.";
                return View("~/Views/Index.cshtml");
            }

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                // 1. Usuario por DNI/correo, con persona. (Esquema core — modelo
                //    institucional compartido por todos los módulos.)
                const string query = @"
                    SELECT u.id_usuario, u.clave_cifrada, u.estado,
                           p.nombres, p.apellidos, p.id_persona
                    FROM core.usuarios u
                    JOIN core.personas p ON p.id_persona = u.id_persona
                    WHERE p.numero_documento = @dni OR LOWER(u.correo) = LOWER(@dni)
                    LIMIT 1;";

                int idUsuario = 0, idPersona = 0;
                string? claveCifradaDb = null;
                string nombres = "Personal", apellidos = "Institucional";
                bool encontrado = false, estadoActivo = false;

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@dni", dni.Trim());
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        encontrado = true;
                        idUsuario = reader.GetInt32(0);
                        claveCifradaDb = reader.IsDBNull(1) ? null : reader.GetString(1);
                        estadoActivo = string.Equals(reader.IsDBNull(2) ? "Activo" : reader.GetString(2), "Activo", StringComparison.OrdinalIgnoreCase);
                        nombres = reader.IsDBNull(3) ? "Personal" : reader.GetString(3);
                        apellidos = reader.IsDBNull(4) ? "Institucional" : reader.GetString(4);
                        idPersona = reader.GetInt32(5);
                    }
                }

                if (!encontrado)
                {
                    await RegistrarIntentoAsync(connection, dni, null, false, "Usuario no encontrado");
                    ViewBag.Error = "DNI no encontrado o usuario inactivo.";
                    return View("~/Views/Index.cshtml");
                }

                if (!estadoActivo)
                {
                    await RegistrarIntentoAsync(connection, dni, idUsuario, false, "Usuario inactivo");
                    ViewBag.Error = "Su cuenta de usuario no se encuentra activa en el sistema.";
                    return View("~/Views/Index.cshtml");
                }

                // 2. Cuentas SSO-only: sin contraseña local no se entra por DNI.
                if (string.IsNullOrEmpty(claveCifradaDb))
                {
                    await RegistrarIntentoAsync(connection, dni, idUsuario, false, "Cuenta SSO sin contraseña");
                    ViewBag.Error = "Esta cuenta utiliza acceso institucional sin contraseña. Ingrese con el botón 'Continuar con Google'.";
                    return View("~/Views/Index.cshtml");
                }

                // 3. Validar contraseña — SOLO formatos hasheados. Sin legacy plano.
                bool esValida = PasswordSecurity.VerificarClave(clave, claveCifradaDb, permitirLegacyDesarrollo: false);

                if (!esValida)
                {
                    await RegistrarIntentoAsync(connection, dni, idUsuario, false, "Contraseña incorrecta");
                    ViewBag.Error = "Contraseña incorrecta.";
                    return View("~/Views/Index.cshtml");
                }

                // 4. Login válido: auditar, cargar roles y firmar la sesión de claims.
                await RegistrarIntentoAsync(connection, dni, idUsuario, true, "Autenticación exitosa");
                await RegistrarSesionAsync(connection, idUsuario, "DNI_LOCAL");

                var roles = await ListarRolesAsync(connection, idUsuario);
                string rolPrincipal = roles.FirstOrDefault() ?? "Alumno";
                string nombreCompleto = $"{nombres} {apellidos}".Trim();

                await FirmarSesionAsync(idUsuario, idPersona, nombreCompleto, rolPrincipal, roles, esDemo: false);

                return Redirect(ObtenerRutaDashboardPorRol(rolPrincipal));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el inicio de sesión por DNI");
                ViewBag.Error = "Ocurrió un error al procesar el inicio de sesión. Por favor, inténtelo nuevamente más tarde.";
                return View("~/Views/Index.cshtml");
            }
        }

        // ==========================================
        // RUTA 2: SSO con Google (Sin contraseña)
        // ==========================================
        [HttpGet("login-google-oauth")]
        public IActionResult LoginGoogleOAuth()
        {
            // Si Google no está configurado (falta .env), mensaje claro.
            var clientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("CONFIGURAR"))
            {
                ViewBag.Error = "El inicio de sesión con Google institucional aún no está configurado. Ingrese con su DNI y contraseña.";
                return View("~/Views/Index.cshtml");
            }

            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
            return Challenge(properties, Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                ViewBag.Error = "No se pudo autenticar con Google.";
                return View("~/Views/Index.cshtml");
            }

            var correo = result.Principal?.Identities.FirstOrDefault()?
                .Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

            if (string.IsNullOrEmpty(correo))
            {
                ViewBag.Error = "No se pudo obtener el correo electrónico de Google.";
                return View("~/Views/Index.cshtml");
            }

            return await ProcessGoogleLogin(correo);
        }

        private async Task<IActionResult> ProcessGoogleLogin(string correo)
        {
            string connectionString = ConfiguracionSegura.ObtenerCadenaConexion(_configuration);

            if (string.IsNullOrEmpty(connectionString))
            {
                ViewBag.Error = "La base de datos no está configurada para Google SSO.";
                return View("~/Views/Index.cshtml");
            }

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT u.id_usuario, u.estado, p.nombres, p.apellidos, p.id_persona
                    FROM core.usuarios u
                    JOIN core.personas p ON p.id_persona = u.id_persona
                    WHERE LOWER(u.correo) = LOWER(@correo)
                    LIMIT 1;";

                int idUsuario = 0, idPersona = 0;
                string nombres = "Personal", apellidos = "Institucional";
                bool encontrado = false, estadoActivo = false;

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@correo", correo.Trim());
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        encontrado = true;
                        idUsuario = reader.GetInt32(0);
                        estadoActivo = string.Equals(reader.IsDBNull(1) ? "Activo" : reader.GetString(1), "Activo", StringComparison.OrdinalIgnoreCase);
                        nombres = reader.IsDBNull(2) ? "Personal" : reader.GetString(2);
                        apellidos = reader.IsDBNull(3) ? "Institucional" : reader.GetString(3);
                        idPersona = reader.GetInt32(4);
                    }
                }

                if (!encontrado)
                {
                    ViewBag.Error = "El correo institucional no está registrado en el padrón o el usuario está inactivo.";
                    return View("~/Views/Index.cshtml");
                }

                if (!estadoActivo)
                {
                    ViewBag.Error = "Su cuenta de usuario no se encuentra activa en el sistema.";
                    return View("~/Views/Index.cshtml");
                }

                await RegistrarSesionAsync(connection, idUsuario, "GOOGLE_SSO");

                var roles = await ListarRolesAsync(connection, idUsuario);
                string rolPrincipal = roles.FirstOrDefault() ?? "Alumno";
                string nombreCompleto = $"{nombres} {apellidos}".Trim();

                await FirmarSesionAsync(idUsuario, idPersona, nombreCompleto, rolPrincipal, roles, esDemo: false);

                return Redirect(ObtenerRutaDashboardPorRol(rolPrincipal));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el inicio de sesión con Google SSO");
                ViewBag.Error = "Ocurrió un error al procesar el acceso con Google. Intente nuevamente más tarde.";
                return View("~/Views/Index.cshtml");
            }
        }

        // ==========================================
        // RUTA 3: Acceso Rápido por Rol (usuario REAL de la BD)
        // Inicia sesión con el usuario institucional sembrado que tiene ese rol:
        // consulta core.usuarios, carga sus roles, firma sus claims reales y
        // registra auditoría (método ACCESO_RAPIDO). En producción solo si el
        // .env define ENABLE_DEMO_LOGIN=true (flag del dueño del sitio).
        // ==========================================
        [HttpGet("login-demo/{rol}")]
        public async Task<IActionResult> LoginDemo(string rol)
        {
            if (!_env.IsDevelopment() && !_configuration.GetValue<bool>("Security:EnableDemoLogin", false))
            {
                return NotFound();
            }

            // 1. Usuario institucional responsable de cada panel (DNI en core.personas)
            string dniDemo = rol?.Trim().ToLowerInvariant() switch
            {
                "topico" or "enfermeria"   => "51000006", // Rosa Fernández
                "psicologia"               => "51000007", // Juan Salazar
                "docente"                  => "20000001", // Carlos Rodríguez
                "soporte"                  => "60000001", // Mario Sastre
                "alumno" or "estudiante"   => "87654321", // Ana Torres
                "practicante"              => "60000002", // Luis Paredes
                "secretaria"               => "30000001", // Carmen Flores
                "admin" or "administrador"  => "10000001", // Felipe Ostos
                "tesoreria" or "tesorero"   => "40000001", // Marta Torres Quispe
                "jefe" or "jefegeneral"    => "60000003", // Gustavo Rojas
                "computacion"              => "60000004", // Elena Vargas
                "taller-administracion"    => "60000005", // Ricardo Paz
                "taller-contabilidad"      => "60000006", // Claudia Mes
                "inventario"               => "60000007", // Víctor Palacios (Encargado de Inventario)
                _ => ""
            };

            if (string.IsNullOrEmpty(dniDemo))
            {
                return NotFound();
            }

            // 2. Autenticar contra la BD (mismo camino que el login por DNI)
            string? connectionString = ConfiguracionSegura.ObtenerCadenaConexion(_configuration);
            if (string.IsNullOrEmpty(connectionString))
            {
                ViewBag.Error = "La base de datos no está configurada (falta .env).";
                return View("~/Views/Index.cshtml");
            }

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT u.id_usuario, u.estado, p.nombres, p.apellidos, p.id_persona
                    FROM core.usuarios u
                    JOIN core.personas p ON p.id_persona = u.id_persona
                    WHERE p.numero_documento = @dni AND u.estado = 'Activo'
                    LIMIT 1;";

                int idUsuario = 0, idPersona = 0;
                string nombres = "Personal", apellidos = "Institucional";
                bool encontrado = false;

                using (var command = new NpgsqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@dni", dniDemo);
                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        encontrado = true;
                        idUsuario = reader.GetInt32(0);
                        nombres = reader.IsDBNull(2) ? "Personal" : reader.GetString(2);
                        apellidos = reader.IsDBNull(3) ? "Institucional" : reader.GetString(3);
                        idPersona = reader.GetInt32(4);
                    }
                }

                if (!encontrado)
                {
                    ViewBag.Error = "El usuario institucional de este panel no está registrado en la base de datos.";
                    return View("~/Views/Index.cshtml");
                }

                // 3. Roles reales del usuario y firma de claims
                var roles = await ListarRolesAsync(connection, idUsuario);
                string rolPrincipal = roles.FirstOrDefault() ?? "Alumno";
                string nombreCompleto = $"{nombres} {apellidos}".Trim();

                await RegistrarSesionAsync(connection, idUsuario, "ACCESO_RAPIDO");
                await FirmarSesionAsync(idUsuario, idPersona, nombreCompleto, rolPrincipal, roles, esDemo: true);

                return Redirect(ObtenerRutaDashboardPorRol(rolPrincipal));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en acceso rápido por rol (DNI {Dni})", dniDemo);
                ViewBag.Error = "Ocurrió un error al iniciar la sesión rápida. Intente con su DNI y contraseña.";
                return View("~/Views/Index.cshtml");
            }
        }

        [HttpGet("logout")]
        [HttpGet("Login/Logout")]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }

        public IActionResult Bienvenido()
        {
            string rol = User.FindFirst("RolActivo")?.Value ?? string.Empty;
            return Redirect(ObtenerRutaDashboardPorRol(rol));
        }

        // ==========================================
        // AUXILIARES DE AUTENTICACIÓN
        // ==========================================

        /// <summary>Firma la cookie de claims con los datos del usuario. Es la ÚNICA fuente de identidad.</summary>
        private async Task FirmarSesionAsync(int usuarioId, int personaId, string nombreCompleto,
            string rolPrincipal, IEnumerable<string> roles, bool esDemo)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                new(ClaimTypes.Name, string.IsNullOrWhiteSpace(nombreCompleto) ? "Usuario Institucional" : nombreCompleto),
                new("UsuarioId", usuarioId.ToString()),
                new("PersonaId", personaId.ToString()),
                new("RolActivo", rolPrincipal),
                new("EsDemo", esDemo ? "true" : "false")
            };

            foreach (var r in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, r));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        /// <summary>Roles activos del usuario desde core.usuarios_roles + core.roles.</summary>
        private static async Task<List<string>> ListarRolesAsync(NpgsqlConnection connection, int idUsuario)
        {
            var roles = new List<string>();
            try
            {
                const string sql = @"
                    SELECT r.nombre
                    FROM core.usuarios_roles ur
                    JOIN core.roles r ON r.id_rol = ur.id_rol
                    WHERE ur.id_usuario = @id AND ur.estado = 'Activo'
                    ORDER BY ur.id_rol ASC;";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id", idUsuario);
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    roles.Add(reader.GetString(0));
                }
            }
            catch (Exception)
            {
                // Sin roles registrados → rol por defecto; el login no debe caerse por esto.
            }
            if (roles.Count == 0) roles.Add("Alumno");
            return roles;
        }

        /// <summary>Auditoría de intentos de login (éxito o fallo) en core.intentos_login.</summary>
        private async Task RegistrarIntentoAsync(NpgsqlConnection connection, string identificador, int? usuarioId, bool exito, string motivo)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                var userAgent = Request.Headers.UserAgent.ToString();
                if (userAgent.Length > 250) userAgent = userAgent[..250];

                const string sql = @"
                    INSERT INTO core.intentos_login (identificador_intentado, id_usuario, exito, motivo, ip_origen, user_agent)
                    VALUES (@ident, @uid, @exito, @motivo, @ip, @ua);";

                using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("@ident", identificador);
                command.Parameters.AddWithValue("@uid", (object?)usuarioId ?? DBNull.Value);
                command.Parameters.AddWithValue("@exito", exito);
                command.Parameters.AddWithValue("@motivo", motivo);
                command.Parameters.AddWithValue("@ip", ip.Length > 45 ? ip[..45] : ip);
                command.Parameters.AddWithValue("@ua", userAgent);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar el intento de login de {Ident}", identificador);
            }
        }

        /// <summary>Sesión válida registrada en core.sesiones_login + último acceso del usuario.</summary>
        private async Task RegistrarSesionAsync(NpgsqlConnection connection, int idUsuario, string metodo)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                if (ip.Length > 45) ip = ip[..45];

                const string sqlSesion = @"
                    INSERT INTO core.sesiones_login (id_usuario, metodo_autenticacion, ip_origen, fecha_inicio, fecha_expiracion, estado)
                    VALUES (@id, @metodo, @ip, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP + INTERVAL '8 hours', 'Activa');";

                using (var cmd = new NpgsqlCommand(sqlSesion, connection))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    cmd.Parameters.AddWithValue("@metodo", metodo);
                    cmd.Parameters.AddWithValue("@ip", ip);
                    await cmd.ExecuteNonQueryAsync();
                }

                const string sqlUpdate = "UPDATE core.usuarios SET ultimo_acceso = CURRENT_TIMESTAMP WHERE id_usuario = @id;";
                using (var cmdUpdate = new NpgsqlCommand(sqlUpdate, connection))
                {
                    cmdUpdate.Parameters.AddWithValue("@id", idUsuario);
                    await cmdUpdate.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo registrar la sesión del usuario {UsuarioId}", idUsuario);
            }
        }

        /// <summary>Mapea el rol al dashboard correspondiente.</summary>
        public static string ObtenerRutaDashboardPorRol(string rolNombre)
        {
            if (string.IsNullOrWhiteSpace(rolNombre)) return "/Dashboard/Alumno";

            if (rolNombre.Contains("Encargado de Inventario", StringComparison.OrdinalIgnoreCase) || (rolNombre.Contains("Inventario", StringComparison.OrdinalIgnoreCase) && !rolNombre.Contains("Jefe", StringComparison.OrdinalIgnoreCase)))
                return "/Dashboard/EncargadoInventario";

            if ((rolNombre.Contains("Taller", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Jefe", StringComparison.OrdinalIgnoreCase)) && rolNombre.Contains("Computación", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/JefeComputacion";

            if ((rolNombre.Contains("Taller", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Jefe", StringComparison.OrdinalIgnoreCase)) && (rolNombre.Contains("Administración", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Administracion", StringComparison.OrdinalIgnoreCase)) && !rolNombre.Contains("General", StringComparison.OrdinalIgnoreCase) && !rolNombre.Equals("Administrador", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/JefeAdministracion";

            if ((rolNombre.Contains("Taller", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Jefe", StringComparison.OrdinalIgnoreCase)) && (rolNombre.Contains("Contabilidad", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Conta", StringComparison.OrdinalIgnoreCase)))
                return "/Dashboard/JefeContabilidad";

            if (rolNombre.Contains("Jefe General", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Almacen", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/JefeGeneral";

            if (rolNombre.Contains("Alumno", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Estudiante", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Alumno";

            if (rolNombre.Contains("Docente", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Profesor", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Docente";

            if (rolNombre.Contains("Psicología", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Psicologo", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Psicologia";

            if (rolNombre.Contains("Jefe de Soporte", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Soporte", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Soporte";

            if (rolNombre.Contains("Practicante", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Practicante";

            if (rolNombre.Contains("Secretar", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Secretaria";

            if (rolNombre.Contains("Tesor", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Tesoreria";

            if (rolNombre.Contains("Admin", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Administrador";

            if (rolNombre.Contains("Tópico", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Enfermer", StringComparison.OrdinalIgnoreCase))
                return "/Dashboard/Topico";

            return "/Dashboard/Alumno";
        }
    }
}
