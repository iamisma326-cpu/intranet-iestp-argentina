using System;
using System.Collections.Generic;
using Npgsql;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Models
{
    public class ActivoHardwareItem
    {
        public int IdBien { get; set; }
        public int IdAlmacen { get; set; }
        public string? Cbi { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public string TipoBien { get; set; } = string.Empty;
        public string? Marca { get; set; }
        public string? Modelo { get; set; }
        public string? Serie { get; set; }
        public string? Procedencia { get; set; }
        public DateTime FechaIngreso { get; set; }
        public string Estado { get; set; } = "Operativo";
        public string? Observacion { get; set; }
        public string? AmbienteAsignado { get; set; }
        public int TotalFallasRegistradas { get; set; }
    }

    public class ActivoHardwareRepository
    {
        private readonly string _connectionString;

        public ActivoHardwareRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ActivoHardwareItem? BuscarPorCbiOSerie(string criterio)
        {
            if (string.IsNullOrWhiteSpace(criterio)) return null;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            const string sql = @"
                SELECT b.id_bien, b.id_almacen, b.cbi, b.descripcion, CAST(b.id_clasificacion AS TEXT), 
                       b.marca, b.modelo, b.serie, b.procedencia, b.fecha_ingreso, b.estado, b.observacion,
                       (SELECT a.nombre FROM mod03.asignacion asg INNER JOIN mod03.ambiente a ON asg.id_ambiente = a.id_ambiente WHERE asg.id_bien = b.id_bien AND asg.estado = 'Activo' LIMIT 1) as ambiente_nombre,
                       (SELECT COUNT(*) FROM mod05.incidencias inc WHERE inc.bien_id = b.id_bien) as total_fallas
                FROM mod03.bien b
                WHERE LOWER(b.cbi) = LOWER(@crit) 
                   OR LOWER(b.serie) = LOWER(@crit)
                   OR CAST(b.id_bien AS TEXT) = @crit
                LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@crit", criterio.Trim());
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ActivoHardwareItem
                {
                    IdBien = reader.GetInt32(0),
                    IdAlmacen = reader.GetInt32(1),
                    Cbi = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    TipoBien = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Marca = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Modelo = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Serie = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Procedencia = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    FechaIngreso = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                    Estado = reader.IsDBNull(10) ? "Operativo" : reader.GetString(10),
                    Observacion = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    AmbienteAsignado = reader.IsDBNull(12) ? "Laboratorio de Cómputo 1" : reader.GetString(12),
                    TotalFallasRegistradas = reader.IsDBNull(13) ? 0 : Convert.ToInt32(reader.GetInt64(13))
                };
            }
            return null;
        }

        public List<ActivoHardwareItem> ListarActivos(string? filtro = null)
        {
            var lista = new List<ActivoHardwareItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            string sql = @"
                SELECT b.id_bien, b.id_almacen, b.cbi, b.descripcion, CAST(b.id_clasificacion AS TEXT), 
                       b.marca, b.modelo, b.serie, b.procedencia, b.fecha_ingreso, b.estado, b.observacion,
                       (SELECT COUNT(*) FROM mod05.incidencias inc WHERE inc.bien_id = b.id_bien) as total_fallas
                FROM mod03.bien b";

            if (!string.IsNullOrWhiteSpace(filtro))
            {
                sql += " WHERE LOWER(b.descripcion) LIKE LOWER(@f) OR LOWER(b.cbi) LIKE LOWER(@f) OR LOWER(b.serie) LIKE LOWER(@f)";
            }
            sql += " ORDER BY b.id_bien DESC LIMIT 100";

            using var cmd = new NpgsqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(filtro))
            {
                cmd.Parameters.AddWithValue("@f", $"%{filtro.Trim()}%");
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new ActivoHardwareItem
                {
                    IdBien = reader.GetInt32(0),
                    IdAlmacen = reader.GetInt32(1),
                    Cbi = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Descripcion = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    TipoBien = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Marca = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Modelo = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Serie = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    Procedencia = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    FechaIngreso = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                    Estado = reader.IsDBNull(10) ? "Operativo" : reader.GetString(10),
                    Observacion = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    TotalFallasRegistradas = reader.IsDBNull(12) ? 0 : Convert.ToInt32(reader.GetInt64(12))
                });
            }
            return lista;
        }
    }
}
