
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 🔒 Tous les endpoints nécessitent un JWT valide
    public class AdminClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context; // Accès à la base de données
        private readonly ILogger<AdminClientController> _logger; // Pour enregistrer les erreurs

        public AdminClientController(ApplicationDbContext context, ILogger<AdminClientController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // ENDPOINT 1 : Créer un nouveau client STB
        // POST /api/AdminClient
        // ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> CreateClient([FromBody] CreateClientDto dto)
        {
            try
            {
                // Vérification : un client avec ce CIN existe déjà ?
                if (await _context.Clients.AnyAsync(c => c.CIN == dto.CIN))
                    return BadRequest(new { message = "Un client avec ce CIN existe déjà." });

                // Construction de l'objet Client
                var client = new Client
                {
                    Nom = dto.Nom,
                    Prenom = dto.Prenom,
                    CIN = dto.CIN,
                    Adresse = dto.Adresse,
                    Email = dto.Email,
                    Telephone = dto.Telephone,
                    IdAgence = dto.IdAgence ?? 1, // Si aucune agence précisée → Direction Générale (id=1)
                    // Génération d'un token unique pour l'accès portail client
                    // Exemple : "tok_a3f9e2b1c4d5e6f7"
                    TokenAcces = "tok_" + Guid.NewGuid().ToString("N").Substring(0, 16)
                };

                // Sauvegarde du client en base
                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Si l'admin a fourni un premier dossier dans la requête, on le crée aussi
                if (dto.PremierDossier != null)
                {
                    var dossier = new DossierRecouvrement
                    {
                        IdClient = client.IdClient,
                        MontantInitial = dto.PremierDossier.MontantInitial ?? 0,
                        MontantImpaye = dto.PremierDossier.MontantInitial ?? 0, // Au départ, tout est impayé
                        TypeEmprunt = dto.PremierDossier.TypeEmprunt,
                        TauxInteret = dto.PremierDossier.TauxInteret ?? 0,
                        StatutDossier = dto.PremierDossier.StatutDossier ?? "aimable", // Statut par défaut
                        DateCreation = DateTime.Now
                    };
                    _context.Dossiers.Add(dossier);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Client créé avec succès.", idClient = client.IdClient });
            }
            catch (Exception ex)
            {
                // Enregistrement de l'erreur dans les logs du serveur
                _logger.LogError(ex, "Erreur création client.");
                return StatusCode(500, new { message = "Erreur lors de la création du client." });
            }
        }

        // ─────────────────────────────────────────────────────────────
        // ENDPOINT 2 : Exporter tous les impayés en fichier Excel
        // GET /api/AdminClient/export/excel
        // ─────────────────────────────────────────────────────────────
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel()
        {
            try
            {
                // Récupération de tous les dossiers avec montant impayé > 0
                // Include = jointures pour avoir les infos Client, Agence et Échéances
                var dossiers = await _context.Dossiers
                    .Include(d => d.Client)
                        .ThenInclude(c => c.Agence)
                    .Include(d => d.Echeances)
                    .Where(d => d.MontantImpaye > 0)
                    .ToListAsync();

                // Création du classeur Excel (ClosedXML)
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Dossiers Impayés STB");

                    // ── Ligne d'en-tête (ligne 1) ──
                    worksheet.Cell(1, 1).Value = "ID Dossier";
                    worksheet.Cell(1, 2).Value = "Client";
                    worksheet.Cell(1, 3).Value = "CIN";
                    worksheet.Cell(1, 4).Value = "Type Crédit";
                    worksheet.Cell(1, 5).Value = "Montant Initial";
                    worksheet.Cell(1, 6).Value = "Montant Impayé";
                    worksheet.Cell(1, 7).Value = "Retard (Jours)";
                    worksheet.Cell(1, 8).Value = "Statut";
                    worksheet.Cell(1, 9).Value = "Agence";

                    // Style de l'en-tête : fond bleu marine, texte blanc, gras
                    var headerRange = worksheet.Range("A1:I1");
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.Navy;
                    headerRange.Style.Font.FontColor = XLColor.White;

                    int row = 2; // On commence à écrire les données à partir de la ligne 2
                    foreach (var d in dossiers)
                    {
                        // Calcul du retard : aujourd'hui - date de la plus ancienne échéance impayée
                        int retard = (int)(DateTime.Now - 
                            (d.Echeances
                                .Where(e => e.Statut == "impaye")   // seulement les échéances non payées
                                .Min(e => (DateTime?)e.DateEcheance) // la plus ancienne
                             ?? DateTime.Now)                         // si aucune → retard = 0
                        ).TotalDays;
                        if (retard < 0) retard = 0; // Sécurité : pas de retard négatif

                        // Remplissage des cellules pour ce dossier
                        worksheet.Cell(row, 1).Value = d.IdDossier;
                        worksheet.Cell(row, 2).Value = $"{d.Client.Nom} {d.Client.Prenom}";
                        worksheet.Cell(row, 3).Value = d.Client.CIN;
                        worksheet.Cell(row, 4).Value = d.TypeEmprunt;
                        worksheet.Cell(row, 5).Value = d.MontantInitial;
                        worksheet.Cell(row, 6).Value = d.MontantImpaye;
                        worksheet.Cell(row, 7).Value = retard;
                        worksheet.Cell(row, 8).Value = d.StatutDossier;
                        worksheet.Cell(row, 9).Value = d.Client.Agence?.Ville ?? "Siège";

                        // Alerte visuelle : retard critique > 90 jours → cellule rouge + gras
                        if (retard > 90)
                        {
                            worksheet.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
                            worksheet.Cell(row, 7).Style.Font.Bold = true;
                        }

                        row++; // Passer à la ligne suivante
                    }

                    // Ajustement automatique de la largeur des colonnes
                    worksheet.Columns().AdjustToContents();

                    // Écriture du fichier dans un stream mémoire (pas sur disque)
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray(); // Conversion en tableau d'octets

                        // Envoi du fichier au client HTTP avec le bon type MIME
                        // Le navigateur déclenchera automatiquement le téléchargement
                        return File(content, 
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                            $"Impayes_STB_{DateTime.Now:yyyyMMdd}.xlsx"); // Ex: Impayes_STB_20250520.xlsx
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur export Excel.");
                return StatusCode(500, new { message = "Erreur lors de la génération du fichier Excel." });
            }
        }

        

        // ─────────────────────────────────────────────────────────────
        // ENDPOINT 3 : Archiver automatiquement les clients soldés
        // POST /api/AdminClient/archiver-soldes
        // ─────────────────────────────────────────────────────────────
        [HttpPost("archiver-soldes")]
        public async Task<IActionResult> ArchiverClientsSoldes()
        {
            try
            {
                // Récupération de tous les clients non encore archivés avec leurs dossiers
                var clients = await _context.Clients
                    .Include(c => c.Dossiers)
                    .Where(c => c.Statut != "Archivé")
                    .ToListAsync();

                int archivesCount = 0; // Compteur pour le rapport final

                foreach (var client in clients)
                {
                    // On traite seulement les clients qui ont au moins un dossier
                    if (client.Dossiers != null && client.Dossiers.Any())
                    {
                        // Condition d'archivage : TOUS les dossiers sont soldés
                        // (statut "regularise" OU montant impayé = 0)
                        bool toutSolder = client.Dossiers.All(d => 
                            d.StatutDossier == "regularise" || d.MontantImpaye <= 0);

                        if (toutSolder)
                        {
                            client.Statut = "Archivé"; // Changement de statut
                            archivesCount++;
                        }
                    }
                }

                // Sauvegarde en base seulement si des changements ont été faits
                if (archivesCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                // Retour du résultat avec le nombre de clients archivés
                return Ok(new { 
                    message = $"{archivesCount} client(s) ont été archivés avec succès car leurs comptes sont soldés.",
                    count = archivesCount 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l'archivage automatique.");
                return StatusCode(500, new { message = "Une erreur est survenue lors de l'archivage." });
            }
        }
    }
}