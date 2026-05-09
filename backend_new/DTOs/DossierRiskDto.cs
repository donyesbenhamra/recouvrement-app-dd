namespace RecouvrementAPI.DTOs
{
    public class DossierRiskDto
    {
        public decimal MontantDu { get; set; }
        public int EcheancesImpayees { get; set; }
        public int JoursRetard { get; set; }
        public string Phase { get; set; } = "";
        public int NombreRelances { get; set; }
        public int IntentionsAnnulees { get; set; }
    }

    public class RiskScoreResult
    {
        public int Score { get; set; }
        public string Niveau { get; set; } = "";
        public string Justification { get; set; } = "";
    }
}