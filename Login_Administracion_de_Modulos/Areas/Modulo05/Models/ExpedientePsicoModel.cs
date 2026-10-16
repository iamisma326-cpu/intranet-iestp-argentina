using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Models
{
    /// <summary>
    /// Modelo para el formulario de registro y visualización de sesiones psicológicas.
    /// Mapea con la tabla atenciones_psicologia de PostgreSQL (Módulo 05).
    /// </summary>
    public class SesionPsicologicaItem
    {
        public int IdAtencionPsico { get; set; }

        [Required(ErrorMessage = "El estudiante es obligatorio.")]
        [Display(Name = "ID del Estudiante")]
        public int PersonaId { get; set; }

        [Display(Name = "Psicólogo Responsable")]
        public int PsicologoId { get; set; }

        [Required(ErrorMessage = "La fecha y hora de la sesión es obligatoria.")]
        [Display(Name = "Fecha y Hora")]
        public DateTime FechaHora { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "El motivo de consulta es obligatorio.")]
        [StringLength(250, ErrorMessage = "El motivo de consulta no puede superar los 250 caracteres.")]
        [Display(Name = "Motivo de Consulta y Contexto Académico / Familiar")]
        public string MotivoConsulta { get; set; } = string.Empty;

        [Display(Name = "Diagnóstico Preliminar y Notas Confidenciales de Terapia")]
        public string? ObservacionNotas { get; set; }

        [Required(ErrorMessage = "El estado clínico es obligatorio.")]
        [StringLength(30)]
        [Display(Name = "Estado Clínico")]
        public string EstadoAtencion { get; set; } = "Pendiente";

        // Campos complementarios de la persona/estudiante
        [Display(Name = "DNI")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe contener 8 dígitos numéricos.")]
        public string? NumeroDocumento { get; set; }

        [Display(Name = "Nombres")]
        public string? EstudianteNombres { get; set; }

        [Display(Name = "Apellidos")]
        public string? EstudianteApellidos { get; set; }

        [Display(Name = "Apellidos y Nombres")]
        public string NombreCompleto => $"{EstudianteApellidos} {EstudianteNombres}".Trim();

        [Display(Name = "Carrera / Programa")]
        public string Carrera { get; set; } = "Desarrollo de Sistemas de Información";

        [Display(Name = "Ciclo / Turno")]
        public string CicloTurno { get; set; } = "IV Ciclo / Turno Noche";

        [Display(Name = "Psicólogo / Especialista")]
        public string? PsicologoNombre { get; set; }
    }

    /// <summary>
    /// Modelo ligero para la búsqueda y selección de estudiantes
    /// </summary>
    public class EstudianteItem
    {
        public int PersonaId { get; set; }
        public string NumeroDocumento { get; set; } = string.Empty;
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public string NombreCompleto => $"{Apellidos} {Nombres}".Trim();
        public string Carrera { get; set; } = "Desarrollo de Sistemas de Información";
        public string CicloTurno { get; set; } = "IV Ciclo / Turno Noche";
    }

    /// <summary>
    /// Métricas estadísticas para el panel de Psicología
    /// </summary>
    public class MetricasPsicoItem
    {
        public int SesionesMes { get; set; }
        public int CasosSeguimiento { get; set; }
        public int CierresExitosos { get; set; }
        public int CitasHoy { get; set; }
    }

    /// <summary>
    /// Repositorio de acceso a datos para el Módulo 05 (Psicología y Bienestar)
    /// Conexión directa a PostgreSQL cumpliendo restricciones e integridad referencial.
    /// </summary>
    public class ExpedientePsicoRepository
    {
        private readonly string _connectionString;

        public ExpedientePsicoRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Busca un estudiante en la tabla personas por su número de DNI
        /// </summary>
        public SesionPsicologicaItem? BuscarEstudiantePorDni(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni)) return null;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                SELECT p.persona_id, p.dni, p.nombres, p.apellidos
                FROM core.personas p
                WHERE p.dni = @dni
                LIMIT 1";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dni", dni.Trim());
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new SesionPsicologicaItem
                {
                    PersonaId = reader.GetInt32(0),
                    NumeroDocumento = reader.GetString(1),
                    EstudianteNombres = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    EstudianteApellidos = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Carrera = "Desarrollo de Sistemas de Información",
                    CicloTurno = "IV Ciclo / Turno Noche"
                };
            }
            return null;
        }

        /// <summary>
        /// Obtiene una persona existente por DNI o la crea en personas si es un nuevo estudiante
        /// </summary>
        public int ObtenerOCrearPersona(string dni, string? nombres = null, string? apellidos = null)
        {
            if (string.IsNullOrWhiteSpace(dni)) return 0;
            dni = dni.Trim();

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            // 1. Buscar si ya existe
            using (var cmdBuscar = new NpgsqlCommand("SELECT persona_id FROM core.personas WHERE dni = @dni LIMIT 1", conn))
            {
                cmdBuscar.Parameters.AddWithValue("@dni", dni);
                var res = cmdBuscar.ExecuteScalar();
                if (res != null) return Convert.ToInt32(res);
            }

            // 2. Si no existe, crear registro en tabla personas
            string nomFinal = string.IsNullOrWhiteSpace(nombres) ? "Estudiante" : nombres.Trim();
            string apeFinal = string.IsNullOrWhiteSpace(apellidos) ? "Institucional" : apellidos.Trim();

            const string sqlInsert = @"
                INSERT INTO core.personas (dni, nombres, apellidos)
                VALUES (@dni, @nombres, @apellidos)
                RETURNING persona_id;";

            using var cmdInsert = new NpgsqlCommand(sqlInsert, conn);
            cmdInsert.Parameters.AddWithValue("@dni", dni);
            cmdInsert.Parameters.AddWithValue("@nombres", nomFinal);
            cmdInsert.Parameters.AddWithValue("@apellidos", apeFinal);
            var nuevoId = cmdInsert.ExecuteScalar();
            return nuevoId != null ? Convert.ToInt32(nuevoId) : 1;
        }

        /// <summary>
        /// Obtiene un ID de psicólogo válido garantizando integridad referencial con usuarios(id_usuario)
        /// </summary>
        public int ObtenerIdPsicologoValido(int? sugeridoId = null)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            if (sugeridoId.HasValue && sugeridoId.Value > 0)
            {
                using var cmdCheck = new NpgsqlCommand("SELECT id FROM core.usuarios WHERE id = @id AND estado = true LIMIT 1", conn);
                cmdCheck.Parameters.AddWithValue("@id", sugeridoId.Value);
                var res = cmdCheck.ExecuteScalar();
                if (res != null) return Convert.ToInt32(res);
            }

            // Buscar usuario con rol de Psicología
            const string sqlPsico = @"
                SELECT u.id 
                FROM core.usuarios u
                INNER JOIN core.usuario_roles ur ON u.id = ur.usuario_id
                INNER JOIN core.roles r ON ur.rol_id = r.id
                WHERE r.nombre ILIKE '%Psicolog%' AND u.estado = true
                LIMIT 1";
            using var cmdPsico = new NpgsqlCommand(sqlPsico, conn);
            var idPsico = cmdPsico.ExecuteScalar();
            if (idPsico != null) return Convert.ToInt32(idPsico);

            // Fallback a cualquier usuario activo para evitar violación de FK
            using var cmdFallback = new NpgsqlCommand("SELECT id FROM core.usuarios WHERE estado = true LIMIT 1", conn);
            var idFallback = cmdFallback.ExecuteScalar();
            return idFallback != null ? Convert.ToInt32(idFallback) : 1;
        }

        /// <summary>
        /// Inserta una nueva sesión en atenciones_psicologia validando el CHECK de estado_atencion
        /// </summary>
        public bool RegistrarSesion(SesionPsicologicaItem item, out int idGenerado)
        {
            idGenerado = 0;
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            int psicologoId = ObtenerIdPsicologoValido(item.PsicologoId);

            // Sanear estado clínico para cumplir estrictamente con CHECK (estado_atencion IN ('Pendiente','En proceso','Dado de Alta','Derivado externamente'))
            string estadoValidado = item.EstadoAtencion switch
            {
                "En proceso" or "En Proceso" => "En proceso",
                "Dado de Alta" or "Alta" => "Dado de Alta",
                "Derivado externamente" or "Derivado" => "Derivado externamente",
                _ => "Pendiente"
            };

            const string sql = @"
                INSERT INTO mod05.atenciones_psicologia 
                (persona_id, psicologo_id, fecha_hora, motivo_consulta, observacion_notas, estado_atencion)
                VALUES (@persona_id, @psicologo_id, @fecha_hora, @motivo, @notas, @estado)
                RETURNING id;";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@persona_id", item.PersonaId);
            cmd.Parameters.AddWithValue("@psicologo_id", psicologoId);
            cmd.Parameters.AddWithValue("@fecha_hora", item.FechaHora);
            cmd.Parameters.AddWithValue("@motivo", item.MotivoConsulta.Length > 250 ? item.MotivoConsulta.Substring(0, 250) : item.MotivoConsulta);
            cmd.Parameters.AddWithValue("@notas", string.IsNullOrWhiteSpace(item.ObservacionNotas) ? (object)DBNull.Value : item.ObservacionNotas);
            cmd.Parameters.AddWithValue("@estado", estadoValidado);

            var res = cmd.ExecuteScalar();
            if (res != null)
            {
                idGenerado = Convert.ToInt32(res);
                return true;
            }
            return false;
        }

        public bool RegistrarSesion(SesionPsicologicaItem item)
        {
            return RegistrarSesion(item, out _);
        }

        /// <summary>
        /// Actualiza el estado de una atención existente
        /// </summary>
        public bool ActualizarEstadoAtencion(int idAtencion, string nuevoEstado)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            string estadoValidado = nuevoEstado switch
            {
                "En proceso" or "En Proceso" => "En proceso",
                "Dado de Alta" or "Alta" => "Dado de Alta",
                "Derivado externamente" or "Derivado" => "Derivado externamente",
                _ => "Pendiente"
            };

            const string sql = @"UPDATE atenciones_psicologia SET estado_atencion = @estado WHERE id_atencion_psico = @id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@estado", estadoValidado);
            cmd.Parameters.AddWithValue("@id", idAtencion);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// Elimina o cancela una atención registrada
        /// </summary>
        public bool EliminarAtencion(int idAtencion)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            using var cmd = new NpgsqlCommand("DELETE FROM atenciones_psicologia WHERE id_atencion_psico = @id", conn);
            cmd.Parameters.AddWithValue("@id", idAtencion);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// Registra una nueva cita o atención desde el modal de agenda
        /// </summary>
        public bool AgendarCita(string dni, string nombres, string motivo, DateTime fechaHora, string estado, string? modalidad, out int idGenerado)
        {
            idGenerado = 0;
            if (string.IsNullOrWhiteSpace(dni)) return false;

            int personaId = ObtenerOCrearPersona(dni, nombres);
            string? notasModalidad = string.IsNullOrWhiteSpace(modalidad) ? null : $"Modalidad: {modalidad.Trim()}";

            var sesion = new SesionPsicologicaItem
            {
                PersonaId = personaId,
                FechaHora = fechaHora,
                MotivoConsulta = string.IsNullOrWhiteSpace(motivo) ? "Cita de orientación psicológica" : motivo.Trim(),
                ObservacionNotas = notasModalidad,
                EstadoAtencion = estado
            };

            return RegistrarSesion(sesion, out idGenerado);
        }

        /// <summary>
        /// Consulta el expediente histórico de atenciones de un alumno por su DNI
        /// </summary>
        public List<SesionPsicologicaItem> ObtenerExpedientePorDni(string dni)
        {
            var lista = new List<SesionPsicologicaItem>();
            if (string.IsNullOrWhiteSpace(dni)) return lista;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                SELECT a.id, a.persona_id, a.psicologo_id, a.fecha_hora, 
                       a.motivo_consulta, a.observacion_notas, a.estado_atencion,
                       p.dni, p.nombres, p.apellidos,
                       COALESCE(ps_p.nombres || ' ' || ps_p.apellidos, 'Psicólogo Responsable') as psicologo_nombre
                FROM mod05.atenciones_psicologia a
                INNER JOIN core.personas p ON a.persona_id = p.persona_id
                LEFT JOIN core.usuarios u ON a.psicologo_id = u.id
                LEFT JOIN core.personas ps_p ON u.persona_id = ps_p.persona_id
                WHERE p.dni = @dni
                ORDER BY a.fecha_hora DESC";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@dni", dni.Trim());
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new SesionPsicologicaItem
                {
                    IdAtencionPsico = reader.GetInt32(0),
                    PersonaId = reader.GetInt32(1),
                    PsicologoId = reader.GetInt32(2),
                    FechaHora = reader.GetDateTime(3),
                    MotivoConsulta = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ObservacionNotas = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EstadoAtencion = reader.IsDBNull(6) ? "Pendiente" : reader.GetString(6),
                    NumeroDocumento = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    EstudianteNombres = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    EstudianteApellidos = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    PsicologoNombre = reader.IsDBNull(10) ? "Lic. Juan Psicólogo" : reader.GetString(10),
                    Carrera = "Desarrollo de Sistemas de Información",
                    CicloTurno = "IV Ciclo / Turno Noche"
                });
            }
            return lista;
        }

        /// <summary>
        /// Obtiene todas las atenciones para la Agenda e Historial del Panel
        /// </summary>
        public List<SesionPsicologicaItem> ObtenerTodasLasAtenciones(int limite = 100)
        {
            var lista = new List<SesionPsicologicaItem>();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                SELECT a.id, a.persona_id, a.psicologo_id, a.fecha_hora, 
                       a.motivo_consulta, a.observacion_notas, a.estado_atencion,
                       p.dni, p.nombres, p.apellidos,
                       COALESCE(ps_p.nombres || ' ' || ps_p.apellidos, 'Lic. Juan Psicólogo') as psicologo_nombre
                FROM mod05.atenciones_psicologia a
                INNER JOIN core.personas p ON a.persona_id = p.persona_id
                LEFT JOIN core.usuarios u ON a.psicologo_id = u.id
                LEFT JOIN core.personas ps_p ON u.persona_id = ps_p.persona_id
                ORDER BY a.fecha_hora DESC
                LIMIT @limite";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@limite", limite);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new SesionPsicologicaItem
                {
                    IdAtencionPsico = reader.GetInt32(0),
                    PersonaId = reader.GetInt32(1),
                    PsicologoId = reader.GetInt32(2),
                    FechaHora = reader.GetDateTime(3),
                    MotivoConsulta = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ObservacionNotas = reader.IsDBNull(5) ? null : reader.GetString(5),
                    EstadoAtencion = reader.IsDBNull(6) ? "Pendiente" : reader.GetString(6),
                    NumeroDocumento = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    EstudianteNombres = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    EstudianteApellidos = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    PsicologoNombre = reader.IsDBNull(10) ? "Lic. Juan Psicólogo" : reader.GetString(10),
                    Carrera = "Desarrollo de Sistemas de Información",
                    CicloTurno = "IV Ciclo / Turno Noche"
                });
            }
            return lista;
        }

        /// <summary>
        /// Obtiene métricas estadísticas reales calculadas desde la base de datos
        /// </summary>
        public MetricasPsicoItem ObtenerMetricas()
        {
            var metricas = new MetricasPsicoItem();
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            const string sql = @"
                SELECT 
                    COUNT(*) FILTER (WHERE DATE_TRUNC('month', fecha_hora) = DATE_TRUNC('month', CURRENT_DATE)) as sesiones_mes,
                    COUNT(*) FILTER (WHERE estado_atencion = 'En proceso') as en_proceso,
                    COUNT(*) FILTER (WHERE estado_atencion = 'Dado de Alta') as altas,
                    COUNT(*) FILTER (WHERE fecha_hora::date = CURRENT_DATE) as citas_hoy
                FROM atenciones_psicologia;";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                metricas.SesionesMes = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0));
                metricas.CasosSeguimiento = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetInt64(1));
                metricas.CierresExitosos = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetInt64(2));
                metricas.CitasHoy = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetInt64(3));
            }
            return metricas;
        }
    }
}
