# Contexto del Sistema: Paneles Laterales (Sidebars) por Rol según `bd.sql`

Este documento consolida la arquitectura y especificación de los **paneles laterales (sidebars)** y vistas de dashboard de los **8 roles** del sistema, diseñados y diferenciados conforme a las tablas y módulos definidos en `bd.sql`.

---

## 1. 📋 Resumen de Mapeo de Roles y Tablas (`bd.sql`)

| Rol | Ruta Dashboard | Vista Razor | Tablas de `bd.sql` Relacionadas | Opciones del Panel Lateral (Sidebar) |
| :--- | :--- | :--- | :--- | :--- |
| **Alumno** | `/Dashboard/Alumno` | [`Alumno.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Alumno.cshtml) | `personas`, `especialidad`, `atenciones_topico`, `atenciones_psicologia`, `requerimientos` | • Mi Portal<br>• Mis Cursos<br>• Calificaciones<br>• Asistencias<br>• Citas de Tópico<br>• Citas de Psicología<br>• Soporte TI |
| **Docente** | `/Dashboard/Docente` | [`Docente.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Docente.cshtml) | `personas`, `ambiente`, `incidencias`, `requerimientos` | • Dashboard<br>• Mis Cursos<br>• Asistencia<br>• Calificaciones<br>• Incidencias de Aula<br>• Soporte TI |
| **Jefe de Soporte** | `/Dashboard/Soporte` | [`Soporte.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Soporte.cshtml) | `bien`, `almacen`, `ambiente`, `asignacion`, `transferencia`, `inventario_anual`, `incidencias`, `requerimientos` | • Dashboard TI<br>• Incidencias Técnicas<br>• Requerimientos<br>• Inventario de Bienes<br>• Ambientes y Labs<br>• Asignaciones<br>• Transferencias<br>• Inventario Anual |
| **Practicante** | `/Dashboard/Practicante` | [`Practicante.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Practicante.cshtml) | `incidencias`, `requerimientos`, `ambiente`, `bien` (`tecnico_id`) | • Mis Tareas<br>• Incidencias Asignadas<br>• Requerimientos<br>• Revisión de Ambientes<br>• Soluciones Aplicadas |
| **Secretaría Académica** | `/Dashboard/Secretaria` | [`Secretaria.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Secretaria.cshtml) | `personas`, `especialidad` | • Dashboard<br>• Padrón de Personas<br>• Especialidades<br>• Matrículas y Trámites<br>• Certificados<br>• Reportes Generales |
| **Tesorería** | `/Dashboard/Tesoreria` | [`Tesoreria.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Tesoreria.cshtml) | `personas`, `usuarios`, `roles` | • **Inicio**<br>• **Bandeja de vouchers** (Notificación: 1)<br>• **Recibos**<br>• **Matrículas**<br>• **Estudiantes**<br>• **Buzón**<br>• **WhatsApp**<br>• **Catálogo TUPA**<br>• *(Trámites y Matrícula, recaudación institucional)* |
| **Administrador** | `/Dashboard/Administrador` | [`Administrador.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Administrador.cshtml) | `usuarios`, `roles`, `usuarios_roles`, `permisos_modulos`, `roles_permisos`, `sesiones_login` | • Dashboard Central<br>• Gestión de Usuarios<br>• Roles y Perfiles<br>• Permisos y Módulos<br>• Auditoría de Sesiones<br>• Configuración Global |
| **Jefe General** | `/Dashboard/JefeGeneral` | [`JefeGeneral.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/JefeGeneral.cshtml) | `bien`, `almacen`, `ambiente`, `especialidad`, `asignacion`, `transferencia`, `inventario_anual`, `usuarios` | • **General:** Inicio, Dashboard<br>• **Inventario:** Bienes, Clasificaciones, Almacenes, Ambientes, Especialidades<br>• **Operaciones:** Asignaciones, Transferencias, Inventario Anual, Recepciones<br>• **Reportes:** Reportes, Exportar Excel<br>• **Administración:** Usuarios, Configuración |
| **Jefe Taller Computación** | `/Dashboard/JefeComputacion` | [`JefeComputacion.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/JefeComputacion.cshtml) | `bien`, `almacen`, `ambiente`, `especialidad`, `asignacion`, `transferencia`, `inventario_anual` | • **General:** Inicio, Dashboard<br>• **Inventario:** Bienes, Almacenes, Ambientes, Especialidades<br>• **Operaciones:** Asignaciones, Transferencias, Inventario Anual<br>• **Reportes:** Reportes, Exportar Excel<br>• *(Exclusivo para el área de Computación e Informática)* |
| **Jefe Taller Administración** | `/Dashboard/JefeAdministracion` | [`JefeAdministracion.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/JefeAdministracion.cshtml) | `bien`, `almacen`, `ambiente`, `especialidad`, `asignacion`, `transferencia`, `inventario_anual` | • **General:** Inicio, Dashboard<br>• **Inventario:** Bienes, Almacenes, Ambientes, Especialidades<br>• **Operaciones:** Asignaciones, Transferencias, Inventario Anual<br>• **Reportes:** Reportes, Exportar Excel<br>• *(Exclusivo para el área de Administración de Empresas)* |
| **Jefe Taller Contabilidad** | `/Dashboard/JefeContabilidad` | [`JefeContabilidad.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/JefeContabilidad.cshtml) | `bien`, `almacen`, `ambiente`, `especialidad`, `asignacion`, `transferencia`, `inventario_anual` | • **General:** Inicio, Dashboard<br>• **Inventario:** Bienes, Almacenes, Ambientes, Especialidades<br>• **Operaciones:** Asignaciones, Transferencias, Inventario Anual<br>• **Reportes:** Reportes, Exportar Excel<br>• *(Exclusivo para el área de Contabilidad)* |
| **Encargado de Inventario** | `/Dashboard/EncargadoInventario` | [`EncargadoInventario.cshtml`](file:///c:/Users/user/Desktop/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/EncargadoInventario.cshtml) | `bien`, `almacen`, `ambiente`, `especialidad`, `asignacion`, `transferencia`, `inventario_anual` | • **General:** Inicio, Dashboard<br>• **Inventario:** Bienes, Almacenes, Ambientes, Especialidades<br>• **Operaciones:** Asignaciones, Transferencias, Inventario Anual, Recepciones<br>• **Reportes:** Reportes, Exportar Excel<br>• *(Gestión general de almacenes e inventario institucional)* |
| **Enfermería (Tópico)** | `/Dashboard/Topico` | [`Topico.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Topico.cshtml) | `atenciones_topico`, `botica_medicamentos`, `personas` | • Dashboard / Resumen<br>• Atención Clínica<br>• Botica y Medicamentos<br>• Historial de Pacientes |
| **Psicología** | `/Dashboard/Psicologia` | [`Psicologia.cshtml`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Vista/Dashboard/Psicologia.cshtml) | `atenciones_psicologia`, `personas` | • Dashboard / Bienestar<br>• Atención Psicológica<br>• Citas Programadas<br>• Historial Clínico<br>• Padrón de Pacientes |

---

## 2. 🔀 Enrutamiento Centralizado de Roles en Backend

### A. [`LoginController.cs`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Controlador/LoginController.cs)
La función `ObtenerRutaDashboardPorRol(string rolNombre)` resuelve automáticamente el destino tanto para el acceso por **DNI** como por **Google SSO**:
```csharp
public static string ObtenerRutaDashboardPorRol(string rolNombre)
{
    if (string.IsNullOrWhiteSpace(rolNombre)) return "/Dashboard/Topico";

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

    if (rolNombre.Contains("Admin", StringComparison.OrdinalIgnoreCase))
        return "/Dashboard/Administrador";

    if (rolNombre.Contains("Tópico", StringComparison.OrdinalIgnoreCase) || rolNombre.Contains("Enfermer", StringComparison.OrdinalIgnoreCase))
        return "/Dashboard/Topico";

    return "/Dashboard/Topico";
}
```

### B. [`DashboardController.cs`](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/Login_Administracion_de_Modulos/Controlador/DashboardController.cs)
Centraliza la inicialización de sesiones y avatars con `CargarDatosSesion()` exponiendo endpoints limpios para cada uno de los 8 roles.

---

## 3. 🎨 Estilos y Comportamiento del Sidebar (`dashboard.css`)
- **Scroll independiente:** `.sidebar-nav` cuenta con `overflow-y: auto; overflow-x: hidden;` para garantizar que menús con múltiples opciones se desplacen suavemente sin recortar el pie de sesión (`.sidebar-footer`).
- **Estados:** `.nav-item:hover` y `.nav-item.active` con brillo y acento cian (`--accent-cyan: #00f2fe;`).
