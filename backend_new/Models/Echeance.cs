using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecouvrementAPI.Models
{
    [Table("echeance")]
    public class Echeance
    {
        [Key]
        [Column("id_echeance")]
        public int IdEcheance { get; set; }

        [Column("id_dossier")]
        public int IdDossier { get; set; }

        [Column("date_echeance")]
        public DateTime DateEcheance { get; set; }

        [Column("montant_du")]
        public decimal MontantDu { get; set; }

        [Column("montant_paye")]
        public decimal MontantPaye { get; set; } = 0;

        [Column("statut")]
        public string Statut { get; set; } = "Impayée"; // Payée, Impayée, Partielle

        [Column("nombre_jours_retard")]
        public int NombreJoursRetard { get; set; } = 0;

        // Navigation
        public DossierRecouvrement Dossier { get; set; }
    }
}
