using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using Login_Administracion_de_Modulos.Areas.Modulo05.Models;
using Login_Administracion_de_Modulos.Seguridad;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Controller
{
    [Area("Modulo05")]
    [Route("Modulo05/[controller]")]
    [ProtegerPanel("Psicología", "Psicologia")]
    public class PsicologiaController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ExpedientePsicoRepository _repo;

        public PsicologiaController(IConfiguration configuration)
        {
            _configuration = configuration;
            string conn = ConfiguracionSegura.ObtenerCadenaModulo(_configuration, "05");
            _repo = new ExpedientePsicoRepository(conn);
        }

        // GET: /Modulo05/Psicologia o /Modulo05/Psicologia/Panel
        [HttpGet("")]
        [HttpGet("Panel")]
        public IActionResult Panel()
        {
            return Redirect("/Dashboard/Psicologia");
        }

        // GET: /Modulo05/Psicologia/FormSesion o /Modulo05/Psicologia/Create
        [HttpGet("Create")]
        [HttpGet("FormSesion")]
        public IActionResult FormSesion(string? dni = null, bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            ViewData["Title"] = "Registro de Sesión Psicológica";
            ViewData["Role"] = "Psicología";
            ViewBag.DniInicial = dni;

            if (partial || isAjax)
            {
                return PartialView("~/Areas/Modulo05/Views/psicologia/form_sesion.cshtml");
            }

            return View("~/Areas/Modulo05/Views/psicologia/form_sesion.cshtml");
        }

        // GET: /Modulo05/Psicologia/ExpedienteAlumno
        [HttpGet("ExpedienteAlumno")]
        public IActionResult ExpedienteAlumno(string? dni = null, bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            ViewData["Title"] = "Expediente Confidencial del Alumno";
            ViewData["Role"] = "Bienestar Estudiantil";
            var historial = !string.IsNullOrWhiteSpace(dni) 
                ? _repo.ObtenerExpedientePorDni(dni) 
                : new List<SesionPsicologicaItem>();

            ViewBag.DniBuscado = dni;
            if (partial || isAjax)
            {
                return PartialView("~/Areas/Modulo05/Views/psicologia/expediente_alumno.cshtml", historial);
            }
            return View("~/Areas/Modulo05/Views/psicologia/expediente_alumno.cshtml", historial);
        }

        // GET: /Modulo05/Psicologia/BuscarPaciente?dni=...
        [HttpGet("BuscarPaciente")]
        public IActionResult BuscarPaciente(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return Json(new { success = false, message = "Por favor ingrese un número de DNI válido." });
            }

            try
            {
                var alumno = _repo.BuscarEstudiantePorDni(dni.Trim());
                if (alumno != null)
                {
                    return Json(new
                    {
                        success = true,
                        personaId = alumno.PersonaId,
                        dni = alumno.NumeroDocumento,
                        nombres = alumno.EstudianteNombres,
                        apellidos = alumno.EstudianteApellidos,
                        nombreCompleto = alumno.NombreCompleto,
                        carrera = alumno.Carrera,
                        cicloTurno = alumno.CicloTurno
                    });
                }

                return Json(new
                {
                    success = false,
                    message = $"No se encontró registro para el DNI {dni} en la base de datos."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al consultar estudiante: " + ex.Message });
            }
        }

        // POST: /Modulo05/Psicologia/GuardarAtencion o /Modulo05/Psicologia/GuardarSesion
        [HttpPost("GuardarAtencion")]
        [HttpPost("GuardarSesion")]
        public IActionResult GuardarAtencion([FromForm] IFormCollection form)
        {
            try
            {
                string? dni = form["dniBusqueda"].ToString();
                if (string.IsNullOrWhiteSpace(dni)) dni = form["dni"].ToString();

                string? motivoConsulta = form["motivoConsulta"].ToString();
                if (string.IsNullOrWhiteSpace(motivoConsulta)) motivoConsulta = form["motivo"].ToString();

                string? fechaHoraStr = form["fechaHora"].ToString();
                string? estadoClinico = form["estadoClinico"].ToString();
                if (string.IsNullOrWhiteSpace(estadoClinico)) estadoClinico = form["estadoAtencion"].ToString();
                if (string.IsNullOrWhiteSpace(estadoClinico)) estadoClinico = form["estado"].ToString();

                string? diagnosticoNotas = form["diagnosticoNotas"].ToString();
                if (string.IsNullOrWhiteSpace(diagnosticoNotas)) diagnosticoNotas = form["observacionNotas"].ToString();
                if (string.IsNullOrWhiteSpace(diagnosticoNotas)) diagnosticoNotas = form["notas"].ToString();

                string? personaIdStr = form["personaId"].ToString();

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new { success = false, message = "Debe buscar y seleccionar el estudiante mediante su DNI." });
                }

                if (string.IsNullOrWhiteSpace(motivoConsulta))
                {
                    return Json(new { success = false, message = "El motivo de consulta y contexto es obligatorio." });
                }

                // Obtener o verificar la persona en la base de datos
                var alumno = _repo.BuscarEstudiantePorDni(dni);
                int personaId = 0;
                if (alumno != null)
                {
                    personaId = alumno.PersonaId;
                }
                else
                {
                    string nomForm = form["apellidosNombres"].ToString();
                    personaId = _repo.ObtenerOCrearPersona(dni, nomForm);
                }

                if (personaId <= 0)
                {
                    return Json(new { success = false, message = "No se pudo identificar o crear la ficha del estudiante en la base de datos." });
                }

                DateTime fechaHora = DateTime.Now;
                if (!string.IsNullOrEmpty(fechaHoraStr) && DateTime.TryParse(fechaHoraStr, out DateTime parsedDate))
                {
                    fechaHora = parsedDate;
                }

                // Identificar psicólogo logueado desde la sesión HTTP
                int? sesionUsuarioId = User.FindFirst("UsuarioId")?.Value is string uid2 ? int.Parse(uid2) : (int?)null;

                var sesion = new SesionPsicologicaItem
                {
                    PersonaId = personaId,
                    PsicologoId = sesionUsuarioId.HasValue ? sesionUsuarioId.Value : 0,
                    FechaHora = fechaHora,
                    MotivoConsulta = motivoConsulta.Trim(),
                    ObservacionNotas = string.IsNullOrWhiteSpace(diagnosticoNotas) ? null : diagnosticoNotas.Trim(),
                    EstadoAtencion = string.IsNullOrWhiteSpace(estadoClinico) ? "Pendiente" : estadoClinico.Trim()
                };

                bool ok = _repo.RegistrarSesion(sesion);
                if (ok)
                {
                    return Json(new
                    {
                        success = true,
                        message = "¡Sesión psicológica registrada exitosamente en la base de datos!"
                    });
                }

                return Json(new { success = false, message = "No se pudo registrar la sesión en la base de datos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al procesar el registro: " + ex.Message });
            }
        }

        // GET: /Modulo05/Psicologia/Historial?dni=...
        [HttpGet("Historial")]
        public IActionResult Historial(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return Json(new { success = false, message = "DNI no proporcionado." });
            }

            try
            {
                var sesiones = _repo.ObtenerExpedientePorDni(dni);
                var lista = new List<object>();
                foreach (var s in sesiones)
                {
                    lista.Add(new
                    {
                        idAtencion = s.IdAtencionPsico,
                        fechaHora = s.FechaHora.ToString("dd/MM/yyyy HH:mm"),
                        motivo = s.MotivoConsulta,
                        estado = s.EstadoAtencion ?? "Pendiente",
                        notas = s.ObservacionNotas ?? ""
                    });
                }
                return Json(new { success = true, data = lista });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al consultar historial: " + ex.Message });
            }
        }

        // GET: /Modulo05/Psicologia/ListarTodas
        [HttpGet("ListarTodas")]
        public IActionResult ListarTodas(int limite = 100)
        {
            try
            {
                var atenciones = _repo.ObtenerTodasLasAtenciones(limite);
                var lista = new List<object>();
                foreach (var a in atenciones)
                {
                    lista.Add(new
                    {
                        id = a.IdAtencionPsico,
                        fecha = a.FechaHora.ToString("yyyy-MM-dd"),
                        hora = a.FechaHora.ToString("hh:mm tt"),
                        fechaHoraRaw = a.FechaHora.ToString("s"),
                        estudiante = a.NombreCompleto,
                        dni = a.NumeroDocumento ?? "",
                        carrera = a.Carrera,
                        ciclo = a.CicloTurno,
                        motivo = a.MotivoConsulta,
                        diagnostico = a.ObservacionNotas ?? "",
                        estado = a.EstadoAtencion ?? "Pendiente",
                        psicologo = a.PsicologoNombre ?? "Lic. Juan Psicólogo",
                        modalidad = (a.ObservacionNotas != null && a.ObservacionNotas.Contains("Modalidad: Virtual")) ? "Virtual" : "Presencial (Consultorio 02)"
                    });
                }

                return Json(new { success = true, data = lista });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al listar atenciones: " + ex.Message });
            }
        }

        // GET: /Modulo05/Psicologia/Metricas
        [HttpGet("Metricas")]
        public IActionResult Metricas()
        {
            try
            {
                var m = _repo.ObtenerMetricas();
                return Json(new
                {
                    success = true,
                    sesionesMes = m.SesionesMes,
                    casosSeguimiento = m.CasosSeguimiento,
                    cierresExitosos = m.CierresExitosos,
                    citasHoy = m.CitasHoy
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al calcular métricas: " + ex.Message });
            }
        }

        // POST: /Modulo05/Psicologia/ActualizarEstado
        [HttpPost("ActualizarEstado")]
        public IActionResult ActualizarEstado([FromForm] int idAtencion, [FromForm] string nuevoEstado)
        {
            try
            {
                if (idAtencion <= 0 || string.IsNullOrWhiteSpace(nuevoEstado))
                {
                    return Json(new { success = false, message = "Parámetros inválidos." });
                }

                bool ok = _repo.ActualizarEstadoAtencion(idAtencion, nuevoEstado);
                return Json(new { success = ok, message = ok ? "Estado actualizado exitosamente." : "No se pudo actualizar." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar estado: " + ex.Message });
            }
        }

        // POST: /Modulo05/Psicologia/AgendarCita
        [HttpPost("AgendarCita")]
        public IActionResult AgendarCita([FromForm] IFormCollection form)
        {
            try
            {
                string dni = form["dni"].ToString().Trim();
                string nombre = form["nombre"].ToString().Trim();
                string motivo = form["motivo"].ToString().Trim();
                string fecha = form["fecha"].ToString().Trim();
                string hora = form["hora"].ToString().Trim();
                string modalidad = form["modalidad"].ToString().Trim();
                string estado = form["estado"].ToString().Trim();

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new { success = false, message = "El DNI es obligatorio." });
                }

                DateTime fechaHora = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(fecha) && !string.IsNullOrWhiteSpace(hora))
                {
                    if (DateTime.TryParse($"{fecha} {hora}", out DateTime dt))
                    {
                        fechaHora = dt;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(fecha))
                {
                    if (DateTime.TryParse(fecha, out DateTime dtFecha))
                    {
                        fechaHora = dtFecha;
                    }
                }

                if (string.IsNullOrWhiteSpace(estado))
                {
                    estado = "Pendiente";
                }

                bool ok = _repo.AgendarCita(dni, nombre, motivo, fechaHora, estado, modalidad, out int idGenerado);
                if (ok)
                {
                    return Json(new { success = true, message = "Cita programada con éxito en la base de datos.", id = idGenerado });
                }
                return Json(new { success = false, message = "No se pudo registrar la cita en la base de datos." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al registrar cita: " + ex.Message });
            }
        }

        // POST: /Modulo05/Psicologia/EliminarAtencion
        [HttpPost("EliminarAtencion")]
        [HttpPost("CancelarCita")]
        public IActionResult EliminarAtencion([FromForm] int idAtencion)
        {
            try
            {
                if (idAtencion <= 0)
                {
                    return Json(new { success = false, message = "Identificador de atención inválido." });
                }

                bool ok = _repo.EliminarAtencion(idAtencion);
                return Json(new { success = ok, message = ok ? "Atención o cita eliminada con éxito." : "No se encontró el registro." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar: " + ex.Message });
            }
        }
    }
}
