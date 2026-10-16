# Sistema Central de Autenticación, Seguridad RBAC y Administración de Módulos (Intranet Institucional)

Bienvenido al repositorio central de la plataforma tecnológica institucional del **IESTP Argentina**. Este proyecto actúa como el **Núcleo Padre (Core / Módulo 8)** del ecosistema de software institucional, proveyendo la infraestructura base para la autenticación única de usuarios, el control de acceso basado en roles (RBAC), los paneles de control unificados (Dashboards) y la gobernanza para la integración desacoplada de los módulos desarrollados por los distintos equipos y carreras.

```
       ┌─────────────────────────────────────────────────────────────┐
       │             NÚCLEO CENTRAL (Módulo 8 - Core)                │
       │    Autenticación Dual (DNI + Google SSO) | Seguridad RBAC    │
       │           Filtro [ProtegerPanel] | Auditoría de Sesión       │
       └──────────────────────────────┬──────────────────────────────┘
                                      │
            ┌─────────────────────────┼─────────────────────────┐
            ▼                         ▼                         ▼
   [Areas/Modulo01]            [Areas/Modulo04]          [Areas/Modulo05]
 Matriculación y Alumnos    Inventario y Activos TI     Salud, Tópico y Psico
 (Submódulo / Repo Ext.)     (Submódulo / Rama Mod)    (Submódulo / Repo Ext.)
```

## 1. Visión General y Características Principales

* **Autenticación Dual Segura:** Permite el ingreso mediante credenciales locales (DNI + Contraseña con hash criptográfico PBKDF2-SHA256) o a través de Single Sign-On (SSO) institucional con Google OAuth 2.0 (`@instituto.edu.pe`). Las cuentas institucionales sin contraseña local son protegidas automáticamente bloqueando accesos por formulario directo.
* **Modo Demo Local Incorporado (Sin Base de Datos):** La pantalla de login incluye una sección de acceso rápido de prototipo que permite iniciar sesión instantáneamente en cualquiera de los 14 paneles con un solo clic, sin requerir credenciales ni conexión a PostgreSQL. Ideal para pruebas de interfaz, maquetado y validación de flujos.
* **Control de Acceso Basado en Roles (`[ProtegerPanel]`):** Filtro de acción de nivel de arquitectura que intercepta cada solicitud a `/Dashboard/...` y a las rutas `/ModuloXX/...`. Valida la identidad y el rol en sesión, protege peticiones AJAX respondiendo HTTP 401/403 en formato JSON, redirige accesos no autorizados a su panel correspondiente e inyecta metadatos del usuario en la vista.
* **Auditoría Centralizada de Accesos:** Todo inicio de sesión válido (DNI o Google SSO) registra automáticamente la IP de origen, marca de tiempo y método de acceso en la tabla `sesiones_login`, manteniendo trazabilidad de seguridad en PostgreSQL.
* **Arquitectura Modular Desacoplada (`Areas/ModuloXX`):** Estructura preparada para integrar los submódulos de cada equipo de desarrollo mediante repositorios independientes de GitHub (`git submodule`) o ramas aisladas gobernadas por flujos automáticos de CI/CD.

## 2. Stack Tecnológico y Arquitectura

| Capa / Componente | Tecnología | Descripción y Uso en el Proyecto |
| :--- | :--- | :--- |
| **Backend Core** | ASP.NET Core MVC (.NET 10 / C# 14) | Framework de alto rendimiento con inyección de dependencias nativa, controladores limpios y middleware modular. |
| **Base de Datos** | PostgreSQL 17 (Neon / Local) | Motor relacional robusto con llaves foráneas, restricciones `CHECK`, UUIDs y roles maestros. |
| **Acceso a Datos** | ADO.NET con Npgsql 10.0 | Consultas SQL parametrizadas de alta velocidad sin sobrecarga de ORM para máxima eficiencia. |
| **Seguridad y Criptografía** | PBKDF2 (SHA-256, 100k iteraciones) | Implementado en `PasswordSecurity.cs` con salt criptográfico de 16 bytes y comparación de tiempo constante. |
| **Configuración Segura** | `.env` + `ConfiguracionSegura.cs` | Aislamiento estricto de secretos fuera del código fuente con fallback a variables de entorno del sistema. |
| **Cookies y Sesiones** | Cookie Session `.IESTP.Session` | Cookies protegidas con `HttpOnly = true`, `SameSite = Lax` y expiración controlada por inactividad. |
| **Cabeceras de Seguridad** | Middleware HTTP Personalizado | Inyección de `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN` y `Referrer-Policy`. |
| **Frontend** | Razor Views (`.cshtml`) + Vanilla CSS | Vistas limpias y diseño responsivo sin dependencias pesadas, estilizado con variables CSS y Phosphor Icons. |
| **Gobernanza y CI/CD** | GitHub Actions + `CODEOWNERS` | Despliegue continuo a MonsterASP (`deploy.yml`) y bloqueo estricto de ramas por módulo (`proteger-ramas-modulos.yml`). |

## 3. Mapa de Estructura del Proyecto

A continuación se detalla la organización del código, destacando la frontera entre el **Núcleo Protegido** y las **Áreas de Módulos**:

```
Login_Administracion_de_Modulos/
├── .github/                                     # 🛡️ GOBERNANZA Y AUTOMATIZACIÓN (Protegido)
│   ├── CODEOWNERS                               # Asignación obligatoria de revisores por carpeta
│   └── workflows/
│       ├── deploy.yml                           # Despliegue automático FTP a MonsterASP
│       └── proteger-ramas-modulos.yml           # Verificación de alcance estricto por rama de módulo
│
├── Login_Administracion_de_Modulos/             # 🛡️ NÚCLEO PADRE DEL SISTEMA (Módulo 8)
│   ├── Controllers/                             # Controladores centrales del núcleo
│   │   ├── DashboardController.cs               # Enrutamiento y vistas de los 14 paneles (Protegido por rol)
│   │   └── LoginController.cs                   # Autenticación DNI, Google SSO y Modo Demo Local
│   │
│   ├── Seguridad/                               # Componentes transversales de seguridad
│   │   ├── ConfiguracionSegura.cs               # Lector de .env y Secrets de GitHub sin exponer credenciales
│   │   ├── PasswordSecurity.cs                  # Hasheo PBKDF2-SHA256 y comparación FixedTimeEquals
│   │   └── ProtegerPanelAttribute.cs            # Filtro [ProtegerPanel] para autorización RBAC
│   │
│   ├── Views/                                   # Vistas Razor del núcleo institucional
│   │   ├── Dashboard/                           # Las 14 plantillas visuales de panel institucional
│   │   │   ├── Administrador.cshtml
│   │   │   ├── Alumno.cshtml
│   │   │   ├── Docente.cshtml
│   │   │   ├── Enfermeria.cshtml
│   │   │   └── ... (Demás vistas de panel)
│   │   └── Index.cshtml                         # Pantalla de Login (Formulario DNI + Google SSO + Botones Demo)
│   │
│   ├── Areas/                                   # 🧩 CONTENEDOR MODULAR DE EQUIPOS
│   │   ├── Modulo01/                            # Módulo 01: Matrículas y Alumnos
│   │   ├── Modulo02/                            # Módulo 02: Gestión Docente
│   │   ├── Modulo03/                            # Módulo 03: Tesorería y Cobranzas
│   │   ├── Modulo04/                            # Módulo 04: Inventario y Activos TI
│   │   ├── Modulo05/                            # Módulo 05: Salud, Tópico Médico y Psicología
│   │   │   ├── Controller/                      # Controladores del módulo (decorados con [Area("Modulo05")])
│   │   │   ├── Models/                          # Modelos DTO y Repositorios de datos
│   │   │   └── Views/                           # Vistas parciales o completas del módulo
│   │   ├── Modulo06/                            # Módulo 06: Mesa de Partes Virtual
│   │   └── Modulo07/                            # Módulo 07: Prácticas Pre-Profesionales
│   │
│   ├── wwwroot/                                 # Archivos estáticos (CSS, JS, imágenes oficiales)
│   ├── appsettings.json                         # Configuración base pública (con marcadores seguros)
│   └── Program.cs                               # Pipeline HTTP, cookies y registro de servicios
│
├── .env.example                                 # Plantilla de variables de entorno para desarrollo local
├── .gitmodules.example                          # Plantilla de configuración para submódulos de Git
└── contexto_bd_completa.sql                     # Script DDL completo de PostgreSQL 17
```

## 4. Guía de Inicio Rápido

Cualquier integrante del equipo o revisor puede ejecutar el sistema de inmediato utilizando cualquiera de las siguientes dos modalidades:

### Opción 1: Ejecutar en Modo Prototipo / Demo Local (Sin Base de Datos)

Esta modalidad permite ejecutar el sistema en cualquier equipo sin instalar PostgreSQL ni solicitar credenciales de conexión.

```bash
# 1. Clonar el repositorio
git clone https://github.com/paulocarreons-max/Login_Administracion_de_Modulos.git

# 2. Entrar al directorio del proyecto
cd Login_Administracion_de_Modulos

# 3. Compilar y ejecutar la aplicación
dotnet run --project Login_Administracion_de_Modulos/Login_Administracion_de_Modulos.csproj
```

Una vez levantado el servidor (por defecto en `http://localhost:5215`):
1. Abre tu navegador y accede a `http://localhost:5215`.
2. En la pantalla de Login, localiza la sección lateral **"Acceso Rápido - Modo Demo Local"**.
3. Haz clic sobre cualquiera de los 14 botones de rol (Tópico, Docente, Soporte, Administrador, etc.).
4. El sistema inicializará una sesión local de prueba válida en memoria y te conducirá directamente al panel correspondiente para probar componentes, navegación y vistas.

### Opción 2: Ejecutar Conectado a Base de Datos PostgreSQL (.env)

Por estándares de seguridad, las credenciales reales de la base de datos y los secretos de Google jamás se almacenan en `appsettings.json`.

1. Copia el archivo de plantilla `.env.example` y crea un archivo `.env` en la raíz del proyecto:
   ```bash
   cp .env.example .env
   ```

2. Abre `.env` con tu editor de texto y completa las variables requeridas:
   ```env
   # Cadena de conexión hacia tu instancia de PostgreSQL o Neon
   DB_CONNECTION_STRING=Host=ep-xxxx-xxxx.us-east-2.aws.neon.tech;Port=5432;Database=neondb;Username=usuario_neon;Password=tu_password;SSL Mode=Require;MaxPoolSize=10;

   # Credenciales de Google OAuth 2.0 (Opcional para SSO institucional)
   GOOGLE_CLIENT_ID=tu-google-client-id.apps.googleusercontent.com
   GOOGLE_CLIENT_SECRET=tu-google-client-secret
   ```

3. Si estás creando una base de datos local desde cero, ejecuta el script SQL estructurado en tu servidor PostgreSQL:
   * Archivo: `Login_Administracion_de_Modulos/contexto_bd_completa.sql`

4. Inicia la aplicación:
   ```bash
   dotnet run --project Login_Administracion_de_Modulos/Login_Administracion_de_Modulos.csproj
   ```
   `ConfiguracionSegura.cs` detectará automáticamente el archivo `.env`, inyectará la cadena de conexión en memoria y habilitará el formulario de ingreso real por DNI y Google SSO.

## 5. Cómo Integrar tu Módulo en este Repositorio

Para garantizar la independencia de cada equipo sin comprometer la estabilidad del Núcleo Central, se admiten dos flujos de trabajo:

### Modalidad A (Recomendada): Repositorio Propio mediante Submódulos de Git

Esta modalidad permite que tu equipo trabaje en su propio repositorio de GitHub independiente, con su propio historial de commits y ramas, sin necesidad de tener permisos de escritura sobre este repositorio padre.

#### Estructura requerida en tu repositorio independiente
El repositorio de tu módulo debe contener únicamente la carpeta funcional de tu área:
```
mi-repositorio-modulo04/
├── Controller/
│   └── InventarioController.cs
├── Models/
│   └── ActivoItem.cs
└── Views/
    └── inventario/
        └── index.cshtml
```

#### Reglas de código en tus controladores
Para que el motor de rutas de ASP.NET Core y el filtro de seguridad reconozcan tu módulo, decora tus controladores de la siguiente manera:

```csharp
using Microsoft.AspNetCore.Mvc;
using Login_Administracion_de_Modulos.Seguridad;

namespace MiOrganizacion.Modulo04.Controller
{
    [Area("Modulo04")]
    [Route("Modulo04/[controller]")]
    [ProtegerPanel("Encargado de Inventario", "Jefe General")]
    public class InventarioController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IConfiguration _configuration;

        public InventarioController(IConfiguration configuration)
        {
            _configuration = configuration;
            // Utilizar ConfiguracionSegura para obtener la conexión central
            string conn = ConfiguracionSegura.ObtenerCadenaConexion(_configuration);
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            // Obtener el ID del usuario actualmente autenticado
            int usuarioId = HttpContext.Session.GetInt32("UsuarioId") ?? 1;
            
            return View("~/Areas/Modulo04/Views/inventario/index.cshtml");
        }
    }
}
```

#### Vinculación en el Repositorio Padre
El administrador del repositorio central vincula tu repositorio ejecutando:
```bash
# Registrar tu repositorio como submódulo dentro de Areas/
git submodule add https://github.com/tu-organizacion/repositorio-modulo04.git Login_Administracion_de_Modulos/Areas/Modulo04

# Confirmar la vinculación
git commit -m "feat(submodule): vincular repositorio de Modulo04 Inventario"
```

Para actualizar los cambios de tu submódulo en cualquier momento:
```bash
git submodule update --remote --merge
```

### Modalidad B: Trabajo por Ramas dentro del Repositorio Central

Si tu equipo colabora directamente sobre este repositorio, se aplican reglas automáticas de gobernanza y protección de ramas.

#### 1. Convención obligatoria de nombres de rama
Tu rama de trabajo debe iniciar obligatoriamente con el identificador de tu módulo en minúsculas:
* `modulo01/gestion-matriculas`
* `modulo04/registro-bienes`
* `modulo05/triaje-enfermeria`

#### 2. Guardián Automático de CI/CD (`proteger-ramas-modulos.yml`)
Cada vez que realices `git push` o abras un Pull Request hacia `master`:
1. El workflow de GitHub Actions detecta el prefijo de tu rama (por ejemplo, `modulo04/...`).
2. Ejecuta un análisis de diferencias (`git diff --name-only`) respecto a la rama base.
3. **Verificación Estricta:** Si tu rama modifica archivos dentro de `Login_Administracion_de_Modulos/Areas/Modulo04/`, la verificación pasará exitosamente.
4. **Bloqueo Automático (Exit 1):** Si tu rama intenta modificar cualquier archivo del Núcleo (`Program.cs`, `LoginController.cs`, `appsettings.json`, `Views/`) o de otro módulo ajeno (`Areas/Modulo05/`), el workflow fallará automáticamente, bloqueando la integración e indicando la lista de archivos prohibidos.

## 6. Catálogo de Roles y Paneles del Sistema

El sistema cuenta con 14 roles configurados con rutas protegidas mediante `[ProtegerPanel]` y rutas de acceso rápido demo:

| N° | Rol Institucional | Ruta del Panel (`GET`) | Ruta Demo Local (`GET`) | Filtro `[ProtegerPanel]` |
| :---: | :--- | :--- | :--- | :--- |
| **01** | Enfermería (Tópico) | `/Dashboard/Topico` | `/login-demo/topico` | `"Enfermería (Tópico)", "Tópico"` |
| **02** | Psicología | `/Dashboard/Psicologia` | `/login-demo/psicologia` | `"Psicología", "Psicologia"` |
| **03** | Docente | `/Dashboard/Docente` | `/login-demo/docente` | `"Docente", "Profesor"` |
| **04** | Jefe de Soporte TI | `/Dashboard/Soporte` | `/login-demo/soporte` | `"Jefe de Soporte", "Soporte"` |
| **05** | Alumno / Estudiante | `/Dashboard/Alumno` | `/login-demo/alumno` | `"Alumno", "Estudiante"` |
| **06** | Practicante | `/Dashboard/Practicante` | `/login-demo/practicante` | `"Practicante"` |
| **07** | Secretaría Académica | `/Dashboard/Secretaria` | `/login-demo/secretaria` | `"Secretaría Académica", "Secretaria"` |
| **08** | Administrador Global | `/Dashboard/Administrador` | `/login-demo/admin` | `"Administrador", "Admin"` *(Acceso Global)* |
| **09** | Jefe General / Almacén | `/Dashboard/JefeGeneral` | `/login-demo/jefegeneral` | `"Jefe General", "Almacen"` |
| **10** | Jefe Taller Computación | `/Dashboard/JefeComputacion` | `/login-demo/computacion` | `"Jefe de Taller Computación"` |
| **11** | Jefe Taller Administración | `/Dashboard/JefeAdministracion` | `/login-demo/taller-administracion` | `"Jefe de Taller Administración"` |
| **12** | Jefe Taller Contabilidad | `/Dashboard/JefeContabilidad` | `/login-demo/taller-contabilidad` | `"Jefe de Taller Contabilidad"` |
| **13** | Encargado de Inventario | `/Dashboard/EncargadoInventario` | `/login-demo/inventario` | `"Encargado de Inventario", "Inventario"` |
| **14** | Tesorería | `/Dashboard/Tesoreria` | `/login-demo/tesoreria` | `"Tesorería", "Tesorero"` |

*Nota: El usuario con rol `Administrador` posee privilegios globales para auditar y acceder a todos los paneles y áreas del sistema.*

## 7. Documentación Complementaria del Repositorio

Para profundizar en aspectos específicos del proyecto, consulta los documentos especializados incluidos en este repositorio:

* 📄 **[Documentación Técnica de Arquitectura y Base de Datos](Login_Administracion_de_Modulos/Documentacion.md):** Especificación completa de endpoints, contratos JSON, modelos de datos y esquemas DDL de las tablas centrales.
* 🗄️ **[Esquema Completo de Base de Datos PostgreSQL](Login_Administracion_de_Modulos/contexto_bd_completa.sql):** Script DDL integral de PostgreSQL 17 con la definición de tablas para los 8 módulos institucionales.
* 📋 **[Especificación de Paneles Laterales y Vistas por Rol](CONTEXTO_PANELES_ROLES.md):** Detalle de componentes visuales, menús de navegación lateral y widgets asignados a cada uno de los 14 roles.
* 🚀 **[Guía de Comandos Git y Despliegue CI/CD](COMANDOS_GIT.md):** Procedimientos de control de versiones, comandos frecuentes de Git y flujo de publicación automática vía FTP a MonsterASP.
