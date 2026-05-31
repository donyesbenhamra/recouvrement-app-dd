using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable enable

namespace RecouvrementAPI.Models
{
    [Table("score_risque")]
    public class ScoreRisque
    {
        [Key]
        [Column("id_score")]
        public int IdScore { get; set; }

        [Column("id_dossier")]
        public int IdDossier { get; set; }

        [Column("valeur")]
        public decimal Valeur { get; set; }

        [Column("points_retard")]
        public int PointsRetard { get; set; }

        [Column("points_historique")]
        public int PointsHistorique { get; set; }

        [Column("points_garantie")]
        public int PointsGarantie { get; set; }

        [Column("points_intention")]
        public int PointsIntention { get; set; }

        [Column("niveau")]
        public string Niveau { get; set; }

        [Column("date_calcul")]
        public DateTime DateCalcul { get; set; }

        [Column("recommandation")]
        public string? Recommandation { get; set; }

        [Column("prob_faible")]
        public decimal ProbFaible { get; set; }

        [Column("prob_moyen")]
        public decimal ProbMoyen { get; set; }

        [Column("prob_eleve")]
        public decimal ProbEleve { get; set; }

        public DossierRecouvrement Dossier { get; set; }
    }
}