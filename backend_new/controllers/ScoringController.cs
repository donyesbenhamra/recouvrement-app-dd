#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScoringController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScoringController> _logger;
        private readonly HttpClient _httpClient;

        public ScoringController(ApplicationDbContext context, ILogger<ScoringController> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient("FastAPI");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DTOs internes pour communiquer avec FastAPI
        // ══════════════════════════════════════════════════════════════════════

        private class FastApiRequest
        {
            [JsonPropertyName("montant_initial")]          public double MontantInitial        { get; set; }
            [JsonPropertyName("montant_impaye")]           public double MontantImpaye          { get; set; }
            [JsonPropertyName("frais_dossier")]            public double FraisDossier           { get; set; }
            [JsonPropertyName("taux_interet")]             public double TauxInteret            { get; set; }
            [JsonPropertyName("confiance_client")]         public double ConfianceClient        { get; set; }
            [JsonPropertyName("type_emprunt")]             public string TypeEmprunt            { get; set; } = "";
            [JsonPropertyName("statut_dossier")]           public string StatutDossier          { get; set; } = "";
            [JsonPropertyName("date_creation")]            public string DateCreation           { get; set; } = "";
            [JsonPropertyName("nb_echeances_total")]       public int    NbEcheancesTotal       { get; set; }
            [JsonPropertyName("nb_echeances_impayees")]    public int    NbEcheancesImpayees    { get; set; }
            [JsonPropertyName("moyenne_jours_retard")]     public double MoyenneJoursRetard     { get; set; }
            [JsonPropertyName("montant_total_du")]         public double MontantTotalDu         { get; set; }
            [JsonPropertyName("nb_garanties")]             public int    NbGaranties            { get; set; }
            [JsonPropertyName("nb_actions")]               public int    NbActions              { get; set; }
            [JsonPropertyName("nb_intentions")]            public int    NbIntentions           { get; set; }
            [JsonPropertyName("montant_propose_moyen")]    public double MontantProposeMoyen    { get; set; }
            [JsonPropertyName("confiance_intention_moy")]  public double ConfianceIntentionMoy  { get; set; }
        }

        private class FastApiResponse
        {
            [JsonPropertyName("score_numerique")]   public int    ScoreNumerique   { get; set; }
            [JsonPropertyName("categorie_risque")]  public string CategorieRisque  { get; set; } = "";
            [JsonPropertyName("recommandation")]    public string Recommandation   { get; set; } = "";
            [JsonPropertyName("probabilites")]      public Dictionary<string, double> Probabilites { get; set; } = new();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DASHBOARD
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet("dashboard")]
        public async Task<ActionResult<ScoringDashboardResponseDto>> GetDashboard(
            [FromQuery] string etatDossier = "Tous",
            [FromQuery] string recherche = "",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Dossiers
                    .Include(d => d.Client)
                    .Include(d => d.ScoresRisque)
                    .Include(d => d.Echeances)
                    .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                    .AsQueryable();

                if (!string.IsNullOrEmpty(etatDossier) && etatDossier != "Tous")
                    query = query.Where(d => d.StatutDossier == etatDossier.ToLower());

                if (!string.IsNullOrEmpty(recherche))
                    query = query.Where(d =>
                        d.Client.Nom.Contains(recherche) ||
                        d.Client.Prenom.Contains(recherche) ||
                        d.IdDossier.ToString().Contains(recherche));

                var tousDossiers = await query.ToListAsync();

                var dossiersScores = tousDossiers
                    .Where(d => d.ScoresRisque != null && d.ScoresRisque.Any())
                    .Select(d => new
                    {
                        Dossier = d,
                        DernierScore = d.ScoresRisque.OrderByDescending(s => s.DateCalcul).First()
                    })
                    .ToList();

                int risqueEleve  = dossiersScores.Count(x => x.DernierScore.Valeur > 60);
                int risqueMoyen  = dossiersScores.Count(x => x.DernierScore.Valeur >= 30 && x.DernierScore.Valeur <= 60);
                int risqueFaible = dossiersScores.Count(x => x.DernierScore.Valeur < 30);

                var items = dossiersScores
                    .OrderByDescending(x => x.DernierScore.Valeur)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(x => new ScoringItemDto
                    {
                        IdDossier        = x.Dossier.IdDossier,
                        Client           = $"{x.Dossier.Client.Nom} {x.Dossier.Client.Prenom.Substring(0, 1)}.",
                        RetardTexte      = GetRetardLabel(CalculerRetardJours(x.Dossier.Echeances)),
                        PointsRetard     = x.DernierScore.PointsRetard,
                        PointsHistorique = x.DernierScore.PointsHistorique,
                        PointsGarantie   = x.DernierScore.PointsGarantie,
                        PointsIntention  = x.DernierScore.PointsIntention,
                        ScoreTotal       = x.DernierScore.Valeur,
                        Niveau           = x.DernierScore.Niveau
                    })
                    .ToList();

                var topScore = dossiersScores.OrderByDescending(x => x.DernierScore.Valeur).FirstOrDefault();
                ScoringDetailsDto? detailsActif = null;
                if (topScore != null)
                    detailsActif = ConstruireDetailsDto(topScore.DernierScore, topScore.Dossier);

                return Ok(new ScoringDashboardResponseDto
                {
                    Kpis = new ScoringKpiDto
                    {
                        DossiersScores = dossiersScores.Count,
                        RisqueEleve    = risqueEleve,
                        RisqueMoyen    = risqueMoyen,
                        RisqueFaible   = risqueFaible
                    },
                    Items       = items,
                    TotalItems  = dossiersScores.Count,
                    TotalPages  = (int)Math.Ceiling(dossiersScores.Count / (double)pageSize),
                    CurrentPage = page,
                    DetailActif = detailsActif
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au rendu du Scoring Dashboard.");
                return StatusCode(500, new { message = "Erreur de chargement du moteur IA." });
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DETAILS D'UN DOSSIER
        // ══════════════════════════════════════════════════════════════════════

        [HttpGet("{id}/details")]
        public async Task<ActionResult<ScoringDetailsDto>> GetScoringDetails(int id)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                .Include(d => d.ScoresRisque)
                .Include(d => d.Echeances)
                .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                .FirstOrDefaultAsync(d => d.IdDossier == id);

            if (dossier == null || !dossier.ScoresRisque.Any())
                return NotFound(new { message = "Dossier ou score introuvable" });

            var dernierScore = dossier.ScoresRisque.OrderByDescending(s => s.DateCalcul).First();
            return Ok(ConstruireDetailsDto(dernierScore, dossier));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RECALCUL GLOBAL
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost("recalculer-tous")]
        public async Task<IActionResult> RecalculerTous()
        {
            var dossiersId = await _context.Dossiers
                .Where(d => d.StatutDossier != "regularise")
                .Select(d => d.IdDossier)
                .ToListAsync();

            int succes = 0, echecs = 0;
            foreach (var id in dossiersId)
            {
                bool ok = await RunScoringAlgorithm(id);
                if (ok) succes++; else echecs++;
                
            }

            return Ok(new { message = $"Recalcul effectué : {succes} succès, {echecs} échecs." });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RECALCUL D'UN SEUL DOSSIER
        // ══════════════════════════════════════════════════════════════════════

        [HttpPost("{id}/recalculer")]
        public async Task<IActionResult> RecalculerDossier(int id)
        {
            bool ok = await RunScoringAlgorithm(id);
            if (!ok) return StatusCode(503, new { message = "Service IA indisponible. Vérifiez que FastAPI tourne sur le port 8000." });
           return Ok(new { message = "Score de risque mis à jour avec succès." });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ALGORITHME PRINCIPAL — APPEL FASTAPI + XGBOOST
        // ══════════════════════════════════════════════════════════════════════

        private async Task<bool> RunScoringAlgorithm(int idDossier)
        {
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                    .ThenInclude(c => c.Dossiers)
                        .ThenInclude(cd => cd.Echeances)
                .Include(d => d.Echeances)
                .Include(d => d.Garanties)
                .Include(d => d.HistoriqueActions)
                .Include(d => d.Intentions.OrderByDescending(i => i.DateIntention))
                .FirstOrDefaultAsync(d => d.IdDossier == idDossier);

            if (dossier == null) return false;

            // ── Agrégation des données ────────────────────────────────────────
            var echeances         = dossier.Echeances.ToList();
            var echeancesImpayees = echeances.Where(e => e.Statut == "impaye").ToList();
            var intentions        = dossier.Intentions.ToList();
            var actions           = dossier.HistoriqueActions?.ToList() ?? new List<HistoriqueAction>();

            double moyenneJoursRetard = echeancesImpayees.Any()
                ? echeancesImpayees.Average(e => e.NombreJoursRetard) : 0;

            double montantTotalDu = echeancesImpayees.Any()
                ? (double)echeancesImpayees.Sum(e => e.MontantDu) : 0;

            var intentionsAvecMontant = intentions.Where(i => i.MontantPropose.HasValue).ToList();
            double montantProposeMoyen = intentionsAvecMontant.Any()
                ? (double)intentionsAvecMontant.Average(i => i.MontantPropose ?? 0) : 0;

            double confianceIntentionMoy = intentions.Any()
                ? (double)intentions.Average(i => i.ConfianceClient) : 0;

            // ── Construction de la requête FastAPI ────────────────────────────
            var request = new FastApiRequest
            {
                MontantInitial        = (double)dossier.MontantInitial,
                MontantImpaye         = (double)dossier.MontantImpaye,
                FraisDossier          = (double)dossier.FraisDossier,
                TauxInteret           = (double)dossier.TauxInteret,
                ConfianceClient       = (double)dossier.ConfianceClient,
               TypeEmprunt = NormaliserTypeEmprunt(dossier.TypeEmprunt),
                StatutDossier         = dossier.StatutDossier ?? "amiable",
                DateCreation          = dossier.DateCreation.ToString("yyyy-MM-dd"),
                NbEcheancesTotal      = echeances.Count,
                NbEcheancesImpayees   = echeancesImpayees.Count,
                MoyenneJoursRetard    = moyenneJoursRetard,
                MontantTotalDu        = montantTotalDu,
                NbGaranties           = dossier.Garanties?.Count ?? 0,
                NbActions             = actions.Count,
                NbIntentions          = intentions.Count,
                MontantProposeMoyen   = montantProposeMoyen,
                ConfianceIntentionMoy = confianceIntentionMoy
            };
            

            // ── Appel FastAPI ─────────────────────────────────────────────────
            FastApiResponse? iaResponse = null;
            try
            {
               var response = await _httpClient.PostAsJsonAsync("/score", request);
if (response.IsSuccessStatusCode)
    iaResponse = await response.Content.ReadFromJsonAsync<FastApiResponse>();
else
{
    var err = await response.Content.ReadAsStringAsync();
    _logger.LogError("FastAPI 400 dossier {Id}: {Err}", idDossier, err);
}
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FastAPI indisponible pour dossier {Id}, fallback rule-based.", idDossier);
            }

            // ── Calcul du score ───────────────────────────────────────────────
            int scoreCalcule;
            string niveau;
            string recommandation;
            decimal probFaible = 0, probMoyen = 0, probEleve = 0;

            if (iaResponse != null)
            {
                scoreCalcule   = iaResponse.ScoreNumerique;
                niveau         = NormaliserNiveau(iaResponse.CategorieRisque);
                recommandation = iaResponse.Recommandation;
                probFaible     = (decimal)iaResponse.Probabilites.GetValueOrDefault("Faible", 0);
                probMoyen      = (decimal)iaResponse.Probabilites.GetValueOrDefault("Moyen",  0);
                probEleve      = (decimal)iaResponse.Probabilites.GetValueOrDefault("Eleve",  0);
            }
            else
            {
                int retardJours   = CalculerRetardJours(echeances);
                int ptsRetard     = retardJours == 0 ? 0 : retardJours < 30 ? 10 : retardJours <= 90 ? 20 : 30;
                int ptsHistorique = echeancesImpayees.Count == 0 ? 0 : echeancesImpayees.Count <= 2 ? 10 : echeancesImpayees.Count <= 5 ? 20 : 25;
                int ptsGarantie   = !dossier.Garanties.Any() ? 25 : dossier.Garanties.Any(g => g.TypeGarantie == "hypotheque") ? 0 : 10;
                var di            = intentions.FirstOrDefault();
                int ptsIntention  = di == null ? 15 : di.TypeIntention == "paiement_immediat" ? 0 : di.TypeIntention == "promesse_paiement" ? 5 : 10;
                scoreCalcule      = Math.Clamp(ptsRetard + ptsHistorique + ptsGarantie + ptsIntention, 0, 100);
                niveau            = scoreCalcule >= 60 ? "Élevé" : scoreCalcule >= 30 ? "Moyen" : "Faible";
                recommandation    = GenererTexteRecommandationFallback(niveau);
            }

            // ── Points détaillés pour affichage dashboard ─────────────────────
            int retardJoursDisplay  = CalculerRetardJours(echeances);
            int ptsRetardDisplay    = retardJoursDisplay == 0 ? 0 : retardJoursDisplay < 30 ? 10 : retardJoursDisplay <= 90 ? 20 : 30;
            int ptsHistDisplay      = echeancesImpayees.Count == 0 ? 0 : echeancesImpayees.Count <= 2 ? 10 : echeancesImpayees.Count <= 5 ? 20 : 25;
            int ptsGarantieDisplay  = !dossier.Garanties.Any() ? 25 : dossier.Garanties.Any(g => g.TypeGarantie == "hypotheque") ? 0 : 10;
            var lastInt             = intentions.FirstOrDefault();
            int ptsIntentionDisplay = lastInt == null ? 15 : lastInt.TypeIntention == "paiement_immediat" ? 0 : lastInt.TypeIntention == "promesse_paiement" ? 5 : 10;

            // ── Sauvegarde en base ────────────────────────────────────────────
            var existant = await _context.ScoresRisque
                .FirstOrDefaultAsync(s => s.IdDossier == idDossier);

            if (existant != null)
            {
                existant.Valeur           = scoreCalcule;
                existant.Niveau           = niveau;
                existant.PointsRetard     = ptsRetardDisplay;
                existant.PointsHistorique = ptsHistDisplay;
                existant.PointsGarantie   = ptsGarantieDisplay;
                existant.PointsIntention  = ptsIntentionDisplay;
                existant.Recommandation   = recommandation;
                existant.ProbFaible       = probFaible;
                existant.ProbMoyen        = probMoyen;
                existant.ProbEleve        = probEleve;
                existant.DateCalcul       = DateTime.Now;
            }
            else
            {
                _context.ScoresRisque.Add(new ScoreRisque
                {
                    IdDossier        = idDossier,
                    Valeur           = scoreCalcule,
                    Niveau           = niveau,
                    PointsRetard     = ptsRetardDisplay,
                    PointsHistorique = ptsHistDisplay,
                    PointsGarantie   = ptsGarantieDisplay,
                    PointsIntention  = ptsIntentionDisplay,
                    Recommandation   = recommandation,
                    ProbFaible       = probFaible,
                    ProbMoyen        = probMoyen,
                    ProbEleve        = probEleve,
                    DateCalcul       = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private string NormaliserNiveau(string categorie) => categorie switch
        {
            "Eleve"  => "Élevé",
            "Moyen"  => "Moyen",
            "Faible" => "Faible",
            _        => "Moyen"
        };

        private string GenererTexteRecommandationFallback(string niveau) => niveau switch
        {
            "Élevé" => "Risque élevé. Escalader vers le service contentieux et engager une procédure juridique immédiatement.",
            "Moyen" => "Risque modéré. Effectuer un appel téléphonique, envoyer une mise en demeure et surveiller de près.",
            _       => "Risque faible. Envoyer une relance amiable par email ou SMS et proposer un échéancier adapté."
        };

        private ScoringDetailsDto ConstruireDetailsDto(ScoreRisque score, DossierRecouvrement dossier)
        {
            var derniereInt  = dossier.Intentions.FirstOrDefault();
            var intentionStr = derniereInt != null ? derniereInt.TypeIntention.Replace("_", " ") : "Non spécifié";

            return new ScoringDetailsDto
            {
                ClientNom        = $"{dossier.Client.Nom} {dossier.Client.Prenom}",
                ScoreTotal       = score.Valeur,
                ConfianceIa      = derniereInt?.ConfianceClient ?? 0,
                DetailRetard     = GetRetardLabel(CalculerRetardJours(dossier.Echeances)),
                PtsRetard        = score.PointsRetard,
                DetailHistorique = score.PointsHistorique >= 20 ? "Retards fréquents" : "Retards moyens/faibles",
                PtsHistorique    = score.PointsHistorique,
                DetailGarantie   = score.PointsGarantie == 25 ? "Aucune garantie" : "Garantie moyenne/Forte",
                PtsGarantie      = score.PointsGarantie,
                DetailIntention  = char.ToUpper(intentionStr[0]) + intentionStr.Substring(1),
                PtsIntention     = score.PointsIntention,
                Recommandation   = score.Recommandation ?? GenererTexteRecommandationFallback(score.Niveau),
                DateCalcul       = score.DateCalcul.ToString("dd MMMM yyyy"),
                ProbFaible       = score.ProbFaible,
                ProbMoyen        = score.ProbMoyen,
                ProbEleve        = score.ProbEleve
            };
        }

        private int CalculerRetardJours(IEnumerable<Echeance> echeances)
        {
            var impayees = echeances.Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now).ToList();
            if (!impayees.Any()) return 0;
            return (int)(DateTime.Now - impayees.Min(e => e.DateEcheance)).TotalDays;
        }

        private string GetRetardLabel(int jours)
        {
            if (jours == 0) return "Aucun retard";
            if (jours < 30) return $"{jours} jours";
            return $"{jours / 30} mois";
        }
        private string NormaliserTypeEmprunt(string? type)
{
    if (string.IsNullOrEmpty(type)) return "personnel";
    var t = type.ToLower();
    if (t.Contains("immobilier")) return "immobilier";
    if (t.Contains("auto"))       return "automobile";
    if (t.Contains("conso"))      return "personnel";
    if (t.Contains("profes"))     return "professionnel";
    return "personnel";
}
    }
}