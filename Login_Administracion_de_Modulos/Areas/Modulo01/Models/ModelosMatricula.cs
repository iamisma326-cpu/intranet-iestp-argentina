namespace Login_Administracion_de_Modulos.Areas.Modulo01.Models;

/// <summary>Matrícula vigente del alumno (vista "Mi matrícula").</summary>
public class ModeloMatriculaActiva
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Estado { get; set; } = "En trámite";
    public string Etapa { get; set; } = "Validación";
    public bool VoucherOk { get; set; }
    public bool FotoOk { get; set; }
    public string Condicion { get; set; } = "";
    public DateTime? FechaMatricula { get; set; }
    public string? Observaciones { get; set; }
    public DateTime CreadoEn { get; set; }
    public string Carrera { get; set; } = "";
    public string CarreraCodigo { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public string Turno { get; set; } = "";
    public string TipoMatricula { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string CodigoEstudiante { get; set; } = "";

    /// <summary>Progreso del proceso Cero Filas: 1..4 pasos completados.</summary>
    public int PasosCompletados =>
        (Estado == "Matriculado") ? 4
        : (VoucherOk && FotoOk) ? 3
        : (VoucherOk || FotoOk) ? 2
        : 1;

    public string EtapaLegible =>
        Estado == "Matriculado" ? "Proceso completado"
        : (VoucherOk && FotoOk) ? "Listo para conformidad"
        : (VoucherOk || FotoOk) ? "Pendiente " + (FotoOk ? "voucher" : "foto")
        : "Pendiente voucher y foto";
}

/// <summary>Fila de los listados del personal.</summary>
public class ModeloMatriculaLista
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Etapa { get; set; } = "";
    public bool VoucherOk { get; set; }
    public bool FotoOk { get; set; }
    public string Condicion { get; set; } = "";
    public DateTime? FechaMatricula { get; set; }
    public DateTime CreadoEn { get; set; }
    public string CarreraCodigo { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public string Turno { get; set; } = "";
    public string TipoMatricula { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string CodigoEstudiante { get; set; } = "";
    /// <summary>Trámite TM05 de reserva vinculado (evidencia: voucher + adjuntos).</summary>
    public int? TramiteReservaId { get; set; }
}

/// <summary>Zona C: matrículas trancadas que liberan su vacante (Art. 24 RI).</summary>
public class ModeloMatriculaPorLiberar
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public DateTime CreadoEn { get; set; }
    public int DiasTranscurridos { get; set; }
    public string CarreraCodigo { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public string Turno { get; set; } = "";
    public string Estudiante { get; set; } = "";
}

/// <summary>Métricas del módulo.</summary>
public class ModeloResumenMatricula
{
    public int Matriculados { get; set; }
    public int EnTramite { get; set; }
    public int ListasParaRegistrar { get; set; }
    public int EnEspera { get; set; }
    public int Reservadas { get; set; }
}

// Resumen del Módulo 01 por puesto (rol activo)

/// <summary>Panel del puesto de Tesorería: vouchers de reserva (CT13/TM05).</summary>
public class ModeloResumenTesoreriaMatricula
{
    public int VouchersPendientes { get; set; }     // TM05 con pago Pendiente
    public int VouchersValidados { get; set; }      // TM05 con pago Validado
    public int VouchersRechazados { get; set; }      // TM05 con pago Rechazado
    public decimal RecaudadoReservas { get; set; }  // sum CT13 validados
    /// <summary>Reservas con voucher validado pero sin matrícula cerrada aún.</summary>
    public int PagadasSinMatricular { get; set; }
}

/// <summary>Panel del puesto de Secretaría: cierre de matrículas.</summary>
public class ModeloResumenSecretariaMatricula
{
    public int ListasParaCerrar { get; set; }       // voucher Validado + período habilitado, sin cerrar
    public int MatriculadosPeriodo { get; set; }    // ya cerradas del período habilitado
    public int EsperandoVoucher { get; set; }       // TM05 activo con voucher no validado
    public int TramitesActivos { get; set; }        // TM05 en mesa (Recibido/En evaluación)
    /// <summary>Matrículas cerradas por carrera (mini-gráfico).</summary>
    public List<ModeloMatriculasPorCarrera> PorCarrera { get; set; } = [];
}

/// <summary>Fila del desglose de matrículas cerradas por carrera.</summary>
public class ModeloMatriculasPorCarrera
{
    public string Codigo { get; set; } = "";   // DSI, CONT, ADM
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }
}

/// <summary>Tablero de vacantes por carril.</summary>
public class ModeloVacante
{
    public string CarreraCodigo { get; set; } = "";
    public string Carrera { get; set; } = "";
    public string Turno { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public int Disponibles { get; set; }
    public int Ocupadas { get; set; }
}

/// <summary>Resultado del RETURNING al registrar conformidad (para consumir vacante).</summary>
public class ModeloVacanteConsumo
{
    public int CarreraId { get; set; }
    public int CicloId { get; set; }
    public int TurnoId { get; set; }
    public int PeriodoId { get; set; }
}


// Matriculatura de Secretaría (flujo Reserva de Matrícula TM05/CT13)

/// <summary>Expediente del alumno listo para matricular (búsqueda por DNI).</summary>
public class ModeloExpedienteMatricula
{
    public int EstudianteId { get; set; }
    public string CodigoEstudiante { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string Dni { get; set; } = "";
    public string EmailPersonal { get; set; } = "";
    public string EmailInstitucional { get; set; } = "";
    public int CarreraId { get; set; }
    public string Carrera { get; set; } = "";
    public string CarreraCodigo { get; set; } = "";
    public string CicloActual { get; set; } = "";

    // situación académica
    public string CicloCulminado { get; set; } = "";
    public string CicloProximo { get; set; } = "";
    public string Condicion { get; set; } = "";          // Promovido | Promovido con curso a cargo | Repitente
    public int CursosDesaprobados { get; set; }
    public string CursosDesaprobadosNombres { get; set; } = "";
    public List<ModeloHistorialFila> Historial { get; set; } = [];
    public List<ModeloUnidadOferta> OfertaProximoCiclo { get; set; } = [];

    // reserva TUPA y su voucher
    public ModeloReserva? Reserva { get; set; }
    public bool PuedeMatricular { get; set; }
    /// <summary>Id de una matrícula ya cerrada en el período destino (no se abre otra).</summary>
    public int YaMatriculadoId { get; set; }
}

/// <summary>Fila del historial académico (aprobado/desaprobado por UD).</summary>
public class ModeloHistorialFila
{
    public string UnidadCodigo { get; set; } = "";
    public string UnidadNombre { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public decimal Nota { get; set; }
    public string Estado { get; set; } = "";   // Aprobado | Desaprobado | Retirado
}

// Panel del alumno (Resumen del Módulo 01)

/// <summary>Mini-dashboard personal del alumno para la pestaña Resumen.</summary>
public class ModeloPanelAlumno
{
    public string CodigoEstudiante { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string Carrera { get; set; } = "";
    public string CarreraCodigo { get; set; } = "";
    public string CicloActual { get; set; } = "";      // ciclo culminado
    public string CicloProximo { get; set; } = "";
    public string Condicion { get; set; } = "";         // Promovido | ... | Repitente
    public string CursosDesaprobadosNombres { get; set; } = "";
    public decimal Promedio { get; set; }               // ponderado del historial
    public int CreditosAprobados { get; set; }
    public List<ModeloHistorialFila> Historial { get; set; } = [];
    /// <summary>Notas SOLO del último ciclo culminado (las barras del Resumen;
    /// el historial completo vive en Mi académico — aquí satura el panel).</summary>
    public List<ModeloHistorialFila> NotasUltimoCiclo { get; set; } = [];

    /// <summary>Pasos del flujo de reserva de matrícula para el timeline.</summary>
    public List<ModeloPasoFlujo> PasosFlujo { get; set; } = [];
}

/// <summary>Paso del timeline del flujo (trámite → voucher → matrícula → ficha).</summary>
public class ModeloPasoFlujo
{
    public string Titulo { get; set; } = "";
    public string Detalle { get; set; } = "";
    /// <summary>Completado | Actual | Pendiente</summary>
    public string Estado { get; set; } = "Pendiente";
}

/// <summary>Fila interna del estado de la reserva más reciente del alumno.</summary>
public class ReservaAlumnoRow
{
    public int MatriculaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Estado { get; set; } = "";
    public string Voucher { get; set; } = "";
    public string TramiteEstado { get; set; } = "";
    public string TramiteCodigo { get; set; } = "";
}

/// <summary>UD matriculable del próximo ciclo (core o espejo oferta_ciclo).</summary>
public class ModeloUnidadOferta
{
    public int Id { get; set; }
    /// <summary>Origen del Id: "core" (unidades_didacticas) o "espejo" (mod01.oferta_ciclo).</summary>
    public string Origen { get; set; } = "core";
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Creditos { get; set; }
    public string Tipo { get; set; } = "";
    public string Ciclo { get; set; } = "";
    public bool Obligatoria { get; set; } = true;
    /// <summary>Curso a cargo: desaprobado que se vuelve a llevar junto al nuevo ciclo.</summary>
    public bool EsCursoACargo { get; set; }
}

/// <summary>Estado del trámite de reserva y su voucher (para Secretaría).</summary>
public class ModeloReserva
{
    public int MatriculaId { get; set; }
    public string CodigoMatricula { get; set; } = "";
    public string Estado { get; set; } = "";
    public bool VoucherOk { get; set; }
    public int? TramiteId { get; set; }
    public string TramiteCodigo { get; set; } = "";
    public string TramiteEstado { get; set; } = "";
    public string VoucherEstado { get; set; } = "";   // Pendiente | Validado | Rechazado
    public decimal VoucherMonto { get; set; }
    /// <summary>El período de la reserva tiene permite_matricula (lo habilita el dueño).</summary>
    public bool PeriodoHabilitado { get; set; } = true;
}

/// <summary>RETURNING interno al cerrar la matrícula (consumo de vacante).</summary>
public class ModeloMatriculaCierre
{
    public int Id { get; set; }
    public int EstudianteId { get; set; }
    public int CarreraId { get; set; }
    public int CicloId { get; set; }
    public int TurnoId { get; set; }
    public int PeriodoId { get; set; }
}

/// <summary>
/// Datos de la ficha de matrícula PDF (QuestPDF) que se envía al estudiante.
/// </summary>
public class ModeloFichaMatricula
{
    public string CodigoMatricula { get; set; } = "";
    public string Periodo { get; set; } = "";
    public string Estudiante { get; set; } = "";
    public string CodigoEstudiante { get; set; } = "";
    public string Dni { get; set; } = "";
    public string Carrera { get; set; } = "";
    public string CarreraCodigo { get; set; } = "";
    public string Condicion { get; set; } = "";
    public string CursosDesaprobadosNombres { get; set; } = "";
    public string CicloCulminado { get; set; } = "";
    public string CicloProximo { get; set; } = "";
    public string Turno { get; set; } = "";
    public string TipoMatricula { get; set; } = "";
    public DateTime? FechaMatricula { get; set; }
    public string Estado { get; set; } = "";
    public List<ModeloUnidadOferta> Unidades { get; set; } = [];
}
