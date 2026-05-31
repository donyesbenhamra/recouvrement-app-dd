// ================================================================
// IMPORTS : bibliothèques nécessaires au fonctionnement du contrôleur
// ================================================================
using Microsoft.AspNetCore.Authorization; // Pour protéger les routes avec [Authorize]
using Microsoft.AspNetCore.Mvc;           // Pour les attributs MVC : [ApiController], [HttpGet], etc.
using Microsoft.EntityFrameworkCore;      // Pour accéder à la base de données via Entity Framework Core
using RecouvrementAPI.Data;               // Notre contexte de base de données (ApplicationDbContext)
using RecouvrementAPI.DTOs;               // Les objets de transfert de données (ce qu'on envoie au frontend)
using RecouvrementAPI.Models;             // Les modèles de données (Dossier, Client, Echeance, etc.)
using QuestPDF.Fluent;                    // API fluente QuestPDF pour construire les documents PDF
using QuestPDF.Helpers;                   // Utilitaires QuestPDF : couleurs, tailles de page
using QuestPDF.Infrastructure;            // Infrastructure QuestPDF : Document.Create, etc.

namespace RecouvrementAPI.Controllers
{
    // ================================================================
    // DÉCLARATION DU CONTRÔLEUR
    // Ce contrôleur gère tout ce qui concerne la "Gestion des impayés"
    // Il est accessible depuis : http://localhost:5000/api/Impaye
    // ================================================================

    [Route("api/[controller]")]  // Route automatique basée sur le nom du contrôleur → /api/Impaye
    [ApiController]              // Active les comportements API automatiques (validation, erreurs 400, etc.)
    [Authorize]                  // Toutes les routes de ce contrôleur nécessitent un token JWT valide
    public class ImpayeController : ControllerBase
    {
        // ================================================================
        // DÉPENDANCES INJECTÉES PAR .NET AU DÉMARRAGE
        // ================================================================

        private readonly ApplicationDbContext _context; // Accès à la base de données MySQL
        private readonly ILogger<ImpayeController> _logger; // Journalisation des erreurs serveur

        // Constructeur : .NET injecte automatiquement les dépendances (Dependency Injection)
        public ImpayeController(ApplicationDbContext context, ILogger<ImpayeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ================================================================
        // ENDPOINT 1 : GET /api/Impaye/gestion
        //
        // Retourne deux choses en une seule réponse JSON :
        //   1. Les KPIs financiers globaux (total impayé, intérêts dus, etc.)
        //   2. La liste paginée des dossiers impayés avec tous leurs détails financiers
        //
        // Paramètres optionnels (query string) :
        //   - filtre    : "Tous" | "Avec intérêt >=90j" | "Sans intérêt" | "Soldé"
        //   - page      : numéro de la page (défaut = 1)
        //   - pageSize  : nombre d'éléments par page (défaut = 10)
        //
        // Exemple d'appel : GET /api/Impaye/gestion?filtre=Avec intérêt >=90j&page=1
        // ================================================================
        [HttpGet("gestion")]
        public async Task<ActionResult<ImpayeResponseDto>> GetImpayesGestion(
            [FromQuery] string filtre = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // ── ÉTAPE 1 : Chargement des données depuis MySQL ──────────────────
                // On charge tous les dossiers avec leurs clients et échéances associées.
                // Le calcul des intérêts utilise Math.Round() qui n'est pas traduisible
                // en SQL natif → on charge tout en RAM puis on calcule côté serveur C#.
                var dossiers = await _context.Dossiers
                    .Include(d => d.Client)    // Charge le client lié à chaque dossier
                    .Include(d => d.Echeances) // Charge les échéances pour calculer le retard
                    .ToListAsync();            // Exécute la requête SQL et stocke en mémoire

                // ── ÉTAPE 2 : Transformation des données en format "métier impayé" ──
                // Pour chaque dossier, on calcule les valeurs financières nécessaires à l'affichage
                var mappedItems = dossiers.Select(d => 
                {
                    // Calcul du retard : nombre de jours depuis la première échéance impayée dépassée
                    var retard = CalculerJoursRetard(d.Echeances);
                    
                    // ── RÈGLE LÉGISLATIVE BANCAIRE ──
                    // Les intérêts de retard ne sont appliqués QUE si le retard dépasse 90 jours
                    // Formule : Principal restant × (Taux annuel / 100) × (Jours retard / 365)
                    decimal interets = 0;
                    if (retard >= 90)
                    {
                        interets = d.MontantImpaye * (d.TauxInteret / 100m) * (retard / 365m);
                    }

                    // Montant déjà remboursé = différence entre le montant initial et ce qui reste
                    var dejaPaye = d.MontantInitial - d.MontantImpaye;

                    // Date de la première échéance impayée (null si toutes sont payées)
                    var premiereEcheance = d.Echeances
                        .Where(e => e.Statut == "impaye")
                        .OrderBy(e => e.DateEcheance)
                        .Select(e => (DateTime?)e.DateEcheance)
                        .FirstOrDefault();

                    // Construction du DTO : objet simplifié envoyé au frontend Angular
                    return new ImpayeItemDto
                    {
                        IdDossier      = d.IdDossier,
                        NomPrenom      = $"{d.Client.Nom} {d.Client.Prenom}",
                        DateOctroi     = d.DateCreation,    // Date de création du dossier
                        DateEcheance   = premiereEcheance,  // Première échéance impayée
                        MontantInitial = d.MontantInitial,
                        DejaPaye       = dejaPaye,
                        PrincipalDu    = d.MontantImpaye,   // Ce qui reste à payer (sans intérêts)
                        Frais          = d.FraisDossier,
                        Taux           = d.TauxInteret,
                        Retard         = retard,
                        Interets       = Math.Round(interets, 3),    // Arrondi à 3 décimales (millimes TND)
                        TotalARegler   = Math.Round(d.MontantImpaye + interets + d.FraisDossier, 3)
                    };
                }).ToList();

                // ── ÉTAPE 3 : Application du filtre sélectionné dans le dropdown Angular ──
                if (filtre == "Avec intérêt >=90j")
                    mappedItems = mappedItems.Where(i => i.Retard >= 90).ToList();           // Dossiers en retard critique
                else if (filtre == "Sans intérêt")
                    mappedItems = mappedItems.Where(i => i.Retard < 90 && i.PrincipalDu > 0).ToList(); // Retard modéré
                else if (filtre == "Soldé")
                    mappedItems = mappedItems.Where(i => i.PrincipalDu <= 0).ToList();       // Dossiers remboursés

                // ── ÉTAPE 4 : Calcul des KPIs globaux du portefeuille ─────────────────
                // Ces 4 indicateurs s'affichent dans les cartes colorées en haut de la page

                // KPI 1 : somme de tous les montants impayés (dossiers non régularisés)
                var totalImpaye = dossiers
                    .Where(d => d.StatutDossier != "regularise")
                    .Sum(d => d.MontantImpaye);

                // KPI 2 : total des intérêts accumulés sur les dossiers en retard > 90j
                var totalInteretsDus = mappedItems
                    .Where(i => i.Retard >= 90)
                    .Sum(i => i.Interets);

                // KPI 3 : total des frais de dossier sur les dossiers actifs
                var totalFrais = dossiers
                    .Where(d => d.StatutDossier != "regularise")
                    .Sum(d => d.FraisDossier);

                // KPI 4 : montant total déjà récupéré sur l'ensemble du portefeuille
                var dejaRecupere = dossiers.Sum(d => d.MontantInitial - d.MontantImpaye);

                // Utilisé pour calculer le taux de récupération en pourcentage
                var totalInitial = dossiers.Sum(d => d.MontantInitial);

                var kpis = new ImpayeKpiDto
                {
                    TotalImpaye     = totalImpaye,
                    InteretsDus     = Math.Round(totalInteretsDus, 2),
                    TotalARecouvrer = Math.Round(totalImpaye + totalInteretsDus + totalFrais, 2),
                    DejaRecupere    = dejaRecupere,
                    // Taux de récupération global = (récupéré / initial) × 100
                    TauxRecuperation = totalInitial > 0
                        ? Math.Round((dejaRecupere / totalInitial) * 100, 1)
                        : 0
                };

                // ── ÉTAPE 5 : Pagination côté serveur ─────────────────────────────────
                // On ne renvoie qu'une page à la fois pour ne pas surcharger le frontend
                int totalItems = mappedItems.Count;
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize); // Arrondi supérieur

                var paginatedItems = mappedItems
                    .OrderByDescending(i => i.TotalARegler) // Les dettes les plus importantes en premier
                    .Skip((page - 1) * pageSize)            // Saute les pages précédentes
                    .Take(pageSize)                          // Prend seulement la page courante
                    .ToList();

                // ── ÉTAPE 6 : Retourne la réponse complète au frontend ─────────────────
                return Ok(new ImpayeResponseDto
                {
                    Kpis        = kpis,          // Les 4 indicateurs financiers
                    Items       = paginatedItems, // Les dossiers de la page courante
                    TotalItems  = totalItems,     // Nombre total de dossiers (pour la pagination Angular)
                    TotalPages  = totalPages,     // Nombre total de pages
                    CurrentPage = page            // Page actuelle
                });
            }
            catch (Exception ex)
            {
                // En cas d'erreur inattendue : journalise l'erreur et retourne un message propre
                _logger.LogError(ex, "Erreur critique de la route Impaye.");
                return StatusCode(500, new { message = "L'API a rencontré un dysfonctionnement lors des calculs financiers." });
            }
        }

        // ================================================================
        // ENDPOINT 2 : GET /api/Impaye/export-pdf
        //
        // Génère et télécharge un fichier PDF complet du tableau des impayés.
        // Le PDF est construit dynamiquement avec QuestPDF (format A4 paysage).
        // Chaque ligne correspond à un dossier actif (non régularisé).
        // Les lignes en rouge = dossiers avec retard > 90 jours.
        // ================================================================
        [HttpGet("export-pdf")]
        public async Task<IActionResult> ExportPdf()
        {
            // ── Chargement des dossiers actifs uniquement ─────────────────────────
            // On exclut les dossiers "regularise" car ils sont soldés → pas d'intérêt dans le rapport
            var dossiers = await _context.Dossiers
                .Include(d => d.Client)
                .Include(d => d.Echeances)
                .Where(d => d.StatutDossier != "regularise")
                .ToListAsync();

            // ── Construction du document PDF avec QuestPDF ────────────────────────
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);                          // Marges de 30 unités sur tous les côtés
                    page.Size(PageSizes.A4.Landscape());      // Format A4 en mode paysage (plus large)
                    page.DefaultTextStyle(x => x.FontSize(9)); // Police par défaut petite pour tenir dans le tableau

                    // ── EN-TÊTE DU PDF ─────────────────────────────────────────────
                    page.Header().Column(col =>
                    {
                        col.Item().Text("GESTION DES IMPAYÉS — STB BANK")
                            .FontSize(16).SemiBold().FontColor(Colors.Blue.Medium); // Titre principal en bleu
                        col.Item().Text($"Édité le {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(9).FontColor(Colors.Grey.Medium); // Date et heure de génération
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2); // Ligne séparatrice
                    });

                    // ── TABLEAU PRINCIPAL ──────────────────────────────────────────
                    page.Content().PaddingVertical(15).Table(table =>
                    {
                        // Définition des largeurs relatives de chaque colonne
                        // (les nombres sont des proportions, pas des pixels)
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2); // NOM & PRÉNOM (plus large)
                            cols.RelativeColumn(1); // RÉF DOSSIER
                            cols.RelativeColumn(2); // PRINCIPAL DÛ
                            cols.RelativeColumn(1); // FRAIS
                            cols.RelativeColumn(1); // TAUX
                            cols.RelativeColumn(1); // RETARD
                            cols.RelativeColumn(2); // INTÉRÊTS >=90J
                            cols.RelativeColumn(2); // TOTAL À RÉGLER (plus large)
                        });

                        // ── EN-TÊTE DU TABLEAU (ligne bleue avec texte blanc) ──────
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "NOM & PRÉNOM", "RÉF", "PRINCIPAL DÛ", "FRAIS", "TAUX", "RETARD", "INTÉRÊTS >=90J", "TOTAL À RÉGLER" })
                            {
                                header.Cell()
                                    .Background(Colors.Blue.Medium) // Fond bleu STB
                                    .Padding(5)
                                    .Text(h)
                                    .FontColor(Colors.White)         // Texte blanc
                                    .Bold()
                                    .FontSize(8);
                            }
                        });

                        // ── LIGNES DE DONNÉES : une ligne par dossier ──────────────
                        foreach (var d in dossiers)
                        {
                            // Recalcul du retard et des intérêts pour ce dossier
                            var retard = CalculerJoursRetard(d.Echeances);
                            decimal interets = retard >= 90
                                ? Math.Round(d.MontantImpaye * (d.TauxInteret / 100m) * (retard / 365m), 3)
                                : 0;
                            decimal total = Math.Round(d.MontantImpaye + interets + d.FraisDossier, 3);

                            // Couleur de fond : rouge clair si retard critique, blanc sinon
                            var bg = retard >= 90 ? Colors.Red.Lighten5 : Colors.White;

                            // Chaque appel à table.Cell() remplit la cellule suivante (de gauche à droite)
                            table.Cell().Background(bg).Padding(4).Text($"{d.Client.Nom} {d.Client.Prenom}");
                            table.Cell().Background(bg).Padding(4).Text($"#{d.IdDossier}");
                            table.Cell().Background(bg).Padding(4).Text($"{d.MontantImpaye:F3} TND");
                            table.Cell().Background(bg).Padding(4).Text($"{d.FraisDossier:F3} TND");
                            table.Cell().Background(bg).Padding(4).Text($"{d.TauxInteret}%");

                            // Colonne retard : rouge si > 90j, vert sinon
                            table.Cell().Background(bg).Padding(4).Text($"{retard} j")
                                .FontColor(retard >= 90 ? Colors.Red.Medium : Colors.Green.Medium);

                            table.Cell().Background(bg).Padding(4).Text($"{interets:F3} TND");
                            table.Cell().Background(bg).Padding(4).Text($"{total:F3} TND").Bold(); // Total en gras
                        }
                    });

                    // ── PIED DE PAGE : mention légale + numéro de page ─────────────
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("STB BANK — Document confidentiel — Page ");
                        x.CurrentPageNumber(); // Numérotation automatique des pages
                    });
                });
            });

            // ── GÉNÉRATION ET RETOUR DU FICHIER PDF ───────────────────────────────
            byte[] pdfBytes = document.GeneratePdf(); // Convertit le document en tableau de bytes
            // Retourne le fichier en téléchargement avec un nom incluant la date du jour
            return File(pdfBytes, "application/pdf", $"STB_Impayes_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ================================================================
        // MÉTHODE PRIVÉE UTILITAIRE : CalculerJoursRetard
        //
        // Calcule le nombre de jours écoulés depuis la première échéance
        // impayée dont la date est dépassée.
        //
        // Utilisée par les deux endpoints ci-dessus pour éviter la duplication.
        // Retourne 0 si aucune échéance n'est en retard.
        // ================================================================
        private int CalculerJoursRetard(IEnumerable<Echeance> echeances)
        {
            // Filtre uniquement les échéances impayées dont la date est déjà passée
            var echeancesDepassees = echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now)
                .ToList();

            if (!echeancesDepassees.Any()) return 0; // Aucun retard → retourne 0

            // Retourne la durée entre la plus ancienne échéance impayée et aujourd'hui
            return (int)(DateTime.Now - echeancesDepassees.Min(e => e.DateEcheance)).TotalDays;
        }

    } 
}     