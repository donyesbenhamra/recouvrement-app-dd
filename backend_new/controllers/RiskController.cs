using Microsoft.AspNetCore.Mvc;
using RecouvrementAPI.DTOs;
using RecouvrementAPI.Models_AI;

namespace RecouvrementAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RiskController : ControllerBase
    {
        private readonly OllamaRiskService _risk;

        public RiskController(OllamaRiskService risk) => _risk = risk;

        [HttpPost("score")]
        public async Task<IActionResult> GetScore([FromBody] DossierRiskDto dto)
        {
            var result = await _risk.CalculerScoreRisque(dto);
            return Ok(result);
        }
    }
}