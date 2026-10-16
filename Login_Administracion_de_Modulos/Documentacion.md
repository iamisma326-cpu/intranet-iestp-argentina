# 📘 Documentación Técnica del Sistema
## Módulo 8: Autenticación, Roles, Seguridad y Administración de Módulos

---

## 1. 📋 Resumen del Módulo
El **Módulo 8** implementa la capa central de seguridad, control de acceso basado en roles (**RBAC** - *Role-Based Access Control*) y administración de sesiones del instituto. Proporciona una experiencia de acceso dual unificada para la comunidad académica y administrativa, integrando tanto autenticación tradicional por credenciales como inicio de sesión institucional federado.

### Principales Características:
- **Autenticación Dual:**
  - **Local:** Acceso mediante DNI y contraseña cifrada.
  - **Federada:** Single Sign-On (SSO) institucional con Google (validación por correo institucional sin exigir clave local).
- **Control de Acceso Basado en Roles (RBAC):** Catálogo de perfiles (Docente, Alumno, Psicología, Tópico, Soporte, etc.) y asignación de permisos modulares granulares.
- **Auditoría de Accesos:** Registro del origen de la sesión, IP, método de login y expiración en la base de datos.
- **Arquitectura MVC en .NET 10:** Código estructurado en Controladores, Modelos y Vistas, desacoplado y mantenible.
- **Despliegue Continuo (CI/CD):** Pipeline con GitHub Actions que compila y publica automáticamente hacia MonsterASP al hacer push en la rama `master`.

---

## 2. 🏗️ Arquitectura y Stack Tecnológico

| Capa | Tecnología / Herramienta | Descripción |
| :--- | :--- | :--- |
| **Framework Web** | ASP.NET Core MVC (.NET 10) | Motor web principal, enrutamiento MVC y middlewares |
| **Lenguaje** | C# 14 / .NET 10 | Lógica del servidor y controladores |
| **Base de Datos** | PostgreSQL | Motor relacional para usuarios, roles, permisos y sesiones |
| **Driver de BD** | Npgsql (ADO.NET) | Conector de alto rendimiento para PostgreSQL |
| **Frontend** | Razor Views (`.cshtml`), HTML5, CSS3, Vanilla JS | Interfaz de usuario con diseño moderno y soporte móvil |
| **Librerías UI** | Bootstrap 5, Font Awesome, Google Fonts | Tipografía, iconos vectoriales y grilla responsiva |
| **CI/CD** | GitHub Actions | Automatización de compilación y publicación FTP |
| **Hosting** | MonsterASP | Servidor de alojamiento web para producción |

---

## 3. 📂 Estructura del Proyecto

```plaintext
Login_Administracion_de_Modulos/
├── .github/
│   └── workflows/
│       └── deploy.yml             # Pipeline de despliegue automático a MonsterASP
├── Login_Administracion_de_Modulos/
│   ├── Controlador/
│   │   └── LoginController.cs     # Controlador MVC: rutas de login DNI, Google y redirecciones
│   ├── Model/
│   │   └── Usuario.cs             # Modelo entidad de datos del usuario
│   ├── Vista/
│   │   ├── Dashboard/
│   │   │   ├── Administrador.cshtml # Panel de auditoría, usuarios y control RBAC
│   │   │   ├── Alumno.cshtml        # Portal del estudiante: notas, asistencia, tópico y soporte
│   │   │   ├── Docente.cshtml       # Panel de gestión académica e incidencias de aula
│   │   │   ├── Practicante.cshtml   # Panel de órdenes técnicas y revisión de laboratorios
│   │   │   ├── Psicologia.cshtml    # Panel de bienestar y expedientes psicológicos
│   │   │   ├── Secretaria.cshtml    # Panel de padrón institucional, personas y carreras
│   │   │   ├── Soporte.cshtml       # Panel de bienes, almacenes, ambientes e incidencias TI
│   │   │   └── Topico.cshtml        # Panel de atenciones de enfermería, salud y botica
│   │   ├── Shared/
│   │   │   ├── _Layout.cshtml     # Plantilla maestra común
│   │   │   └── _ValidationScriptsPartial.cshtml
│   │   ├── Index.cshtml           # Vista principal con formulario dual (DNI / Google SSO)
│   │   ├── Error.cshtml           # Vista amigable de control de excepciones
│   │   └── Privacy.cshtml         # Políticas de privacidad
│   ├── wwwroot/
│   │   ├── css/
│   │   │   ├── login.css          # Estilos personalizados de la interfaz de login
│   │   │   ├── dashboard.css      # Estilos visuales de los dashboards por rol
│   │   │   └── site.css           # Estilos globales de la aplicación
│   │   ├── js/                    # Scripts de frontend
│   │   └── lib/                   # Librerías cliente (Bootstrap, jQuery)
│   ├── appsettings.json           # Configuración de conexiones de BD y entorno
│   ├── Documentacion.md           # Este archivo de especificación técnica y SQL
│   ├── Login_Administracion_de_Modulos.csproj
│   └── Program.cs                 # Punto de entrada, configuración de servicios y middlewares
├── COMANDOS_GIT.md                # Guía de comandos Git para ramas y despliegue
└── README.md                      # Resumen general del repositorio
```

---

## 4. 🔀 Flujo y Rutas de Autenticación

El controlador principal `LoginController` expone las siguientes rutas:

### 1. `GET /` o `GET /Login`
- **Método:** `Index()`
- **Comportamiento:** Renderiza la vista `Vista/Index.cshtml`, permitiendo al usuario seleccionar entre login por DNI y acceso mediante Google SSO.

### 2. `POST /login-dni`
- **Método:** `LoginDni(string dni, string clave)`
- **Parámetros:** `dni` (número de documento) y `clave` (contraseña ingresada).
- **Proceso:**
  1. Conecta a PostgreSQL usando la cadena de conexión `DefaultConnection`.
  2. Consulta la tabla `usuarios` verificando que `dni_login = @dni` y `estado = 'Activo'`.
  3. Compara la clave ingresada contra `clave_cifrada`.
  4. Si las credenciales son válidas, registra la sesión y redirige a la acción de bienvenida o dashboard correspondiente.
  5. En caso de error, retorna a `Index.cshtml` con un mensaje descriptivo en `ViewBag.Error`.

### 3. `POST /login-google`
- **Método:** `LoginGoogle(string correo)`
- **Parámetros:** `correo` (correo electrónico institucional autenticado).
- **Proceso:**
  1. Valida la existencia del correo en la tabla `usuarios` donde `estado = 'Activo'`.
  2. No solicita ni valida contraseña (confianza delegada en el proveedor SSO).
  3. Registra la sesión en la base de datos con método `GOOGLE_SSO`.
  4. Responde con un payload JSON `{ success: true, redirectUrl: "/Login/Bienvenido" }`.

### 4. `GET /Login/Bienvenido`
- **Método:** `Bienvenido()`
- **Comportamiento:** Punto de entrada posterior a la validación satisfactoria previa a la navegación hacia el dashboard del rol asignado.

---

## 5. 🗄️ Esquema de Base de Datos (PostgreSQL)

A continuación se presenta el script DDL completo para la creación del esquema relacional, catálogos por defecto y disparadores de auditoría:

```sql
-- ====================================================================================
-- MÓDULO 8: AUTENTICACIÓN, ROLES Y SEGURIDAD (PostgreSQL)
-- Lógica: Login Dual (DNI local o Google SSO por validación de correo único)
-- ====================================================================================

-- (Opcional/Referencia) Creación mínima de la tabla 'personas' del Módulo 1 
-- para que las llaves foráneas de este script funcionen si lo pruebas desde cero.
CREATE TABLE IF NOT EXISTS personas (
    id_persona INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo_documento VARCHAR(10) NOT NULL,
    numero_documento VARCHAR(20) UNIQUE NOT NULL,
    nombres VARCHAR(100) NOT NULL,
    apellidos VARCHAR(100) NOT NULL
);

-- ------------------------------------------------------------------------------------
-- 1. TABLA: roles
-- Catálogo de perfiles del sistema (Alumno, Docente, Psicología, Enfermería, etc.)
-- ------------------------------------------------------------------------------------
CREATE TABLE roles (
    id_rol INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre VARCHAR(50) UNIQUE NOT NULL,
    descripcion TEXT,
    estado VARCHAR(15) DEFAULT 'Activo' NOT NULL,
    creado_el TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    -- Regla de Dominio: Solo permite estados válidos
    CONSTRAINT chk_roles_estado CHECK (estado IN ('Activo', 'Inactivo'))
);

-- ------------------------------------------------------------------------------------
-- 2. TABLA: usuarios
-- Núcleo de acceso. Soporta login local (DNI + Clave) o Google SSO (Correo + Sin Clave)
-- ------------------------------------------------------------------------------------
CREATE TABLE usuarios (
    id_usuario INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_persona INTEGER NOT NULL,
    dni_login VARCHAR(20) UNIQUE NOT NULL,
    correo VARCHAR(100) UNIQUE NOT NULL,
    clave_cifrada VARCHAR(255), -- NULLABLE: Si solo entra con Google, no necesita clave
    estado VARCHAR(15) DEFAULT 'Activo' NOT NULL,
    ultimo_acceso TIMESTAMP,
    creado_el TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    actualizado_el TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Relación con Módulo 1 (Cada usuario pertenece a una única persona)
    CONSTRAINT fk_usuarios_persona FOREIGN KEY (id_persona) REFERENCES personas(id_persona) ON DELETE RESTRICT,
    -- Regla: Una persona solo puede tener una cuenta de usuario en todo el instituto
    CONSTRAINT uq_usuarios_persona UNIQUE (id_persona),
    -- Regla de Dominio: Control estricto del estado de la cuenta
    CONSTRAINT chk_usuarios_estado CHECK (estado IN ('Activo', 'Inactivo', 'Bloqueado'))
);

-- ------------------------------------------------------------------------------------
-- 3. TABLA: usuarios_roles
-- Rompe la relación muchos a muchos. Un usuario (Docente) puede ser también (Soporte).
-- ------------------------------------------------------------------------------------
CREATE TABLE usuarios_roles (
    id_usuario INTEGER NOT NULL,
    id_rol INTEGER NOT NULL,
    estado VARCHAR(15) DEFAULT 'Activo' NOT NULL,
    fecha_asignacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (id_usuario, id_rol), -- Regla: Un usuario no puede tener el mismo rol 2 veces
    CONSTRAINT fk_ur_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    CONSTRAINT fk_ur_rol FOREIGN KEY (id_rol) REFERENCES roles(id_rol) ON DELETE RESTRICT,
    CONSTRAINT chk_ur_estado CHECK (estado IN ('Activo', 'Revocado'))
);

-- ------------------------------------------------------------------------------------
-- 4. TABLA: permisos_modulos (RBAC)
-- Define a qué partes del sistema da acceso un rol (ej. 'Topico_Enfermeria')
-- ------------------------------------------------------------------------------------
CREATE TABLE permisos_modulos (
    id_permiso INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_modulo VARCHAR(100) UNIQUE NOT NULL,
    nivel_acceso VARCHAR(20) NOT NULL,
    descripcion TEXT
);

-- ------------------------------------------------------------------------------------
-- 5. TABLA: roles_permisos
-- Asigna los permisos específicos a cada rol.
-- ------------------------------------------------------------------------------------
CREATE TABLE roles_permisos (
    id_rol INTEGER NOT NULL,
    id_permiso INTEGER NOT NULL,

    PRIMARY KEY (id_rol, id_permiso),
    CONSTRAINT fk_rp_rol FOREIGN KEY (id_rol) REFERENCES roles(id_rol) ON DELETE CASCADE,
    CONSTRAINT fk_rp_permiso FOREIGN KEY (id_permiso) REFERENCES permisos_modulos(id_permiso) ON DELETE CASCADE
);

-- ------------------------------------------------------------------------------------
-- 6. TABLA: sesiones_login
-- Auditoría de seguridad: Registra CÓMO entró el usuario (DNI o Google) y desde dónde.
-- ------------------------------------------------------------------------------------
CREATE TABLE sesiones_login (
    id_sesion UUID DEFAULT gen_random_uuid() PRIMARY KEY,
    id_usuario INTEGER NOT NULL,
    metodo_autenticacion VARCHAR(20) NOT NULL,
    ip_origen VARCHAR(45),
    fecha_inicio TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    fecha_expiracion TIMESTAMP NOT NULL,
    estado VARCHAR(15) DEFAULT 'Activa' NOT NULL,

    CONSTRAINT fk_sesiones_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    CONSTRAINT chk_sesiones_metodo CHECK (metodo_autenticacion IN ('DNI_LOCAL', 'GOOGLE_SSO')),
    CONSTRAINT chk_sesiones_estado CHECK (estado IN ('Activa', 'Expirada', 'Cerrada'))
);

-- ====================================================================================
-- INSERCIÓN DE DATOS POR DEFECTO (CATÁLOGOS)
-- ====================================================================================

-- Insertar los roles solicitados
INSERT INTO roles (nombre, descripcion) VALUES
('Alumno', 'Estudiante matriculado o egresado del instituto'),
('Docente', 'Personal docente con carga académica'),
('Enfermería (Tópico)', 'Personal de salud a cargo de las atenciones físicas (Módulo 5)'),
('Psicología', 'Personal a cargo del bienestar mental y citas psicológicas (Módulo 5)'),
('Jefe de Soporte', 'Jefatura de laboratorios, inventario y practicantes'),
('Practicante', 'Estudiante en horas de práctica de soporte (Módulo 6)'),
('Secretaría Académica', 'Motor operativo, trámites y egresos'),
('Administrador', 'Acceso total y configuración del sistema');

-- Insertar algunos permisos clave como ejemplo
INSERT INTO permisos_modulos (nombre_modulo, nivel_acceso, descripcion) VALUES
('portal_academico', 'Total', 'Acceso a notas, sílabos y asistencias'),
('topico_salud', 'Total', 'Acceso a historiales médicos de alumnos (Privado)'),
('bienestar_psicologico', 'Total', 'Acceso a expedientes psicológicos (Privado)'),
('soporte_inventario', 'Total', 'Gestión de reservas y mantenimiento de equipos');

-- Enlazar los roles sensibles con sus módulos (Ejemplo)
-- Asignar permiso "topico_salud" (id 2) a "Enfermería (Tópico)" (id 3)
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (3, 2);
-- Asignar permiso "bienestar_psicologico" (id 3) a "Psicología" (id 4)
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (4, 3);
-- Asignar permiso "soporte_inventario" (id 4) a "Jefe de Soporte" (id 5)
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (5, 4);

-- ====================================================================================
-- FUNCIÓN / TRIGGER PARA TRAZABILIDAD AUTOMÁTICA
-- (Asegura que el campo actualizado_el cambie solo cada vez que modifican un usuario)
-- ====================================================================================
CREATE OR REPLACE FUNCTION actualizar_timestamp()
RETURNS TRIGGER AS $$
BEGIN
   NEW.actualizado_el = CURRENT_TIMESTAMP;
   RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER tr_usuarios_actualizado
BEFORE UPDATE ON usuarios
FOR EACH ROW
EXECUTE FUNCTION actualizar_timestamp();
```

---

## 6. ⚙️ Configuración del Entorno (`appsettings.json`)

Para conectar la aplicación con PostgreSQL, configura la cadena de conexión dentro de `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=TU_HOST_POSTGRES;Port=5432;Database=TU_BASE_DATOS;Username=TU_USUARIO;Password=TU_PASSWORD;SSL Mode=Prefer;Trust Server Certificate=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

## 7. 🚀 Despliegue e Integración Continua (CI/CD)

El archivo `.github/workflows/deploy.yml` orquesta la compilación y entrega continua:

1. **Disparador:** Cada `push` hacia la rama **`master`** inicia automáticamente el pipeline en GitHub Actions.
2. **Entorno de compilación:** Contenedor `ubuntu-latest` con el SDK oficial de **.NET 10**.
3. **Publicación:** Ejecuta `dotnet publish -c Release -o ./publish_output`.
4. **Sincronización:** Mediante la acción `SamKirkland/FTP-Deploy-Action@v4.3.5`, transfiere el contenido generado al directorio `/wwwroot/` de **MonsterASP**.

> [!TIP]
> Para consultar cómo gestionar ramas, preparar commits y sincronizar con GitHub, consulta el documento:
> 👉 [Guía de Comandos Git (COMANDOS_GIT.md)](file:///c:/Users/Arias/Videos/Login_Administracion_de_Modulos/COMANDOS_GIT.md)