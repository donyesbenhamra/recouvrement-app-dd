// Importation des outils nécessaires : MVC, base de données, modèles, PDF.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RecouvrementAPI.Controllers
{
    [Route("api/client")]
    public class ClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ClientController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public class CreateClientDto
        {
            public string Client     { get; set; }
            public string Telephone  { get; set; }
            public string Email      { get; set; }
            public string Agence     { get; set; }
            public string TypeCredit { get; set; }
            public decimal MontantDu { get; set; }
            public int Retard        { get; set; }
            public string Statut     { get; set; } = "Amiable";
        }

        public class SubmitIntentionDto
        {
            public int? IdDossier { get; set; }
            public string TypeIntention { get; set; }
            public DateTime? DatePaiementPrevue { get; set; }
            public decimal? MontantPropose { get; set; }
            public int? ConfianceClient { get; set; }
            public string Commentaire { get; set; }
        }

        // ==============================
        // MÉTHODE PRIVÉE : Vérifier le token du client
        // ==============================
        private async Task<Client> VerifierToken(string token)
        {
            if (!(token.StartsWith("tok_") || token.StartsWith("stb_")))
                return null;

            return await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token &&
                                          (c.TokenExpiration == null || c.TokenExpiration > DateTime.Now));
        }

        // ==============================
        // MÉTHODE PRIVÉE : Trouver le bon dossier pour un client
        // ==============================
        private DossierRecouvrement ResoudreDossier(Client client, int? idDossier)
        {
            if (idDossier.HasValue)
                return client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier.Value);

            return client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();
        }

        // ==============================
        // MÉTHODE PRIVÉE : Calculer le nombre de jours de retard
        //
        // LOGIQUE CORRIGÉE :
        //   - On prend la DERNIÈRE échéance impayée dont la date est dépassée
        //   - Les jours de retard = CURDATE - date de cette dernière échéance
        //   - Cela reflète le retard "actif" le plus récent, pas l'accumulation
        //     depuis la toute première échéance manquée
        //
        //   Exemple :
        //     Échéance Jan  → impayée (il y a 300 jours)
        //     Échéance Fév  → impayée (il y a 270 jours)
        //     Échéance Mars → impayée (il y a 240 jours)  ← DERNIÈRE
        //   → joursRetard = 240 jours (et non 300)
        // ==============================
        private int CalculerJoursRetard(DossierRecouvrement dossier)
        {
            // Filtre les échéances impayées dont la date est déjà dépassée
            var echeancesImpayeesDepassees = dossier.Echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now);

            if (!echeancesImpayeesDepassees.Any()) return 0;

            // ✅ CORRECTION : on prend la DERNIÈRE (Max) et non la première (Min)
            DateTime derniereEcheanceImpayee = echeancesImpayeesDepassees
                .Max(e => e.DateEcheance);

            return (int)(DateTime.Now - derniereEcheanceImpayee).TotalDays;
        }

        // ==============================
        // MÉTHODE PRIVÉE : Passer en contentieux si retard > 180 jours
        //
        // CORRECTION : seuil 90 jours → 180 jours
        // ==============================
        private async Task VerifierRetard180Jours(DossierRecouvrement dossier)
        {
            // Utilise la même logique corrigée : DERNIÈRE échéance impayée
            var echeancesImpayeesDepassees = dossier.Echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now);

            if (!echeancesImpayeesDepassees.Any()) return;

            DateTime derniereEcheance = echeancesImpayeesDepassees.Max(e => e.DateEcheance);
            int joursRetard = (int)(DateTime.Now - derniereEcheance).TotalDays;

            // ✅ CORRECTION : seuil 180 jours (au lieu de 90)
            if (joursRetard > 180)
            {
                if (dossier.StatutDossier != "contentieux" && dossier.StatutDossier != "regularise")
                {
                    dossier.StatutDossier = "contentieux";

                    _context.HistoriqueActions.Add(new HistoriqueAction
                    {
                        IdDossier    = dossier.IdDossier,
                        ActionDetail = $"Dossier passé automatiquement en contentieux — retard de {joursRetard} jours (dernière échéance impayée : {derniereEcheance:dd/MM/yyyy}).",
                        Acteur       = "systeme",
                        DateAction   = DateTime.Now
                    });
                }

                // Anti-doublon : pas de message si déjà envoyé ce mois-ci
                bool dejaEnvoyee = await _context.Communications
                    .AnyAsync(c =>
                        c.IdDossier == dossier.IdDossier &&
                        c.Origine == "systeme" &&
                        c.DateEnvoi >= DateTime.Now.AddMonths(-1));

                if (!dejaEnvoyee)
                {
                    _context.Communications.Add(new Communication
                    {
                        IdDossier = dossier.IdDossier,
                        Message = $"Alerte automatique : retard de {joursRetard} jours " +
                                  $"détecté sur votre dossier (depuis le {derniereEcheance:dd/MM/yyyy}). " +
                                  $"Montant impayé : {dossier.MontantImpaye} TND. " +
                                  $"Votre dossier est désormais en phase contentieuse. " +
                                  $"Veuillez contacter votre agence directement.",
                        Origine   = "systeme",
                        DateEnvoi = DateTime.Now
                    });

                    _context.HistoriqueActions.Add(new HistoriqueAction
                    {
                        IdDossier    = dossier.IdDossier,
                        ActionDetail = $"Communication auto déclenchée — retard > 180 jours ({joursRetard} jours)",
                        Acteur       = "systeme",
                        DateAction   = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }
        }

        // ==============================
        // MÉTHODE PRIVÉE : Mapper DossierRecouvrement → DossierDto
        // ==============================
        private DossierDto MapDossierToDto(DossierRecouvrement dossier)
        {
            int joursRetard = CalculerJoursRetard(dossier);

            return new DossierDto
            {
                IdDossier      = dossier.IdDossier,
                TypeEmprunt    = dossier.TypeEmprunt,
                MontantImpaye  = dossier.MontantImpaye,
                MontantInitial = dossier.MontantInitial,
                MontantPaye    = dossier.MontantInitial - dossier.MontantImpaye,
                FraisDossier   = dossier.FraisDossier,
                StatutDossier  = dossier.StatutDossier,
                TauxInteret    = dossier.TauxInteret,

                // ✅ CORRECTION : intérêts déclenchés à 180 jours (aligné sur le seuil contentieux)
                MontantInterets = joursRetard > 180
                    ? dossier.MontantImpaye * (dossier.TauxInteret / 100) * (decimal)joursRetard / 365
                    : 0,

                NombreJoursRetard = joursRetard,

                DateEcheance = dossier.Echeances
                    .OrderBy(e => e.DateEcheance)
                    .Select(e => e.DateEcheance)
                    .FirstOrDefault(),

                Garanties = dossier.Garanties.Select(g => new GarantieDto
                {
                    TypeGarantie = g.TypeGarantie,
                    Description  = g.Description
                }).ToList(),

                Echeances = dossier.Echeances.Select(e => new EcheanceDto
                {
                    Montant      = e.MontantDu,
                    DateEcheance = e.DateEcheance,
                    Statut       = e.Statut
                }).ToList(),

                Paiements = dossier.HistoriquePaiements.Select(p => new HistoriquePaiementDto
                {
                    MontantPaye  = p.MontantPaye,
                    TypePaiement = p.TypePaiement,
                    DatePaiement = p.DatePaiement
                }).ToList(),

                Relances = dossier.Relances.Select(r => new RelanceDto
                {
                    IdRelance   = r.IdRelance,
                    DateRelance = r.DateRelance,
                    Moyen       = r.Moyen,
                    Statut      = r.Statut,
                    contenu     = r.Contenu,
                }).ToList(),

                Communications = dossier.Communications.Select(c => new CommunicationDto
                {
                    Message   = c.Message,
                    Origine   = c.Origine,
                    DateEnvoi = c.DateEnvoi,
                    IdRelance = c.IdRelance
                }).ToList()
            };
        }

        // ==============================
        // PATCH api/client/{idDossier}/archiver
        // ==============================
        [HttpPatch("{idDossier}/archiver")]
        public async Task<IActionResult> ArchiverClient(int idDossier)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

            if (dossier == null)
                return NotFound(new { message = "Dossier introuvable." });

            if (dossier.MontantImpaye != 0)
                return BadRequest(new { message = "Impossible d'archiver : montant impayé non nul." });

            dossier.StatutDossier  = "regularise";
            dossier.Client.Statut  = "Archivé";

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = "Dossier archivé — montant impayé soldé.",
                Acteur       = "agent",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Client archivé avec succès." });
        }

        // ==============================
        // GET api/client/historique/{token}
        // ==============================
        [HttpGet("historique/{token}")]
        public async Task<IActionResult> GetHistorique(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("Token requis");

            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Echeances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.HistoriquePaiements)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Communications)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Garanties)
                .FirstOrDefaultAsync(c => c.TokenAcces == token &&
                                          (c.TokenExpiration == null || c.TokenExpiration > DateTime.Now));

            if (client == null)
                return Unauthorized("Token invalide");

            var dossierPrincipal = client.Dossiers
                .OrderByDescending(d => d.DateCreation)
                .FirstOrDefault();

            if (dossierPrincipal != null)
            {
                _context.HistoriqueActions.Add(new HistoriqueAction
                {
                    IdDossier    = dossierPrincipal.IdDossier,
                    ActionDetail = $"Accès client via token UUID — IP : {HttpContext.Connection.RemoteIpAddress}",
                    Acteur       = "client",
                    DateAction   = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }

            // ✅ CORRECTION : appelle VerifierRetard180Jours (plus VerifierRetard3Mois)
            foreach (var dossier in client.Dossiers)
            {
                await VerifierRetard180Jours(dossier);
            }

            var dto = new ClientHistoriqueDto
            {
                NomComplet  = client.Nom + " " + client.Prenom,
                IdAgence    = client.Agence != null ? client.Agence.IdAgence : 0,
                VilleAgence = client.Agence?.Ville,

                Dossiers = client.Dossiers
                    .OrderByDescending(d => d.DateCreation)
                    .Select(dossier => MapDossierToDto(dossier))
                    .ToList()
            };

            return Ok(dto);
        }

        // ==============================
        // GET api/client/recu/{token}?idDossier=42
        // ==============================
        [HttpGet("recu/{token}")]
        public async Task<IActionResult> GenerateRecu(string token, [FromQuery] int? idDossier = null)
        {
            var client = await VerifierToken(token);
            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null) return NotFound("Dossier introuvable");

            dossier = await _context.Dossiers
                .Include(d => d.Echeances)
                .FirstOrDefaultAsync(d => d.IdDossier == dossier.IdDossier);

            int joursRetard = CalculerJoursRetard(dossier);
            decimal montantPaye = dossier.MontantInitial - dossier.MontantImpaye;

            // ✅ CORRECTION : intérêts à 180 jours
            decimal montantInterets = joursRetard > 180
                ? dossier.MontantImpaye * (dossier.TauxInteret / 100) * ((decimal)joursRetard / 365)
                : 0;

            decimal totalARegler = dossier.MontantImpaye + montantInterets;

            string colorHex = dossier.StatutDossier == "regularise" ? Colors.Green.Medium :
                             (dossier.StatutDossier == "contentieux" ? Colors.Red.Medium : Colors.Blue.Medium);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("REÇU DE SITUATION").FontSize(22).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Dossier n° {dossier.IdDossier}").FontSize(10);
                        });
                        row.RelativeItem().AlignRight().Text($"STB BANK - {client.Agence?.Ville}").Bold();
                    });

                    page.Content().PaddingVertical(25).Column(col =>
                    {
                        col.Spacing(10);

                        col.Item().Text($"Client : {client.Nom} {client.Prenom}").Bold();

                        col.Item().Text(text => {
                            text.Span("Retard constaté : ").Bold();
                            text.Span($"{joursRetard} jours")
                                .FontColor(joursRetard > 0 ? Colors.Red.Medium : Colors.Green.Medium).Bold();
                        });

                        // ✅ Mention contentieux si > 180 jours
                        if (joursRetard > 180)
                        {
                            col.Item().Text(text => {
                                text.Span("⚠ Dossier en phase contentieuse. ")
                                    .FontColor(Colors.Red.Medium).Bold();
                                text.Span("Veuillez contacter votre agence directement.")
                                    .FontColor(Colors.Red.Medium);
                            });
                        }

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().Text($"Type de crédit : {dossier.TypeEmprunt}");
                        col.Item().Text($"Montant initial : {dossier.MontantInitial:F3} TND");

                        col.Item().Text(text => {
                            text.Span("Montant déjà payé : ");
                            text.Span($"{montantPaye:F3} TND").FontColor(Colors.Green.Medium).SemiBold();
                        });

                        col.Item().Text($"Principal restant : {dossier.MontantImpaye:F3} TND");

                        if (montantInterets > 0)
                        {
                            col.Item().Text(text => {
                                text.Span($"Intérêts de retard ({dossier.TauxInteret}%) : ").Bold();
                                text.Span($"{montantInterets:F3} TND").FontColor(Colors.Red.Medium);
                            });
                        }

                        col.Item().Text($"Frais de dossier : {dossier.FraisDossier:F3} TND");

                        col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Padding(15).Column(inner =>
                        {
                            inner.Item().Text("Montant à payer").FontSize(11).Bold();
                            inner.Item().Text($"{totalARegler:F3} TND")
                                .FontSize(28).Bold().FontColor(colorHex);
                        });
                    });

                    page.Footer().AlignCenter().Text($"Document généré le {DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Recu_STB_Dossier_{dossier.IdDossier}.pdf");
        }

        // ==============================
        // POST api/client/upload/{token}
        // ==============================
        [HttpPost("upload/{token}")]
        public async Task<IActionResult> UploadJustificatif(
            string token,
            IFormFile File,
            [FromQuery] int? idDossier = null)
        {
            var client = await VerifierToken(token);
            if (client == null)
                return Unauthorized("Token invalide");

            if (File == null || File.Length == 0)
                return BadRequest("Aucun fichier envoyé");

            var extensionsAutorisees = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(File.FileName).ToLower();
            if (!extensionsAutorisees.Contains(extension))
                return BadRequest("Format non autorisé. Utilisez PDF, JPG ou PNG.");

            if (File.Length > 5 * 1024 * 1024)
                return BadRequest("Fichier trop volumineux. Maximum 5 MB.");

            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound(idDossier.HasValue
                    ? $"Dossier {idDossier} introuvable ou n'appartient pas à ce client."
                    : "Aucun dossier trouvé.");

            var uploadsPath = Path.Combine(
                _env.ContentRootPath, "uploads", dossier.IdDossier.ToString());
            Directory.CreateDirectory(uploadsPath);

            var nomFichier    = $"{DateTime.Now:yyyyMMddHHmmss}_{client.Nom}{extension}";
            var cheminComplet = Path.Combine(uploadsPath, nomFichier);

            using (var stream = new FileStream(cheminComplet, FileMode.Create))
            {
                await File.CopyToAsync(stream);
            }

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a uploadé un justificatif : {nomFichier}",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message   = $"Le client a envoyé un justificatif : {nomFichier}",
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message          = "Fichier uploadé avec succès",
                nomFichier       = nomFichier,
                idDossierUtilise = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/message/{token}
        // ==============================
        [HttpPost("message/{token}")]
        public async Task<IActionResult> EnvoyerMessage(
            string token,
            [FromBody] EnvoyerMessageDto messageDto,
            [FromQuery] int? idDossier = null)
        {
            if (string.IsNullOrWhiteSpace(messageDto?.Contenu))
                return BadRequest("Le contenu du message est requis.");

            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = ResoudreDossier(client, idDossier);
            if (dossier == null)
                return NotFound("Dossier introuvable");

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = null,
                Message   = messageDto.Contenu.Trim(),
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a envoyé un message : \"{messageDto.Contenu.Trim()}\"",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message          = "Message envoyé avec succès",
                idDossierUtilise = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/repondre-relance/{token}/{idRelance}
        // ==============================
        [HttpPost("repondre-relance/{token}/{idRelance}")]
        public async Task<IActionResult> RepondreRelance(
            string token,
            int idRelance,
            [FromBody] EnvoyerMessageDto reponseDto)
        {
            if (string.IsNullOrWhiteSpace(reponseDto?.Contenu))
                return BadRequest("Le contenu de la réponse est requis.");

            var client = await _context.Clients
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = client.Dossiers
                .FirstOrDefault(d => d.Relances.Any(r => r.IdRelance == idRelance));

            if (dossier == null)
                return NotFound("Relance introuvable ou n'appartient pas à ce client.");

            var relance = dossier.Relances.First(r => r.IdRelance == idRelance);
            relance.Statut = "repondu";

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                IdRelance = idRelance,
                Message   = reponseDto.Contenu.Trim(),
                Origine   = "client",
                DateEnvoi = DateTime.Now
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Client a répondu à la relance #{idRelance}",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message       = "Réponse enregistrée avec succès",
                idRelance     = idRelance,
                nouveauStatut = relance.Statut,
                idDossier     = dossier.IdDossier
            });
        }

        // ==============================
        // POST api/client/intention/{token}
        // ==============================
        [HttpPost("intention/{token}")]
        public async Task<IActionResult> PostIntention(string token, [FromBody] SubmitIntentionDto dto)
        {
            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = ResoudreDossier(client, dto.IdDossier);
            if (dossier == null)
                return NotFound("Dossier introuvable");

            var intention = new IntentionClient
            {
                IdDossier          = dossier.IdDossier,
                TypeIntention      = dto.TypeIntention,
                DateIntention      = DateTime.Now,
                DatePaiementPrevue = dto.DatePaiementPrevue,
                MontantPropose     = dto.MontantPropose,
                ConfianceClient    = dto.ConfianceClient,
                Statut             = "En attente"
            };

            _context.Intentions.Add(intention);

            if (!string.IsNullOrWhiteSpace(dto.Commentaire))
            {
                _context.Communications.Add(new Communication
                {
                    IdDossier = dossier.IdDossier,
                    Message   = dto.Commentaire.Trim(),
                    Origine   = "client",
                    DateEnvoi = DateTime.Now
                });
            }

            _context.Communications.Add(new Communication
            {
                IdDossier = dossier.IdDossier,
                Message   = $"[ACCUSÉ DE RÉCEPTION] Nous avons bien enregistré votre '{dto.TypeIntention.Replace("_", " ")}'. Votre demande est en cours de traitement par votre agence.",
                Origine   = "systeme",
                DateEnvoi = DateTime.Now.AddSeconds(1)
            });

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = dossier.IdDossier,
                ActionDetail = $"Soumission d'intention : {dto.TypeIntention}",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message     = "Intention enregistrée avec succès",
                idIntention = intention.IdIntention,
                idDossier   = dossier.IdDossier
            });
        }

        // ==============================
        // GET api/client/accuse-reception/{token}/{idIntention}
        // ==============================
        [HttpGet("accuse-reception/{token}/{idIntention}")]
        public async Task<IActionResult> GenerateAccuseReception(string token, int idIntention)
        {
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Intentions)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null) return Unauthorized("Token invalide");

            var intention = client.Dossiers
                .SelectMany(d => d.Intentions)
                .FirstOrDefault(i => i.IdIntention == idIntention);

            if (intention == null) return NotFound("Accusé de réception introuvable.");

            var dossier = client.Dossiers.First(d => d.IdDossier == intention.IdDossier);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(50);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("ACCUSÉ DE RÉCEPTION").FontSize(24).ExtraBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("SOCIÉTÉ TUNISIENNE DE BANQUE").FontSize(10).SemiBold();
                            col.Item().Text($"Réf : ACK-INT-{intention.IdIntention:D5}").FontSize(9).Italic();
                        });

                        row.ConstantItem(100).AlignRight().Column(col => {
                            col.Item().Height(40).Background(Colors.Blue.Medium);
                            col.Item().AlignCenter().Text("STB BANK").FontSize(8);
                        });
                    });

                    page.Content().PaddingVertical(30).Column(col =>
                    {
                        col.Spacing(15);

                        col.Item().Row(row => {
                            row.RelativeItem().Column(c => {
                                c.Item().Text("Détails Client").Bold().Underline();
                                c.Item().Text($"{client.Nom} {client.Prenom}");
                                c.Item().Text($"CIN : {client.CIN}");
                            });
                            row.RelativeItem().AlignRight().Column(c => {
                                c.Item().Text("Agence de Rattachement").Bold().Underline();
                                c.Item().Text(client.Agence?.Ville ?? "Direction Générale");
                            });
                        });

                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        col.Item().PaddingTop(10).Text(text => {
                            text.Span("Objet : ").Bold();
                            text.Span($"Confirmation de réception d'intention de {intention.TypeIntention.Replace("_", " ")}.");
                        });

                        col.Item().Text($"Nous confirmons avoir reçu votre déclaration le {intention.DateIntention:dd/MM/yyyy} à {intention.DateIntention:HH:mm} concernant votre dossier de crédit n°{dossier.IdDossier}.");

                        col.Item().Background(Colors.Grey.Lighten4).Padding(15).Column(inner => {
                            inner.Spacing(5);
                            inner.Item().Text("Récapitulatif de votre déclaration :").Bold().FontSize(12);
                            inner.Item().Text($"• Type d'action : {intention.TypeIntention}");

                            if (intention.DatePaiementPrevue.HasValue)
                                inner.Item().Text($"• Date de règlement prévue : {intention.DatePaiementPrevue.Value:dd/MM/yyyy}");

                            if (intention.MontantPropose.HasValue)
                                inner.Item().Text($"• Montant proposé : {intention.MontantPropose.Value:F3} TND");

                            inner.Item().Text($"• Indice de confiance déclaré : {intention.ConfianceClient ?? 0}%");
                        });

                        col.Item().PaddingTop(20).Text("Informations Importantes :").Bold();
                        col.Item().Text("Cet accusé de réception atteste de votre volonté de régulariser votre situation, mais ne constitue pas une quittance de paiement ou une mainlevée. Votre dossier reste sous surveillance active jusqu'au règlement effectif des sommes dues.");

                        col.Item().PaddingTop(40).AlignRight().Column(sig => {
                            sig.Item().Text("Généré numériquement par le").FontSize(9);
                            sig.Item().Text("Moteur de Recouvrement STB").FontSize(9).Bold();
                            sig.Item().PaddingTop(10).AlignCenter().Width(80).Height(80).Background(Colors.Grey.Lighten3);
                            sig.Item().AlignCenter().Text("Certifié conforme").FontSize(7).Italic();
                        });
                    });

                    page.Footer().AlignCenter().Column(f => {
                        f.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                        f.Item().PaddingTop(5).Text("Ceci est un document officiel généré par le système d'information de la STB BANK.").FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Accuse_Reception_{intention.IdIntention}.pdf");
        }

        // ==============================
        // GET api/client/historique-pdf/{token}/{idDossier}
        // ==============================
        [HttpGet("historique-pdf/{token}/{idDossier}")]
        public async Task<IActionResult> GenerateHistoriquePdf(string token, int idDossier)
        {
            var client = await _context.Clients
                .Include(c => c.Agence)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Echeances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.HistoriquePaiements)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Relances)
                .Include(c => c.Dossiers)
                    .ThenInclude(d => d.Communications)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null)
                return Unauthorized("Token invalide");

            var dossier = client.Dossiers.FirstOrDefault(d => d.IdDossier == idDossier);
            if (dossier == null)
                return NotFound("Dossier introuvable");

            int joursRetard = CalculerJoursRetard(dossier);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text("HISTORIQUE DU DOSSIER")
                                .FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            row.RelativeItem().AlignRight()
                                .Text($"Édité le {DateTime.Now:dd/MM/yyyy}");
                        });
                        col.Item().Text($"Client : {client.Nom} {client.Prenom}  |  Agence : {client.Agence?.Ville}");
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(20).Column(col =>
                    {
                        col.Spacing(16);

                        col.Item().Background(Colors.Grey.Lighten4).Padding(12).Column(inner =>
                        {
                            inner.Item().Text("INFORMATIONS DU DOSSIER").Bold().FontSize(12);
                            inner.Item().Text($"Montant initial : {dossier.MontantInitial} TND");
                            inner.Item().Text($"Montant impayé : {dossier.MontantImpaye} TND");
                            // ✅ Affiche les jours de retard avec la logique corrigée
                            inner.Item().Text($"Jours de retard : {joursRetard} (depuis la dernière échéance impayée)");
                            inner.Item().Text($"Statut : {dossier.StatutDossier.ToUpper()}");
                            inner.Item().Text($"Type : {dossier.TypeEmprunt}");
                        });

                        col.Item().Text("ÉCHÉANCES").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var e in dossier.Echeances)
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{e.DateEcheance:dd/MM/yyyy}");
                                row.RelativeItem().Text($"{e.MontantDu} TND");
                                row.RelativeItem().Text(e.Statut.ToUpper());
                            });
                        }

                        col.Item().Text("PAIEMENTS").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var p in dossier.HistoriquePaiements)
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text($"{p.DatePaiement:dd/MM/yyyy}");
                                row.RelativeItem().Text($"{p.MontantPaye} TND");
                                row.RelativeItem().Text(p.TypePaiement);
                            });
                        }

                        col.Item().Text("COMMUNICATIONS").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                        foreach (var c in dossier.Communications)
                        {
                            col.Item().Column(inner =>
                            {
                                inner.Item().Text($"{c.DateEnvoi:dd/MM/yyyy HH:mm} — {c.Origine.ToUpper()}")
                                    .FontSize(10).FontColor(Colors.Grey.Medium);
                                inner.Item().Text(c.Message);
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Document généré automatiquement — Page ");
                        x.CurrentPageNumber();
                    });
                });
            });

            byte[] pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Historique_Dossier_{idDossier}.pdf");
        }

        // ==============================
        // GET api/client/intention-existante/{token}/{idDossier}
        // ==============================
        [HttpGet("intention-existante/{token}/{idDossier}")]
        public async Task<IActionResult> GetIntentionExistante(string token, int idDossier)
        {
            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null) return Unauthorized("Token invalide");

            var intention = await _context.Intentions
                .Where(i => i.IdDossier == idDossier && i.Statut == "En attente")
                .OrderByDescending(i => i.DateIntention)
                .FirstOrDefaultAsync();

            if (intention == null)
                return Ok(new { existe = false });

            return Ok(new
            {
                existe        = true,
                idIntention   = intention.IdIntention,
                typeIntention = intention.TypeIntention,
                dateIntention = intention.DateIntention
            });
        }

        // ==============================
        // DELETE api/client/intention/{token}/{idIntention}
        // ==============================
        [HttpDelete("intention/{token}/{idIntention}")]
        public async Task<IActionResult> AnnulerIntention(string token, int idIntention)
        {
            var client = await _context.Clients
                .Include(c => c.Dossiers)
                .FirstOrDefaultAsync(c => c.TokenAcces == token);

            if (client == null) return Unauthorized("Token invalide");

            var intention = await _context.Intentions
                .FirstOrDefaultAsync(i =>
                    i.IdIntention == idIntention &&
                    i.Statut == "En attente" &&
                    client.Dossiers.Select(d => d.IdDossier).Contains(i.IdDossier));

            if (intention == null)
                return NotFound("Intention introuvable ou déjà traitée.");

            intention.Statut = "Annulé";

            _context.HistoriqueActions.Add(new HistoriqueAction
            {
                IdDossier    = intention.IdDossier,
                ActionDetail = $"Client a annulé son intention #{idIntention} de type '{intention.TypeIntention}'",
                Acteur       = "client",
                DateAction   = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Intention annulée avec succès." });
        }
    }
}