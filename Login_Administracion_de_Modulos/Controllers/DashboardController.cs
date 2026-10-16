using Microsoft.AspNetCore.Mvc;
using Login_Administracion_de_Modulos.Seguridad;

namespace Login_Administracion_de_Modulos.Controllers
{
    public class DashboardController : Controller
    {
        [HttpGet("/Dashboard/Topico")]
        [ProtegerPanel("Enfermería (Tópico)", "Tópico", "Enfermeria")]
        public IActionResult Topico()
        {
            return View("~/Views/Dashboard/Topico.cshtml");
        }

        [HttpGet("/Dashboard/Psicologia")]
        [ProtegerPanel("Psicología", "Psicologia")]
        public IActionResult Psicologia()
        {
            return View("~/Views/Dashboard/Psicologia.cshtml");
        }

        [HttpGet("/Dashboard/Docente")]
        [ProtegerPanel("Docente", "Profesor")]
        public IActionResult Docente()
        {
            return View("~/Views/Dashboard/Docente.cshtml");
        }

        [HttpGet("/Dashboard/Soporte")]
        [ProtegerPanel("Jefe de Soporte", "Soporte")]
        public IActionResult Soporte()
        {
            return View("~/Views/Dashboard/Soporte.cshtml");
        }

        [HttpGet("/Dashboard/Alumno")]
        [ProtegerPanel("Alumno", "Estudiante")]
        public IActionResult Alumno()
        {
            return View("~/Views/Dashboard/Alumno.cshtml");
        }

        [HttpGet("/Dashboard/Practicante")]
        [ProtegerPanel("Practicante")]
        public IActionResult Practicante()
        {
            return View("~/Views/Dashboard/Practicante.cshtml");
        }

        [HttpGet("/Dashboard/Secretaria")]
        [ProtegerPanel("Secretaría Académica", "Secretaria")]
        public IActionResult Secretaria()
        {
            return View("~/Views/Dashboard/Secretaria.cshtml");
        }

        [HttpGet("/Dashboard/Administrador")]
        [ProtegerPanel("Administrador", "Admin")]
        public IActionResult Administrador()
        {
            return View("~/Views/Dashboard/Administrador.cshtml");
        }

        [HttpGet("/Dashboard/JefeGeneral")]
        [HttpGet("/Dashboard/Jefe")]
        [ProtegerPanel("Jefe General", "Almacen")]
        public IActionResult JefeGeneral()
        {
            return View("~/Views/Dashboard/JefeGeneral.cshtml");
        }

        [HttpGet("/Dashboard/JefeComputacion")]
        [HttpGet("/Dashboard/TallerComputacion")]
        [ProtegerPanel("Jefe de Taller Computación", "Computacion")]
        public IActionResult JefeComputacion()
        {
            return View("~/Views/Dashboard/JefeComputacion.cshtml");
        }

        [HttpGet("/Dashboard/JefeAdministracion")]
        [HttpGet("/Dashboard/TallerAdministracion")]
        [ProtegerPanel("Jefe de Taller Administración", "Administracion")]
        public IActionResult JefeAdministracion()
        {
            return View("~/Views/Dashboard/JefeAdministracion.cshtml");
        }

        [HttpGet("/Dashboard/JefeContabilidad")]
        [HttpGet("/Dashboard/TallerContabilidad")]
        [ProtegerPanel("Jefe de Taller Contabilidad", "Contabilidad")]
        public IActionResult JefeContabilidad()
        {
            return View("~/Views/Dashboard/JefeContabilidad.cshtml");
        }

        [HttpGet("/Dashboard/EncargadoInventario")]
        [HttpGet("/Dashboard/Inventario")]
        [ProtegerPanel("Encargado de Inventario", "Inventario")]
        public IActionResult EncargadoInventario()
        {
            return View("~/Views/Dashboard/EncargadoInventario.cshtml");
        }

        [HttpGet("/Dashboard/Tesoreria")]
        [HttpGet("/Dashboard/Tesorero")]
        [ProtegerPanel("Tesorería", "Tesoreria")]
        public IActionResult Tesoreria()
        {
            return View("~/Views/Dashboard/Tesoreria.cshtml");
        }
    }
}
