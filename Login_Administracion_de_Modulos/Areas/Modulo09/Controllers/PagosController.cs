using System.IO;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using Login_Administracion_de_Modulos.Areas.Modulo09.Models;
using Login_Administracion_de_Modulos.Areas.Modulo09.Services;

namespace Login_Administracion_de_Modulos.Areas.Modulo09.Controllers;

/// <summary>
/// Pagos del Módulo 09 (Tesorería & Pagos) — CU-09.1 y CU-09.2 del informe.
/// Vistas duales según rol (patrón del prototipo WebForms):
///   Alumno    → "Mis pagos" + registrar pago con voucher
///   Tesorería → bandeja de vouchers (Aprobar/Rechazar con motivo)
///               + emisión de recibos directos de caja
/// </summary>
[Area("Modulo09")]
[Route("Modulo09/[controller]")]
public class PagosController : ModuloBaseController
{
    private readonly IPagoServicio _servicioPago;

    public PagosController(IPagoServicio servicioPago)
    {
        _servicioPago = servicioPago;
    }

    /// <summary>Expone los roles del usuario a las vistas (tabs rol-aware).</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        // Fix multi-rol: el filtro de tabs usa el ROL ACTIVO (el modo elegido con el selector
        // se guarda en el claim "ActiveRole"), no la lista completa de roles — si no, un
        // multi-rol (p. ej. Director+Alumno) ve pestañas de staff estando en Modo Alumno.
        ViewData["RolesUsuario"] = User.FindFirst("ActiveRole")?.Value ?? UsuarioActualRol;
        base.OnActionExecuting(context);
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? voucher)
    {
        // Puerta por rol (misma regla de la portada): sin funciones aquí → SinAcceso.
        if (!AccedeAlModulo) return View("SinAcceso");

        ViewData["Title"] = "09. Pagos y Recibos";
        ViewData["TeamName"] = "Equipo 09";
        ViewData["UsuarioNombre"] = UsuarioActualNombre;
        ViewData["UsuarioRol"] = UsuarioActualRol;

        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            return VistaModulo("Mis", new ModeloMisPagosVista
            {
                Pagos = await _servicioPago.ListarPorEstudianteAsync(estudianteId),
                Conceptos = await _servicioPago.ListarConceptosAsync(),
                TiposPago = await _servicioPago.ListarTiposPagoAsync()
            });
        }

        return VistaModulo("Bandeja", new ModeloBandejaPagosVista
        {
            Pagos = await _servicioPago.ListarBandejaAsync(voucher),
            Resumen = await _servicioPago.ResumenAsync(),
            FiltroVoucher = voucher,
            FormRecibo = new ModeloRegistroPagoVista
            {
                Conceptos = await _servicioPago.ListarConceptosAsync(),
                TiposPago = await _servicioPago.ListarTiposPagoAsync(),
                Estudiantes = await ListarEstudiantesAsync()
            }
        });
    }

    // ALUMNO: registrar pago con voucher (queda Pendiente para la bandeja)
    [HttpPost("Registrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registrar(string concepto, string tipoPago, string? monto, string? nota)
    {
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            var periodoId = await ObtenerPeriodoActivoIdAsync();

            // El importe NO se toma del formulario: el catálogo TUPA es la única
            // autoridad. Antes el código hacía
            //   if (!decimal.TryParse(monto, out m) && concepto != null) m = concepto.Monto;
            // o sea que el catálogo solo se usaba si el campo venía vacío o con
            // basura, y cualquier número válido que posteara el navegador se
            // guardaba tal cual (S/ 0.01 por una Reserva de Matrícula de S/ 30).
            // Ahora se resuelve contra el catálogo y el valor posteado se ignora.
            var conceptos = await _servicioPago.ListarConceptosAsync();
            var conceptoModelo = conceptos.FirstOrDefault(c => c.Codigo == concepto);
            if (conceptoModelo == null)
            {
                AlertaError("El concepto de pago no existe o está inactivo.");
                return RedirectToAction(nameof(Index));
            }

            if (decimal.TryParse(monto, out var posted) && posted != conceptoModelo.Monto)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[Pagos] monto posteado {posted} ignorado; el catálogo manda ({conceptoModelo.Monto}).");
            }

            var (ok, mensaje) = await _servicioPago.RegistrarPagoEstudianteAsync(
                estudianteId, periodoId, concepto, tipoPago, conceptoModelo.Monto, nota);
            if (ok) AlertaExito(mensaje);
            else AlertaError(mensaje);
        }
        return RedirectToAction(nameof(Index));
    }

    // TESORERÍA: emitir recibo directo de caja
    [HttpPost("EmitirRecibo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmitirRecibo(int estudianteId, string concepto, string tipoPago, decimal monto)
    {
        if (!EsTesoreria && !EsDirector)
            return Forbid();

        var periodoId = await ObtenerPeriodoActivoIdAsync();
        var (ok, mensaje) = await _servicioPago.EmitirReciboDirectoAsync(
            estudianteId, periodoId, concepto, tipoPago, monto, UsuarioActualId ?? 0);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // TESORERÍA: validar voucher desde la bandeja (aprobar / rechazar)
    [HttpPost("Validar/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Validar(int id, bool aprobar, string? motivo)
    {
        if (!EsTesoreria && !EsAdmin)
            return Forbid();

        var (ok, mensaje) = await _servicioPago.ValidarVoucherAsync(
            id, aprobar, motivo, UsuarioActualId ?? 0);
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // ALUMNO: subir el voucher PDF de SU pago (recibo generado por un
    // trámite o registrado a mano). Sin esto Tesorería validaba "a ciegas".
    [HttpPost("SubirVoucher/{id}")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> SubirVoucher(int id)
    {
        if (!EsAlumno) return Forbid();
        var estudianteId = await ObtenerEstudianteIdAsync();

        var archivoAdjunto = Request.Form.Files.FirstOrDefault(f => f.Name == "voucher" && f.Length > 0);
        if (archivoAdjunto == null)
        {
            AlertaError("Adjunta el voucher en PDF.");
            return RedirectToAction(nameof(Index));
        }
        if (archivoAdjunto.Length > 2_097_152)
        {
            AlertaError("El voucher supera los 2 MB permitidos.");
            return RedirectToAction(nameof(Index));
        }

        using var ms = new MemoryStream();
        await archivoAdjunto.CopyToAsync(ms);
        var (ok, mensaje) = await _servicioPago.SubirVoucherAsync(
            id, estudianteId, archivoAdjunto.FileName, archivoAdjunto.ContentType, ms.ToArray());
        if (ok) AlertaExito(mensaje);
        else AlertaError(mensaje);
        return RedirectToAction(nameof(Index));
    }

    // Voucher PDF del pago.
    //   Alumno    → solo los pagos de SU estudiante_id (anti-suplantación:
    //                un404, no un403, para no revelar que el id existe).
    //   Personal  → cualquier pago de la bandeja.
    [HttpGet("Voucher/{id}")]
    public async Task<IActionResult> Voucher(int id)
    {
        if (EsAlumno)
        {
            var estudianteId = await ObtenerEstudianteIdAsync();
            if (estudianteId <= 0 || !await _servicioPago.PerteneceAlEstudianteAsync(id, estudianteId))
                return NotFound();
        }

        var v = await _servicioPago.ObtenerVoucherAsync(id);
        if (v == null) return NotFound();
        return File(v.Contenido, v.Tipo, v.Nombre);
    }

    // Helpers de contexto
    private async Task<int> ObtenerEstudianteIdAsync()
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        var id = await conn.ExecuteScalarAsync<int?>(
            "SELECT e.id FROM core.estudiantes e WHERE e.persona_id = @PersonaId;",
            new { PersonaId = PersonaActualId ?? 0 });
        return id ?? 0;
    }

    private async Task<int> ObtenerPeriodoActivoIdAsync()
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        return await conn.ExecuteScalarAsync<int>(
            "SELECT id FROM core.periodos_academicos WHERE es_activo ORDER BY id DESC LIMIT 1;");
    }

    private async Task<IEnumerable<ModeloEstudianteSeleccion>> ListarEstudiantesAsync()
    {
        var fabrica = HttpContext.RequestServices.GetRequiredService<Login_Administracion_de_Modulos.Areas.Modulo00.IModuleDbConnectionFactory>();
        using var conn = fabrica.CreateConnection("09");
        return await conn.QueryAsync<ModeloEstudianteSeleccion>("""
            SELECT e.id, e.codigo_estudiante AS CodigoEstudiante,
                   p.nombres || ' ' || p.apellidos AS NombreCompleto
            FROM core.estudiantes e
            JOIN core.personas p ON p.id = e.persona_id
            ORDER BY e.codigo_estudiante;
            """);
    }
}

// ViewModels
public class ModeloMisPagosVista
{
    public IEnumerable<ModeloPagoLista> Pagos { get; set; } = [];
    public IEnumerable<ModeloConceptoPago> Conceptos { get; set; } = [];
    public IEnumerable<ModeloTipoPago> TiposPago { get; set; } = [];
}

public class ModeloBandejaPagosVista
{
    public IEnumerable<ModeloPagoBandeja> Pagos { get; set; } = [];
    public ModeloResumenPagos Resumen { get; set; } = new();
    public string? FiltroVoucher { get; set; }
    public ModeloRegistroPagoVista FormRecibo { get; set; } = new();
}
