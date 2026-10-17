using Dapper;
using Login_Administracion_de_Modulos.Areas.Modulo00;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Login_Administracion_de_Modulos.Areas.Modulo01.Services;

/// <summary>
/// Envío de la ficha de matrícula al correo del estudiante.
/// SMTP real vía MailKit cuando la configuración existe (Smtp:Host/Port/User/Pass);
/// sin SMTP configurado, el envío queda registrado en mod01.fichas_enviadas
/// y la ficha es descargable desde la vista de Secretaría.
/// </summary>
public interface IServicioCorreoFicha
{
    /// <summary>Envía (o registra) la ficha. Devuelve mensaje de resultado.</summary>
    Task<(bool Ok, string Mensaje)> EnviarFichaAsync(
        int matriculaId, string? emailInstitucional, string? emailPersonal,
        byte[] pdf, string codigoMatricula, int usuarioId);
}

public class ServicioCorreoFicha : IServicioCorreoFicha
{
    private readonly IModuleDbConnectionFactory _fabricaConexion;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServicioCorreoFicha> _logger;

    public ServicioCorreoFicha(IModuleDbConnectionFactory fabricaConexion,
        IConfiguration configuration, ILogger<ServicioCorreoFicha> logger)
    {
        _fabricaConexion = fabricaConexion;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(bool, string)> EnviarFichaAsync(
        int matriculaId, string? emailInstitucional, string? emailPersonal,
        byte[] pdf, string codigoMatricula, int usuarioId)
    {
        var destino = !string.IsNullOrWhiteSpace(emailInstitucional) ? emailInstitucional
                    : !string.IsNullOrWhiteSpace(emailPersonal) ? emailPersonal : null;
        if (destino == null)
            return (false, "El estudiante no tiene correo registrado.");

        var anfitrion = _configuration["Smtp:Host"];
        var puerto = _configuration.GetValue<int?>("Smtp:Port");
        var usuario = _configuration["Smtp:User"];
        var clave = _configuration["Smtp:Pass"];
        var remitente = _configuration["Smtp:From"] ?? usuario ?? "secretaria@iestpargentina.edu.pe";

        var enviado = false;
        if (!string.IsNullOrWhiteSpace(anfitrion) && puerto is > 0)
        {
            try
            {
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(anfitrion, puerto.Value, SecureSocketOptions.StartTlsWhenAvailable);
                if (!string.IsNullOrWhiteSpace(usuario))
                    await smtp.AuthenticateAsync(usuario, clave ?? "");
                var tipoMime = new MimePart("application", "pdf")
                {
                    FileName = $"Ficha-{codigoMatricula}.pdf",
                    Content = new MimeContent(new MemoryStream(pdf)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment)
                };
                var multipart = new Multipart("mixed")
                {
                    new TextPart("html") { Text = $"""
                        <p>Estimado(a) estudiante:</p>
                        <p>Su matrícula <b>{codigoMatricula}</b> quedó registrada. Se adjunta la
                        ficha de matrícula en PDF con las unidades didácticas inscritas.</p>
                        <p>Secretaría Académica — IESTP "Argentina"</p>
                        """ },
                    tipoMime
                };
                var msg = new MimeMessage();
                msg.From.Add(MailboxAddress.Parse(remitente));
                msg.To.Add(MailboxAddress.Parse(destino));
                msg.Subject = $"Ficha de matrícula {codigoMatricula} — IESTP Argentina";
                msg.Body = multipart;
                await smtp.SendAsync(msg);
                await smtp.DisconnectAsync(true);
                enviado = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SMTP falló al enviar la ficha {Codigo}", codigoMatricula);
            }
        }

        // Bitácora del envío (o del intento) — la matrícula queda trazada siempre
        using var db = _fabricaConexion.CreateConnection("01");
        await db.ExecuteAsync("""
            INSERT INTO mod01.fichas_enviadas (matricula_id, enviado_a, enviado_por)
            VALUES (@MatriculaId, @EnviadoA, @UsuarioId);
            """, new { MatriculaId = matriculaId, EnviadoA = destino, UsuarioId = usuarioId });

        return enviado
            ? (true, $"Ficha enviada a {destino}.")
            : (true, $"SMTP no configurado: ficha generada y registrada para {destino} (descargable desde la vista).");
    }
}
