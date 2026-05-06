using ClosedXML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;


namespace RecouvrementAPI.Controllers
{
    [ApiController]
    [Route("api/ClientList")]
    [Authorize]
    public class ClientListController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClientListController> _logger;

        public ClientListController(ApplicationDbContext context, ILogger<ClientListController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("gestion")]
        public async Task<ActionResult<ClientListResponseDto>> GetClientsGestion(
            [FromQuery] string statut = "Tous",
            [FromQuery] string agence = "Toutes",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.Dossiers
                    .Include(d => d.Client)
                        .ThenInclude(c => c.Agence)
                    .Include(d => d.Echeances)
                    .Where(d => d.Client.Statut != "Archivé")
                    .AsQueryable();

                var totalClients = await _context.Clients.CountAsync(c => c.Statut != "Archivé");
                var dossiersActifs = _context.Dossiers.Where(d => d.Client.Statut != "Archivé");

                var montantEmprunte = await dossiersActifs.SumAsync(d => d.MontantInitial);
                var contentieux = await dossiersActifs.CountAsync(d => d.StatutDossier == "contentieux");
                var amiable = await dossiersActifs.CountAsync(d => d.StatutDossier == "amiable");
                var regularise = await dossiersActifs.CountAsync(d => d.StatutDossier == "regularise");

                if (!string.IsNullOrEmpty(statut) && statut != "Tous")
                    query = query.Where(d => d.StatutDossier == statut.ToLower());

                if (!string.IsNullOrEmpty(agence) && agence != "Toutes")
                    query = query.Where(d => d.Client.Agence != null && d.Client.Agence.Ville == agence);

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var dossiers = await query
                    .OrderByDescending(d => d.DateCreation)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = dossiers.Select(d => new ClientListItemDto
                {
                    IdDossier = d.IdDossier,
                    Client = $"{d.Client.Nom} {d.Client.Prenom}",
                    Telephone = d.Client.Telephone,
                    Email = d.Client.Email,
                    Agence = d.Client.Agence?.Ville ?? "Inconnue",
                    TypeCredit = d.TypeEmprunt,
                    MontantDu = d.MontantImpaye,
                    Retard = CalculerJoursRetard(d.Echeances),
                    Statut = CapitalizeFirstLetter(d.StatutDossier)
                }).ToList();

                return Ok(new ClientListResponseDto
                {
                    Kpis = new ClientListKpiDto
                    {
                        TotalClients = totalClients,
                        MontantTotalEmprunte = montantEmprunte,
                        Contentieux = contentieux,
                        Amiable = amiable,
                        Regularise = regularise
                    },
                    Items = items,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue au rendu de la vue ClientList.");
                return StatusCode(500, new { message = "Erreur de chargement du module clients." });
            }
        }
          [HttpPut("{id}")]
public async Task<IActionResult> UpdateClient(int id, [FromBody] UpdateClientDto dto)
{
    var dossier = await _context.Dossiers
        .Include(d => d.Client)
        .FirstOrDefaultAsync(d => d.IdDossier == id);
    
    if (dossier == null) return NotFound();
    
    dossier.Client.Telephone = dto.Telephone;
    dossier.StatutDossier = dto.Statut.ToLower();
    
    await _context.SaveChangesAsync();
    return Ok(new { message = "Dossier mis à jour." });
}
        [HttpPost("create")]
        public async Task<IActionResult> CreatesClient([FromBody] CreateClientsDto dto)
        {
            try
            {
                // Vérifier doublon email
                var emailExiste = await _context.Clients
                    .AnyAsync(c => c.Email == dto.Email);

                if (emailExiste)
                    return BadRequest(new { message = "Un client avec cet email existe déjà." });

                // Trouver l'agence par ville
                var agence = await _context.Agences
                    .FirstOrDefaultAsync(a => a.Ville == dto.Agence);

                // Créer le client
                var client = new Client
                {
                    Nom = dto.Client.Split(' ').Last(),
                    Prenom = dto.Client.Split(' ').First(),
                    Telephone = dto.Telephone,
                    Email = dto.Email,
                    Agence = agence,
                    Statut = "Actif"
                };

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Créer le dossier lié
                var dossier = new DossierRecouvrement
                {
                    IdClient = client.IdClient,
                    TypeEmprunt = dto.TypeEmprunt,
                    MontantInitial = (decimal)dto.MontantDu,
                    MontantImpaye = (decimal)dto.MontantDu,
                    StatutDossier = dto.Retard > 90 ? "contentieux" : "amiable",
                    TauxInteret = 12,
                    FraisDossier = 0,
                    DateCreation = DateTime.Now
                };

                _context.Dossiers.Add(dossier);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Client créé avec succès", idClient = client.IdClient, idDossier = dossier.IdDossier });
            }
            catch (Exception ex)
            {
                Console.WriteLine("=== ERREUR CREATE CLIENT ===");
                Console.WriteLine("Message: " + ex.Message);
                Console.WriteLine("Inner: " + ex.InnerException?.Message);
                Console.WriteLine("Inner2: " + ex.InnerException?.InnerException?.Message);
                Console.WriteLine("============================");
                _logger.LogError(ex, "Erreur lors de la création du client.");
                return StatusCode(500, new
                {
                    message = ex.Message,
                    inner = ex.InnerException?.Message,
                    inner2 = ex.InnerException?.InnerException?.Message
                });
            }
        }

        private int CalculerJoursRetard(IEnumerable<Echeance> echeances)
        {
            var echeancesImpayeesDepassees = echeances
                .Where(e => e.Statut == "impaye" && e.DateEcheance < DateTime.Now)
                .ToList();

            if (!echeancesImpayeesDepassees.Any()) return 0;

            return (int)(DateTime.Now - echeancesImpayeesDepassees.Min(e => e.DateEcheance)).TotalDays;
        }

        private string CapitalizeFirstLetter(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return char.ToUpper(str[0]) + str.Substring(1).ToLower();
        }
    }
}