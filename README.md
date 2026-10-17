# Intranet IESTP Argentina — Núcleo de Autenticación y Módulos

Sistema institucional del **IESTP Argentina**: un núcleo central de autenticación
y paneles por rol, con un área modular donde cada equipo de desarrollo construye
su módulo de forma aislada.

```
       ┌───────────────────────────────────────────────┐
       │            NÚCLEO (equipo core)               │
       │   Login DNI + Google SSO · Cookie de claims   │
       │   [ProtegerPanel] RBAC · Auditoría de accesos │
       └───────────────────────┬───────────────────────┘
                               │
         ┌──────────┬──────────┼──────────┬──────────┐
         ▼          ▼          ▼          ▼          ▼
   [Modulo01]  [Modulo02]  [Modulo03]  [Modulo04]  [Modulo05]  ... (07)
   (equipo 01) (equipo 02) (equipo 03) (equipo 04)  Salud, Tópico
                                                y Psicología (con código)
```

- **Sitio en producción:** https://intranetiestp.runasp.net (MonsterASP)
- **Base de datos:** PostgreSQL 18 en Neon (esquemas `core` + `modXX`)

## 1. Stack

| Capa | Tecnología |
| :--- | :--- |
| Backend | ASP.NET Core MVC, .NET 10, C# |
| Base de datos | PostgreSQL 18 (Neon) — acceso con ADO.NET/Npgsql 10, SQL parametrizado |
| Autenticación | Cookie de claims (usuario + todos sus roles + `RolActivo`) · Google SSO opcional |
| Contraseñas | PBKDF2-SHA256, 100k iteraciones, comparación de tiempo constante |
| Frontend | Razor Views + CSS vanilla + Phosphor Icons |
| CI/CD | GitHub Actions → WebDeploy a MonsterASP (cada push a `master` publica) |

## 2. Estructura

```
├── .github/
│   ├── CODEOWNERS                        # quién revisa cada carpeta
│   └── workflows/
│       ├── deploy.yml                    # push a master → compilar y publicar
│       ├── proteger-ramas-modulos.yml    # guardian: rama moduloXX solo toca su carpeta
│       └── setup-env.yml                 # uso único: publica el .env al sitio
├── Sql/
│   ├── core_schema.sql                   # esquema core (idempotente) + seeds
│   └── roles_equipos.sql                 # roles de BD user_equipoXX + user_app
└── Login_Administracion_de_Modulos/
    ├── Controllers/                      # Login y Dashboards (14 paneles por rol)
    ├── Seguridad/
    │   ├── ConfiguracionSegura.cs        # carga .env / env vars (secreto jamás en código)
    │   ├── PasswordSecurity.cs           # PBKDF2 hash/verify
    │   └── ProtegerPanelAttribute.cs     # filtro RBAC sobre claims
    ├── Views/                            # login + 14 paneles (marcos de rol)
    ├── Areas/
    │   ├── Modulo01..04, 06, 07/         # slots de cada equipo (vacíos hasta que arranquen)
    │   └── Modulo05/                      # Salud: tópico y psicología (referencia viva)
    │       ├── Controller/  ├── Models/  └── Sql/schema.sql
    └── Program.cs / appsettings.json     # pipeline y placeholders (secretos en .env)
```

## 3. Contrato de sesión y seguridad

- **Identidad = cookie de claims**, firmada por el login (`SignInAsync`). Claims
  disponibles en toda la app: `UsuarioId`, `PersonaId`, `RolActivo`,
  `ClaimTypes.Role` (todos los roles). `HttpContext.Session` NO autentica.
- **`[ProtegerPanel("Rol", ...)]`** valida el rol activo: peticiones AJAX/JSON
  reciben 401/403; la navegación se redirige al panel propio del usuario.
  `Administrador` tiene acceso global.
- **Antiforgery** automático en todo POST (`AutoValidateAntiforgeryToken`).
- **Auditoría:** cada intento de login (éxito o fallo) queda en
  `core.intentos_login`; cada sesión válida en `core.sesiones_login`.
- **Acceso rápido por rol** (`/login-demo/{rol}`): inicia sesión con el usuario
  institucional REAL que tiene ese rol (misma cookie, claims y auditoría que el
  login por DNI, método `ACCESO_RAPIDO`). Visible en producción; se desactiva
  poniendo `ENABLE_DEMO_LOGIN=false` en el `.env` del sitio.
- **Secretos** (cadena de BD, credenciales Google) viven en `.env` o variables
  de entorno — nunca en el repositorio. `appsettings.json` trae placeholders.

## 4. Roles de base de datos (aislamiento real)

Neon permite roles de BD ilimitados en el free tier; cada equipo recibe uno:

| Rol | Puede | No puede |
| :--- | :--- | :--- |
| `user_equipoXX` | TODO en su esquema `modXX`, LEER `core` | Escribir `core`, tocar `modYY` ajeno |
| `user_app` (runtime) | LEER `core` + auditoría + lo que su módulo requiere | Escribir personas/usuarios/roles |
| `neondb_owner` | Administración (solo el dueño del proyecto) | Entregarse a equipos |

Los crea `Sql/roles_equipos.sql` (con `GRANT REFERENCES` sobre `core` para que
las FK de cada módulo funcionen). Las contraseñas las entrega el dueño por canal
privado; jamás van al repo.

## 5. Cómo empezar (desarrollo local)

```bash
git clone https://github.com/iamisma326-cpu/intranet-iestp-argentina.git
cd intranet-iestp-argentina
cp .env.example .env      # completar DB_CONNECTION_STRING (Neon o PostgreSQL local)
dotnet run --project Login_Administracion_de_Modulos/Login_Administracion_de_Modulos.csproj
```

Para crear la BD desde cero: ejecutar `Sql/core_schema.sql` y
`Sql/roles_equipos.sql` como administrador, luego el `Sql/schema.sql` interno de
cada módulo con su usuario `user_equipoXX`.

**Usuarios institucionales de prueba** (contraseña de todos: `Demo2026!`).
Todos existen de verdad en la BD y pueden entrar por DNI o por el acceso
rápido del login:

| DNI | Usuario | Rol del panel |
| :--- | :--- | :--- |
| 47915633 | Felipe Ostos Bermúdez | Administrador |
| 77654321 | Ana García Flores | Alumno |
| 30000001 | Rosa Morales Salazar | Secretaría Académica |
| 40000001 | Elena Ramos Palacios | Tesorería |
| 10000002 | Gina Huertas Camacho | Docente |
| 51000005 | Luis Chávez Paredes | Jefe de Soporte |
| 51000006 | Lucía Fernández Quispe | Enfermería (Tópico) |
| 51000007 | Andrés Salazar Morales | Psicología |
| 51000001 | Jorge Ramírez Salazar | Jefe General / Inventario |
| 51000002 | María Torres Vega | Jefe Taller Computación |
| 51000003 | Carlos Huamán Ríos | Jefe Taller Administración |
| 51000004 | Rosa Vargas Muñoz | Jefe Taller Contabilidad |
| 74096398 | Franco Stefano Alcála | Practicante |

## 6. Cómo trabaja cada equipo (gobernanza)

1. Crea tu rama con el prefijo de tu módulo: `modulo04/registro-bienes`.
2. Trabaja SOLO dentro de `Areas/ModuloXX/`. El workflow
   `proteger-ramas-modulos.yml` bloquea automáticamente cualquier PR que toque
   el núcleo u otro módulo.
3. Abre el Pull Request a `master`. Al aprobarse y fusionarse, el deploy a
   producción es automático (GitHub Actions → WebDeploy). Nadie necesita
   entrar al panel del hosting.

Reglas de código para tu módulo (ver `Areas/Modulo05/` como referencia viva):

```csharp
[Area("Modulo04")]
[Route("Modulo04/[controller]")]
[ProtegerPanel("Encargado de Inventario", "Jefe General")]  // tus roles
public class InventarioController : Controller
{
    private readonly string _conn;

    public InventarioController(IConfiguration configuration)
    {
        _conn = ConfiguracionSegura.ObtenerCadenaConexion(_configuration);
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // Identidad del usuario autenticado (claims, no Session):
        // var usuarioId = int.Parse(User.FindFirst("UsuarioId")!.Value);
        return View();
    }
}
```

Tu `Sql/schema.sql` crea tus tablas en el esquema `modXX` (idempotente) y puede
referenciar `core.personas`/`core.usuarios` por FK (los permisos ya están
otorgados). Ejecútalo con tu usuario `user_equipoXX`.

## 7. Trabajo en paralelo y paneles compartidos (LEER ANTES DE EMPEZAR)

A diferencia de un sistema donde cada equipo era dueño de su propia pestaña,
aquí los **paneles por rol son una vitrina compartida**: cada panel es un marco
(sidebar + secciones) al que varios equipos aportan pestañas. Un mismo módulo
puede aparecer en varios paneles: el Módulo 05 (Salud) alimenta hoy los paneles
de Docente (reporte de averías), Soporte y Practicante (tickets y activos),
Enfermería/Tópico (atenciones y botica) y Psicología (expediente y sesiones).

### Regla 1 — Tu territorio

| Capa | Territorio del equipo | Regla |
| :--- | :--- | :--- |
| Código | `Areas/ModuloXX/**` | TODO tu código va ahí: controllers, models, views, JS, SQL. Nada fuera. |
| Datos | Esquema `modXX.*` en la BD | Todas tus tablas y queries van a tu esquema, SIEMPRE con el prefijo explícito (`FROM mod04.bienes`, no `FROM bienes`). |
| Dashboards | `Views/Dashboard/*.cshtml` | Compartidos. Solo se permiten CAMBIOS ADITIVOS (ver regla 3). |

### Regla 2 — Límites de datos

- Puedes LEER `core.*` (personas, estudiantes, malla, roles) con JOIN en tus
  queries. NUNCA escribas en `core`: ni INSERT, ni UPDATE, ni DELETE.
- Si necesitas una columna nueva en `core`, NO la crees tú: propónla al dueño
  del proyecto; se evalúa y se aplica de forma centralizada.
- Tus conexiones usan tu usuario `user_equipoXX` en desarrollo; en producción
  el runtime usa `user_app` con privilegios mínimos.

### Regla 3 — Cómo aparecer en un panel (cambio aditivo mínimo)

Los dashboards NO contienen el código de los equipos: sus pestañas cargan
parciales tuyos con `fetch()`. Para aparecer en un panel solo añades:

1. Una línea en el sidebar del panel (nav):
   `<a class="nav-item" onclick="cargarMiFuncion(this)">…</a>`
2. Una función JS de 2-3 líneas que hace:
   `fetch('/Modulo04/MiVista?partial=true')` y vuelca el HTML en tu sección.
3. Tu HTML/CSS real vive en tu vista parcial DENTRO de `Areas/ModuloXX/Views/`.

Ejemplo real (Docente.cshtml cargando un parcial del Módulo 05):

```javascript
fetch('/Modulo05/AveriaDocente/Reporte?partial=true')
    .then(r => r.text())
    .then(html => { seccion.innerHTML = html; });
```

No edites el HTML de secciones de otros equipos, no reformules clases CSS
compartidas del `dashboard.css` y no quiteís pestañas ajenas. Un PR que solo
AÑADE su nav + su fetch + su carpeta jamás genera conflicto con otro equipo.

### Regla 4 — CSS y estilos

- Prefija TODAS tus clases con tu módulo: `.mod04-tabla`, `.mod04-filtros`.
- No sobreescribas variables ni clases de `dashboard.css`/`login.css`.
- Los iconos son Phosphor (`ph ph-…`); usa los existentes, no cargues otra
  librería de iconos.

### Regla 5 — Coordinación

Antes de empezar un panel nuevo, declara en el grupo: **qué pestaña, en qué
panel** (ej. "equipo 02 → asistencia en Docente y Alumno"). Ese es el ÚNICO
punto de coordinación necesario; después de eso, cada equipo trabaja aislado.
Si dos equipos piden la misma pestaña, decide el dueño del proyecto.

## 8. Paneles del sistema (14 roles)

| N° | Rol | Ruta del panel |
| :---: | :--- | :--- |
| 01 | Enfermería (Tópico) | `/Dashboard/Topico` |
| 02 | Psicología | `/Dashboard/Psicologia` |
| 03 | Docente | `/Dashboard/Docente` |
| 04 | Jefe de Soporte TI | `/Dashboard/Soporte` |
| 05 | Alumno | `/Dashboard/Alumno` |
| 06 | Practicante | `/Dashboard/Practicante` |
| 07 | Secretaría Académica | `/Dashboard/Secretaria` |
| 08 | Administrador (acceso global) | `/Dashboard/Administrador` |
| 09 | Jefe General / Almacén | `/Dashboard/JefeGeneral` |
| 10 | Jefe Taller Computación | `/Dashboard/JefeComputacion` |
| 11 | Jefe Taller Administración | `/Dashboard/JefeAdministracion` |
| 12 | Jefe Taller Contabilidad | `/Dashboard/JefeContabilidad` |
| 13 | Encargado de Inventario | `/Dashboard/EncargadoInventario` |
| 14 | Tesorería | `/Dashboard/Tesoreria` |

El rol se asigna en `core.usuario_roles`; el usuario inicia sesión con su DNI
y el núcleo lo lleva al panel de su rol activo. El acceso rápido del login
entra con el mismo usuario real de cada rol, sin contraseña.
