using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RecouvrementAPI.Data;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RecouvrementAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Tentative de connexion pour l'utilisateur : {request.Email}");

                // 1. Recherche de l'utilisateur par email
                var agent = await _context.UtilisateursBack
                    .FirstOrDefaultAsync(u => u.Email == request.Email);

                if (agent == null)
                {
                    _logger.LogWarning($"Connexion échouée : Utilisateur introuvable ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 2. Vérification du mot de passe
                bool isPasswordValid = false;

                if (agent.MotDePasse.StartsWith("$2a$") || agent.MotDePasse.StartsWith("$2y$") || agent.MotDePasse.StartsWith("$2b$"))
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.MotDePasse, agent.MotDePasse);
                }
                else
                {
                    isPasswordValid = (agent.MotDePasse == request.MotDePasse);
                }

                if (!isPasswordValid)
                {
                    _logger.LogWarning($"Connexion échouée : Mot de passe invalide ({request.Email})");
                    return Unauthorized(new { message = "Email ou mot de passe incorrect." });
                }

                // 3. ✅ Vérification du statut du compte
                if (agent.Statut != "Actif")
                {
                    _logger.LogWarning($"Connexion refusée : Compte suspendu ({request.Email})");
                    return Unauthorized(new { message = "Votre compte est suspendu. Contactez l'administrateur." });
                }

                // 4. Récupération des clés JWT
                var jwtKey = _configuration["Jwt:Key"];
                var jwtIssuer = _configuration["Jwt:Issuer"];

                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("Erreur Critique : La clé secrète JWT n'est pas configurée.");
                    return StatusCode(500, new { message = "Erreur interne du serveur de configuration." });
                }

                // 5. Création du Token JWT
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtKey);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new Claim[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, agent.IdAgent.ToString()),
                        new Claim(ClaimTypes.Email, agent.Email),
                        new Claim(ClaimTypes.Role, agent.Role ?? "Utilisateur")
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = jwtIssuer,
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                // 6. Mise à jour de la dernière connexion
                agent.DerniereConnexion = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Connexion réussie pour l'agent {agent.IdAgent} ({agent.Nom})");

                return Ok(new LoginResponseDto
                {
                    Token = tokenString,
                    AgentId = agent.IdAgent,
                    Nom = agent.Nom,
                    Prenom = agent.Prenom,
                    Role = agent.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Une erreur inattendue s'est produite lors de la connexion.");
                return StatusCode(500, new { message = "Une erreur interne s'est produite. Veuillez contacter l'administrateur." });
            }
        }

        [HttpPost("seed-admin")]
        public async Task<IActionResult> SeedAdmin()
        {
            try
            {
                if (await _context.UtilisateursBack.AnyAsync(u => u.Email == "admin@stb.tn"))
                    return BadRequest(new { message = "Un administrateur existe déjà dans la base de données." });

                var newAdmin = new UtilisateurBack
                {
                    Nom = "Admin",
                    Prenom = "STB",
                    Email = "admin@stb.tn",
                    MotDePasse = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = "Admin",
                    Statut = "Actif"
                };

                _context.UtilisateursBack.Add(newAdmin);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Agent administrateur créé avec succès. (Email: admin@stb.tn | Password: admin123)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la génération de l'Admin par défaut.");
                return StatusCode(500, new { message = "Erreur interne lors de l'insertion." });
            }
        }
    }
}