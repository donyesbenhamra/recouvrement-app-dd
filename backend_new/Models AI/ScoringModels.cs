using Microsoft.ML.Data;

namespace RecouvrementAPI.Models.ML
{
    public class ScoringInput
    {
        public float Retard { get; set; }
        public float Historique { get; set; }
        public float Garantie { get; set; }
        public float Intention { get; set; }

        [ColumnName("Label")]
        public float Score { get; set; }
    }

    public class ScoringPrediction
    {
        [ColumnName("Score")]  // ✅ ML.NET Sdca output = "Score"
        public float Score { get; set; }
    }
}