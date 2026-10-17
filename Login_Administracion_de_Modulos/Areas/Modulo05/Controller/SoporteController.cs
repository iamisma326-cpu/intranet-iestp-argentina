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
    [ProtegerPanel("Jefe de Soporte", "Soporte")]
    public class SoporteController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly IConfiguration _configuration;
        private readonly TicketAveriaRepository _ticketRepo;
        private readonly ActivoHardwareRepository _activoRepo;

        public SoporteController(IConfiguration configuration)
        {
            _configuration = configuration;
            string conn = ConfiguracionSegura.ObtenerCadenaModulo(_configuration, "05");
            _ticketRepo = new TicketAveriaRepository(conn);
            _activoRepo = new ActivoHardwareRepository(conn);
        }

        // GET: /Modulo05/Soporte o /Modulo05/Soporte/ConsolaPrincipal
        [HttpGet("")]
        [HttpGet("Consola")]
        [HttpGet("ConsolaPrincipal")]
        public IActionResult ConsolaPrincipal(string? estado = null, bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (!partial && !isAjax)
            {
                return Redirect("/Dashboard/Soporte");
            }

            ViewData["Title"] = "Consola Técnica de Soporte TI";
            ViewData["Role"] = "Jefe de Soporte";
            var tickets = _ticketRepo.ObtenerTodosTickets(estado);
            ViewBag.FiltroEstado = estado;
            return PartialView("~/Areas/Modulo05/Views/soporte_tecnico/consola_principal.cshtml", tickets);
        }

        // GET: /Modulo05/Soporte/FormAtenderTicket?id=...
        [HttpGet("FormAtenderTicket")]
        [HttpGet("Atender/{id}")]
        public IActionResult FormAtenderTicket(int id, bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            ViewData["Title"] = "Atención y Reparación de Ticket";
            ViewData["Role"] = "Jefe de Soporte";

            var ticket = _ticketRepo.ObtenerTicketPorId(id);
            if (ticket == null)
            {
                return NotFound("El ticket solicitado no existe.");
            }

            if (partial || isAjax)
            {
                return PartialView("~/Areas/Modulo05/Views/soporte_tecnico/form_atender_ticket.cshtml", ticket);
            }

            return View("~/Areas/Modulo05/Views/soporte_tecnico/form_atender_ticket.cshtml", ticket);
        }

        // GET: /Modulo05/Soporte/HistorialEquipos
        [HttpGet("HistorialEquipos")]
        public IActionResult HistorialEquipos(string? filtro = null, bool partial = false)
        {
            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            ViewData["Title"] = "Historial y Trazabilidad de Equipos";
            ViewData["Role"] = "Jefe de Soporte";
            var equipos = _activoRepo.ListarActivos(filtro);
            ViewBag.Filtro = filtro;

            if (partial || isAjax)
            {
                return PartialView("~/Areas/Modulo05/Views/soporte_tecnico/historial_equipos.cshtml", equipos);
            }

            return View("~/Areas/Modulo05/Views/soporte_tecnico/historial_equipos.cshtml", equipos);
        }

        // GET: /Modulo05/Soporte/EscanearActivo?codigo=...
        [HttpGet("EscanearActivo")]
        public IActionResult EscanearActivo(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                return Json(new { success = false, message = "Ingrese o escanee un código de activo." });
            }

            try
            {
                var activo = _activoRepo.BuscarPorCbiOSerie(codigo);
                if (activo != null)
                {
                    return Json(new
                    {
                        success = true,
                        idBien = activo.IdBien,
                        cbi = activo.Cbi,
                        descripcion = activo.Descripcion,
                        tipo = activo.TipoBien,
                        marca = activo.Marca,
                        modelo = activo.Modelo,
                        serie = activo.Serie,
                        estado = activo.Estado,
                        ambiente = activo.AmbienteAsignado,
                        totalFallas = activo.TotalFallasRegistradas
                    });
                }

                return Json(new { success = false, message = $"No se encontró equipo con código/serie '{codigo}'." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al escanear activo: " + ex.Message });
            }
        }

        // POST: /Modulo05/Soporte/ActualizarTicket
        [HttpPost("ActualizarTicket")]
        public IActionResult ActualizarTicket([FromForm] IFormCollection form)
        {
            try
            {
                if (!int.TryParse(form["idIncidencia"].ToString(), out int idIncidencia))
                {
                    return Json(new { success = false, message = "ID de incidencia inválido." });
                }

                string nuevoEstado = form["estado"].ToString();
                string? solucion = form["solucionAplicada"].ToString();

                if (string.IsNullOrWhiteSpace(nuevoEstado))
                {
                    return Json(new { success = false, message = "Debe seleccionar un estado." });
                }

                int tecnicoId = (User.FindFirst("uid")?.Value ?? User.FindFirst("UsuarioId")?.Value) is string uid ? int.Parse(uid) : 1;
                bool ok = _ticketRepo.ActualizarEstadoTicket(idIncidencia, nuevoEstado, solucion, tecnicoId);
                if (ok)
                {
                    return Json(new
                    {
                        success = true,
                        message = $"Ticket #{idIncidencia} actualizado a estado '{nuevoEstado}' correctamente."
                    });
                }

                return Json(new { success = false, message = "No se pudo actualizar el ticket." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar ticket: " + ex.Message });
            }
        }
    }
}
