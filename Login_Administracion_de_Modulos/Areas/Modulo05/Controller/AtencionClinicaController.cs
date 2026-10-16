using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Login_Administracion_de_Modulos.Areas.Modulo05.Models;
using Login_Administracion_de_Modulos.Seguridad;

namespace Login_Administracion_de_Modulos.Areas.Modulo05.Controller
{
    [Area("Modulo05")]
    [Route("Modulo05/[controller]")]
    [ProtegerPanel("Enfermería (Tópico)", "Tópico", "Enfermeria")]
    public class AtencionClinicaController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IConfiguration _configuration;
        private readonly AtencionMedicaRepository _repo;

        public AtencionClinicaController(IConfiguration configuration)
        {
            _configuration = configuration;
            string conn = ConfiguracionSegura.ObtenerCadenaModulo(_configuration, "05");
            _repo = new AtencionMedicaRepository(conn);
        }

        // GET: /Modulo05/AtencionClinica/Create
        [HttpGet("Create")]
        public IActionResult Create(bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            ViewData["Title"] = "Registro de Atención Clínica";
            ViewData["Role"] = "Enfermería (Tópico)";
            ViewBag.Medicamentos = _repo.ListarMedicamentos();

            if (partial || isAjax)
            {
                return PartialView("~/Areas/Modulo05/Views/enfermeria/form_atencion.cshtml");
            }

            return View("~/Areas/Modulo05/Views/enfermeria/form_atencion.cshtml");
        }

        // GET: /Modulo05/AtencionClinica/BuscarPaciente?dni=...
        [HttpGet("BuscarPaciente")]
        public IActionResult BuscarPaciente(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return Json(new { success = false, message = "Ingrese un número de DNI válido." });
            }

            try
            {
                var paciente = _repo.BuscarPacientePorDni(dni);
                if (paciente != null)
                {
                    return Json(new
                    {
                        success = true,
                        idPersona = paciente.IdPersona,
                        nombres = paciente.Nombres,
                        apellidos = paciente.Apellidos,
                        carrera = "Desarrollo de Sistemas de Información",
                        cicloTurno = "IV Ciclo / Turno Noche"
                    });
                }

                return Json(new
                {
                    success = false,
                    message = $"No se encontró registro para el DNI {dni} en la tabla personas."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al consultar paciente: " + ex.Message });
            }
        }

        // POST: /Modulo05/AtencionClinica/GuardarAtencion
        [HttpPost("GuardarAtencion")]
        public IActionResult GuardarAtencion([FromForm] IFormCollection form)
        {
            try
            {
                string? dni = form["buscarDni"].ToString();
                if (string.IsNullOrWhiteSpace(dni)) dni = form["dni"].ToString();

                string? motivo = form["motivoAtencion"].ToString();
                if (string.IsNullOrWhiteSpace(motivo)) motivo = form["motivo"].ToString();

                string? fechaHoraStr = form["fechaHora"].ToString();
                string? medicamentosStr = form["medicamentosAdministrados"].ToString();
                string? observaciones = form["observacionesTratamiento"].ToString();
                string? seguro = form["tipoSeguroSalud"].ToString();
                bool esAlergico = !string.IsNullOrEmpty(form["reportaAlergias"]) || form["esAlergico"] == "true";
                string? detalleAlergia = form["especificarAlergias"].ToString();

                if (string.IsNullOrWhiteSpace(dni))
                {
                    return Json(new { success = false, message = "Debe buscar y validar un DNI primero." });
                }

                if (string.IsNullOrWhiteSpace(motivo))
                {
                    return Json(new { success = false, message = "El motivo de atención es obligatorio." });
                }

                var paciente = _repo.BuscarPacientePorDni(dni);
                if (paciente == null)
                {
                    return Json(new { success = false, message = "El paciente no está registrado en el sistema." });
                }

                DateTime fechaHora = DateTime.Now;
                if (!string.IsNullOrEmpty(fechaHoraStr) && DateTime.TryParse(fechaHoraStr, out DateTime parsed))
                {
                    fechaHora = parsed;
                }

                int? medicamentoId = null;
                if (int.TryParse(form["medicamentoId"].ToString(), out int medId) && medId > 0)
                {
                    medicamentoId = medId;
                }

                string obsCompleta = observaciones ?? "";
                if (!string.IsNullOrWhiteSpace(medicamentosStr))
                {
                    obsCompleta = $"Medicamento recetado: {medicamentosStr}\n{obsCompleta}".Trim();
                }

                int atendidoPorId = User.FindFirst("UsuarioId")?.Value is string uid ? int.Parse(uid) : 1;

                var item = new AtencionMedicaItem
                {
                    PersonaId = paciente.IdPersona,
                    AtendidoPorId = atendidoPorId,
                    FechaHora = fechaHora,
                    Motivo = motivo,
                    MedicamentoId = medicamentoId,
                    EsAlergico = esAlergico,
                    DetalleAlergia = detalleAlergia,
                    SeguroSalud = string.IsNullOrWhiteSpace(seguro) ? "SIS" : seguro,
                    Observacion = obsCompleta
                };

                bool ok = _repo.RegistrarAtencion(item);
                if (ok)
                {
                    return Json(new
                    {
                        success = true,
                        message = "¡Atención clínica registrada exitosamente en el sistema de Tópico!"
                    });
                }

                return Json(new { success = false, message = "No se pudo registrar la atención." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al procesar registro: " + ex.Message });
            }
        }

        // GET: /Modulo05/AtencionClinica/Historial?dni=...
        [HttpGet("Historial")]
        public IActionResult Historial(string dni)
        {
            if (string.IsNullOrWhiteSpace(dni))
            {
                return Json(new { success = false, message = "DNI no proporcionado." });
            }

            try
            {
                var atenciones = _repo.ObtenerHistorialPorDni(dni);
                return Json(new { success = true, data = atenciones });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al consultar historial: " + ex.Message });
            }
        }
    }
}
