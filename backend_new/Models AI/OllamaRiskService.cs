using System.Text.Json;
using System.Text.Json.Serialization;
using RecouvrementAPI.DTOs;

namespace RecouvrementAPI.Models_AI
{
    public class OllamaRiskService
    {
        private readonly HttpClient _http;

        public OllamaRiskService(HttpClient http)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(180); // ← augmenté
        }

        public async Task<RiskScoreResult> CalculerScoreRisque(DossierRiskDto d)
        {
            int score = 0;

            score += d.MontantDu switch {
                > 50000 => 30,
                > 20000 => 20,
                > 5000  => 10,
                _       => 5
            };

            score += d.JoursRetard switch {
                > 180 => 25,
                > 90  => 18,
                > 30  => 10,
                _     => 3
            };

            score += Math.Min(d.EcheancesImpayees * 4, 20);

            score += d.Phase.ToLower() switch {
                "contentieux" => 15,
                "amiable"     => 8,
                "régularisé"  => 2,
                _             => 5
            };

            score += Math.Min(d.NombreRelances * 2, 10);

            string niveau = score switch {
                >= 80 => "critique",
                >= 60 => "élevé",
                >= 40 => "moyen",
                _     => "faible"
            };

            var prompt = $@"Tu es expert en recouvrement bancaire STB Bank Tunisie.
Score de risque calculé : {score}/100, niveau : {niveau}.
Données : montant dû {d.MontantDu} TND, {d.JoursRetard} jours de retard, 
{d.EcheancesImpayees} échéances impayées, phase {d.Phase}, {d.NombreRelances} relances.
Rédige une justification professionnelle en 2 phrases en français.
Réponds UNIQUEMENT avec ce JSON : {{""justification"": ""...""}}";

            var body = new { model = "llama3", prompt, stream = false };
            var response = await _http.PostAsJsonAsync("http://localhost:11434/api/generate", body);
            var raw = await response.Content.ReadFromJsonAsync<OllamaResponse>();

            string justification = "";
            try {
                var clean = raw!.Response.Replace("```json", "").Replace("```", "").Trim();
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(clean);
                justification = parsed?["justification"] ?? raw.Response;
            } catch {
                justification = raw!.Response;
            }

            return new RiskScoreResult { Score = score, Niveau = niveau, Justification = justification };
        }
    }

    public class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";
    }
}