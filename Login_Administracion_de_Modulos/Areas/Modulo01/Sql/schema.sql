-- ============================================================================
-- 🗄️ ESQUEMA SOBERANO mod01: MATRÍCULA, ADMISIÓN Y CARNÉS (Equipo 01 - Ismael)
-- Portado desde db_intranet_iestp local (schema 3.2.0) a las reglas del
-- proyecto: FKs INT al core de Felipe, catálogos propios dentro de mod01,
-- DDL idempotente (IF NOT EXISTS), snake_case, TIMESTAMPTZ.
-- Historia: diseño original del equipo 01, normalizado 3FN y verificado con
-- pruebas negativas (ver repo gestion-estudiantes-intranet).
-- ============================================================================

-- 1. Catálogo: tipos de matrícula (TUPA: ordinaria, extemporánea, traslados...)
CREATE TABLE IF NOT EXISTS mod01.tipos_matricula (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(10) NOT NULL UNIQUE,
    nombre VARCHAR(100) NOT NULL,
    recargo NUMERIC(5,2) NOT NULL DEFAULT 0,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 2. Catálogo: turnos (M=Diurno, N=Nocturno) — catálogo propio del módulo
--    mientras core no lo adopte (decisión de arquitectura 2026-09-23)
CREATE TABLE IF NOT EXISTS mod01.turnos (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(5) NOT NULL UNIQUE,
    nombre VARCHAR(50) NOT NULL
);

-- 3. Catálogo: ciclos (I..VI) — catálogo propio del módulo
CREATE TABLE IF NOT EXISTS mod01.ciclos (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(5) NOT NULL UNIQUE,
    nombre VARCHAR(50) NOT NULL
);

-- 4. Vacantes ofertadas por semestre/carrera/turno (admisión)
CREATE TABLE IF NOT EXISTS mod01.vacantes (
    id SERIAL PRIMARY KEY,
    periodo_id INT NOT NULL REFERENCES core.periodos_academicos(id) ON DELETE CASCADE,
    carrera_id INT NOT NULL REFERENCES core.carreras(id) ON DELETE CASCADE,
    turno_id INT NOT NULL REFERENCES mod01.turnos(id) ON DELETE RESTRICT,
    ciclo_id INT NOT NULL REFERENCES mod01.ciclos(id) ON DELETE RESTRICT,
    vacantes INT NOT NULL DEFAULT 0 CHECK (vacantes >= 0),
    UNIQUE (periodo_id, carrera_id, turno_id, ciclo_id)
);

-- 5. Matrícula: el hecho central (la historia académica vive AQUÍ, no en
--    core.estudiantes — el ciclo cambia cada semestre, es dato de la matrícula)
CREATE TABLE IF NOT EXISTS mod01.matriculas (
    id SERIAL PRIMARY KEY,
    codigo_matricula VARCHAR(20) NOT NULL UNIQUE,
    estudiante_id INT NOT NULL REFERENCES core.estudiantes(id) ON DELETE RESTRICT,
    periodo_id INT NOT NULL REFERENCES core.periodos_academicos(id) ON DELETE RESTRICT,
    carrera_id INT NOT NULL REFERENCES core.carreras(id) ON DELETE RESTRICT,
    ciclo_id INT NOT NULL REFERENCES mod01.ciclos(id) ON DELETE RESTRICT,
    turno_id INT NOT NULL REFERENCES mod01.turnos(id) ON DELETE RESTRICT,
    tipo_matricula_id INT NOT NULL REFERENCES mod01.tipos_matricula(id) ON DELETE RESTRICT,
    -- dominios reales del diseño original del equipo 01 (verificados con
    -- pruebas negativas en el repo gestion-estudiantes-intranet)
    condicion VARCHAR(30) NOT NULL DEFAULT 'Ingresante' CHECK (condicion IN
        ('Ingresante','Promovido','Promovido con curso a cargo','Repitente',
         'Reingresante','Traslado')),
    estado VARCHAR(20) NOT NULL DEFAULT 'En trámite' CHECK (estado IN
        ('En trámite','Matriculado','Reservada','Anulada')),
    etapa VARCHAR(20) NOT NULL DEFAULT 'Validación' CHECK (etapa IN
        ('Validación','Conformidad','Cerrada')),
    voucher_ok BOOLEAN NOT NULL DEFAULT FALSE,
    foto_ok BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_conformidad DATE NULL,
    conforme_por INT NULL REFERENCES core.usuarios(id) ON DELETE SET NULL,
    fecha_matricula DATE NULL,
    observaciones_cursos TEXT NULL,
    turno_origen_id INT NULL REFERENCES mod01.turnos(id),
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    actualizado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
-- regla de negocio del diseño original: UNA matrícula vigente por
-- estudiante y periodo (las anuladas liberan la vacante)
CREATE UNIQUE INDEX IF NOT EXISTS uq_matricula_vigente
  ON mod01.matriculas (estudiante_id, periodo_id)
  WHERE estado <> 'Anulada';
CREATE INDEX IF NOT EXISTS idx_mat_estudiante ON mod01.matriculas (estudiante_id);
CREATE INDEX IF NOT EXISTS idx_mat_periodo ON mod01.matriculas (periodo_id);
CREATE INDEX IF NOT EXISTS idx_mat_carrera ON mod01.matriculas (carrera_id);

-- 6. Detalle: cursos (unidades didácticas) inscritos en la matrícula
CREATE TABLE IF NOT EXISTS mod01.detalles_matricula (
    id SERIAL PRIMARY KEY,
    matricula_id INT NOT NULL REFERENCES mod01.matriculas(id) ON DELETE CASCADE,
    unidad_didactica_id INT NOT NULL REFERENCES core.unidades_didacticas(id) ON DELETE RESTRICT,
    estado VARCHAR(20) NOT NULL DEFAULT 'Inscrito' CHECK (estado IN ('Inscrito','Retirado','Convalidado'))
);
CREATE INDEX IF NOT EXISTS idx_detmat_matricula ON mod01.detalles_matricula (matricula_id);

-- 7. Carné universitario: el trámite de carnetización (cruce con mod09:
--    se genera cuando el trámite TUPA de carné es aprobado)
CREATE TABLE IF NOT EXISTS mod01.carnes (
    id SERIAL PRIMARY KEY,
    estudiante_id INT NOT NULL REFERENCES core.estudiantes(id) ON DELETE RESTRICT,
    periodo_id INT NOT NULL REFERENCES core.periodos_academicos(id) ON DELETE RESTRICT,
    -- tramite_tupa: referencia débil al código de trámite de mod09 (sin FK
    -- cruzada inter-módulo: soberanía. El evento/código mantiene el enlace)
    tramite_tupa VARCHAR(20) NULL,
    estado_foto VARCHAR(20) NOT NULL DEFAULT 'Pendiente' CHECK (estado_foto IN ('Pendiente','Aprobada','Rechazada')),
    entregado BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_entrega DATE NULL,
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_carnes_estudiante ON mod01.carnes (estudiante_id);

-- ============================================================================
-- SEMILLA (catálogos del TUPA real del instituto)
-- ============================================================================
INSERT INTO mod01.tipos_matricula (codigo, nombre, recargo) VALUES
  ('TM1','Ordinaria',0), ('TM2','Extemporánea',0.20), ('TM3','Extraordinaria',0.50),
  ('TM4','Traslado Interno',0), ('TM5','Traslado Externo',0), ('TM6','Reserva',0)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO mod01.turnos (codigo, nombre) VALUES ('M','Diurno'),('N','Nocturno')
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO mod01.ciclos (codigo, nombre) VALUES
  ('I','Ciclo I'),('II','Ciclo II'),('III','Ciclo III'),
  ('IV','Ciclo IV'),('V','Ciclo V'),('VI','Ciclo VI')
ON CONFLICT (codigo) DO NOTHING;

-- vacantes por carril (carrera × turno × ciclo) del periodo activo 2026-I (id 2).
-- Cupos oficiales del IESTP Argentina; se consumen al registrar la conformidad.
INSERT INTO mod01.vacantes (periodo_id, carrera_id, turno_id, ciclo_id, vacantes)
SELECT 2, c.id, t.id, ci.id,
       CASE WHEN c.codigo = 'DSI' THEN 24 WHEN c.codigo = 'CONT' THEN 30 ELSE 28 END
FROM core.carreras c
CROSS JOIN mod01.turnos t
CROSS JOIN mod01.ciclos ci
WHERE t.codigo IN ('M','N') AND ci.codigo IN ('I','II','III')
ON CONFLICT (periodo_id, carrera_id, turno_id, ciclo_id) DO NOTHING;


-- ============================================================================
-- FLUJO DE MATRICULATURA DE SECRETARÍA (Reserva de Matrícula TM05 / CT13)
-- Cierre: expediente por DNI → UDs del nuevo ciclo → ficha PDF al correo.
-- En producción "matriculas" es legacy (owner postgres); la tabla viva del
-- equipo es matriculas (consolidada). El service usa el nombre canónico.
-- estructura completa por si el schema se aplica en un entorno limpio.
-- ============================================================================

-- 1) Historial académico del estudiante (aprobados/desaprobados por UD)
CREATE TABLE IF NOT EXISTS mod01.historial_academico (
    id           SERIAL PRIMARY KEY,
    estudiante_id INT NOT NULL REFERENCES core.estudiantes(id) ON DELETE CASCADE,
    periodo_id   INT NOT NULL REFERENCES core.periodos_academicos(id),
    unidad_didactica_id INT NOT NULL REFERENCES core.unidades_didacticas(id),
    nota         NUMERIC(4,1) NOT NULL CHECK (nota BETWEEN 0 AND 20),
    estado       VARCHAR(12) NOT NULL CHECK (estado IN ('Aprobado','Desaprobado','Retirado')),
    creado_en    TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (estudiante_id, periodo_id, unidad_didactica_id)
);

-- Semilla demo: historial de Felipe/Ana con las notas exactas de la malla SI
-- (períodos: 3=2025-I, 4=2025-II, 1=2026-I; EFSRT SI-209 sin nota aún).
DELETE FROM mod01.historial_academico WHERE estudiante_id IN (1, 2);

INSERT INTO mod01.historial_academico (estudiante_id, periodo_id, unidad_didactica_id, nota, estado) VALUES
-- Felipe Ostos — Ciclo I · 2025-I
(1, 3, 1,   16, 'Aprobado'), (1, 3, 2,   17, 'Aprobado'),
(1, 3, 18,  15, 'Aprobado'), (1, 3, 19,  16, 'Aprobado'),
(1, 3, 20,  17, 'Aprobado'), (1, 3, 100, 13, 'Aprobado'),
(1, 3, 101, 17, 'Aprobado'), (1, 3, 102, 16, 'Aprobado'),
-- Felipe — Ciclo II · 2025-II
(1, 4, 3,   16, 'Aprobado'), (1, 4, 21,  18, 'Aprobado'),
(1, 4, 22,  17, 'Aprobado'), (1, 4, 4,   17, 'Aprobado'),
(1, 4, 23,  15, 'Aprobado'), (1, 4, 103, 14, 'Aprobado'),
(1, 4, 104, 16, 'Aprobado'), (1, 4, 105, 18, 'Aprobado'),
-- Felipe — Ciclo III · 2026-I
(1, 1, 107, 17, 'Aprobado'), (1, 1, 6,   18, 'Aprobado'),
(1, 1, 5,   15, 'Aprobado'), (1, 1, 24,  17, 'Aprobado'),
(1, 1, 25,  15, 'Aprobado'), (1, 1, 26,  15, 'Aprobado'),
(1, 1, 108, 16, 'Aprobado'), (1, 1, 109, 15, 'Aprobado'),
-- Ana García — Ciclo I · 2025-I
(2, 3, 1,   15, 'Aprobado'), (2, 3, 2,   16, 'Aprobado'),
(2, 3, 18,  14, 'Aprobado'), (2, 3, 19,  15, 'Aprobado'),
(2, 3, 20,  16, 'Aprobado'), (2, 3, 100, 12, 'Aprobado'),
(2, 3, 101, 16, 'Aprobado'), (2, 3, 102, 15, 'Aprobado'),
-- Ana — Ciclo II · 2025-II
(2, 4, 3,   15, 'Aprobado'), (2, 4, 21,  17, 'Aprobado'),
(2, 4, 22,  16, 'Aprobado'), (2, 4, 4,   16, 'Aprobado'),
(2, 4, 23,  14, 'Aprobado'), (2, 4, 103, 13, 'Aprobado'),
(2, 4, 104, 15, 'Aprobado'), (2, 4, 105, 17, 'Aprobado'),
-- Ana — Ciclo III · 2026-I (SI-305 desaprobado → conserva el caso "curso a cargo" en el demo)
(2, 1, 107, 16, 'Aprobado'), (2, 1, 6,   17, 'Aprobado'),
(2, 1, 5,   14, 'Aprobado'), (2, 1, 24,  16, 'Aprobado'),
(2, 1, 25,   8, 'Desaprobado'), (2, 1, 26, 14, 'Aprobado'),
(2, 1, 108, 15, 'Aprobado'), (2, 1, 109, 14, 'Aprobado');

-- 2) Oferta del próximo ciclo (espejo del plan cuando core aún no lo tiene)
CREATE TABLE IF NOT EXISTS mod01.oferta_ciclo (
    id           SERIAL PRIMARY KEY,
    carrera_id   INT NOT NULL REFERENCES core.carreras(id),
    ciclo        VARCHAR(5) NOT NULL CHECK (ciclo IN ('I','II','III','IV','V','VI')),
    unidad_didactica_id INT REFERENCES core.unidades_didacticas(id),  -- NULL si core no la tiene
    codigo       VARCHAR(20) NOT NULL,
    nombre       VARCHAR(150) NOT NULL,
    creditos     INT NOT NULL DEFAULT 3,
    tipo         VARCHAR(30) NOT NULL DEFAULT 'Formativa',
    obligatoria  BOOLEAN NOT NULL DEFAULT TRUE,
    UNIQUE (carrera_id, ciclo, codigo)
);

-- 3) La tabla de matrículas es ÚNICA: mod01.matriculas (consolidada 2026-09-29,
--    la era matriculas/matriculas_v2 terminó — la v2 fue renombrada al nombre
--    canónico en producción y espejos). Solo asegurar la columna del trámite.
ALTER TABLE mod01.matriculas ADD COLUMN IF NOT EXISTS tramite_reserva_id INT;

-- 4) El detalle admite UDs del espejo oferta_ciclo mientras core.unidades_didacticas
--    no tenga el ciclo (seed de ciclos IV–VI pendiente de merge PR #34)
ALTER TABLE mod01.detalles_matricula ADD COLUMN IF NOT EXISTS oferta_ciclo_id INT REFERENCES mod01.oferta_ciclo(id);
ALTER TABLE mod01.detalles_matricula ALTER COLUMN unidad_didactica_id DROP NOT NULL;

CREATE TABLE IF NOT EXISTS mod01.fichas_enviadas (
    id           SERIAL PRIMARY KEY,
    matricula_id INT NOT NULL REFERENCES mod01.matriculas(id) ON DELETE CASCADE,
    enviado_a    VARCHAR(150) NOT NULL,
    enviado_por  INT REFERENCES core.usuarios(id),
    enviado_en   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
