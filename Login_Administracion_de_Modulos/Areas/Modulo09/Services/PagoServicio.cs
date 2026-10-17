using System.Data;
using Dapper;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo09.Models;

namespace Login_Administracion_de_Modulos.Areas.Modulo09.Services;

/// <summary>
/// Servicio de Pagos del Módulo 09 (Tesorería & Pagos).
/// Flujo del prototipo WebForms (Bandejas.aspx + Pagos.aspx):
///   Estudiante registra su pago con voucher → Tesorería lo valida desde
///   la Bandeja (Aprobar / Rechazar con motivo) → el pago Validado habilita
///   la conformidad de matrícula y la aprobación de trámites TUPA.
///   Tesorería también puede emitir recibos directos (caja).
/// Dominio real de voucher_estado: Pendiente | Validado | Rechazado
/// (CHECK de coherencia: Validado exige fecha_validacion + validado_por).
/// </summary>
public interface IPagoServicio
{
    Task<IEnumerable<ModeloPagoLista>> ListarPorEstudianteAsync(int estudianteId);
    Task<IEnumerable<ModeloPagoBandeja>> ListarBandejaAsync(string? voucherEstado);
    Task<ModeloPagoBandeja?> ObtenerAsync(int pagoId);
    Task<(bool Ok, string Mensaje)> RegistrarPagoEstudianteAsync(
        int estudianteId, int periodoId, string conceptoCodigo, string tipoPagoCodigo, decimal monto, string? nota);
    Task<(bool Ok, string Mensaje)> EmitirReciboDirectoAsync(
        int estudianteId, int periodoId, string conceptoCodigo, string tipoPagoCodigo, decimal monto, int cajeroId);
    Task<(bool Ok, string Mensaje)> ValidarVoucherAsync(int pagoId, bool aprobar, string? motivo, int validadorId);
    Task<(bool Ok, string Mensaje)> SubirVoucherAsync(int pagoId, int estudianteId, string nombre, string tipo, byte[] contenido);
    Task<ModeloArchivoVoucher?> ObtenerVoucherAsync(int pagoId);
    Task<bool> PerteneceAlEstudianteAsync(int pagoId, int estudianteId);
    Task<ModeloResumenPagos> ResumenAsync();
    Task<ModeloResumenTesoreria> ResumenTesoreriaAsync();
    Task<ModeloResumenAlumno> ResumenAlumnoAsync(int estudianteId);
    Task<IEnumerable<ModeloConceptoPago>> ListarConceptosAsync();
    Task<IEnumerable<ModeloTipoPago>> ListarTiposPagoAsync();
}

public class PagoServicio : IPagoServicio
{
    private readonly IModuleDbConnectionFactory _fabricaConexion;
    private readonly ITramiteServicio _servicioTramite;

    public PagoServicio(IModuleDbConnectionFactory fabricaConexion, ITramiteServicio servicioTramite)
    {
        _fabricaConexion = fabricaConexion;
        _servicioTramite = servicioTramite;
    }

    private IDbConnection CreateConnection() => _fabricaConexion.CreateConnection("09");

    private const string SqlNuevoRecibo = """
        SELECT 'REC-' || lpad((COALESCE((SELECT MAX(right(codigo, 4)::int)
                                         FROM pagos WHERE codigo ~ '^REC-[0-9]{4}$'), 0) + 1)::text, 4, '0');
        """;

    // ALUMNO: sus pagos con el concepto y estado del voucher
    public async Task<IEnumerable<ModeloPagoLista>> ListarPorEstudianteAsync(int estudianteId)
    {
        using var db = CreateConnection();
        const string sql = """
            SELECT p.id,
                   p.codigo,
                   p.monto,
                   p.fecha_pago AS FechaPago,
                   p.voucher_estado AS VoucherEstado,
                   p.motivo_rechazo AS MotivoRechazo,
                   cp.codigo AS ConceptoCodigo,
                   cp.nombre AS ConceptoNombre,
                   tp.nombre AS TipoPago,
                   pa.codigo AS Periodo,
                   (SELECT count(*) FROM voucher_archivos va WHERE va.pago_id = p.id) > 0 AS TieneVoucher
            FROM pagos p
            JOIN conceptos_pago cp ON cp.id = p.concepto_pago_id
            JOIN tipos_pago tp ON tp.id = p.tipo_pago_id
            JOIN periodos_academicos pa ON pa.id = p.periodo_id
            WHERE p.estudiante_id = @EstudianteId
            ORDER BY p.fecha_pago DESC, p.id DESC;
            """;
        return await db.QueryAsync<ModeloPagoLista>(sql, new { EstudianteId = estudianteId });
    }

    // BANDEJA DE TESORERÍA: todos los pagos con datos del estudiante
    public async Task<IEnumerable<ModeloPagoBandeja>> ListarBandejaAsync(string? voucherEstado)
    {
        using var db = CreateConnection();
        const string sql = """
            SELECT p.id,
                   p.codigo,
                   p.monto,
                   p.fecha_pago AS FechaPago,
                   p.voucher_estado AS VoucherEstado,
                   p.motivo_rechazo AS MotivoRechazo,
                   p.fecha_validacion AS FechaValidacion,
                   p.intentos,
                   cp.codigo AS ConceptoCodigo,
                   cp.nombre AS ConceptoNombre,
                   tp.nombre AS TipoPago,
                   e.codigo_estudiante AS CodigoEstudiante,
                   pe.nombres || ' ' || pe.apellidos AS Estudiante,
                   (SELECT count(*) FROM voucher_archivos va WHERE va.pago_id = p.id) > 0 AS TieneVoucher
            FROM pagos p
            JOIN conceptos_pago cp ON cp.id = p.concepto_pago_id
            JOIN tipos_pago tp ON tp.id = p.tipo_pago_id
            JOIN estudiantes e ON e.id = p.estudiante_id
            JOIN personas pe ON pe.id = e.persona_id
            WHERE (@Estado IS NULL OR p.voucher_estado = @Estado)
            ORDER BY CASE p.voucher_estado WHEN 'Pendiente' THEN 0 ELSE 1 END,
                     p.fecha_pago DESC;
            """;
        return await db.QueryAsync<ModeloPagoBandeja>(sql,
            new { Estado = string.IsNullOrWhiteSpace(voucherEstado) ? null : voucherEstado });
    }

    public async Task<ModeloPagoBandeja?> ObtenerAsync(int pagoId)
    {
        using var db = CreateConnection();
        const string sql = """
            SELECT p.id,
                   p.codigo,
                   p.monto,
                   p.fecha_pago AS FechaPago,
                   p.voucher_estado AS VoucherEstado,
                   p.motivo_rechazo AS MotivoRechazo,
                   p.fecha_validacion AS FechaValidacion,
                   p.intentos,
                   cp.codigo AS ConceptoCodigo,
                   cp.nombre AS ConceptoNombre,
                   tp.nombre AS TipoPago,
                   e.codigo_estudiante AS CodigoEstudiante,
                   pe.nombres || ' ' || pe.apellidos AS Estudiante,
                   (SELECT count(*) FROM voucher_archivos va WHERE va.pago_id = p.id) > 0 AS TieneVoucher
            FROM pagos p
            JOIN conceptos_pago cp ON cp.id = p.concepto_pago_id
            JOIN tipos_pago tp ON tp.id = p.tipo_pago_id
            JOIN estudiantes e ON e.id = p.estudiante_id
            JOIN personas pe ON pe.id = e.persona_id
            WHERE p.id = @Id;
            """;
        return await db.QueryFirstOrDefaultAsync<ModeloPagoBandeja>(sql, new { Id = pagoId });
    }

    // ALUMNO: registrar pago con voucher (queda Pendiente → bandeja)
    public async Task<(bool, string)> RegistrarPagoEstudianteAsync(
        int estudianteId, int periodoId, string conceptoCodigo, string tipoPagoCodigo, decimal monto, string? nota)
    {
        // El importe lo manda SIEMPRE el catálogo TUPA, nunca el formulario.
        // Se conserva el parámetro por compatibilidad de firma, pero el alumno
        // no tiene autoridad para decidir cuánto se paga: antes el valor que
        // posteaba el navegadorganaba siempre que fuera un número válido
        // (S/ 0.01 para una Reserva de Matrícula de S/ 30), y eso habilitaba
        // matricular sin pagar.
        return await InsertarPagoAsync(estudianteId, periodoId, conceptoCodigo,
            tipoPagoCodigo, monto, "Pendiente", null, nota, usarMontoDelCatalogo: true);
    }

    // TESORERÍA: recibo directo de caja (ya validado por quien lo emite)
    public async Task<(bool, string)> EmitirReciboDirectoAsync(
        int estudianteId, int periodoId, string conceptoCodigo, string tipoPagoCodigo, decimal monto, int cajeroId)
    {
        // El cajero SÍ tiene autoridad sobre el importe cobrado en caja (pago
        // parcial, ajuste, recargo), pero se le avisa cuando lo que cobra no
        // coincide con el importe del TUPA para que la diferencia quede
        // registrada en la bitácora y no pase inadvertida.
        return await InsertarPagoAsync(estudianteId, periodoId, conceptoCodigo,
            tipoPagoCodigo, monto, "Validado", cajeroId, null, usarMontoDelCatalogo: false);
    }

    private async Task<(bool, string)> InsertarPagoAsync(
        int estudianteId, int periodoId, string conceptoCodigo, string tipoPagoCodigo,
        decimal monto, string estadoInicial, int? validadorId, string? nota,
        bool usarMontoDelCatalogo)
    {
        using var db = CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        var concepto = await db.QueryFirstOrDefaultAsync<(int Id, decimal Monto)>(
            "SELECT id, monto FROM conceptos_pago WHERE codigo = @C;",
            new { C = conceptoCodigo }, tx);
        if (concepto.Id == 0)
        {
            tx.Rollback();
            return (false, "El concepto de pago no existe.");
        }

        var tipoId = await db.ExecuteScalarAsync<int?>(
            "SELECT id FROM tipos_pago WHERE codigo = @C;",
            new { C = tipoPagoCodigo }, tx);
        if (tipoId is null)
        {
            tx.Rollback();
            return (false, "El tipo de pago no existe.");
        }

        if (monto <= 0)
        {
            tx.Rollback();
            return (false, "El monto debe ser mayor que cero.");
        }

        // El catálogo es la autoridad del importe. Si el flujo no delega en él
        // (recibo de caja), se avisa de la diferencia en vez de ignorarla.
        decimal montoFinal = usarMontoDelCatalogo ? concepto.Monto : monto;
        string? avisoDiferencia = (!usarMontoDelCatalogo && monto != concepto.Monto)
            ? $" Ojo: el importe cobrado (S/ {monto:0.00}) difiere del TUPA (S/ {concepto.Monto:0.00})."
            : null;

        var codigo = await db.ExecuteScalarAsync<string>(SqlNuevoRecibo, transaction: tx);

        var filas = await db.ExecuteAsync("""
            INSERT INTO pagos (codigo, estudiante_id, concepto_pago_id, periodo_id,
                               tipo_pago_id, monto, fecha_pago, voucher_estado,
                               fecha_validacion, validado_por, motivo_rechazo)
            VALUES (@Codigo, @EstudianteId, @ConceptoId, @PeriodoId,
                    @TipoId, @Monto, CURRENT_DATE, @Estado,
                    CASE WHEN @Estado = 'Validado' THEN CURRENT_DATE END,
                    @Validador, @Nota);
            """, new
        {
            Codigo = codigo,
            EstudianteId = estudianteId,
            ConceptoId = concepto.Id,
            PeriodoId = periodoId,
            TipoId = tipoId.Value,
            Monto = montoFinal,
            Estado = estadoInicial,
            Validador = validadorId,
            Nota = nota
        }, tx);

        tx.Commit();
        return filas > 0
            ? (true, $"Recibo {codigo} registrado por S/ {montoFinal:0.00}.{avisoDiferencia}")
            : (false, "No se pudo registrar el pago.");
    }

    // BANDEJA: validar voucher — la acción central de Tesorería.
    // Aprobar marca Validado con fecha y validador (CHECK de coherencia
    // de la tabla lo exige); Rechazar exige motivo para el estudiante
    // e incrementa intentos (máx 20 por CHECK).
    public async Task<(bool, string)> ValidarVoucherAsync(int pagoId, bool aprobar, string? motivo, int validadorId)
    {
        using var db = CreateConnection();

        if (!aprobar && string.IsNullOrWhiteSpace(motivo))
            return (false, "Indica el motivo del rechazo (el estudiante lo verá).");

        var tabla = "pagos";
        var filas = await db.ExecuteAsync("""
            UPDATE pagos
            SET voucher_estado = @Estado,
                fecha_validacion = CASE WHEN @Estado = 'Validado' THEN CURRENT_DATE ELSE NULL END,
                validado_por = CASE WHEN @Estado = 'Validado' THEN @Validador ELSE NULL END,
                motivo_rechazo = @Motivo,
                intentos = intentos + 1,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE id = @Id
              AND voucher_estado <> 'Validado';
            """.Replace("pagos", tabla), new { Id = pagoId, Estado = aprobar ? "Validado" : "Rechazado",
                       Motivo = motivo, Validador = validadorId });

        if (filas == 0) return (false, "El pago no existe o ya fue validado.");

        // Propagación trámite↔pago: si el pago pertenece a un trámite en curso
        // (p. ej. Reserva de Matrícula TM05), la validación de Tesorería
        // impulsa su avance: rechazo → trámite Observado con el motivo;
        // aprobación → el trámite queda listo para que Mesa lo cierre
        // (Aprobado/Entregado sigue siendo decisión de Secretaría).
        // La vinculación se resuelve SOLO por tramites.pago_id, presente en
        // producción (pagos) y en espejos (pagos); la columna inversa
        // pagos.tramite_id solo existe en algunos espejos y NO en producción.
        var tablaPagos = "pagos";
        var sqlTramite = "SELECT t.id FROM mod09.tramites t " +
                         "WHERE t.pago_id = @Id LIMIT 1;";
        var tramiteAfectado = await db.ExecuteScalarAsync<int?>(sqlTramite, new { Id = pagoId });
        if (tramiteAfectado is int tid)
        {
            await db.ExecuteAsync("""
                UPDATE mod09.tramites
                SET estado = CASE WHEN @Aprobado THEN 'En evaluación' ELSE 'Observado' END,
                    resolucion = CASE WHEN @Aprobado
                                THEN 'Voucher validado por Tesorería.'
                                ELSE 'Voucher rechazado: ' || @Motivo END,
                    fecha_resolucion = CURRENT_DATE,
                    fecha_limite_correccion = CASE WHEN @Aprobado
                                THEN NULL ELSE mod09.fecha_limite_correccion() END,
                    actualizado_en = CURRENT_TIMESTAMP
                WHERE id = @Tid AND estado NOT IN ('Aprobado','Entregado','Rechazado','Vencido');
                """, new { Aprobado = aprobar, Motivo = motivo, Tid = tid });
        }

        return (true, aprobar
            ? "Voucher validado. La matrícula/trámite queda habilitado para conformidad."
            : "Voucher rechazado con motivo. El estudiante debe corregirlo.");
    }

    // ALUMNO: adjuntar el voucher PDF de su pago. Verifica que el pago sea
    // del estudiante (Zero-Blast-Radius: no se puede adjuntar a pagos ajenos).
    public async Task<(bool, string)> SubirVoucherAsync(
        int pagoId, int estudianteId, string nombre, string tipo, byte[] contenido)
    {
        using var db = CreateConnection();
        var dueno = await db.ExecuteScalarAsync<int?>(
            "SELECT estudiante_id FROM pagos WHERE id = @Id;", new { Id = pagoId });
        if (dueno != estudianteId)
            return (false, "El recibo no existe o no es tuyo.");

        var filas = await db.ExecuteAsync("""
            INSERT INTO voucher_archivos (pago_id, archivo_nombre, archivo_tipo, archivo_contenido)
            VALUES (@Id, @Nombre, @Tipo, @Contenido)
            ON CONFLICT (pago_id) DO UPDATE SET
                archivo_nombre = EXCLUDED.archivo_nombre,
                archivo_tipo = EXCLUDED.archivo_tipo,
                archivo_contenido = EXCLUDED.archivo_contenido,
                subido_en = CURRENT_TIMESTAMP;
            """, new { Id = pagoId, Nombre = nombre, Tipo = tipo, Contenido = contenido });

        if (filas == 0) return (false, "No se pudo guardar el voucher.");

        // Corrección de voucher rechazado: el re-envío devuelve el pago a la
        // cola de validación de Tesorería (voucher_estado = 'Pendiente') con
        // el intento registrado. Antes el pago quedaba en 'Rechazado' y el
        // voucher corregido no reaparecía en la bandeja "Por validar" —
        // Tesorería tenía que filtrar por Rechazado para encontrarlo.
        await db.ExecuteAsync("""
            UPDATE pagos
            SET voucher_estado = 'Pendiente',
                fecha_validacion = NULL,
                validado_por = NULL,
                motivo_rechazo = NULL,
                intentos = intentos + 1,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE id = @Id
              AND voucher_estado = 'Rechazado';
            """, new { Id = pagoId });

        // Si el pago pertenece a un trámite en curso, también retoma su flujo:
        // el trámite que quedó 'Observado' por el rechazo vuelve a evaluación.
        await db.ExecuteAsync("""
            UPDATE mod09.tramites
            SET estado = 'En evaluación',
                resolucion = NULL,
                fecha_resolucion = NULL,
                actualizado_en = CURRENT_TIMESTAMP
            WHERE pago_id = @Id
              AND estado = 'Observado';
            """, new { Id = pagoId });

        return (true, "Voucher adjuntado. Tesorería lo validará desde su bandeja.");
    }

    // PERSONAL: leer el voucher PDF de un pago (validación con evidencia)
    public async Task<ModeloArchivoVoucher?> ObtenerVoucherAsync(int pagoId)
    {
        using var db = CreateConnection();
        return await db.QueryFirstOrDefaultAsync<ModeloArchivoVoucher>("""
            SELECT archivo_nombre AS Nombre,
                   archivo_tipo AS Tipo,
                   archivo_contenido AS Contenido
            FROM voucher_archivos WHERE pago_id = @Id;
            """, new { Id = pagoId });
    }

    // ALUMNO: el voucher solo si el pago le pertenece. Se usa desde
    // PagosController.Voucher para que el alumno vea SU propio PDF sin
    // abrirle la bandeja de Tesorería.
    public async Task<bool> PerteneceAlEstudianteAsync(int pagoId, int estudianteId)
    {
        using var db = CreateConnection();
        return await db.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pagos WHERE id = @PagoId AND estudiante_id = @EstudianteId;",
            new { PagoId = pagoId, EstudianteId = estudianteId }) > 0;
    }

    // Métricas del dashboard del módulo
    public async Task<ModeloResumenPagos> ResumenAsync()
    {
        using var db = CreateConnection();
        const string sql = """
            SELECT count(*) FILTER (WHERE voucher_estado = 'Pendiente')  AS PorValidar,
                   count(*) FILTER (WHERE voucher_estado = 'Validado')  AS Validados,
                   count(*) FILTER (WHERE voucher_estado = 'Rechazado') AS Rechazados,
                   COALESCE(sum(monto) FILTER (WHERE voucher_estado = 'Validado'), 0) AS Recaudado
            FROM pagos;
            """;
        return await db.QueryFirstOrDefaultAsync<ModeloResumenPagos>(sql) ?? new ModeloResumenPagos();
    }

    // Panel del puesto de TESORERÍA (Resumen del módulo): vouchers y
    // recaudación — sin nada de la mesa de trámites (eso es de Secretaría).
    public async Task<ModeloResumenTesoreria> ResumenTesoreriaAsync()
    {
        using var db = CreateConnection();
        var t = "pagos";
        var dto = await db.QueryFirstOrDefaultAsync<ModeloResumenTesoreria>("""
            SELECT count(*) FILTER (WHERE voucher_estado = 'Pendiente')   AS VouchersPendientes,
                   count(*) FILTER (WHERE voucher_estado = 'Rechazado')   AS VouchersRechazados,
                   count(*) FILTER (WHERE voucher_estado = 'Validado'
                                      AND fecha_validacion = CURRENT_DATE) AS VouchersValidadosHoy,
                   COALESCE(sum(monto) FILTER (WHERE voucher_estado = 'Validado'
                                      AND fecha_validacion = CURRENT_DATE), 0) AS RecaudadoHoy,
                   COALESCE(sum(monto) FILTER (WHERE voucher_estado = 'Validado'), 0) AS RecaudadoPeriodo,
                   count(*) FILTER (WHERE voucher_estado = 'Validado')   AS RecibosValidados
            FROM pagos;
            """.Replace("pagos", t)) ?? new ModeloResumenTesoreria();

        const string porConceptoSql = """
            SELECT cp.codigo AS Codigo,
                   cp.nombre AS Nombre,
                   COALESCE(sum(p.monto), 0) AS Monto,
                   count(p.id) AS Recibos
            FROM conceptos_pago cp
            LEFT JOIN pagos p ON p.concepto_pago_id = cp.id AND p.voucher_estado = 'Validado'
            GROUP BY cp.codigo, cp.nombre
            HAVING count(p.id) > 0
            ORDER BY 3 DESC
            LIMIT 5;
            """;
        dto.PorConcepto = (await db.QueryAsync<ModeloRecaudacionPorConcepto>(
            porConceptoSql.Replace("pagos", t))).ToList();
        return dto;
    }

    // Panel personal del ALUMNO (Resumen del módulo): SUS trámites,
    // pagos y vouchers — cero cifras globales del instituto.
    public async Task<ModeloResumenAlumno> ResumenAlumnoAsync(int estudianteId)
    {
        // Cerrar vencidos primero: los trámites cuyo plazo de corrección expiró
        // dejan de contar como Observados en el panel del alumno.
        await _servicioTramite.CerrarVencidosAsync();
        using var db = CreateConnection();
        var t = "pagos";
        var dto = new ModeloResumenAlumno();

        // Contadores de SUS trámites por estado (mesa de mod09)
        dto = await db.QueryFirstOrDefaultAsync<ModeloResumenAlumno>("""
            SELECT count(*) FILTER (WHERE tr.estado IN ('Recibido', 'En evaluación')) AS TramitesActivos,
                   count(*) FILTER (WHERE tr.estado = 'Observado')                    AS TramitesObservados,
                   count(*) FILTER (WHERE tr.estado IN ('Aprobado', 'Entregado'))     AS TramitesCerrados
            FROM mod09.tramites tr
            WHERE tr.estudiante_id = @Id;
            """, new { Id = estudianteId }) ?? dto;

        // Pagos del alumno: total validado + vouchers pendientes SIN adjunto
        var pagos = await db.QueryFirstOrDefaultAsync<(decimal Total, int PorSubir)>($"""
            SELECT COALESCE(sum(monto) FILTER (WHERE p.voucher_estado = 'Validado'), 0),
                   count(*) FILTER (WHERE p.voucher_estado <> 'Validado'
                       AND NOT EXISTS (SELECT 1 FROM mod09.voucher_archivos va
                                       WHERE va.pago_id = p.id))
            FROM {t} p
            WHERE p.estudiante_id = @Id;
            """, new { Id = estudianteId });
        dto.TotalPagado = pagos.Total;
        dto.VouchersPorSubir = pagos.PorSubir;

        // Últimos trámites con el voucher de su pago vinculado
        const string ultimosSql = """
            SELECT tr.id AS Id,
                   tr.codigo AS Codigo,
                   tt.nombre AS TipoNombre,
                   tr.estado AS Estado,
                   COALESCE(p.voucher_estado, '') AS VoucherEstado,
                   tr.creado_en AS FechaSolicitud
            FROM mod09.tramites tr
            JOIN mod09.tipos_tramite tt ON tt.id = tr.tipo_tramite_id
            LEFT JOIN pagos p ON p.id = tr.pago_id
            WHERE tr.estudiante_id = @Id
            ORDER BY tr.creado_en DESC
            LIMIT 5;
            """;
        dto.Ultimos = (await db.QueryAsync<ModeloTarjetaMiTramite>(
            ultimosSql.Replace("pagos", t), new { Id = estudianteId })).ToList();
        return dto;
    }

    public async Task<IEnumerable<ModeloConceptoPago>> ListarConceptosAsync()
    {
        using var db = CreateConnection();
        return await db.QueryAsync<ModeloConceptoPago>("""
            SELECT id, codigo, nombre, monto FROM conceptos_pago WHERE activo ORDER BY codigo;
            """);
    }

    public async Task<IEnumerable<ModeloTipoPago>> ListarTiposPagoAsync()
    {
        using var db = CreateConnection();
        return await db.QueryAsync<ModeloTipoPago>(
            "SELECT id, codigo, nombre FROM tipos_pago ORDER BY id;");
    }
}
