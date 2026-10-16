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
│   ├── core_schema.sql                   # esquema core (idempotente) + seeds demo
│   └── roles_equipos.sql                 # roles de BD user_equipo01..07 + user_app
└── Login_Administracion_de_Modulos/
    ├── Controllers/                      # Login y Dashboards (14 paneles por rol)
    ├── Seguridad/
    │   ├── ConfiguracionSegura.cs        # carga .env / env vars (secreto jamás en código)
    │   ├── PasswordSecurity.cs           # PBKDF2 hash/verify
    │   └── ProtegerPanelAttribute.cs     # filtro RBAC sobre claims
    ├── Views/                            # login + 14 paneles
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
- **Login demo** (`/login-demo/{rol}`) existe solo en Development; en producción
  responde 404.
- **Secretos** (cadena de BD, credenciales Google) viven en `.env` o variables
  de entorno — nunca en el repositorio. `appsettings.json` trae placeholders.

## 4. Roles de base de datos (aislamiento real)

Neon permite roles de BD ilimitados en el free tier; cada equipo recibe uno:

| Rol | Puede | No puede |
| :--- | :--- | :--- |
| `user_equipoXX` | TODO en su esquema `modXX`, LEER `core` | Escribir `core`, tocar `modYY` ajeno |
| `user_app` (runtime) | LEER `core` + INSERT/UPDATE auditoría | Escribir personas/usuarios |
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

**Usuarios demo** (ya sembrados; contraseña de todos: `Demo2026!`):

| DNI | Usuario | Rol |
| :--- | :--- | :--- |
| 10000001 | Felipe Ostos | Administrador |
| 87654321 | Ana Torres | Alumno |
| 30000001 | Carmen Flores | Secretaría Académica |
| 40000001 | Marta Torres | Tesorería |
| 51000006 | Rosa Fernández | Enfermería (Tópico) |
| 51000007 | Juan Salazar | Psicología |
| 12345678 / 20000001 | Sheyla Ríos / Carlos Rodríguez | Docente |

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
        // SearchPath mod04,core,public: tus queries van sin prefijo de esquema
        _conn = ConfiguracionSegura.ObtenerCadenaModulo(configuration, "04");
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

## 7. Paneles del sistema (14 roles)

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

El rol se asigna en `core.usuarios_roles`; el usuario inicia sesión con su DNI
y el núcleo lo lleva al panel de su rol activo.
