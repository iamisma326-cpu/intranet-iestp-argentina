-- ============================================================================
-- NÚCLEO DEL SISTEMA — Esquema `core` (PostgreSQL 16+ / Neon)
-- ============================================================================
-- Modelo institucional compartido, adaptado del proyecto Intranet Modular
-- (AQUILA) al estándar de este repositorio. Es IDEMPOTENTE: puede ejecutarse
-- las veces que sea sin romper datos existentes.
--
-- Contrato con la aplicación:
--   * Login DNI  → core.usuarios.clave_cifrada (PBKDF2$... o BCrypt) + core.personas.numero_documento
--   * Google SSO → core.usuarios.correo
--   * Roles      → core.usuarios_roles (estado 'Activo') + core.roles
--   * Auditoría  → core.intentos_login y core.sesiones_login
--
-- Ejecutar CONECTADO COMO EL ROL DE ADMINISTRACIÓN de Neon (el que crea el
-- proyecto). Los roles por equipo se crean en roles_equipos.sql.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS core;

-- ---------------------------------------------------------------------------
-- PERSONAS — padrón institucional (alumnos, docentes, personal)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.personas (
    id_persona      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo_documento   varchar(20)  NOT NULL DEFAULT 'DNI',
    numero_documento varchar(15)  NOT NULL UNIQUE,
    nombres          varchar(100) NOT NULL,
    apellidos         varchar(100) NOT NULL,
    correo           varchar(150),
    telefono         varchar(30),
    creado_el        timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ---------------------------------------------------------------------------
-- USUARIOS — credenciales de acceso al sistema
--   clave_cifrada: PBKDF2$iteraciones$salt$hash  (generado por PasswordSecurity)
--                  o BCrypt ($2a$/$2b$/$2y$). NULL = cuenta solo-Google.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.usuarios (
    id_usuario     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_persona     integer NOT NULL UNIQUE REFERENCES core.personas(id_persona) ON DELETE RESTRICT,
    correo         varchar(150) UNIQUE,
    clave_cifrada  varchar(300),
    estado         varchar(20)  NOT NULL DEFAULT 'Activo'
                   CHECK (estado IN ('Activo', 'Inactivo', 'Bloqueado')),
    ultimo_acceso  timestamptz,
    creado_el      timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    actualizado_el timestamptz
);

-- ---------------------------------------------------------------------------
-- ROLES Y ASIGNACIÓN
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.roles (
    id_rol      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre      varchar(60) NOT NULL UNIQUE,
    descripcion text,
    estado      varchar(20) NOT NULL DEFAULT 'Activo',
    creado_el   timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS core.usuarios_roles (
    id_usuario      integer NOT NULL REFERENCES core.usuarios(id_usuario) ON DELETE CASCADE,
    id_rol          integer NOT NULL REFERENCES core.roles(id_rol) ON DELETE RESTRICT,
    estado          varchar(20) NOT NULL DEFAULT 'Activo',
    fecha_asignacion timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id_usuario, id_rol)
);

-- ---------------------------------------------------------------------------
-- AUDITORÍA DE ACCESOS (la escribe el LoginController)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.intentos_login (
    id_intentos_login    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    identificador_intentado varchar(100) NOT NULL,
    id_usuario           integer REFERENCES core.usuarios(id_usuario) ON DELETE SET NULL,
    exito                boolean NOT NULL,
    motivo               varchar(150),
    ip_origen            varchar(45),
    user_agent           varchar(250),
    fecha                timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS core.sesiones_login (
    id_sesion             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    id_usuario            integer NOT NULL REFERENCES core.usuarios(id_usuario) ON DELETE CASCADE,
    metodo_autenticacion  varchar(20) NOT NULL CHECK (metodo_autenticacion IN ('DNI_LOCAL', 'GOOGLE_SSO')),
    ip_origen             varchar(45),
    fecha_inicio          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_expiracion      timestamptz NOT NULL,
    estado                varchar(15) NOT NULL DEFAULT 'Activa'
                          CHECK (estado IN ('Activa', 'Expirada', 'Cerrada'))
);

-- ---------------------------------------------------------------------------
-- PERMISOS DE MÓDULOS POR ROL (gobernanza declarativa, editable luego)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS core.permisos_modulos (
    id_permiso     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_modulo  varchar(60) NOT NULL UNIQUE,   -- ej: 'Modulo05'
    nivel_acceso   varchar(30) NOT NULL DEFAULT 'Lectura',
    descripcion    text
);

CREATE TABLE IF NOT EXISTS core.roles_permisos (
    id_rol     integer NOT NULL REFERENCES core.roles(id_rol) ON DELETE CASCADE,
    id_permiso integer NOT NULL REFERENCES core.permisos_modulos(id_permiso) ON DELETE CASCADE,
    PRIMARY KEY (id_rol, id_permiso)
);

-- ============================================================================
-- DATOS INICIALES (solo si no existen — idempotente)
-- ============================================================================

-- Catálogo de roles institucionales (12, alineados con el sistema académico)
INSERT INTO core.roles (nombre, descripcion) VALUES
    ('Administrador',        'Acceso global al sistema'),
    ('Director',             'Dirección general del IESTP'),
    ('Docente',              'Personal docente'),
    ('Alumno',               'Estudiante matriculado'),
    ('Secretaría Académica', 'Secretaría académica'),
    ('Tesorería',            'Tesorería y pagos'),
    ('Enfermería (Tópico)',  'Tópico de salud'),
    ('Psicología',           'Bienestar psicológico'),
    ('Jefe de Soporte',      'Soporte TI'),
    ('Practicante',          'Practicante de apoyo'),
    ('Jefe General',         'Jefatura general / almacén'),
    ('Encargado de Inventario', 'Inventario de bienes')
ON CONFLICT (nombre) DO NOTHING;

-- Módulos registrados para la gobernanza de permisos
INSERT INTO core.permisos_modulos (nombre_modulo, nivel_acceso, descripcion) VALUES
    ('Modulo01', 'Total', 'Matrícula y alumno'),
    ('Modulo02', 'Total', 'Gestión docente / asistencia'),
    ('Modulo03', 'Total', 'Inventario y activos TI'),
    ('Modulo04', 'Total', 'Módulo del equipo 04'),
    ('Modulo05', 'Total', 'Salud: tópico y psicología'),
    ('Modulo06', 'Total', 'Módulo del equipo 06'),
    ('Modulo07', 'Total', 'Módulo del equipo 07'),
    ('Modulo08', 'Total', 'Login, seguridad y dashboards')
ON CONFLICT (nombre_modulo) DO NOTHING;

-- ============================================================================
-- USUARIOS DEMO — personal institucional de prueba
-- La contraseña de TODOS los usuarios demo es: Demo2026!
-- (hash PBKDF2 generado con PasswordSecurity.HashearClave, 100k iteraciones)
-- ¡REEMPLAZAR EN PRODUCCIÓN! Son solo para desarrollo y validación del login.
-- ============================================================================

INSERT INTO core.personas (tipo_documento, numero_documento, nombres, apellidos, correo) VALUES
    ('DNI', '10000001', 'Felipe',      'Ostos',         'felipe.ostos@istpargentina.edu.pe'),
    ('DNI', '87654321', 'Ana',         'Torres',        'ana.torres@istpargentina.edu.pe'),
    ('DNI', '30000001', 'Carmen',      'Flores',        'carmen.flores@istpargentina.edu.pe'),
    ('DNI', '40000001', 'Marta',       'Torres Quispe', 'marta.torres@istpargentina.edu.pe'),
    ('DNI', '51000006', 'Rosa',        'Fernández',     'rosa.fernandez@istpargentina.edu.pe'),
    ('DNI', '51000007', 'Juan',        'Salazar',       'juan.salazar@istpargentina.edu.pe'),
    ('DNI', '12345678', 'Sheyla',      'Ríos',          'sheyla.rios@istpargentina.edu.pe'),
    ('DNI', '20000001', 'Carlos',      'Rodríguez',     'carlos.rodriguez@istpargentina.edu.pe')
ON CONFLICT (numero_documento) DO NOTHING;

-- clave 'Demo2026!' hasheada PBKDF2 (100000 iteraciones, SHA-256, salt 16B)
-- Se usa la misma para todos los demo: cada quien la cambia al primer login real.
INSERT INTO core.usuarios (id_persona, correo, clave_cifrada, estado)
SELECT p.id_persona, p.correo,
       'PBKDF2$100000$' ||
       encode(gen_random_bytes(16), 'base64') || '$' ||
       '' -- placeholder: el hash real se inserta abajo por UPDATE (ver nota)
       , 'Activo'
FROM core.personas p
WHERE p.numero_documento IN ('10000001','87654321','30000001','40000001','51000006','51000007','12345678','20000001')
ON CONFLICT (id_persona) DO NOTHING;

-- NOTA sobre el INSERT anterior: el hash PBKDF2 no puede generarse en SQL puro.
-- Ejecutar tras este script el comando de la app (o el script hashear_demo.py)
-- que hashea 'Demo2026!' con PasswordSecurity.HashearClave y hace UPDATE.
-- Alternativa rápida con pgcrypto para uso de SOLO desarrollo:
UPDATE core.usuarios
SET clave_cifrada = crypt('Demo2026!', gen_salt('bf', 10))
WHERE clave_cifrada = '' OR clave_cifrada IS NULL;

-- ---------------------------------------------------------------------------
-- Asignación de roles demo
-- ---------------------------------------------------------------------------
INSERT INTO core.usuarios_roles (id_usuario, id_rol, estado)
SELECT u.id_usuario, r.id_rol, 'Activo'
FROM core.usuarios u
JOIN core.personas p ON p.id_persona = u.id_persona
JOIN core.roles r ON (
    (p.numero_documento = '10000001' AND r.nombre = 'Administrador') OR
    (p.numero_documento = '87654321' AND r.nombre = 'Alumno') OR
    (p.numero_documento = '30000001' AND r.nombre = 'Secretaría Académica') OR
    (p.numero_documento = '40000001' AND r.nombre = 'Tesorería') OR
    (p.numero_documento = '51000006' AND r.nombre = 'Enfermería (Tópico)') OR
    (p.numero_documento = '51000007' AND r.nombre = 'Psicología') OR
    (p.numero_documento = '12345678' AND r.nombre = 'Docente') OR
    (p.numero_documento = '20000001' AND r.nombre = 'Docente')
)
ON CONFLICT (id_usuario, id_rol) DO NOTHING;

-- ============================================================================
-- FIN. Verificación rápida:
--   SELECT COUNT(*) FROM core.roles;         -- 12
--   SELECT COUNT(*) FROM core.personas;      -- 8
--   SELECT u.correo, r.nombre FROM core.usuarios u
--     JOIN core.usuarios_roles ur ON ur.id_usuario = u.id_usuario
--     JOIN core.roles r ON r.id_rol = ur.id_rol;  -- 8 usuarios con su rol
-- ============================================================================
