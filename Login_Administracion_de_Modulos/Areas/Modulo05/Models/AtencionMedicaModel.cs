using System;
using System.Collections.Generic;
using Npgsql;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Models
{
    public class AtencionMedicaItem
    {
        public int IdAtencionTop { get; set; }
        public int PersonaId { get; set; }
        public int AtendidoPorId { get; set; }
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Motivo { get; set; } = string.Empty;
        public int? MedicamentoId { get; set; }
        public bool EsAlergico { get; set; }
        public string? DetalleAlergia { get; set; }
        public string? SeguroSalud { get; set; }
        public string? Observacion { get; set; }

        // Datos complementarios de persona y medicamento
        public string? NumeroDocumento { get; set; }
        public string? PacienteNombres { get; set; }
        public string? PacienteApellidos { get; set; }
        public string? MedicamentoNombre { get; set; }
    }

    public class MedicamentoItem
    {
        public int IdMedicamento { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Presentacion { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string? Estado { get; set; }
    }

    public class PacienteBusquedaDto
    {
        public int IdPersona { get; set; }
        public string NumeroDocumento { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
    }

    public class AtencionMedicaRepository
    {
        private readonly string _connectionString;

        public AtencionMedicaRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public PacienteBusquedaDto? BuscarPacientePorDni(string dni)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"SELECT persona_id, dni, nombres, apellidos 
                                 FROM core.personas 
                                 WHERE dni = @dni LIMIT 1";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dni", dni.Trim());
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new PacienteBusquedaDto
                {
                    IdPersona = reader.GetInt32(0),
                    NumeroDocumento = reader.GetString(1),
                    Nombres = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Apellidos = reader.IsDBNull(3) ? "" : reader.GetString(3)
                };
            }
            return null;
        }

        public bool RegistrarAtencion(AtencionMedicaItem item)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                INSERT INTO mod05.atenciones_topico 
                (persona_id, atendido_por, fecha_hora, motivo, medicamento_id, es_alergico, detalle_alergia, seguro_salud, observacion)
                VALUES (@persona_id, @atendido_por, @fecha_hora, @motivo, @medicamento_id, @es_alergico, @detalle_alergia, @seguro_salud, @observacion)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@persona_id", item.PersonaId);
            cmd.Parameters.AddWithValue("@atendido_por", item.AtendidoPorId > 0 ? item.AtendidoPorId : 1);
            cmd.Parameters.AddWithValue("@fecha_hora", item.FechaHora);
            cmd.Parameters.AddWithValue("@motivo", item.Motivo ?? "");
            cmd.Parameters.AddWithValue("@medicamento_id", (object?)item.MedicamentoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@es_alergico", item.EsAlergico);
            cmd.Parameters.AddWithValue("@detalle_alergia", (object?)item.DetalleAlergia ?? DBNull.Value);
            string seguroVal = "NINGUNO";
            if (!string.IsNullOrWhiteSpace(item.SeguroSalud))
            {
                string s = item.SeguroSalud.Trim().ToUpperInvariant();
                if (s.Contains("SIS")) seguroVal = "SIS";
                else if (s.Contains("ESSALUD")) seguroVal = "ESSALUD";
                else if (s.Contains("PRIVADO") || s.Contains("EPS")) seguroVal = "EPS";
                else seguroVal = "NINGUNO";
            }
            cmd.Parameters.AddWithValue("@seguro_salud", seguroVal);
            cmd.Parameters.AddWithValue("@observacion", (object?)item.Observacion ?? DBNull.Value);

            int rows = cmd.ExecuteNonQuery();

            // Si se recetó medicamento, descontar stock en botica
            if (item.MedicamentoId.HasValue && item.MedicamentoId.Value > 0)
            {
                const string sqlStock = "UPDATE botica_medicamentos SET stock_actual = GREATEST(stock_actual - 1, 0) WHERE id_medicamento = @id";
                using var cmdStock = new NpgsqlCommand(sqlStock, conn);
                cmdStock.Parameters.AddWithValue("@id", item.MedicamentoId.Value);
                cmdStock.ExecuteNonQuery();
            }

            return rows > 0;
        }

        public List<AtencionMedicaItem> ObtenerHistorialPorDni(string dni)
        {
            var lista = new List<AtencionMedicaItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                SELECT a.id, a.persona_id, a.atendido_por, a.fecha_hora, a.motivo, 
                       a.medicamento_id, a.es_alergico, a.detalle_alergia, a.seguro_salud, a.observacion,
                       p.dni, p.nombres, p.apellidos, m.nombre AS medicamento_nombre
                FROM atenciones_topico a
                INNER JOIN core.personas p ON a.persona_id = p.persona_id
                LEFT JOIN botica_medicamentos m ON a.medicamento_id = m.id_medicamento
                WHERE p.dni = @dni
                ORDER BY a.fecha_hora DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dni", dni.Trim());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new AtencionMedicaItem
                {
                    IdAtencionTop = reader.GetInt32(0),
                    PersonaId = reader.GetInt32(1),
                    AtendidoPorId = reader.GetInt32(2),
                    FechaHora = reader.GetDateTime(3),
                    Motivo = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    MedicamentoId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    EsAlergico = reader.GetBoolean(6),
                    DetalleAlergia = reader.IsDBNull(7) ? null : reader.GetString(7),
                    SeguroSalud = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Observacion = reader.IsDBNull(9) ? null : reader.GetString(9),
                    NumeroDocumento = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    PacienteNombres = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    PacienteApellidos = reader.IsDBNull(12) ? "" : reader.GetString(12),
                    MedicamentoNombre = reader.IsDBNull(13) ? null : reader.GetString(13)
                });
            }
            return lista;
        }

        public List<MedicamentoItem> ListarMedicamentos()
        {
            var medicamentos = new List<MedicamentoItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = "SELECT id_medicamento, nombre, presentacion, stock_actual, fecha_vencimiento, estado FROM botica_medicamentos WHERE estado = 'Activo' OR estado IS NULL ORDER BY nombre ASC";
            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                medicamentos.Add(new MedicamentoItem
                {
                    IdMedicamento = reader.GetInt32(0),
                    Nombre = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Presentacion = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    StockActual = reader.GetInt32(3),
                    FechaVencimiento = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4),
                    Estado = reader.IsDBNull(5) ? "Activo" : reader.GetString(5)
                });
            }
            return medicamentos;
        }
    }
}
