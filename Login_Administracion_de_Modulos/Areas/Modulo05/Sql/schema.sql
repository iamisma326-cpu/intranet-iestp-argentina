-- ============================================================================
-- MÓDULO 05 — Salud, Tópico y Psicología (esquema `mod05`)
-- ============================================================================
-- Tablas de negocio del equipo 05. IDEMPOTENTE.
-- Las columnas calzan EXACTAMENTE con los queries de Areas/Modulo05/Models/.
-- El padrón de personas/usuarios está en core.* (solo lectura para el módulo).
-- Ejecutar como user_equipo05 (ver Sql/roles_equipos.sql).
-- ============================================================================

-- Botica del tópico
CREATE TABLE IF NOT EXISTS mod05.botica_medicamentos (
    id_medicamento     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre             varchar(120) NOT NULL,
    presentacion      varchar(80),
    stock_actual       integer NOT NULL DEFAULT 0 CHECK (stock_actual >= 0),
    fecha_vencimiento  date,
    estado             varchar(20) NOT NULL DEFAULT 'Activo',
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Atenciones médicas del tópico
CREATE TABLE IF NOT EXISTS mod05.atenciones_topico (
    id_atencion_top    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    persona_id         integer NOT NULL REFERENCES core.personas(id_persona) ON DELETE RESTRICT,
    atendido_por_id    integer REFERENCES core.usuarios(id_usuario) ON DELETE SET NULL,
    fecha_hora         timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    motivo             varchar(300) NOT NULL,
    medicamento_id     integer REFERENCES mod05.botica_medicamentos(id_medicamento) ON DELETE SET NULL,
    es_alergico        boolean NOT NULL DEFAULT false,
    detalle_alergia    varchar(250),
    seguro_salud       varchar(60) NOT NULL DEFAULT 'NINGUNO',
    observacion        text,
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Atenciones psicológicas (sesiones)
CREATE TABLE IF NOT EXISTS mod05.atenciones_psicologia (
    id_atencion_psico  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    persona_id          integer NOT NULL REFERENCES core.personas(id_persona) ON DELETE RESTRICT,
    psicologo_id       integer REFERENCES core.usuarios(id_usuario) ON DELETE SET NULL,
    fecha_hora         timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    motivo_consulta    varchar(300),
    observacion_notas  text,
    estado_atencion    varchar(20) NOT NULL DEFAULT 'Registrada',
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Bienes de TI (para reportes de avería y soporte)
CREATE TABLE IF NOT EXISTS mod05.bien (
    id_bien            integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_almacen         integer,
    cbi                varchar(40) UNIQUE,           -- código bien institucional
    descripcion        varchar(200) NOT NULL,
    tipo_bien          varchar(60),
    marca              varchar(60),
    modelo             varchar(60),
    serie              varchar(60),
    procedencia        varchar(80),
    fecha_ingreso      date,
    estado             varchar(30) NOT NULL DEFAULT 'Bueno'
                       CHECK (estado IN ('Bueno','Regular','Malo','En reparación','De baja')),
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Ambientes (aulas, talleres, laboratorios)
CREATE TABLE IF NOT EXISTS mod05.ambiente (
    id_ambiente        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nombre             varchar(100) NOT NULL,
    tipo               varchar(40) DEFAULT 'Aula',
    estado             varchar(20) NOT NULL DEFAULT 'Activo',
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Asignación de un bien a un ambiente
CREATE TABLE IF NOT EXISTS mod05.asignacion (
    id_asignacion      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien            integer NOT NULL REFERENCES mod05.bien(id_bien) ON DELETE RESTRICT,
    id_ambiente        integer NOT NULL REFERENCES mod05.ambiente(id_ambiente) ON DELETE RESTRICT,
    estado             varchar(15) NOT NULL DEFAULT 'Activo' CHECK (estado IN ('Activo','Vigente','Finalizada','Anulada')),
    fecha_inicio       timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_fin          timestamptz
);

-- Tickets de avería (docentes → soporte TI)
CREATE TABLE IF NOT EXISTS mod05.incidencias (
    id_incidencia      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id_bien            integer REFERENCES mod05.bien(id_bien) ON DELETE SET NULL,
    reportador_id      integer NOT NULL REFERENCES core.personas(id_persona) ON DELETE RESTRICT,
    tecnico_id         integer REFERENCES core.usuarios(id_usuario) ON DELETE SET NULL,
    fecha_reporte      timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_cierre       timestamptz,
    tipo_falla         varchar(60),
    gravedad           varchar(15) NOT NULL DEFAULT 'Media' CHECK (gravedad IN ('Baja','Media','Alta')),
    descripcion        text NOT NULL,
    estado             varchar(20) NOT NULL DEFAULT 'Abierto'
                       CHECK (estado IN ('Abierto','En proceso','Reparado','Resuelto','Cerrado')),
    creado_el          timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ============================================================================
-- DATOS DEMO MÍNIMOS (idempotentes)
-- ============================================================================
INSERT INTO mod05.ambiente (nombre, tipo) VALUES
    ('Aula 101', 'Aula'),
    ('Taller de Computación 1', 'Taller'),
    ('Laboratorio de Redes', 'Laboratorio')
ON CONFLICT DO NOTHING;

INSERT INTO mod05.bien (id_almacen, cbi, descripcion, tipo_bien, marca, modelo, serie, procedencia, fecha_ingreso, estado) VALUES
    (1, 'CBI-0001', 'Proyector Epson EB-X51', 'Proyector', 'Epson', 'EB-X51', 'SN-A1B2C3', 'Compra directa', CURRENT_DATE - 400, 'Bueno'),
    (1, 'CBI-0002', 'PC Dell OptiPlex 3090', 'Computadora', 'Dell', 'OptiPlex 3090', 'SN-998877', 'Donación', CURRENT_DATE - 200, 'Regular'),
    (1, 'CBI-0003', 'Switch TP-Link TL-SG1024', 'Red', 'TP-Link', 'TL-SG1024', 'SN-554433', 'Compra directa', CURRENT_DATE - 90, 'Bueno')
ON CONFLICT (cbi) DO NOTHING;

INSERT INTO mod05.botica_medicamentos (nombre, presentacion, stock_actual, fecha_vencimiento, estado) VALUES
    ('Paracetamol 500mg', 'Caja x 10 tabletas', 40, CURRENT_DATE + 365, 'Activo'),
    ('Alcohol en gel 70%', 'Frasco 500ml', 15, CURRENT_DATE + 180, 'Activo'),
    ('Suero fisiológico', 'Frasco 250ml', 8, CURRENT_DATE + 365, 'Activo'),
    ('Gasas estériles', 'Paquete x 10', 25, NULL, 'Activo')
ON CONFLICT DO NOTHING;

-- ============================================================================
-- VERIFICACIÓN:
--   SELECT COUNT(*) FROM mod05.bien;                -- 3
--   SELECT COUNT(*) FROM mod05.botica_medicamentos; -- 4
-- ============================================================================
