using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Login_Administracion_de_Modulos.Controllers;

namespace Login_Administracion_de_Modulos.Seguridad
{
    /// <summary>
    /// Filtro de autorización para proteger paneles y módulos.
    ///
    /// Funciona sobre la COOKIE DE CLAIMS firmada en el login (NUNCA sobre
    /// HttpContext.Session, que no autentica a nadie):
    ///   - Sesión activa  → User.Identity.IsAuthenticated
    ///   - Rol operando  → claim "RolActivo" (el "sombrero")
    ///   - Todos roles   → User.IsInRole(...) / ClaimTypes.Role
    ///
    /// Comportamiento:
    ///   - Sin sesión       → AJAX/JSON: 401; navegador: redirige a /Login.
    ///   - Rol no autorizado→ AJAX/JSON: 403; navegador: redirige al dashboard
    ///                        del propio usuario.
    ///   - "Administrador"  → acceso global (pasa siempre).
    ///   - Autorizado       → inyecta ViewBag.UserName / UserRole / UserInitials.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ProtegerPanelAttribute : ActionFilterAttribute
    {
        private readonly string[] _rolesPermitidos;

        public ProtegerPanelAttribute(params string[] rolesPermitidos)
        {
            _rolesPermitidos = rolesPermitidos ?? Array.Empty<string>();
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            // Determinar si la petición es AJAX o solicita JSON
            bool esPeticionAjaxOJson =
                context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                (context.HttpContext.Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

            // 1. Validar que exista sesión autenticada (cookie de claims)
            if (user.Identity?.IsAuthenticated != true)
            {
                if (esPeticionAjaxOJson)
                {
                    context.Result = new JsonResult(new { success = false, message = "Sesión no iniciada." })
                    {
                        StatusCode = StatusCodes.Status401Unauthorized
                    };
                    return;
                }

                context.Result = new RedirectResult("/Login");
                return;
            }

            // 2. Rol con el que opera el usuario (claim RolActivo)
            string? rolActivo = user.FindFirst("rol")?.Value ?? user.FindFirst("RolActivo")?.Value;
            string? nombreUsuario = user.Identity?.Name ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            // 3. Si hay roles permitidos definidos, verificar coincidencia
            if (_rolesPermitidos.Length > 0)
            {
                // El rol "Administrador" posee acceso global a todos los módulos y paneles
                bool esAdministrador = user.IsInRole("Administrador")
                    || user.IsInRole("Admin")
                    || (rolActivo?.Contains("Admin", StringComparison.OrdinalIgnoreCase) ?? false);

                // Autoriza si el sombrero activo coincide O cualquiera de sus roles lo hace
                bool rolAutorizado = esAdministrador || _rolesPermitidos.Any(r =>
                    (rolActivo != null && (rolActivo.Contains(r, StringComparison.OrdinalIgnoreCase) || r.Contains(rolActivo, StringComparison.OrdinalIgnoreCase)))
                    || user.IsInRole(r));

                if (!rolAutorizado)
                {
                    if (esPeticionAjaxOJson)
                    {
                        context.Result = new JsonResult(new { success = false, message = "Acceso denegado: rol no autorizado." })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                        return;
                    }

                    // Navegación normal: redirigir a su propio dashboard
                    string rutaPropia = LoginController.ObtenerRutaDashboardPorRol(rolActivo ?? string.Empty);
                    context.Result = new RedirectResult(rutaPropia);
                    return;
                }
            }

            // 4. Sesión válida y autorizada: inyectar datos en ViewBag del controlador
            if (context.Controller is Controller controller)
            {
                string nombre = string.IsNullOrWhiteSpace(nombreUsuario) ? "Usuario Institucional" : nombreUsuario;
                controller.ViewBag.UserName = nombre;
                controller.ViewBag.UserRole = rolActivo ?? string.Empty;

                var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                controller.ViewBag.UserInitials = partes.Length >= 2
                    ? $"{partes[0][0]}{partes[1][0]}".ToUpper()
                    : (nombre.Length > 0 ? nombre[0].ToString().ToUpper() : "US");
            }

            base.OnActionExecuting(context);
        }
    }
}
