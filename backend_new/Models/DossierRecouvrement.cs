using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecouvrementAPI.Models
{
    [Table("dossier_recouvrement")]
    public class DossierRecouvrement
    {
        [Key]
        [Column("id_dossier")]
        public int IdDossier { get; set; }

        [Column("id_client")]
        public int IdClient { get; set; }

        [Column("type_emprunt")]
        public string TypeEmprunt { get; set; }

        [Column("montant_initial")]
        public decimal MontantInitial { get; set; }

        [Column("montant_impaye")]
        public decimal MontantImpaye { get; set; }

        [Column("frais_dossier")]
        public decimal FraisDossier { get; set; }

        [Column("statut_dossier")]
        public string StatutDossier { get; set; }

        [Column("date_creation")]
        public DateTime DateCreation { get; set; } = DateTime.Now;

        [Column("taux_interet")]
        public decimal TauxInteret { get; set; }

        [Column("confiance_client")]
        public int ConfianceClient { get; set; } = 0;

        // Navigation
        public Client Client { get; set; }
        public ICollection<Echeance> Echeances { get; set; }
        public ICollection<HistoriquePaiement> HistoriquePaiements { get; set; }
        public ICollection<IntentionClient> Intentions { get; set; }
        public ICollection<RelanceClient> Relances { get; set; }
        public ICollection<Garantie> Garanties { get; set; }
        public ICollection<Communication> Communications { get; set; }
        public ICollection<ScoreRisque> ScoresRisque { get; set; }
        public ICollection<HistoriqueAction> HistoriqueActions { get; set; }
    }
}