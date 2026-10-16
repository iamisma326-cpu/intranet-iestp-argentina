using System;
using System.Collections.Generic;
using Npgsql;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Models
{
    public class TicketAveriaItem
    {
        public int IdIncidencia { get; set; }
        public int IdBien { get; set; }
        public int ReportadorId { get; set; }
        public int? TecnicoId { get; set; }
        public DateTime FechaReporte { get; set; } = DateTime.Now;
        public DateTime? FechaCierre { get; set; }
        public string TipoFalla { get; set; } = string.Empty;
        public string Gravedad { get; set; } = "Media";
        public string Descripcion { get; set; } = string.Empty;
        public string? SolucionAplicada { get; set; }
        public string Estado { get; set; } = "Pendiente";

        // Información relacional del bien y ambiente
        public string? CodigoBien { get; set; }
        public string? DescripcionBien { get; set; }
        public string? MarcaModelo { get; set; }
        public string? Ambiente { get; set; }
        public string? NombreReportador { get; set; }
        public string? NombreTecnico { get; set; }
    }

    public class TicketAveriaRepository
    {
        private readonly string _connectionString;

        public TicketAveriaRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool CrearReporte(TicketAveriaItem item)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                INSERT INTO incidencias 
                (id_bien, reportador_id, fecha_reporte, tipo_falla, gravedad, descripcion, estado)
                VALUES (@id_bien, @reportador_id, @fecha_reporte, @tipo_falla, @gravedad, @descripcion, @estado)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id_bien", item.IdBien > 0 ? item.IdBien : 1);
            cmd.Parameters.AddWithValue("@reportador_id", item.ReportadorId > 0 ? item.ReportadorId : 1);
            cmd.Parameters.AddWithValue("@fecha_reporte", item.FechaReporte);
            cmd.Parameters.AddWithValue("@tipo_falla", item.TipoFalla ?? "Falla General");
            cmd.Parameters.AddWithValue("@gravedad", item.Gravedad ?? "Media");
            cmd.Parameters.AddWithValue("@descripcion", item.Descripcion ?? "");
            cmd.Parameters.AddWithValue("@estado", string.IsNullOrEmpty(item.Estado) ? "Pendiente" : item.Estado);

            return cmd.ExecuteNonQuery() > 0;
        }

        public List<TicketAveriaItem> ObtenerReportesPorDocente(int docenteId)
        {
            var lista = new List<TicketAveriaItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                SELECT i.id_incidencia, i.id_bien, i.reportador_id, i.tecnico_id, i.fecha_reporte, 
                       i.fecha_cierre, i.tipo_falla, i.gravedad, i.descripcion, i.solucion_aplicada, i.estado,
                       b.cbi, b.descripcion AS bien_desc, b.marca, b.modelo
                FROM incidencias i
                LEFT JOIN bien b ON i.id_bien = b.id_bien
                WHERE i.reportador_id = @docenteId OR @docenteId = 0
                ORDER BY i.fecha_reporte DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@docenteId", docenteId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new TicketAveriaItem
                {
                    IdIncidencia = reader.GetInt32(0),
                    IdBien = reader.GetInt32(1),
                    ReportadorId = reader.GetInt32(2),
                    TecnicoId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    FechaReporte = reader.GetDateTime(4),
                    FechaCierre = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    TipoFalla = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Gravedad = reader.IsDBNull(7) ? "Media" : reader.GetString(7),
                    Descripcion = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SolucionAplicada = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Estado = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10),
                    CodigoBien = reader.IsDBNull(11) ? "S/C" : reader.GetString(11),
                    DescripcionBien = reader.IsDBNull(12) ? "Equipo General" : reader.GetString(12),
                    MarcaModelo = $"{(reader.IsDBNull(13) ? "" : reader.GetString(13))} {(reader.IsDBNull(14) ? "" : reader.GetString(14))}".Trim()
                });
            }
            return lista;
        }

        public List<TicketAveriaItem> ObtenerTodosTickets(string? filtroEstado = null)
        {
            var lista = new List<TicketAveriaItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            string sql = @"
                SELECT i.id_incidencia, i.id_bien, i.reportador_id, i.tecnico_id, i.fecha_reporte, 
                       i.fecha_cierre, i.tipo_falla, i.gravedad, i.descripcion, i.solucion_aplicada, i.estado,
                       b.cbi, b.descripcion AS bien_desc, b.marca, b.modelo
                FROM incidencias i
                LEFT JOIN bien b ON i.id_bien = b.id_bien";

            if (!string.IsNullOrEmpty(filtroEstado))
            {
                sql += " WHERE i.estado = @estado";
            }
            sql += " ORDER BY i.fecha_reporte DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(filtroEstado))
            {
                cmd.Parameters.AddWithValue("@estado", filtroEstado);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new TicketAveriaItem
                {
                    IdIncidencia = reader.GetInt32(0),
                    IdBien = reader.GetInt32(1),
                    ReportadorId = reader.GetInt32(2),
                    TecnicoId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    FechaReporte = reader.GetDateTime(4),
                    FechaCierre = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    TipoFalla = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Gravedad = reader.IsDBNull(7) ? "Media" : reader.GetString(7),
                    Descripcion = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SolucionAplicada = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Estado = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10),
                    CodigoBien = reader.IsDBNull(11) ? "S/C" : reader.GetString(11),
                    DescripcionBien = reader.IsDBNull(12) ? "Equipo General" : reader.GetString(12),
                    MarcaModelo = $"{(reader.IsDBNull(13) ? "" : reader.GetString(13))} {(reader.IsDBNull(14) ? "" : reader.GetString(14))}".Trim()
                });
            }
            return lista;
        }

        public TicketAveriaItem? ObtenerTicketPorId(int idIncidencia)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                SELECT i.id_incidencia, i.id_bien, i.reportador_id, i.tecnico_id, i.fecha_reporte, 
                       i.fecha_cierre, i.tipo_falla, i.gravedad, i.descripcion, i.solucion_aplicada, i.estado,
                       b.cbi, b.descripcion AS bien_desc, b.marca, b.modelo
                FROM incidencias i
                LEFT JOIN bien b ON i.id_bien = b.id_bien
                WHERE i.id_incidencia = @id LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idIncidencia);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new TicketAveriaItem
                {
                    IdIncidencia = reader.GetInt32(0),
                    IdBien = reader.GetInt32(1),
                    ReportadorId = reader.GetInt32(2),
                    TecnicoId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    FechaReporte = reader.GetDateTime(4),
                    FechaCierre = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    TipoFalla = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Gravedad = reader.IsDBNull(7) ? "Media" : reader.GetString(7),
                    Descripcion = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SolucionAplicada = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Estado = reader.IsDBNull(10) ? "Pendiente" : reader.GetString(10),
                    CodigoBien = reader.IsDBNull(11) ? "S/C" : reader.GetString(11),
                    DescripcionBien = reader.IsDBNull(12) ? "Equipo General" : reader.GetString(12),
                    MarcaModelo = $"{(reader.IsDBNull(13) ? "" : reader.GetString(13))} {(reader.IsDBNull(14) ? "" : reader.GetString(14))}".Trim()
                };
            }
            return null;
        }

        public bool ActualizarEstadoTicket(int idIncidencia, string estado, string? solucionAplicada, int tecnicoId)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                UPDATE incidencias 
                SET estado = @estado, 
                    solucion_aplicada = @solucion, 
                    tecnico_id = @tecnico_id,
                    fecha_cierre = CASE WHEN @estado IN ('Reparado', 'Cerrado', 'Resuelto') THEN NOW() ELSE fecha_cierre END
                WHERE id_incidencia = @id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", idIncidencia);
            cmd.Parameters.AddWithValue("@estado", estado);
            cmd.Parameters.AddWithValue("@solucion", (object?)solucionAplicada ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tecnico_id", tecnicoId > 0 ? tecnicoId : 1);

            return cmd.ExecuteNonQuery() > 0;
        }
    }
}
