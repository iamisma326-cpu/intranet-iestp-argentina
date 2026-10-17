-- ============================================================================
-- 🗄️ ESQUEMA SOBERANO mod09: TESORERÍA, TRÁMITES TUPA Y PAGOS (Equipo 09)
-- Portado desde db_intranet_iestp local (schema 3.2.0) a las reglas del
-- proyecto: FKs INT al core de Felipe, DDL idempotente, snake_case.
-- Historia: diseño del equipo 09 (54 conceptos TUPA reales del IESTP),
-- normalizado 3NF, verificado con pruebas negativas en el repo
-- gestion-estudiantes-intranet.
-- ============================================================================

-- 1. Catálogo: tipos de pago (Efectivo, Transferencia...)
CREATE TABLE IF NOT EXISTS mod09.tipos_pago (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(10) NOT NULL UNIQUE,
    nombre VARCHAR(50) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- 2. Catálogo: conceptos de pago TUPA (los 53 conceptos oficiales)
CREATE TABLE IF NOT EXISTS mod09.conceptos_pago (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(10) NOT NULL UNIQUE,
    nombre VARCHAR(200) NOT NULL,
    monto NUMERIC(10,2) NOT NULL DEFAULT 0 CHECK (monto >= 0),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- 3. Catálogo: tipos de trámite TUPA (admisión, certificados, titulación...)
CREATE TABLE IF NOT EXISTS mod09.tipos_tramite (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(10) NOT NULL UNIQUE,
    nombre VARCHAR(200) NOT NULL,
    -- el trámite se paga con un concepto TUPA (FK interna del módulo)
    concepto_pago_id INT NULL REFERENCES mod09.conceptos_pago(id) ON DELETE SET NULL,
    dias_habiles INT NOT NULL DEFAULT 3 CHECK (dias_habiles > 0),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

-- 4. Catálogo: requisitos por tipo de trámite (checklist TUPA)
CREATE TABLE IF NOT EXISTS mod09.requisitos_tipos_tramite (
    id SERIAL PRIMARY KEY,
    tipo_tramite_id INT NOT NULL REFERENCES mod09.tipos_tramite(id) ON DELETE CASCADE,
    orden INT NOT NULL DEFAULT 1,
    requisito VARCHAR(300) NOT NULL,
    UNIQUE (tipo_tramite_id, orden, requisito)
);

-- 5. Feriados (para cálculo de plazos en días hábiles)
CREATE TABLE IF NOT EXISTS mod09.feriados (
    id SERIAL PRIMARY KEY,
    fecha DATE NOT NULL UNIQUE,
    descripcion VARCHAR(150) NOT NULL
);

-- 6. Trámites TUPA: el expediente del ciudadano
CREATE TABLE IF NOT EXISTS mod09.tramites (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(20) NOT NULL UNIQUE,
    tipo_tramite_id INT NOT NULL REFERENCES mod09.tipos_tramite(id) ON DELETE RESTRICT,
    estudiante_id INT NULL REFERENCES core.estudiantes(id) ON DELETE SET NULL,
    periodo_id INT NOT NULL REFERENCES core.periodos_academicos(id) ON DELETE RESTRICT,
    fecha_solicitud DATE NOT NULL DEFAULT CURRENT_DATE,
    fecha_limite DATE NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'Recibido' CHECK (estado IN ('Recibido','En evaluación','Aprobado','Observado','Rechazado','Entregado')),
    datos JSONB NULL,
    resolucion VARCHAR(300) NULL,
    fecha_resolucion DATE NULL,
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    actualizado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_tramites_estudiante ON mod09.tramites (estudiante_id);
CREATE INDEX IF NOT EXISTS idx_tramites_estado ON mod09.tramites (estado);

-- 6b. Plazo del ALUMNO para corregir un trámite Observado: 15 días hábiles
--     (L-V, excluyendo mod09.feriados). Distinto de fecha_limite, que es el
--     plazo TUPA de la mesa. Se fija al observar el trámite (mesa o rechazo
--     de voucher de Tesorería); la corrección a tiempo lo limpia.
ALTER TABLE mod09.tramites
    ADD COLUMN IF NOT EXISTS fecha_limite_correccion DATE NULL;

-- Estado terminal 'Vencido': el alumno no corrigió dentro del plazo.
-- Idempotente para el auto-runner (DROP IF EXISTS + ADD en cada arranque).
ALTER TABLE mod09.tramites DROP CONSTRAINT IF EXISTS tramites_estado_check;
ALTER TABLE mod09.tramites ADD CONSTRAINT tramites_estado_check
    CHECK (estado IN ('Recibido','En evaluación','Aprobado','Observado',
                      'Rechazado','Entregado','Vencido'));

-- Función de días hábiles: N días L-V después de `desde`, excluyendo
-- feriados del calendario TUPA. Única fuente de verdad del criterio.
CREATE OR REPLACE FUNCTION mod09.fecha_limite_correccion(
    desde DATE DEFAULT CURRENT_DATE, dias INT DEFAULT 15)
RETURNS DATE
LANGUAGE sql STABLE AS $$
    SELECT MAX(d::date)
    FROM (
        SELECT d
        FROM generate_series(desde + 1, desde + 60, interval '1 day') AS g(d)
        WHERE EXTRACT(ISODOW FROM g.d) < 6
          AND g.d::date NOT IN (SELECT fecha FROM mod09.feriados)
        LIMIT dias
    ) s;
$$;

-- Backfill idempotente: observados previos a esta versión reciben plazo
-- desde su fecha de observación (solo filas sin plazo; luego el código lo fija).
UPDATE mod09.tramites
   SET fecha_limite_correccion =
       mod09.fecha_limite_correccion(COALESCE(fecha_resolucion, actualizado_en::date))
 WHERE estado = 'Observado' AND fecha_limite_correccion IS NULL;

-- 7. Requisitos presentados por trámite (instancia del checklist)
CREATE TABLE IF NOT EXISTS mod09.tramite_requisitos (
    id SERIAL PRIMARY KEY,
    tramite_id INT NOT NULL REFERENCES mod09.tramites(id) ON DELETE CASCADE,
    requisito_catalogo_id INT NOT NULL REFERENCES mod09.requisitos_tipos_tramite(id) ON DELETE RESTRICT,
    presentado BOOLEAN NOT NULL DEFAULT FALSE,
    observacion VARCHAR(300) NULL,
    archivo_nombre VARCHAR(260) NULL,
    archivo_tipo VARCHAR(100) NULL,
    archivo_contenido BYTEA NULL,
    UNIQUE (tramite_id, requisito_catalogo_id)
);

-- 8. Pagos TUPA
CREATE TABLE IF NOT EXISTS mod09.pagos (
    id SERIAL PRIMARY KEY,
    codigo VARCHAR(20) NOT NULL UNIQUE,
    estudiante_id INT NOT NULL REFERENCES core.estudiantes(id) ON DELETE RESTRICT,
    concepto_pago_id INT NOT NULL REFERENCES mod09.conceptos_pago(id) ON DELETE RESTRICT,
    periodo_id INT NOT NULL REFERENCES core.periodos_academicos(id) ON DELETE RESTRICT,
    tipo_pago_id INT NOT NULL REFERENCES mod09.tipos_pago(id) ON DELETE RESTRICT,
    tramite_id INT NULL REFERENCES mod09.tramites(id) ON DELETE SET NULL,
    monto NUMERIC(10,2) NOT NULL CHECK (monto > 0),
    fecha_pago DATE NOT NULL DEFAULT CURRENT_DATE,
    voucher_estado VARCHAR(20) NOT NULL DEFAULT 'Pendiente' CHECK (voucher_estado IN ('Pendiente','Validado','Rechazado')),
    motivo_rechazo VARCHAR(300) NULL,
    fecha_validacion DATE NULL,
    validado_por INT NULL REFERENCES core.usuarios(id) ON DELETE SET NULL,
    intentos INT NOT NULL DEFAULT 0,
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    actualizado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_pagos_estudiante ON mod09.pagos (estudiante_id);
CREATE INDEX IF NOT EXISTS idx_pagos_periodo ON mod09.pagos (periodo_id);

-- 9. Avisos internos (buzón de confirmaciones para el estudiante)
CREATE TABLE IF NOT EXISTS mod09.avisos (
    id SERIAL PRIMARY KEY,
    remitente_id INT NULL REFERENCES core.usuarios(id) ON DELETE SET NULL,
    tipo VARCHAR(30) NOT NULL DEFAULT 'Información',
    titulo VARCHAR(150) NOT NULL,
    mensaje TEXT NOT NULL,
    creado_en TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- 10. Destinatarios de avisos (muchos por aviso)
CREATE TABLE IF NOT EXISTS mod09.avisos_destinatarios (
    id SERIAL PRIMARY KEY,
    aviso_id INT NOT NULL REFERENCES mod09.avisos(id) ON DELETE CASCADE,
    destinatario_id INT NOT NULL REFERENCES core.usuarios(id) ON DELETE CASCADE,
    leido BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_lectura TIMESTAMP WITH TIME ZONE NULL,
    UNIQUE (aviso_id, destinatario_id)
);

-- ============================================================================
-- SEMILLA (TUPA real del instituto: tipos de pago, feriados, plantillas)
-- ============================================================================
INSERT INTO mod09.tipos_pago (codigo, nombre) VALUES
  ('TPM','Matrícula'),('MEN','Mensual'),('TRA','Trámite'),('OTR','Otro')
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO mod09.feriados (fecha, descripcion) VALUES
  ('2026-01-01','Año Nuevo'),('2026-05-01','Día del Trabajo'),
  ('2026-06-24','Día del Campesino'),('2026-07-28','Fiestas Patrias'),
  ('2026-07-29','Fiestas Patrias'),('2026-08-30','Santa Rosa'),
  ('2026-10-08','Combate de Angamos'),('2026-11-01','Todos los Santos'),
  ('2026-12-08','Inmaculada Concepción'),('2026-12-25','Navidad')
ON CONFLICT (fecha) DO NOTHING;

-- ---------------------------------------------------------------------------
-- SEMILLAS TUPA: tipos de trámite (con su concepto de pago y plazo en días
-- hábiles) y los requisitos por tipo. Datos del TUPA 2026 del IESTP Argentina.
-- ---------------------------------------------------------------------------
-- concepto_pago_id se resuelve por CÓDIGO (ids no secuenciales entre entornos)
INSERT INTO mod09.tipos_tramite (codigo, nombre, concepto_pago_id, dias_habiles)
SELECT v.codigo, v.nombre, cp.id, v.dias
FROM (VALUES
  ('TT01','Carné de Medio Pasaje',   'CP03', 3),
  ('TT02','Cambio de Turno',        'CT05', 5),
  ('TT03','Constancia de Matrícula', 'CT24', 3),
  ('TT04','Duplicado de Carné',      'CP04', 5),
  ('TA05','Traslado interno (cambio de turno)', 'CT05', 5),
  -- NOTA: TA14 'Reserva de matrícula' fue RETIRADO del seed: duplicaba a TM05
  -- ('Reserva de Matrícula' en el catálogo oficial del TUPA-2026). El trámite
  -- histórico T0003 que nació como TA14 fue re-etiquetado a TM05 en producción
  -- (2026-10-01) y TA14 quedó activo=false. No re-insertar aquí.
  ('TA14','[RETIRADO] Reserva de matrícula (usar TM05)', 'CT13', 5)
) AS v(codigo, nombre, concepto, dias)
JOIN mod09.conceptos_pago cp ON cp.codigo = v.concepto
ON CONFLICT (codigo) DO NOTHING;

-- Alineación idempotente: TA14 siempre inactivo (la Reserva de Matrícula es
-- exclusiva de TM05 en el TUPA-2026) y TM05 siempre activo.
UPDATE mod09.tipos_tramite SET activo = FALSE WHERE codigo = 'TA14';
UPDATE mod09.tipos_tramite SET activo = TRUE  WHERE codigo = 'TM05';

-- ---------------------------------------------------------------------------
-- MONTOS DEL TUPA-2026 —pineados por código
--
-- El catálogo de conceptos_pago lo carga el dueño a mano, así que los importes
-- se derivaban del prototipo y ya no coincidían con la columna "IMPORTE A PAGAR
-- S/" del TUPA-2026.pdf (50 de 55 discrepantes; el caso visible era
-- "Fraccionamiento de matrícula" a S/ 10.00 en vez de S/ 100.00). Este bloque
-- los fija al TUPA para que no vuelvan a derivar.
--
-- Se actualiza SOLO el catálogo: mod09.pagos.monto es una copia del importe al
-- momento del cobro, así que los recibos ya emitidos conservan su monto.
-- Los conceptos marcados 0.00 son "GRATUITO" en el TUPA.
-- ---------------------------------------------------------------------------
UPDATE mod09.conceptos_pago c SET monto = v.monto
FROM (VALUES
  ('CT01',  20.00),  -- 1.1  Carpeta de postulante (prospecto)
  ('CT02', 130.00),  -- 2.1  Examen de admisión ordinario
  ('CT03', 130.00),  -- 2.2  Examen de admisión exonerados/traslado
  ('CT04', 200.00),  -- 3.1  Traslado interno (de carrera a carrera)
  ('CT05', 120.00),  -- 3.2  Traslado interno (cambio de turno)
  ('CT06', 250.00),  -- 3.3  Traslado externo
  ('CT07', 200.00),  -- 4.1  Matrícula ingresante
  ('CT08', 100.00),  -- 4.2  Matrícula traslado externo e interno
  ('CT09', 200.00),  -- 4.3  Ratificación de matrícula
  ('CT10', 100.00),  -- 4.4/4.5 Fraccionamiento de matrícula (ambas variantes)
  ('CT11', 100.00),  -- 4.6  Ratificación media beca 50% (rendimiento)
  ('CT12', 100.00),  -- 4.7  Ratificación media beca 50% (trabajador/familiar)
  ('CT13',  30.00),  -- 4.8  Reserva de matrícula
  ('CT14', 100.00),  -- 7.1  Derecho de convalidación de estudios
  ('CT15',  50.00),  -- 8.1  Repitencia de unidad didáctica (estudiante)
  ('CT16',  70.00),  -- 8.2  Repitencia de unidad didáctica (egresado)
  ('CT17', 200.00),  -- 8.3  Repitencia de módulo
  ('CT18',  50.00),  -- 8.4  Reingreso
  ('CT19',  25.00),  -- 9.1  Evaluación extraordinaria
  ('CT20',  60.00),  -- 10.1 Reporte record de notas (hasta 1998)
  ('CT21',  60.00),  -- 10.2 Reporte record de notas (desde 1999)
  ('CT22',  20.00),  -- 11.1 Constancia de ingreso
  ('CT23',  60.00),  -- 11.2 Constancia de estudios (hasta 1998)
  ('CT24',  60.00),  -- 11.3 Constancia de estudios (a partir de 1999)
  ('CT25',  30.00),  -- 11.4 Constancia de primera matrícula
  ('CT26',  60.00),  -- 11.5/11.6 Constancia de egresado
  ('CT27',  60.00),  -- 11.7 Constancia de título en trámite
  ('CT28',  30.00),  -- 11.8 Constancia de tercio superior o conducta
  ('CT29',  15.00),  -- 11.9 Carta de presentación
  ('CT30', 180.00),  -- 12.3 Expedición de certificado + formato (6 sem), 2ª vez o más
  ('CT31',  15.00),  -- 12.2 Formato de certificado de estudios
  ('CT32',  30.00),  -- 12.4 Expedición de certificado (1 semestre)
  ('CT33',  30.00),  -- 13.1 Carpeta de prácticas preprofesionales
  ('CT34',  30.00),  -- 13.2 Constancia de prácticas por todos los módulos
  ('CT35',  50.00),  -- 13.3 Certificado por módulo
  ('CT36', 120.00),  -- 13.4 Certificado por 03 módulos
  ('CT37',  50.00),  -- 14.1 Carpeta de titulación (egresados del instituto)
  ('CT38',  70.00),  -- 14.2 Carpeta de titulación (egresados de otros institutos)
  ('CT39', 100.00),  -- 15.1 Examen de suficiencia de inglés
  ('CT40', 150.00),  -- 16.1 Titulación egresados de nuestro instituto
  ('CT41', 250.00),  -- 16.2 Titulación egresados de otros institutos
  ('CT42', 250.00),  -- 17.1 Expedición de título (egresados del instituto)
  ('CT43', 300.00),  -- 17.2 Expedición de título (egresados de otros institutos)
  ('CT44', 250.00),  -- 18.1 Duplicado de título técnico profesional
  ('CT45',  50.00),  -- 19.1 Diploma de egresado
  ('CT46',  50.00),  -- 19.2 Sílabus (egresado de nuestro instituto)
  ('CT47',  60.00),  -- 19.3 Sílabus (egresado de otro instituto)
  ('CT48',   0.00),  -- 20.1 Duplicado de recibo de caja — GRATUITO
  ('CT49',  15.00),  -- 20.2 Duplicado de boleta de notas
  ('CT50',  30.00),  -- 21.2 Rectificación de nombre y apellido
  ('CT51',   0.00),  -- 21.1 Fedateo de documentos — GRATUITO
  ('CT52',   0.00),  -- 22.1 Copia simple de documento por hoja — GRATUITO
  ('CT53',  60.00)   -- 23.1 Venta de bases para licitación
) AS v(codigo, monto)
WHERE c.codigo = v.codigo
  AND c.monto IS DISTINCT FROM v.monto;

-- El carné no es un procedimiento del TUPA-2026 (ver nota del DELETE más abajo),
-- así que el tipo de trámite queda sin concepto de pago: CrearTramiteAsync ya
-- sólo genera recibo cuando ConceptoPagoId es un entero.
UPDATE mod09.tipos_tramite SET concepto_pago_id = NULL WHERE codigo IN ('TT01','TM19');

INSERT INTO mod09.requisitos_tipos_tramite (tipo_tramite_id, orden, requisito)
SELECT tt.id, v.orden, v.requisito
FROM (VALUES
  ('TT01', 1, 'Solicitud dirigida al Director'),
  -- El carné no exige recibo: no figura en el TUPA-2026 (ver nota más abajo).
  ('TT01', 2, 'Foto carné fondo blanco'),
  ('TT02', 1, 'Solicitud dirigida al Director'),
  ('TT02', 2, 'Recibo de pago (CT05)'),
  ('TT02', 3, 'Record de notas'),
  ('TT03', 1, 'Solicitud dirigida al Director'),
  ('TT03', 2, 'Recibo de pago (CT24)'),
  ('TT04', 1, 'Solicitud dirigida al Director'),
  ('TT04', 2, 'Recibo de pago (CP04)'),
  ('TT04', 3, 'Foto carné fondo blanco'),
  ('TA05', 1, 'Solicitud dirigida al Director'),
  ('TA05', 2, 'Recibo de pago (CT05)'),
  ('TA05', 3, 'Carta de no adeudo de biblioteca'),
  ('TA14', 1, 'Solicitud dirigida al Director'),
  ('TA14', 2, 'Recibo de pago (CT13)'),
  ('TA14', 3, 'Carta de no adeudo de biblioteca')
) AS v(codigo, orden, requisito)
JOIN mod09.tipos_tramite tt ON tt.codigo = v.codigo
ON CONFLICT DO NOTHING;

-- ============================================================
-- TUPA 2026 ampliado (TM01..TM20) — catálogo oficial del TUPA-2026.pdf
-- con requisitos por tipo. Idempotente.
-- ============================================================
INSERT INTO mod09.tipos_tramite (codigo, nombre, concepto_pago_id, dias_habiles, activo) VALUES
 ('TM01','Matrícula de Ingresante',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT07'),1,true),
 ('TM02','Matrícula por Traslado Externo/Interno',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT08'),1,true),
 ('TM03','Ratificación de Matrícula',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT09'),1,true),
 ('TM04','Fraccionamiento de Matrícula',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT10'),1,true),
 ('TM05','Reserva de Matrícula',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT13'),5,true),
 ('TM06','Convalidación de Estudios',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT14'),5,true),
 ('TM07','Repitencia de Unidad Didáctica',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT15'),1,true),
 ('TM08','Repitencia de Módulo',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT17'),1,true),
 ('TM09','Reingreso',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT18'),5,true),
 ('TM10','Evaluación Extraordinaria',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT19'),1,true),
 ('TM11','Reporte Record de Notas',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT21'),10,true),
 ('TM12','Constancia de Ingreso',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT22'),15,true),
 ('TM13','Constancia de Estudios',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT24'),15,true),
 ('TM14','Constancia de Primera Matrícula',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT25'),15,true),
 ('TM15','Constancia de Egresado',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT26'),15,true),
 ('TM16','Constancia de Título en Trámite',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT27'),15,true),
 ('TM17','Constancia de Tercio Superior o Conducta',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT28'),15,true),
 ('TM18','Carta de Presentación',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT29'),10,true),
 ('TM19','Duplicado de Carné Estudiantil',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT04'),5,true),
 ('TM20','Diploma de Egresado',(SELECT id FROM mod09.conceptos_pago WHERE codigo='CT45'),15,true)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO mod09.requisitos_tipos_tramite (tipo_tramite_id, orden, requisito)
SELECT tt.id, v.orden, v.requisito
FROM (VALUES
 ('TM01',1,'Recibo de pago de matrícula ingresante'),
 ('TM01',2,'Copia de DNI'),
 ('TM02',1,'Recibo de pago de matrícula por traslado'),
 ('TM02',2,'Constancia de vacante o resolución de traslado'),
 ('TM03',1,'Copia de boleta de notas'),
 ('TM03',2,'Recibo de pago de ratificación'),
 ('TM04',1,'Copia de boleta de notas'),
 ('TM04',2,'Declaración jurada de fraccionamiento'),
 ('TM05',1,'Solicitud dirigida al Director General'),
 ('TM05',2,'Recibo de pago (CT13 Reserva de matrícula)'),
 ('TM05',3,'Declaración jurada del motivo de reserva'),
 ('TM06',1,'Solicitud dirigida al Director General'),
 ('TM06',2,'Certificados de estudios originales'),
 ('TM06',3,'Recibo de pago de convalidación'),
 ('TM07',1,'Verificación previa de horarios (sábana)'),
 ('TM07',2,'Boleta de notas'),
 ('TM08',1,'Recibo de pago de repitencia de módulo'),
 ('TM08',2,'Historial académico'),
 ('TM09',1,'Solicitud dirigida al Director General'),
 ('TM09',2,'Copia de boleta de notas'),
 ('TM10',1,'Solicitud dirigida al Director General'),
 ('TM10',2,'Record de notas'),
 ('TM10',3,'Recibo de pago de evaluación extraordinaria'),
 ('TM11',1,'Solicitud dirigida al Director General'),
 ('TM11',2,'Recibo de pago de record de notas'),
 ('TM12',1,'Verificación de ingreso en listado oficial de admisión'),
 ('TM12',2,'Recibo de pago de constancia de ingreso'),
 ('TM13',1,'Solicitud dirigida al Director General'),
 ('TM13',2,'01 foto tamaño carné'),
 ('TM13',3,'Recibo de pago de constancia de estudios'),
 ('TM14',1,'Solicitud dirigida al Director General'),
 ('TM14',2,'01 foto tamaño carné'),
 ('TM14',3,'Recibo de pago de constancia de primera matrícula'),
 ('TM15',1,'Solicitud dirigida al Director General'),
 ('TM15',2,'02 fotos tamaño carné'),
 ('TM15',3,'Recibo de pago de constancia de egresado'),
 ('TM16',1,'Solicitud dirigida al Director General'),
 ('TM16',2,'02 fotos tamaño carné'),
 ('TM16',3,'Recibo de pago de constancia de título en trámite'),
 ('TM17',1,'Solicitud dirigida al Director General'),
 ('TM17',2,'Recibo de pago de constancia de tercio superior'),
 ('TM18',1,'Solicitud dirigida al Director General'),
 ('TM18',2,'Recibo de pago de carta de presentación'),
 ('TM19',1,'Solicitud dirigida al Director General'),
 -- El carné no exige recibo: no figura en el TUPA-2026 (ver nota más abajo).
 ('TM19',2,'01 foto tamaño carné fondo blanco'),
 ('TM20',1,'Solicitud dirigida al Director General'),
 ('TM20',2,'01 foto tamaño carné'),
 ('TM20',3,'Recibo de pago de diploma de egresado')
) AS v(codigo, orden, requisito)
JOIN mod09.tipos_tramite tt ON tt.codigo = v.codigo
ON CONFLICT (tipo_tramite_id, orden, requisito) DO NOTHING;

-- ---------------------------------------------------------------------------
-- Carné sin voucher
--
-- El carné (TT01, TM19) no figura en ninguna de las 23 secciones del TUPA-2026:
-- no hay procedimiento al que cobrar, así que su tipo de trámite queda sin
-- concepto de pago y su catálogo de requisitos NO incluye el recibo (se quitó
-- de las semillas de arriba, no se borra en runtime).
--
-- Importante: no conviene borrar el requisito con un DELETE en cada arranque.
-- mod09.tramite_requisitos.requisito_catalogo_id es ON DELETE RESTRICT, así que
-- en cuanto un alumno presentara ese requisito el DELETE revienta el
-- schema.sql completo y la app deja de arrancar. Tampoco sirve "arreglarlo"
-- borrando después del INSERT: el INSERT recrearía la fila con un id nuevo y
-- dejaría huérfanos los tramite_requisitos que apuntaran al id anterior.
-- ---------------------------------------------------------------------------

-- ============================================================
-- Flujo voucher: el trámite con costo genera el pago y el alumno
-- adjunta el voucher PDF que Tesorería valida.
-- ============================================================
ALTER TABLE mod09.tramites ADD COLUMN IF NOT EXISTS pago_id INT REFERENCES mod09.pagos(id);
CREATE INDEX IF NOT EXISTS idx_tramites_pago ON mod09.tramites(pago_id);

CREATE TABLE IF NOT EXISTS mod09.voucher_archivos (
    pago_id INT NOT NULL PRIMARY KEY REFERENCES mod09.pagos(id) ON DELETE CASCADE,
    archivo_nombre VARCHAR(200) NOT NULL,
    archivo_tipo VARCHAR(100) NOT NULL,
    archivo_contenido BYTEA NOT NULL,
    subido_en TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
