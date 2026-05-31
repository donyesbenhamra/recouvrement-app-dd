using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]  // ✅ Admin uniquement sur tout le controller
    public class UtilisateurController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UtilisateurController> _logger;

        public UtilisateurController(ApplicationDbContext context, ILogger<UtilisateurController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET /api/Utilisateur/gestion
        [HttpGet("gestion")]
        // ✅ [AllowAnonymous] supprimé
        public async Task<ActionResult<UtilisateurListResponseDto>> GetUtilisateurs(
            [FromQuery] string agence = "Toutes",
            [FromQuery] string role = "Tous",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var query = _context.UtilisateursBack
                    .Include(u => u.Agence)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(agence) && agence != "Toutes")
                    query = query.Where(u => u.Agence != null && u.Agence.Ville == agence);

                if (!string.IsNullOrEmpty(role) && role != "Tous")
                    query = query.Where(u => u.Role == role);

                int totalItems = await query.CountAsync();
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var users = await query
                    .OrderBy(u => u.Nom)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var items = users.Select(u => new UtilisateurItemDto
                {
                    IdAgent = u.IdAgent,
                    NomComplet = $"{u.Nom} {u.Prenom}",
                    Email = u.Email,
                    Telephone = u.Telephone ?? "Non renseigné",
                    Role = u.Role,
                    IdAgence = u.IdAgence,
                    Agence = u.Agence?.Ville ?? "Siège",
                    DerniereConnexion = FormatDerniereConnexion(u.DerniereConnexion),
                    Statut = u.Statut ?? "Actif"
                }).ToList();

                return Ok(new UtilisateurListResponseDto
                {
                    Items = items,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    CurrentPage = page
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur au chargement de la liste des utilisateurs.");
                return StatusCode(500, new { message = "Impossible d'accéder à la liste du personnel." });
            }
        }

        // POST /api/Utilisateur
        [HttpPost]
        public async Task<IActionResult> CreateUtilisateur([FromBody] CreateUtilisateurDto dto)
        {
            try
            {
                if (await _context.UtilisateursBack.AnyAsync(u => u.Email == dto.Email))
                    return BadRequest(new { message = "L'adresse email est déjà utilisée." });

                var nouveau = new UtilisateurBack
                {
                    Nom = dto.Nom,
                    Prenom = dto.Prenom,
                    Email = dto.Email,
                    Telephone = dto.Telephone,
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword(dto.MotDePasse),
                    Role = dto.Role,
                    IdAgence = dto.IdAgence,
                    Statut = "Actif"
                };

                _context.UtilisateursBack.Add(nouveau);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Utilisateur créé avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur création utilisateur.");
                return StatusCode(500, new { message = "Impossible d'insérer l'utilisateur." });
            }
        }

        // PUT /api/Utilisateur/{id}/statut
        [HttpPut("{id}/statut")]
        public async Task<IActionResult> ToggleStatut(int id)
        {
            try
            {
                var user = await _context.UtilisateursBack.FindAsync(id);
                if (user == null) return NotFound(new { message = "Agent introuvable." });

                user.Statut = user.Statut == "Actif" ? "Inactif" : "Actif";
                await _context.SaveChangesAsync();

                return Ok(new { message = $"Le compte agent est désormais {user.Statut}." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur statut agent.");
                return StatusCode(500, new { message = "La mise à jour du statut a échouée." });
            }
        }

        // PUT /api/Utilisateur/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUtilisateur(int id, [FromBody] UpdateUtilisateurDto dto)
        {
            try
            {
                var user = await _context.UtilisateursBack.FindAsync(id);
                if (user == null) return NotFound(new { message = "Agent introuvable." });

                // Vérification email unique si changé
                if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
                {
                    if (await _context.UtilisateursBack.AnyAsync(u => u.Email == dto.Email && u.IdAgent != id))
                        return BadRequest(new { message = "Cet email existe déjà." });
                    user.Email = dto.Email;
                }

                if (!string.IsNullOrEmpty(dto.Nom)) user.Nom = dto.Nom;
                if (!string.IsNullOrEmpty(dto.Prenom)) user.Prenom = dto.Prenom;
                if (!string.IsNullOrEmpty(dto.Telephone)) user.Telephone = dto.Telephone;
                if (!string.IsNullOrEmpty(dto.Role)) user.Role = dto.Role;
                user.IdAgence = dto.IdAgence;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Modifications enregistrées avec succès." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur d'édition agent.");
                return StatusCode(500, new { message = "La modification a échouée." });
            }
        }

        private string FormatDerniereConnexion(DateTime? dateConnexion)
        {
            if (!dateConnexion.HasValue) return "Jamais";

            var nbJours = (DateTime.Now.Date - dateConnexion.Value.Date).Days;
            var heure = dateConnexion.Value.ToString("HH:mm");

            return nbJours switch
            {
                0 => $"Aujourd'hui {heure}",
                1 => $"Hier {heure}",
                _ => $"Il y a {nbJours} jours"
            };
        }
    }
}