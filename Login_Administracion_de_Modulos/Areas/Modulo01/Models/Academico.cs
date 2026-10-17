namespace Login_Administracion_de_Modulos.Areas.Modulo01.Models;

/// <summary>DTOs de la pestaña "Académico" (Equipo 01) — formato SIGA.</summary>

/// <summary>Promedio por ciclo (barras del gráfico de rendimiento).</summary>
public class RendimientoCiclo
{
    public string Ciclo { get; set; } = "";
    public decimal Promedio { get; set; }
    public int Cursos { get; set; }
    public int Aprobados { get; set; }
}

/// <summary>Un curso del historial (fila cruda con ciclo y período).</summary>
public class CursoHistorial
{
    public string Ciclo { get; set; } = "";
    public string PeriodoCodigo { get; set; } = "";
    public int PeriodoId { get; set; }
    public int UnidadId { get; set; }
    public string Codigo { get; set; } = "";
    public string UnidadNombre { get; set; } = "";
    public decimal Creditos { get; set; }
    public decimal Nota { get; set; }
    public string Estado { get; set; } = "";
    public DateTime Fecha { get; set; }
}

/// <summary>Un ciclo del record académico (bloque SIGA): sus cursos y totales.</summary>
public class CicloAcademico
{
    public string Ciclo { get; set; } = "";
    public string PeriodoCodigo { get; set; } = "";
    public List<CursoHistorial> Cursos { get; set; } = new();
    public decimal CreditosMatriculados { get; set; }
    public decimal CreditosAprobados { get; set; }
    public decimal PromedioPonderado { get; set; }
}

/// <summary>Cards de resumen de la pestaña Académico.</summary>
public class ResumenAcademico
{
    public decimal? PromedioGeneral { get; set; }
    public int? CursosTotales { get; set; }
    public int? CursosAprobados { get; set; }
    public int? CursosDesaprobados { get; set; }
}

/// <summary>Modelo completo de la vista Academico.</summary>
public class AcademicoVista
{
    public ResumenAcademico Resumen { get; set; } = new();
    public List<RendimientoCiclo> Rendimiento { get; set; } = new();
    public List<CicloAcademico> Record { get; set; } = new();
    public CicloAcademico CicloSeleccionado { get; set; } = new();
    public string EstudianteNombre { get; set; } = "";
}
