-- =====================================================================
-- CONTEXTO COMPLETO DE LA BASE DE DATOS (PostgreSQL 17) — estado actual
-- Incluye la actualización a ENUM de las columnas "estado".
-- Uso: pegar este archivo como contexto al trabajar con una IA.
--
-- NOTAS PARA LA IA
--  * Todas las PK "id_*" son GENERATED ALWAYS AS IDENTITY (para insertar
--    ids manuales se requiere OVERRIDING SYSTEM VALUE).
--  * Esquema: public. Idioma de nombres: español, snake_case.
--  * Módulos del sistema:
--      - Inventario / soporte: especialidad, almacen, ambiente, bien,
--        asignacion, transferencia, inventario_anual, incidencias,
--        requerimientos
--      - Salud (Módulo 5): botica_medicamentos, atenciones_topico,
--        atenciones_psicologia
--      - Seguridad / acceso: personas, usuarios, roles, usuarios_roles,
--        permisos_modulos, roles_permisos, sesiones_login
--  * Una persona (personas) tiene como máximo 1 usuario (usuarios.id_persona UNIQUE).
--    Un usuario puede tener varios roles (usuarios_roles).
--    Login: DNI local (dni_login + clave_cifrada) o Google SSO.
--  * Tablas SIN datos actualmente: especialidad, almacen, ambiente, bien,
--    asignacion, transferencia, inventario_anual, incidencias,
--    requerimientos, botica_medicamentos, atenciones_topico, sesiones_login.
-- =====================================================================

-- ---------------------------------------------------------------------
-- TIPOS ENUM
-- ---------------------------------------------------------------------
CREATE TYPE estado_general    AS ENUM ('Activo', 'Inactivo');
CREATE TYPE estado_bien       AS ENUM ('Bueno', 'Regular', 'Malo', 'En reparación', 'De baja');
CREATE TYPE estado_asignacion AS ENUM ('Vigente', 'Finalizada', 'Anulada');

-- =====================================================================
-- ESQUEMA
-- =====================================================================

-- ===== INVENTARIO (según diagrama ER) ================================

CREATE TABLE especialidad (
    id_especialidad integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre          varchar(100)   NOT NULL,
    estado          estado_general NOT NULL DEFAULT 'Activo'
);

CREATE TABLE almacen (                       -- N:1 con especialidad
    id_almacen      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_especialidad integer        NOT NULL CONSTRAINT fk_almacen_especialidad REFERENCES especialidad(id_especialidad),
    nombre          varchar(100)   NOT NULL,
    ubicacion       varchar(150),
    responsable     varchar(100),
    estado          estado_general NOT NULL DEFAULT 'Activo'
);

CREATE TABLE ambiente (                      -- N:1 con especialidad
    id_ambiente     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_especialidad integer        NOT NULL CONSTRAINT fk_ambiente_especialidad REFERENCES especialidad(id_especialidad),
    nombre          varchar(100)   NOT NULL,
    estado          estado_general NOT NULL DEFAULT 'Activo'
);

CREATE TABLE bien (                          -- N:1 con almacen
    id_bien         integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_almacen      integer      NOT NULL CONSTRAINT fk_bien_almacen REFERENCES almacen(id_almacen),
    cbi             varchar(30)  UNIQUE,
    descripcion     varchar(150) NOT NULL,
    tipo_bien       varchar(50)  NOT NULL,
    marca           varchar(50),
    modelo          varchar(50),
    serie           varchar(50)  UNIQUE,
    procedencia     varchar(50),
    fecha_ingreso   date         NOT NULL DEFAULT CURRENT_DATE,
    estado          estado_bien  NOT NULL DEFAULT 'Bueno',
    observacion     text
);

CREATE TABLE asignacion (                    -- bien asignado a un ambiente
    id_asignacion    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien          integer      NOT NULL CONSTRAINT fk_asig_bien     REFERENCES bien(id_bien),
    id_ambiente      integer      NOT NULL CONSTRAINT fk_asig_ambiente REFERENCES ambiente(id_ambiente),
    responsable      varchar(100) NOT NULL,
    fecha_asignacion date         NOT NULL DEFAULT CURRENT_DATE,
    fecha_fin        date,
    estado           estado_asignacion NOT NULL DEFAULT 'Vigente',
    observacion      text
);

CREATE TABLE transferencia (                 -- movimiento de un bien entre ambientes
    id_transferencia    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien             integer      NOT NULL CONSTRAINT fk_transf_bien    REFERENCES bien(id_bien),
    id_ambiente_origen  integer      NOT NULL CONSTRAINT fk_transf_origen  REFERENCES ambiente(id_ambiente),
    id_ambiente_destino integer      NOT NULL CONSTRAINT fk_transf_destino REFERENCES ambiente(id_ambiente),
    fecha               date         NOT NULL DEFAULT CURRENT_DATE,
    motivo              varchar(150) NOT NULL,
    dni_responsable     char(8)      NOT NULL,
    observacion         text
);

CREATE TABLE inventario_anual (              -- verificación anual por bien
    id_inventario      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien            integer     NOT NULL CONSTRAINT fk_inv_bien REFERENCES bien(id_bien),
    anio               smallint    NOT NULL CONSTRAINT inventario_anual_anio_check CHECK (anio BETWEEN 1901 AND 2155),
    fecha_verificacion date        NOT NULL DEFAULT CURRENT_DATE,
    resultado          varchar(50) NOT NULL,
    observacion        text
);

-- ===== SEGURIDAD / ACCESO ============================================

CREATE TABLE personas (
    id_persona       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tipo_documento   varchar(10)  NOT NULL,
    numero_documento varchar(20)  NOT NULL UNIQUE,
    nombres          varchar(100) NOT NULL,
    apellidos        varchar(100) NOT NULL
);

CREATE TABLE usuarios (                      -- 1:1 con personas
    id_usuario     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_persona     integer      NOT NULL CONSTRAINT uq_usuarios_persona UNIQUE
                                CONSTRAINT fk_usuarios_persona REFERENCES personas(id_persona) ON DELETE RESTRICT,
    dni_login      varchar(20)  NOT NULL UNIQUE,
    correo         varchar(100) NOT NULL UNIQUE,
    clave_cifrada  varchar(255),             -- NULL si entra solo por Google SSO
    estado         varchar(15)  NOT NULL DEFAULT 'Activo'
                                CONSTRAINT chk_usuarios_estado CHECK (estado IN ('Activo','Inactivo','Bloqueado')),
    ultimo_acceso  timestamp,
    creado_el      timestamp DEFAULT CURRENT_TIMESTAMP,
    actualizado_el timestamp DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE roles (
    id_rol      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre      varchar(50) NOT NULL UNIQUE,
    descripcion text,
    estado      varchar(15) NOT NULL DEFAULT 'Activo'
                            CONSTRAINT chk_roles_estado CHECK (estado IN ('Activo','Inactivo')),
    creado_el   timestamp DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE usuarios_roles (                -- N:M usuarios <-> roles
    id_usuario       integer     NOT NULL CONSTRAINT fk_ur_usuario REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    id_rol           integer     NOT NULL CONSTRAINT fk_ur_rol     REFERENCES roles(id_rol)       ON DELETE RESTRICT,
    estado           varchar(15) NOT NULL DEFAULT 'Activo'
                                 CONSTRAINT chk_ur_estado CHECK (estado IN ('Activo','Revocado')),
    fecha_asignacion timestamp DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id_usuario, id_rol)
);

CREATE TABLE permisos_modulos (
    id_permiso    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre_modulo varchar(100) NOT NULL UNIQUE,
    nivel_acceso  varchar(20)  NOT NULL,
    descripcion   text
);

CREATE TABLE roles_permisos (                -- N:M roles <-> permisos_modulos
    id_rol     integer NOT NULL CONSTRAINT fk_rp_rol     REFERENCES roles(id_rol)               ON DELETE CASCADE,
    id_permiso integer NOT NULL CONSTRAINT fk_rp_permiso REFERENCES permisos_modulos(id_permiso) ON DELETE CASCADE,
    PRIMARY KEY (id_rol, id_permiso)
);

CREATE TABLE sesiones_login (
    id_sesion            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    id_usuario           integer     NOT NULL CONSTRAINT fk_sesiones_usuario REFERENCES usuarios(id_usuario) ON DELETE CASCADE,
    metodo_autenticacion varchar(20) NOT NULL
                                     CONSTRAINT chk_sesiones_metodo CHECK (metodo_autenticacion IN ('DNI_LOCAL','GOOGLE_SSO')),
    ip_origen            varchar(45),
    fecha_inicio         timestamp   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_expiracion     timestamp   NOT NULL,
    estado               varchar(15) NOT NULL DEFAULT 'Activa'
                                     CONSTRAINT chk_sesiones_estado CHECK (estado IN ('Activa','Expirada','Cerrada'))
);

-- ===== SALUD (Módulo 5) ==============================================

CREATE TABLE botica_medicamentos (
    id_medicamento    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre            varchar(100) NOT NULL,
    presentacion      varchar(50)  NOT NULL,
    stock_actual      integer      NOT NULL DEFAULT 0 CONSTRAINT botica_medicamentos_stock_actual_check CHECK (stock_actual >= 0),
    fecha_vencimiento date         NOT NULL,
    estado            varchar(20)  DEFAULT 'Disponible'
                                   CONSTRAINT botica_medicamentos_estado_check CHECK (estado IN ('Disponible','Por vencer','Agotado'))
);

CREATE TABLE atenciones_topico (             -- atención física/enfermería
    id_atencion_top integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    persona_id      integer      NOT NULL CONSTRAINT fk_topico_persona     REFERENCES personas(id_persona),
    atendido_por_id integer      NOT NULL CONSTRAINT fk_topico_usuario     REFERENCES usuarios(id_usuario),
    fecha_hora      timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    motivo          varchar(250) NOT NULL,
    medicamento_id  integer      CONSTRAINT fk_topico_medicamento REFERENCES botica_medicamentos(id_medicamento),
    es_alergico     boolean      NOT NULL DEFAULT false,
    detalle_alergia varchar(250),
    seguro_salud    varchar(20)  DEFAULT 'NINGUNO'
                                 CONSTRAINT atenciones_topico_seguro_salud_check CHECK (seguro_salud IN ('SIS','ESSALUD','EPS','NINGUNO')),
    observacion     text
);

CREATE TABLE atenciones_psicologia (         -- atención psicológica
    id_atencion_psico integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    persona_id        integer      NOT NULL CONSTRAINT fk_psico_persona REFERENCES personas(id_persona),
    psicologo_id      integer      NOT NULL CONSTRAINT fk_psico_usuario REFERENCES usuarios(id_usuario),
    fecha_hora        timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    motivo_consulta   varchar(250) NOT NULL,
    observacion_notas text,
    estado_atencion   varchar(30)  DEFAULT 'Pendiente'
                                   CONSTRAINT atenciones_psicologia_estado_atencion_check
                                   CHECK (estado_atencion IN ('Pendiente','En proceso','Dado de Alta','Derivado externamente'))
);

-- ===== SOPORTE: incidencias y requerimientos =========================

CREATE TABLE incidencias (                   -- fallas reportadas sobre un bien
    id_incidencia     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien           integer      NOT NULL CONSTRAINT fk_inci_bien      REFERENCES bien(id_bien),
    reportador_id     integer      NOT NULL CONSTRAINT fk_inci_reportador REFERENCES usuarios(id_usuario),
    tecnico_id        integer      CONSTRAINT fk_inci_tecnico REFERENCES usuarios(id_usuario),
    fecha_reporte     timestamp    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_cierre      timestamp,
    tipo_falla        varchar(100) NOT NULL,
    gravedad          varchar(20)  DEFAULT 'Media'
                                   CONSTRAINT incidencias_gravedad_check CHECK (gravedad IN ('Baja','Media','Alta','Crítica')),
    descripcion       text         NOT NULL,
    solucion_aplicada text,
    estado            varchar(30)  DEFAULT 'Abierta'
                                   CONSTRAINT incidencias_estado_check CHECK (estado IN ('Abierta','En diagnóstico','En espera de repuesto','Reparado','Baja Definitiva'))
);

CREATE TABLE requerimientos (                -- solicitudes de servicio
    id_requerimiento       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    solicitante_id         integer     NOT NULL CONSTRAINT fk_req_solicitante REFERENCES usuarios(id_usuario),
    tecnico_id             integer     CONSTRAINT fk_req_tecnico  REFERENCES usuarios(id_usuario),
    id_ambiente            integer     CONSTRAINT fk_req_ambiente REFERENCES ambiente(id_ambiente),
    tipo_servicio          varchar(50) NOT NULL,
    prioridad              varchar(20) NOT NULL DEFAULT 'Media'
                                       CONSTRAINT requerimientos_prioridad_check CHECK (prioridad IN ('Baja','Media','Alta','Urgente')),
    descripcion            text        NOT NULL,
    fecha_solicitud        timestamp   NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_atencion         timestamp,
    estado                 varchar(30) NOT NULL DEFAULT 'Pendiente'
                                       CONSTRAINT requerimientos_estado_check CHECK (estado IN ('Pendiente','En revisión','Aprobado','Atendido','Rechazado')),
    observacion_resolucion text
);

-- =====================================================================
-- DATOS ACTUALES (para entender cómo funciona el sistema)
--  Roles: Alumno, Docente, Enfermería, Psicología, Jefe de Soporte,
--  Practicante, Secretaría Académica, Administrador.
--  Los usuarios con clave NULL ingresan por Google SSO (@istpargentina.edu.pe).
-- =====================================================================

-- personas
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (1, 'DNI', '74296138', 'Juan Diego', 'Pérez');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (3, 'DNI', '44556677', 'Rosa María', 'García');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (4, 'DNI', '48123456', 'Carlos Alberto', 'Mendoza');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (6, 'DNI', '60914536', 'Paulo', 'Carreons');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (7, 'DNI', '74114320', 'Richard', 'Yancced');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (8, 'DNI', '63229403', 'Diana', 'Contrerasl');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (9, 'DNI', '72910115', 'Josias', 'Torog');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (5, 'DNI', '73928172', 'jherson', 'arias');
INSERT INTO personas (id_persona, tipo_documento, numero_documento, nombres, apellidos) OVERRIDING SYSTEM VALUE VALUES (10, 'DNI', '23456789', 'Estudiante', 'Prueba Alumno');

-- roles
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (1, 'Alumno', 'Estudiante matriculado o egresado del instituto', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (2, 'Docente', 'Personal docente con carga académica', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (3, 'Enfermería (Tópico)', 'Personal de salud a cargo de las atenciones físicas (Módulo 5)', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (4, 'Psicología', 'Personal a cargo del bienestar mental y citas psicológicas (Módulo 5)', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (5, 'Jefe de Soporte', 'Jefatura de laboratorios, inventario y practicantes', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (6, 'Practicante', 'Estudiante en horas de práctica de soporte (Módulo 6)', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (7, 'Secretaría Académica', 'Motor operativo, trámites y egresos', 'Activo', '2026-09-26 00:26:36.716481');
INSERT INTO roles (id_rol, nombre, descripcion, estado, creado_el) OVERRIDING SYSTEM VALUE VALUES (8, 'Administrador', 'Acceso total y configuración del sistema', 'Activo', '2026-09-26 00:26:36.716481');

-- permisos_modulos
INSERT INTO permisos_modulos (id_permiso, nombre_modulo, nivel_acceso, descripcion) OVERRIDING SYSTEM VALUE VALUES (1, 'portal_academico', 'Total', 'Acceso a notas, sílabos y asistencias');
INSERT INTO permisos_modulos (id_permiso, nombre_modulo, nivel_acceso, descripcion) OVERRIDING SYSTEM VALUE VALUES (2, 'topico_salud', 'Total', 'Acceso a historiales médicos de alumnos (Privado)');
INSERT INTO permisos_modulos (id_permiso, nombre_modulo, nivel_acceso, descripcion) OVERRIDING SYSTEM VALUE VALUES (3, 'bienestar_psicologico', 'Total', 'Acceso a expedientes psicológicos (Privado)');
INSERT INTO permisos_modulos (id_permiso, nombre_modulo, nivel_acceso, descripcion) OVERRIDING SYSTEM VALUE VALUES (4, 'soporte_inventario', 'Total', 'Gestión de reservas y mantenimiento de equipos');

-- roles_permisos (Enfermería->topico_salud, Psicología->bienestar_psicologico, Jefe de Soporte->soporte_inventario)
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (3, 2);
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (4, 3);
INSERT INTO roles_permisos (id_rol, id_permiso) VALUES (5, 4);

-- usuarios
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (1, 1, '74296138', 'jperez@instituto.edu.pe', '$2b$10$HashSimuladoDe123456', 'Activo', NULL, '2026-09-26 01:21:47.261302', '2026-09-26 01:21:47.261302');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (2, 3, '44556677', 'rmgarcia@instituto.edu.pe', '$2b$10$HashSimuladoDe123456', 'Activo', NULL, '2026-09-27 01:06:41.626711', '2026-09-27 01:06:41.626711');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (5, 6, '60914536', 'paulo.carreons@istpargentina.edu.pe', NULL, 'Activo', NULL, '2026-09-29 01:40:29.153296', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (6, 7, '74114320', 'richard.yancced@istpargentina.edu.pe', NULL, 'Activo', NULL, '2026-09-29 01:40:29.153296', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (7, 8, '63229403', 'diana.contrerasl@istpargentina.edu.pe', NULL, 'Activo', NULL, '2026-09-29 01:40:29.153296', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (8, 9, '72910115', 'josias.torog@istpargentina.edu.pe', NULL, 'Activo', NULL, '2026-09-29 01:40:29.153296', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (4, 5, '73928172', 'jherson.ariasv@istpargentina.edu.pe', NULL, 'Activo', NULL, '2026-09-27 02:26:20.899139', '2026-10-01 16:35:07.128052');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (9, 10, '23456789', '23456789@istpargentina.edu.pe', '123456', 'Activo', NULL, '2026-10-07 00:58:20.486315', '2026-10-07 01:05:53.172562');
INSERT INTO usuarios (id_usuario, id_persona, dni_login, correo, clave_cifrada, estado, ultimo_acceso, creado_el, actualizado_el) OVERRIDING SYSTEM VALUE VALUES (3, 4, '48123456', 'cmendoza@instituto.edu.pe', '123456', 'Activo', NULL, '2026-09-27 01:26:15.129326', '2026-10-11 16:09:23.728689');

-- usuarios_roles
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (1, 1, 'Activo', '2026-09-26 01:21:47.261302');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (2, 3, 'Activo', '2026-09-27 01:06:41.626711');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (3, 4, 'Activo', '2026-09-27 01:26:15.129326');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (4, 4, 'Activo', '2026-09-27 02:26:20.899139');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (5, 4, 'Activo', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (6, 4, 'Activo', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (7, 4, 'Activo', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (8, 4, 'Activo', '2026-09-29 01:40:29.153296');
INSERT INTO usuarios_roles (id_usuario, id_rol, estado, fecha_asignacion) VALUES (9, 1, 'Activo', '2026-10-07 00:58:20.486315');

-- atenciones_psicologia
INSERT INTO atenciones_psicologia (id_atencion_psico, persona_id, psicologo_id, fecha_hora, motivo_consulta, observacion_notas, estado_atencion) OVERRIDING SYSTEM VALUE VALUES (2, 1, 1, '2026-09-28 21:16:00', 'nos e', 'dwdsds', 'Pendiente');
