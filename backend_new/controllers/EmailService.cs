using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace RecouvrementAPI.Controllers
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task EnvoyerAsync(string destinataire, string nomClient, string lien)
        {
            var settings = _config.GetSection("EmailSettings");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings["SenderName"], settings["SenderEmail"]));
            message.To.Add(new MailboxAddress(nomClient, destinataire));
            message.Subject = "STB Bank - Action requise : Regularisez votre situation";
            var body = new BodyBuilder
            {
                HtmlBody = "<p>Bonjour " + nomClient + ", cliquez ici : <a href='" + lien + "'>Acceder</a></p>"
            };
            message.Body = body.ToMessageBody();
            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(settings["SmtpHost"], int.Parse(settings["SmtpPort"]!), SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(settings["SenderEmail"], settings["Password"]);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
    }
}
