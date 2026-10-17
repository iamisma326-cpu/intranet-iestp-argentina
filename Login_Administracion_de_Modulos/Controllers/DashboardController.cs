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

                [HttpGet("/Dashboard/JefePatrimonial")]
        [HttpGet("/Dashboard/JefeGeneral")]
        [ProtegerPanel("Jefe Patrimonial", "Almacen")]
        public IActionResult JefePatrimonial()
        {
            return View("~/Views/Dashboard/JefePatrimonial.cshtml");
        }

        [HttpGet("/Dashboard/CoordinadorCarrera")]
        [ProtegerPanel("Coordinador de Carrera")]
        public IActionResult CoordinadorCarrera()
        {
            return View("~/Views/Dashboard/CoordinadorCarrera.cshtml");
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
