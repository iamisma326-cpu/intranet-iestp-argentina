-- ============================================================================
-- ROLES DE BASE DE DATOS POR EQUIPO — aislamiento real (Neon permite 500 roles)
-- ============================================================================
-- Ejecutar conectado como el rol de administración de Neon.
-- Cada equipo obtiene:
--   * Su propio LOGIN con contraseña (la entrega el admin, nunca al repo)
--   * Un GRUPO de privilegios por módulo, reutilizable
--   * SELECT (lectura) sobre `core` — nunca escritura
--   * Propiedad TOTAL de su esquema modXX (tablas que crea su schema.sql)
--
-- El esquema de cada módulo lo crea su propio schema.sql (Areas/ModuloXX/Sql/).
-- Este script solo crea esquemas vacíos + roles + grants.
--
-- CONTRASEÑAS: generarlas con `openssl rand -base64 18` y entregarlas por
-- canal privado a cada equipo. NO van en el repositorio.
-- ============================================================================

-- Esquemas soberanos de cada módulo
CREATE SCHEMA IF NOT EXISTS mod01;
CREATE SCHEMA IF NOT EXISTS mod02;
CREATE SCHEMA IF NOT EXISTS mod03;
CREATE SCHEMA IF NOT EXISTS mod04;
CREATE SCHEMA IF NOT EXISTS mod05;
CREATE SCHEMA IF NOT EXISTS mod06;
CREATE SCHEMA IF NOT EXISTS mod07;

-- ---------------------------------------------------------------------------
-- GRUPOS DE PRIVILEGIOS (roles sin login, se heredan)
-- ---------------------------------------------------------------------------
-- Lectura del núcleo: cualquier módulo puede consultar personas/roles, nadie
-- puede escribirlos (solo el login escribe en core.intentos_login/sesiones_login).
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'grupo_core_lectura') THEN
        CREATE ROLE grupo_core_lectura NOLOGIN;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'grupo_auditoria_login') THEN
        -- El login (user_app) escribe auditoría en core y lee todo lo demás
        CREATE ROLE grupo_auditoria_login NOLOGIN;
    END IF;
END
$$;

GRANT USAGE ON SCHEMA core TO grupo_core_lectura;
GRANT SELECT, REFERENCES ON ALL TABLES IN SCHEMA core TO grupo_core_lectura;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA core TO grupo_core_lectura;
ALTER DEFAULT PRIVILEGES IN SCHEMA core GRANT SELECT, REFERENCES ON TABLES TO grupo_core_lectura;

-- El usuario de la APLICACIÓN (login) necesita además INSERT/UPDATE de auditoría
GRANT INSERT, UPDATE ON core.intentos_login, core.sesiones_login TO grupo_auditoria_login;
GRANT UPDATE ON core.usuarios TO grupo_auditoria_login; -- solo ultimo_acceso

-- ---------------------------------------------------------------------------
-- USUARIOS POR EQUIPO (uno por módulo) — CAMBIAR LAS CONTRASEÑAS ANTES DE EJECUTAR
-- ---------------------------------------------------------------------------
DO $$
DECLARE
    equipos text[][] := ARRAY[
        -- [login,        esquema,  contraseña_placeholder]
        ARRAY['user_equipo01', 'mod01', 'CAMBIAR_EQUIPO01'],
        ARRAY['user_equipo02', 'mod02', 'CAMBIAR_EQUIPO02'],
        ARRAY['user_equipo03', 'mod03', 'CAMBIAR_EQUIPO03'],
        ARRAY['user_equipo04', 'mod04', 'CAMBIAR_EQUIPO04'],
        ARRAY['user_equipo05', 'mod05', 'CAMBIAR_EQUIPO05'],
        ARRAY['user_equipo06', 'mod06', 'CAMBIAR_EQUIPO06'],
        ARRAY['user_equipo07', 'mod07', 'CAMBIAR_EQUIPO07']
    ];
    i int;
    login_name text;
    esquema text;
BEGIN
    FOR i IN 1..array_length(equipos, 1) LOOP
        login_name := equipos[i][1];
        esquema := equipos[i][2];

        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = login_name) THEN
            EXECUTE format(
                'CREATE ROLE %I LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE',
                login_name, equipos[i][3]
            );
        END IF;

        -- Propiedad y privilegios sobre SU esquema (tablas presentes y futuras)
        EXECUTE format('GRANT USAGE, CREATE ON SCHEMA %I TO %I', esquema, login_name);
        EXECUTE format('GRANT ALL ON ALL TABLES IN SCHEMA %I TO %I', esquema, login_name);
        EXECUTE format('GRANT ALL ON ALL SEQUENCES IN SCHEMA %I TO %I', esquema, login_name);
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL ON TABLES TO %I',
            esquema, login_name
        );
        EXECUTE format(
            'ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT ALL ON SEQUENCES TO %I',
            esquema, login_name
        );

        -- Lectura del núcleo compartido
        EXECUTE format('GRANT grupo_core_lectura TO %I', login_name);
    END LOOP;
END
$$;

-- ---------------------------------------------------------------------------
-- USUARIO DE LA APLICACIÓN (runtime del login/dashboards)
-- Conecta con la cadena DB_CONNECTION_STRING del .env
-- ---------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'user_app') THEN
        CREATE ROLE user_app LOGIN PASSWORD 'CAMBIAR_APP' NOSUPERUSER NOCREATEDB NOCREATEROLE;
    END IF;
END
$$;

GRANT grupo_core_lectura TO user_app;
GRANT grupo_auditoria_login TO user_app;

-- ============================================================================
-- VERIFICACIÓN:
--   SELECT rolname FROM pg_roles WHERE rolname LIKE 'user_%' ORDER BY 1;
--   → user_app, user_equipo01..07
-- La app (user_app) puede: leer core, insertar auditoría, actualizar ultimo_acceso.
-- Cada equipo puede: TODO en su esquema, LEER core, NADA en esquemas ajenos.
-- ============================================================================
