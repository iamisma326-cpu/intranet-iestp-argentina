using Dapper;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo01.Models;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Services;

/// <summary>
/// Pestaña "Académico" del alumno (Equipo 01).
/// Rendimiento por ciclo (barras), record académico por período con promedio
/// ponderado y créditos, siguiendo el formato del SIGA institucional.
/// Solo consulta SU historial (mod01.historial_academico × core.unidades_didacticas).
/// </summary>
public class AcademicoServicio
{
    private readonly IModuleDbConnectionFactory _factory;

    public AcademicoServicio(IModuleDbConnectionFactory factory)
    {
        _factory = factory;
    }

    // (resolución del estudiante movida al controller: PersonaActualId del base,
    //  patrón de MatriculasController — el service solo consulta por estudianteId)

    // ---------------------------------------------------------------
    // 1) Rendimiento por ciclo — barras (promedio vigesimal por ciclo I-VI)
    // ---------------------------------------------------------------
    public async Task<List<RendimientoCiclo>> RendimientoPorCicloAsync(int estudianteId)
    {
        using var connection = _factory.CreateConnection("01");
        const string sql = """
            SELECT ud.ciclo              AS Ciclo,
                   ROUND(AVG(h.nota), 1) AS Promedio,
                   COUNT(*)              AS Cursos,
                   SUM(CASE WHEN h.estado = 'Aprobado' THEN 1 ELSE 0 END) AS Aprobados
            FROM mod01.historial_academico h
            JOIN core.unidades_didacticas ud ON ud.id = h.unidad_didactica_id
            WHERE h.estudiante_id = @EstudianteId
            GROUP BY ud.ciclo
            ORDER BY MIN(ud.id)
            """;
        var filas = await connection.QueryAsync<RendimientoCiclo>(sql, new { EstudianteId = estudianteId });
        return filas.ToList();
    }

    // ---------------------------------------------------------------
    // 2) Record académico por CICLO (tabla del SIGA)
    //    Agrupa el historial por el ciclo de cada unidad didáctica (I–VI):
    //    las pills son los ciclos CON notas del alumno (nunca hay
    //    "sin notas" — un ciclo en curso todavía no tiene notas finales).
    //    Cada bloque muestra el período-año de fondo (p. ej. "Ciclo III · 2026-I").
    //    Si un curso fue re-llevado en otro período, cuenta el intento más
    //    reciente (una fila por unidad didáctica por ciclo).
    // ---------------------------------------------------------------
    public async Task<List<CicloAcademico>> RecordPorCicloAsync(int estudianteId)
    {
        using var connection = _factory.CreateConnection("01");
        const string sql = """
            SELECT ud.ciclo   AS Ciclo,
                   pr.codigo  AS PeriodoCodigo,
                   h.periodo_id AS PeriodoId,
                   ud.id      AS UnidadId,
                   ud.codigo  AS Codigo,
                   ud.nombre  AS UnidadNombre,
                   ud.creditos AS Creditos,
                   h.nota     AS Nota,
                   h.estado   AS Estado,
                   h.creado_en AS Fecha
            FROM mod01.historial_academico h
            JOIN core.unidades_didacticas ud ON ud.id = h.unidad_didactica_id
            JOIN core.periodos_academicos pr ON pr.id = h.periodo_id
            WHERE h.estudiante_id = @EstudianteId
            ORDER BY ud.ciclo, ud.id, h.periodo_id
            """;
        var filas = (await connection.QueryAsync<CursoHistorial>(
            sql, new { EstudianteId = estudianteId })).ToList();

        // Una fila por unidad didáctica (intento más reciente) — evita duplicar
        // un curso re-llevado dentro del mismo ciclo.
        var cursos = filas
            .GroupBy(f => f.UnidadId)
            .Select(g => g.OrderByDescending(x => x.PeriodoId).First())
            .ToList();

        var ordenCiclo = new[] { "I", "II", "III", "IV", "V", "VI" };
        var ciclos = cursos
            .GroupBy(c => c.Ciclo)
            .Select(g =>
            {
                var creditos = g.Sum(c => c.Creditos);
                return new CicloAcademico
                {
                    Ciclo = g.Key,
                    PeriodoCodigo = string.Join(" · ", g.Select(x => x.PeriodoCodigo).Distinct()),
                    Cursos = g.OrderBy(c => c.Codigo).ToList(),
                    CreditosMatriculados = creditos,
                    CreditosAprobados = g.Where(c => c.Estado == "Aprobado").Sum(c => c.Creditos),
                    PromedioPonderado = creditos > 0
                        ? Math.Round(g.Sum(c => c.Nota * c.Creditos) / creditos, 2)
                        : 0
                };
            })
            .OrderBy(b => Array.IndexOf(ordenCiclo, b.Ciclo))
            .ToList();
        return ciclos;
    }

    /// <summary>Resumen académico global del alumno (cards de arriba).</summary>
    public async Task<ResumenAcademico> ResumenAcademicoAsync(int estudianteId)
    {
        using var connection = _factory.CreateConnection("01");
        const string sql = """
            SELECT ROUND(AVG(h.nota), 1) AS PromedioGeneral,
                   COUNT(*) AS CursosTotales,
                   SUM(CASE WHEN h.estado = 'Aprobado' THEN 1 ELSE 0 END) AS CursosAprobados,
                   SUM(CASE WHEN h.estado = 'Desaprobado' THEN 1 ELSE 0 END) AS CursosDesaprobados
            FROM mod01.historial_academico h
            WHERE h.estudiante_id = @EstudianteId
            """;
        var r = await connection.QuerySingleOrDefaultAsync<ResumenAcademico>(sql, new { EstudianteId = estudianteId });
        return r ?? new ResumenAcademico();
    }
}
