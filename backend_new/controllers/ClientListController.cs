using ClosedXML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;

namespace RecouvrementAPI.Controllers
{
    // Indique que ce contrôleur est une API REST
    [ApiController]

    // Route principale de l'API : api/ClientList
    [Route("api/ClientList")]

    // Oblige l'utilisateur à être authentifié
    [Authorize]
    public class ClientListController : ControllerBase
    {
        // Contexte de la base de données
        private readonly ApplicationDbContext _context;

        // Service de logs pour enregistrer les erreurs
        private readonly ILogger<ClientListController> _logger;

        // Constructeur avec injection de dépendances
        public ClientListController(ApplicationDbContext context, ILogger<ClientListController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Endpoint GET : api/ClientList/gestion
        [HttpGet("gestion")]
        public async Task<ActionResult<ClientListResponseDto>> GetClientsGestion(
            [FromQuery] string statut = "Tous",
            [FromQuery] string agence = "Toutes",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                // Récupération des dossiers avec les informations liées
                var query = _context.Dossiers

                    // Charger les informations du client
                    .Include(d => d.Client)

                        // Charger l'agence du client
                        .ThenInclude(c => c.Agence)

                    // Charger les échéances
                    .Include(d => d.Echeances)

                    // Exclure les clients archivés
                    .Where(d => d.Client.Statut != "Archivé")

                    // Transformer en requête modifiable
                    .AsQueryable();

                // Nombre total de clients actifs
                var totalClients = await _context.Clients
                    .CountAsync(c => c.Statut != "Archivé");

                // Récupération des dossiers actifs
                var dossiersActifs = _context.Dossiers
                    .Where(d => d.Client.Statut != "Archivé");

                // Calcul du montant total emprunté
                var montantEmprunte = await dossiersActifs
                    .SumAsync(d => d.MontantInitial);

                // Nombre de dossiers contentieux
                var contentieux = await dossiersActifs
                    .CountAsync(d => d.StatutDossier == "contentieux");

                // Nombre de dossiers amiables
                var amiable = await dossiersActifs
                    .CountAsync(d => d.StatutDossier == "amiable");

                // Nombre de dossiers régularisés
                var regularise = await dossiersActifs
                    .CountAsync(d => d.StatutDossier == "regularise");

                // Filtre par statut si sélectionné
                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                    query = query.Where(d => d.StatutDossier == statut.ToLower());

                // Filtre par agence si sélectionnée
                if (!string.IsNullOrEmpty(agence) && agence != "Toutes")
                    query = query.Where(d =>
                        d.Client.Agence != null &&
                        d.Client.Agence.Ville == agence);

                // Nombre total d'éléments après filtrage
                int totalItems = await query.CountAsync();

                // Calcul du nombre total de pages
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                // Pagination + tri par date de création décroissante
                var dossiers = await query
                    .OrderByDescending(d => d.DateCreation)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Transformation des données en DTO
                var items = dossiers.Select(d => new ClientListItemDto
                {
                    // ID du dossier
                    IdDossier = d.IdDossier,

                    // Nom complet du client
                    Client = $"{d.Client.Nom} {d.Client.Prenom}",

                    // Téléphone du client
                    Telephone = d.Client.Telephone,

                    // Email du client
                    Email = d.Client.Email,

                    // Ville de l'agence
                    Agence = d.Client.Agence?.Ville ?? "Inconnue",

                    // Type de crédit
                    TypeCredit = d.TypeEmprunt,

                    // Montant impayé
                    MontantDu = d.MontantImpaye,

                    // Calcul du retard
                    Retard = CalculerJoursRetard(d.Echeances),

                    // Mise en forme du statut
                    Statut = CapitalizeFirstLetter(d.StatutDossier)

                }).ToList();

                // Retourner la réponse finale
                return Ok(new ClientListResponseDto
                {
                    // KPIs affichés dans le dashboard
                    Kpis = new ClientListKpiDto
                    {
                        TotalClients = totalClients,
                        MontantTotalEmprunte = montantEmprunte,
                        Contentieux = contentieux,
                        Amiable = amiable,
                        Regularise = regularise
                    },

                    // Liste des clients
                    Items = items,

                    // Pagination
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                // Enregistrer l'erreur dans les logs
                _logger.LogError(ex, "Erreur inattendue au rendu de la vue ClientList.");

                // Retourner une erreur serveur
                return StatusCode(500, new
                {
                    message = "Erreur de chargement du module clients."
                });
            }
        }

        // Endpoint PUT : mise à jour d'un dossier/client
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(
            int id,
            [FromBody] UpdateClientDto dto)
        {
            // Rechercher le dossier avec le client associé
            var dossier = await _context.Dossiers
                .Include(d => d.Client)
                .FirstOrDefaultAsync(d => d.IdDossier == id);

            // Vérifier si le dossier existe
            if (dossier == null)
                return NotFound();

            // Mise à jour du téléphone
            dossier.Client.Telephone = dto.Telephone;

            // Mise à jour du statut du dossier
            dossier.StatutDossier = dto.Statut.ToLower();

            // Sauvegarde dans la base
            await _context.SaveChangesAsync();

            // Réponse succès
            return Ok(new
            {
                message = "Dossier mis à jour."
            });
        }

        // Endpoint POST : création d'un client
        [HttpPost("create")]
        public async Task<IActionResult> CreatesClient(
            [FromBody] CreateClientsDto dto)
        {
            try
            {
                // Vérifier si l'email existe déjà
                var emailExiste = await _context.Clients
                    .AnyAsync(c => c.Email == dto.Email);

                // Retourner une erreur si doublon
                if (emailExiste)
                    return BadRequest(new
                    {
                        message = "Un client avec cet email existe déjà."
                    });

                // Rechercher l'agence par ville
                var agence = await _context.Agences
                    .FirstOrDefaultAsync(a => a.Ville == dto.Agence);

                // Création du client
                var client = new Client
                {
                    // Nom du client
                    Nom = dto.Client.Split(' ').Last(),

                    // Prénom du client
                    Prenom = dto.Client.Split(' ').First(),

                    // Téléphone
                    Telephone = dto.Telephone,

                    // Email
                    Email = dto.Email,

                    // Association avec l'agence
                   Agence = agence ?? throw new Exception($"Agence '{dto.Agence}' introuvable."),
                    // Statut du client
                    Statut = "Actif"
                };

                // Ajouter le client dans la base
                _context.Clients.Add(client);

                // Sauvegarder pour générer l'ID
                await _context.SaveChangesAsync();

                // Création du dossier de recouvrement
                var dossier = new DossierRecouvrement
                {
                    // ID du client associé
                    IdClient = client.IdClient,

                    // Type d'emprunt
                    TypeEmprunt = dto.TypeEmprunt,

                    // Montant initial du crédit
                    MontantInitial = (decimal)dto.MontantDu,

                    // Montant impayé
                    MontantImpaye = (decimal)dto.MontantDu,

                    // Détermination automatique du statut
                    StatutDossier = dto.Retard > 90
                        ? "contentieux"
                        : "amiable",

                    // Taux d'intérêt
                    TauxInteret = 12,

                    // Frais de dossier
                    FraisDossier = 0,

                    // Date de création
                    DateCreation = DateTime.Now
                };

                // Ajouter le dossier
                _context.Dossiers.Add(dossier);

                // Sauvegarder dans la base
                await _context.SaveChangesAsync();

                // Retour succès
                return Ok(new
                {
                    message = "Client créé avec succès",
                    idClient = client.IdClient,
                    idDossier = dossier.IdDossier
                });
            }
            catch (Exception ex)
            {
                // Affichage détaillé des erreurs dans la console
                Console.WriteLine("=== ERREUR CREATE CLIENT ===");
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("Inner: " + ex.InnerException?.Message);
                Console.WriteLine("Inner2: " + ex.InnerException?.InnerException?.Message);
                Console.WriteLine("============================");

                // Log de l'erreur
                _logger.LogError(ex, "Erreur lors de la création du client.");

                // Retour erreur serveur
                return StatusCode(500, new
                {
                    message = ex.Message,
                    inner = ex.InnerException?.Message,
                    inner2 = ex.InnerException?.InnerException?.Message
                });
            }
        }

        // Méthode privée pour calculer les jours de retard
        private int CalculerJoursRetard(IEnumerable<Echeance> echeances)
        {
            // Récupérer les échéances impayées dépassées
            var echeancesImpayeesDepassees = echeances
                .Where(e =>
                    e.Statut == "impaye" &&
                    e.DateEcheance < DateTime.Now)
                .ToList();

            // Aucun retard
            if (!echeancesImpayeesDepassees.Any())
                return 0;

            // Calcul du nombre de jours depuis la plus ancienne échéance
            return (int)(
                DateTime.Now -
                echeancesImpayeesDepassees.Min(e => e.DateEcheance)
            ).TotalDays;
        }

        // Méthode privée pour mettre la première lettre en majuscule
        private string CapitalizeFirstLetter(string str)
        {
            // Vérifier si la chaîne est vide
            if (string.IsNullOrEmpty(str))
                return str;

            // Mettre la première lettre en majuscule
            return char.ToUpper(str[0]) +
                   str.Substring(1).ToLower();
        }
    }
}