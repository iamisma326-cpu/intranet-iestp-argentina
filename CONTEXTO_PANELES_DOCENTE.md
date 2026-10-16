# Contexto Técnico: Paneles Laterales del Docente (Sidebar)

Este documento contiene el análisis completo sobre la ubicación, arquitectura, estilos y funcionamiento del **panel lateral (sidebar)** del perfil docente en el sistema.

---

## 1. 📍 Ubicación de Archivos Clave

| Componente | Archivo / Ruta | Descripción |
| :--- | :--- | :--- |
| **Ítems del Menú Docente** | [`Docente.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Docente.cshtml) | Define los botones y rutas del panel lateral mediante la sección `@section SidebarNav`. |
| **Estructura Base del Sidebar** | [`_DashboardLayout.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Shared/_DashboardLayout.cshtml) | Plantilla maestra que contiene el contenedor `<aside class="sidebar">`, perfil y pie con logout. |
| **Controlador de Dashboard** | [`DashboardController.cs`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Controlador/DashboardController.cs) | Enruta la petición `GET /Dashboard/Docente` y proporciona los datos del docente a la vista. |
| **Controlador de Autenticación** | [`LoginController.cs`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Controlador/LoginController.cs) | Gestiona el inicio de sesión y redirige al rol `Docente` hacia `/Dashboard/Docente`. |
| **Estilos CSS del Sidebar** | [`dashboard.css`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/wwwroot/css/dashboard.css) | Define las medidas, colores oscuros, efectos hover y el layout del panel lateral. |

---

## 2. 🧩 Estructura y Código

### A. Elementos de Navegación del Docente
En [`Docente.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Docente.cshtml#L7-L24):
```razor
@section SidebarNav {
    <a href="/Dashboard/Docente" class="nav-item active">
        <i class="ph ph-squares-four"></i>
        Dashboard
    </a>
    <a href="#" class="nav-item">
        <i class="ph ph-books"></i>
        Mis Cursos
    </a>
    <a href="#" class="nav-item">
        <i class="ph ph-student"></i>
        Asistencia
    </a>
    <a href="#" class="nav-item">
        <i class="ph ph-exam"></i>
        Calificaciones
    </a>
}
```

### B. Contenedor Maestro del Sidebar
En [`_DashboardLayout.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Shared/_DashboardLayout.cshtml#L28-L51):
```razor
<aside class="sidebar">
    <div class="sidebar-profile">
        <div class="profile-avatar-large">
            @(ViewBag.UserInitials ?? "RF")
            <span class="online-dot"></span>
        </div>
        <div class="profile-info">
            <h4>@(ViewBag.UserName ?? "Lic. Rosa Fernández")</h4>
            <p>@(ViewBag.UserRole ?? ViewData["Role"] ?? "Enfermería (Tópico)")</p>
        </div>
    </div>

    <nav class="sidebar-nav">
        @RenderSection("SidebarNav", required: false)
    </nav>

    <div class="sidebar-footer">
        <a href="/logout" class="btn-logout">
            <i class="ph ph-sign-out"></i>
            Cerrar Sesión
        </a>
    </div>
</aside>
```

### C. Enrutamiento en Controlador
En [`DashboardController.cs`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Controlador/DashboardController.cs#L32-L38):
```csharp
[HttpGet("/Dashboard/Docente")]
public IActionResult Docente()
{
    ViewBag.UserName = HttpContext.Session.GetString("UsuarioNombre") ?? "Prof. Carlos Docente";
    ViewBag.UserRole = HttpContext.Session.GetString("UsuarioRol") ?? "Docente";
    return View("~/Vista/Dashboard/Docente.cshtml");
}
```

---

## 3. 🎨 Reglas de Estilos (`dashboard.css`)
- **Ancho del Sidebar:** `280px` (`--sidebar-width: 280px;`).
- **Fondo:** `#0b1120` (`--bg-sidebar`).
- **Posicionamiento:** Fijo a la izquierda (`position: fixed; top: 60px; left: 0; bottom: 0;`).
- **Librería de Íconos:** Phosphor Icons (`<i class="ph ph-..."></i>`).
