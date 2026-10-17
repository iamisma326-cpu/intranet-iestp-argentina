using System.Data;
using Dapper;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Services;

/// <summary>
/// Servicio de datos del Módulo 01 (Matrícula Académica) — proceso "Cero Filas".
/// Patrón del proyecto: Dapper + IModuleDbConnectionFactory (SearchPath=mod01,core,public).
/// Dominios reales del esquema mod01 (verificados en servidor):
///   estado:  En trámite | Matriculado | Reservada | Anulada
///   etapa:   Validación | Conformidad | Cerrada
///   condicion: Ingresante | Promovido | Promovido con curso a cargo | Repitente | Reingresante | Traslado
/// </summary>
public interface IMatriculaServicio
{
    Task<ModeloMatriculaActiva?> ObtenerMatriculaActivaAsync(int estudianteId);
    Task<IEnumerable<ModeloMatriculaLista>> ListarPorPeriodoAsync(string? filtroEstado);
    Task<IEnumerable<ModeloMatriculaLista>> ListarListasParaRegistrarAsync();
    Task<IEnumerable<ModeloMatriculaLista>> ListarEnEsperaAsync();
    Task<IEnumerable<ModeloMatriculaPorLiberar>> ListarVacantesPorLiberarAsync(int diasLimite = 20);
    Task<ModeloResumenMatricula> ObtenerResumenAsync();
    Task<bool> ValidarVoucherAsync(int matriculaId, bool aprobar);
    Task<bool> ValidarFotoAsync(int matriculaId, bool aprobar);
    Task<(bool Ok, string Mensaje)> RegistrarMatriculaAsync(int matriculaId, int usuarioId);
    Task<IEnumerable<ModeloVacante>> ListarVacantesAsync();
}

public class MatriculaServicio : IMatriculaServicio
{
    private readonly IModuleDbConnectionFactory _fabricaConexion;

    public MatriculaServicio(IModuleDbConnectionFactory fabricaConexion)
    {
        _fabricaConexion = fabricaConexion;
    }

    private IDbConnection CreateConnection() => _fabricaConexion.CreateConnection("01");

    // Vista ALUMNO: su matrícula vigente con el avance del proceso
    public async Task<ModeloMatriculaActiva?> ObtenerMatriculaActivaAsync(int estudianteId)
    {
        using var db = CreateConnection();
        var sql = """
            SELECT m.id,
                   m.codigo_matricula AS Codigo,
                   m.estado, m.etapa,
                   m.voucher_ok AS VoucherOk,
                   m.foto_ok    AS FotoOk,
                   m.condicion,
                   m.fecha_matricula AS FechaMatricula,
                   m.observaciones_cursos AS Observaciones,
                   m.creado_en AS CreadoEn,
                   c.nombre AS Carrera,
                   c.codigo AS CarreraCodigo,
                   ci.nombre AS Ciclo,
                   t.nombre AS Turno,
                   tm.nombre AS TipoMatricula,
                   p.nombres || ' ' || p.apellidos AS Estudiante,
                   e.codigo_estudiante AS CodigoEstudiante,
                   m.tramite_reserva_id AS TramiteReservaId
            FROM matriculas m
            JOIN estudiantes e ON e.id = m.estudiante_id
            JOIN personas p ON p.id = e.persona_id
            JOIN carreras c ON c.id = m.carrera_id
            JOIN ciclos ci ON ci.id = m.ciclo_id
            JOIN turnos t ON t.id = m.turno_id
            JOIN tipos_matricula tm ON tm.id = m.tipo_matricula_id
            WHERE m.estudiante_id = @EstudianteId
              AND m.estado <> 'Anulada'
            ORDER BY m.creado_en DESC
            LIMIT 1;
            """;
        return await db.QueryFirstOrDefaultAsync<ModeloMatriculaActiva>(sql, new { EstudianteId = estudianteId });
    }

    // Vista PERSONAL: tabla general del semestre con filtro por estado
    public async Task<IEnumerable<ModeloMatriculaLista>> ListarPorPeriodoAsync(string? filtroEstado)
    {
        using var db = CreateConnection();
        var sql = """
            SELECT m.id,
                   m.codigo_matricula AS Codigo,
                   m.estado, m.etapa,
                   m.voucher_ok AS VoucherOk,
                   m.foto_ok    AS FotoOk,
                   m.condicion,
                   m.fecha_matricula AS FechaMatricula,
                   m.creado_en AS CreadoEn,
                   c.codigo AS CarreraCodigo,
                   ci.codigo AS Ciclo,
                   t.codigo AS Turno,
                   tm.codigo AS TipoMatricula,
                   p.nombres || ' ' || p.apellidos AS Estudiante,
                   e.codigo_estudiante AS CodigoEstudiante,
                   m.tramite_reserva_id AS TramiteReservaId
            FROM matriculas m
            JOIN estudiantes e ON e.id = m.estudiante_id
            JOIN personas p ON p.id = e.persona_id
            JOIN carreras c ON c.id = m.carrera_id
            JOIN ciclos ci ON ci.id = m.ciclo_id
            JOIN turnos t ON t.id = m.turno_id
            JOIN tipos_matricula tm ON tm.id = m.tipo_matricula_id
            WHERE (@Estado IS NULL OR m.estado = @Estado)
            ORDER BY m.creado_en DESC;
            """;
        return await db.QueryAsync<ModeloMatriculaLista>(sql,
            new { Estado = string.IsNullOrWhiteSpace(filtroEstado) ? null : filtroEstado });
    }

    // Zona A del puesto de trabajo: doble validación completa → conformidad
    public async Task<IEnumerable<ModeloMatriculaLista>> ListarListasParaRegistrarAsync()
    {
        using var db = CreateConnection();
        var sql = """
            SELECT m.id,
                   m.codigo_matricula AS Codigo,
                   m.estado, m.etapa,
                   m.voucher_ok AS VoucherOk,
                   m.foto_ok    AS FotoOk,
                   m.condicion,
                   m.fecha_matricula AS FechaMatricula,
                   m.creado_en AS CreadoEn,
                   c.codigo AS CarreraCodigo,
                   ci.codigo AS Ciclo,
                   t.codigo AS Turno,
                   tm.codigo AS TipoMatricula,
                   p.nombres || ' ' || p.apellidos AS Estudiante,
                   e.codigo_estudiante AS CodigoEstudiante,
                   m.tramite_reserva_id AS TramiteReservaId
            FROM matriculas m
            JOIN estudiantes e ON e.id = m.estudiante_id
            JOIN personas p ON p.id = e.persona_id
            JOIN carreras c ON c.id = m.carrera_id
            JOIN ciclos ci ON ci.id = m.ciclo_id
            JOIN turnos t ON t.id = m.turno_id
            JOIN tipos_matricula tm ON tm.id = m.tipo_matricula_id
            WHERE m.estado = 'En trámite'
              AND m.voucher_ok = TRUE
              AND m.foto_ok = TRUE
            ORDER BY m.creado_en;
            """;
        return await db.QueryAsync<ModeloMatriculaLista>(sql);
    }

    // Zona B: esperando alguna de las dos validaciones
    public async Task<IEnumerable<ModeloMatriculaLista>> ListarEnEsperaAsync()
    {
        using var db = CreateConnection();
        var sql = """
            SELECT m.id,
                   m.codigo_matricula AS Codigo,
                   m.estado, m.etapa,
                   m.voucher_ok AS VoucherOk,
                   m.foto_ok    AS FotoOk,
                   m.condicion,
                   m.fecha_matricula AS FechaMatricula,
                   m.creado_en AS CreadoEn,
                   c.codigo AS CarreraCodigo,
                   ci.codigo AS Ciclo,
                   t.codigo AS Turno,
                   tm.codigo AS TipoMatricula,
                   p.nombres || ' ' || p.apellidos AS Estudiante,
                   e.codigo_estudiante AS CodigoEstudiante,
                   m.tramite_reserva_id AS TramiteReservaId
            FROM matriculas m
            JOIN estudiantes e ON e.id = m.estudiante_id
            JOIN personas p ON p.id = e.persona_id
            JOIN carreras c ON c.id = m.carrera_id
            JOIN ciclos ci ON ci.id = m.ciclo_id
            JOIN turnos t ON t.id = m.turno_id
            JOIN tipos_matricula tm ON tm.id = m.tipo_matricula_id
            WHERE m.estado = 'En trámite'
              AND (m.voucher_ok = FALSE OR m.foto_ok = FALSE)
            ORDER BY m.creado_en;
            """;
        return await db.QueryAsync<ModeloMatriculaLista>(sql);
    }

    // Zona C (Art. 24 RI): En trámite con más de N días sin completar.
    // Nota: días naturales (el cálculo en días hábiles con feriados
    // pertenece a mod09 — soberanía de esquemas; se coordina por evento).
    public async Task<IEnumerable<ModeloMatriculaPorLiberar>> ListarVacantesPorLiberarAsync(int diasLimite = 20)
    {
        using var db = CreateConnection();
        var sql = """
            SELECT m.id,
                   m.codigo_matricula AS Codigo,
                   m.creado_en AS CreadoEn,
                   CURRENT_DATE - m.creado_en::date AS DiasTranscurridos,
                   c.codigo AS CarreraCodigo,
                   ci.codigo AS Ciclo,
                   t.codigo AS Turno,
                   p.nombres || ' ' || p.apellidos AS Estudiante
            FROM matriculas m
            JOIN estudiantes e ON e.id = m.estudiante_id
            JOIN personas p ON p.id = e.persona_id
            JOIN carreras c ON c.id = m.carrera_id
            JOIN ciclos ci ON ci.id = m.ciclo_id
            JOIN turnos t ON t.id = m.turno_id
            WHERE m.estado = 'En trámite'
              AND m.creado_en::date < CURRENT_DATE - @DiasLimite
            ORDER BY m.creado_en;
            """;
        return await db.QueryAsync<ModeloMatriculaPorLiberar>(sql, new { DiasLimite = diasLimite });
    }

    // Métricas del dashboard del módulo
    public async Task<ModeloResumenMatricula> ObtenerResumenAsync()
    {
        using var db = CreateConnection();
        var sql = """
            SELECT count(*) FILTER (WHERE estado = 'Matriculado')                AS Matriculados,
                   count(*) FILTER (WHERE estado = 'En trámite')                 AS EnTramite,
                   count(*) FILTER (WHERE estado = 'En trámite'
                                     AND voucher_ok AND foto_ok)                  AS ListasParaRegistrar,
                   count(*) FILTER (WHERE estado = 'En trámite'
                                     AND (NOT voucher_ok OR NOT foto_ok))        AS EnEspera,
                   count(*) FILTER (WHERE estado = 'Reservada')                  AS Reservadas
            FROM matriculas;
            """;
        return await db.QueryFirstOrDefaultAsync<ModeloResumenMatricula>(sql)
               ?? new ModeloResumenMatricula();
    }

    // Validaciones (doble validación Cero Filas).
    // Aprobar marca el booleano y avanza la etapa a Conformidad cuando
    // ambas validaciones están completas. El rechazo CON MOTIVO se
    // registra en mod09 (pagos.motivo_rechazo / trámite Observado):
    // aquí solo se deja el booleano en FALSE.
    public async Task<bool> ValidarVoucherAsync(int matriculaId, bool aprobar)
    {
        using var db = CreateConnection();
        var sql = """
            UPDATE matriculas
            SET voucher_ok = @Aprobar,
                etapa = CASE WHEN @Aprobar AND foto_ok THEN 'Conformidad' ELSE etapa END,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE id = @Id AND estado = 'En trámite';
            """;
        return await db.ExecuteAsync(sql, new { Id = matriculaId, Aprobar = aprobar }) > 0;
    }

    public async Task<bool> ValidarFotoAsync(int matriculaId, bool aprobar)
    {
        using var db = CreateConnection();
        var sql = """
            UPDATE matriculas
            SET foto_ok = @Aprobar,
                etapa = CASE WHEN @Aprobar AND voucher_ok THEN 'Conformidad' ELSE etapa END,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE id = @Id AND estado = 'En trámite';
            """;
        return await db.ExecuteAsync(sql, new { Id = matriculaId, Aprobar = aprobar }) > 0;
    }

    // Conformidad: registrar la matrícula y consumir 1 vacante,
    // atómicamente (o ninguna de las dos).
    public async Task<(bool Ok, string Mensaje)> RegistrarMatriculaAsync(int matriculaId, int usuarioId)
    {
        using var db = CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        // 1) Conformidad solo si la doble validación está completa
        const string upd = """
            UPDATE matriculas
            SET estado = 'Matriculado',
                etapa = 'Cerrada',
                fecha_matricula = CURRENT_DATE,
                conforme_por = @UsuarioId,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE id = @Id
              AND estado = 'En trámite'
              AND voucher_ok = TRUE
              AND foto_ok = TRUE
            RETURNING carrera_id AS CarreraId, ciclo_id AS CicloId,
                      turno_id AS TurnoId, periodo_id AS PeriodoId;
            """;
        var fila = await db.QueryFirstOrDefaultAsync<ModeloVacanteConsumo>(upd,
            new { Id = matriculaId, UsuarioId = usuarioId }, tx);

        if (fila == null || fila.CarreraId == 0)
        {
            tx.Rollback();
            return (false, "La matrícula no existe, ya fue registrada o le falta una validación (voucher/foto).");
        }

        // 2) Consumir 1 vacante del carril correspondiente (atómico)
        const string vac = """
            UPDATE vacantes
            SET vacantes = vacantes - 1
            WHERE periodo_id = @PeriodoId
              AND carrera_id = @CarreraId
              AND turno_id = @TurnoId
              AND ciclo_id = @CicloId
              AND vacantes > 0;
            """;
        var consumidas = await db.ExecuteAsync(vac,
            new { fila.PeriodoId, fila.CarreraId, fila.TurnoId, fila.CicloId }, tx);

        if (consumidas == 0)
        {
            tx.Rollback();
            return (false, "No hay vacantes disponibles en ese carril (carrera/turno/ciclo).");
        }

        tx.Commit();
        return (true, "Matrícula registrada y vacante consumida.");
    }

    // Tablero de vacantes por carril (periodo activo)
    public async Task<IEnumerable<ModeloVacante>> ListarVacantesAsync()
    {
        using var db = CreateConnection();
        var sql = """
            SELECT c.codigo AS CarreraCodigo,
                   c.nombre AS Carrera,
                   t.codigo AS Turno,
                   ci.codigo AS Ciclo,
                   v.vacantes AS Disponibles,
                   (SELECT count(*) FROM matriculas m
                     WHERE m.carrera_id = v.carrera_id
                       AND m.turno_id = v.turno_id
                       AND m.ciclo_id = v.ciclo_id
                       AND m.periodo_id = v.periodo_id
                       AND m.estado = 'Matriculado') AS Ocupadas
            FROM vacantes v
            JOIN carreras c ON c.id = v.carrera_id
            JOIN turnos t ON t.id = v.turno_id
            JOIN ciclos ci ON ci.id = v.ciclo_id
            ORDER BY c.codigo, t.codigo, ci.codigo;
            """;
        return await db.QueryAsync<ModeloVacante>(sql);
    }
}
