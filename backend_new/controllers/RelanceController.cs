using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using RecouvrementAPI.Controllers;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RelanceController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RelanceController> _logger;
        private readonly EmailService _emailService;

        public RelanceController(ApplicationDbContext context, ILogger<RelanceController> logger, EmailService emailService)
        {
            _context = context;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet("dashboard")]
        public async Task<ActionResult<RelanceDashboardResponseDto>> GetRelancesDashboard(
            [FromQuery] string canal = "Tous",
            [FromQuery] string statut = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var relances = await _context.Relances
                    .Include(r => r.Dossier)
                        .ThenInclude(d => d.Client)
                    .Include(r => r.Communications)
                    .ToListAsync();

                int totalEnvoyees = relances.Count;
                int enAttente = relances.Count(r => r.Statut == "envoye" || r.Statut == "sans_reponse");
                int formulairesSoumis = relances.Count(r => r.Statut == "repondu");
                decimal tauxReponse = totalEnvoyees > 0 ? (decimal)formulairesSoumis / totalEnvoyees * 100 : 0;

                var canaux = new RelanceCanalStatDto
                {
                    Appels = relances.Count(r => r.Moyen == "appel"),
                    AppelsDecroches = relances.Count(r => r.Moyen == "appel" && r.Statut == "repondu"),
                    AppelsNonJoignables = relances.Count(r => r.Moyen == "appel" && (r.Statut == "envoye" || r.Statut == "sans_reponse")),
                    SmsEnvoyes = relances.Count(r => r.Moyen == "sms"),
                    SmsRepondus = relances.Count(r => r.Moyen == "sms" && r.Statut == "repondu"),
                    SmsEnAttente = relances.Count(r => r.Moyen == "sms" && (r.Statut == "envoye" || r.Statut == "sans_reponse")),
                    EmailsEnvoyes = relances.Count(r => r.Moyen == "email"),
                    EmailsRepondus = relances.Count(r => r.Moyen == "email" && r.Statut == "repondu"),
                    EmailsEnAttente = relances.Count(r => r.Moyen == "email" && (r.Statut == "envoye" || r.Statut == "sans_reponse"))
                };

                var query = relances.AsEnumerable();

                if (!string.IsNullOrEmpty(canal) && canal != "Tous")
                    query = query.Where(r => r.Moyen.Equals(canal, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                    query = query.Where(r => r.Statut.Equals(statut, StringComparison.OrdinalIgnoreCase));

                var itemsToMap = query.OrderByDescending(r => r.DateRelance).ToList();
                int totalItems = itemsToMap.Count;
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var paginatedItems = itemsToMap
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new RelanceItemDto
                    {
                        IdRelance = r.IdRelance,
                        IdDossier = r.IdDossier,
                        Client = $"{r.Dossier.Client.Nom} {r.Dossier.Client.Prenom}",
                        Telephone = r.Dossier.Client.Telephone,
                        Email = r.Dossier.Client.Email,
                        Canal = r.Moyen,
                        Token = string.IsNullOrEmpty(r.Dossier.Client.TokenAcces) ? "" :
                                (r.Dossier.Client.TokenAcces.Length > 8 ? r.Dossier.Client.TokenAcces.Substring(0, 8) + "..." : r.Dossier.Client.TokenAcces),
                        DateExpiration = r.Dossier.Client.TokenExpiration,
                        Statut = r.Statut,
                        Reponse = r.Statut == "repondu" ?
                            (r.Communications.OrderByDescending(c => c.DateEnvoi).FirstOrDefault(c => c.Origine == "client")?.Message ?? "Demande soumise")
                            : "Aucune"
                    }).ToList();

                return Ok(new RelanceDashboardResponseDto
                {
                    Kpis = new RelanceKpiDto
                    {
                        TotalEnvoyees = totalEnvoyees,
                        EnAttenteReponse = enAttente,
                        FormulairesSoumis = formulairesSoumis,
                        TauxReponse = Math.Round(tauxReponse, 1)
                    },
                    Canaux = canaux,
                    Items = paginatedItems,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Échec lors de la récolte des logs Relance.");
                return StatusCode(500, new { message = "L'API a échoué à composer l'historique des requêtes manuelles." });
            }
        }
        [AllowAnonymous]
        [HttpPost("{idDossier}/message")]
public async Task<IActionResult> EnvoyerMessage(int idDossier, [FromBody] EnvoyerMessageDto dto)
{
    var dossier = await _context.Dossiers
        .Include(d => d.Client)
        .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

    if (dossier == null) return NotFound(new { message = "Dossier introuvable." });

    var relance = new RelanceClient
    {
        IdDossier = idDossier,
        Moyen = "message",
        Statut = "envoye",
        DateRelance = DateTime.Now,
        Contenu = dto.Message
    };
    _context.Relances.Add(relance);

    var communication = new Communication
    {
        IdDossier = idDossier,
        Message = dto.Message,
        Origine = "agent",
        DateEnvoi = DateTime.Now
    };
    _context.Communications.Add(communication);

    await _context.SaveChangesAsync();

    return Ok(new { message = "Message envoyé au client." });
}

        [HttpPost("{idDossier}/envoyer-token")]
        
        public async Task<ActionResult<EnvoiTokenResponseDto>> EnvoyerToken(int idDossier, [FromBody] EnvoiTokenDto req)
        {
            try
            {
                var dossier = await _context.Dossiers
                    .Include(d => d.Client)
                    .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

                if (dossier == null)
                    return NotFound(new { message = "Dossier introuvable." });

              string nouveauToken = "stb_" + Guid.NewGuid().ToString("N").Substring(0, 16);
                dossier.Client.TokenExpiration = DateTime.Now.AddDays(7);

                var relance = new RelanceClient
                {
                    IdDossier = dossier.IdDossier,
                    Moyen = req.Canal ?? "sms",
                    Statut = "envoye",
                    DateRelance = DateTime.Now,
                    Contenu = $"Envoi {req.Canal} manuel depuis interface"
                };

                _context.Relances.Add(relance);
                await _context.SaveChangesAsync();

string lien = $"http://localhost:4200/client/{nouveauToken}";
                string nomClient = $"{dossier.Client.Nom} {dossier.Client.Prenom}";
                string messageRetour;

                if (req.Canal == "email")
                {
                    await _emailService.EnvoyerAsync(dossier.Client.Email, nomClient, lien);
                    messageRetour = $"Email envoyé à {dossier.Client.Email}";
                }
                else
                {
                    messageRetour = $"[SIMULATION SMS] STB BANK: Cher {dossier.Client.Nom}, réglez votre retard via : {lien}";
                }

                _logger.LogInformation($"[ENVOI TOKEN] Dossier: {idDossier} | Canal: {req.Canal} | Lien: {lien}");

                return Ok(new EnvoiTokenResponseDto
                {
                    Message = messageRetour,
                    TokenGenere = nouveauToken,
                    LienPaiement = lien
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération du token.");
                return StatusCode(500, new { message = "Impossible d'expédier le lien d'accès.", detail = ex.Message });
            }
        }
    }
}