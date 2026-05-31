using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecouvrementAPI.Models
{
    [Table("relance_client")]
    public class RelanceClient
    {
        [Key]
        [Column("id_relance")]
        public int IdRelance { get; set; }

        [Column("id_dossier")]
        public int IdDossier { get; set; }

        [Column("moyen")]
        public string Moyen { get; set; } = string.Empty;

        [Column("statut")]
        public string Statut { get; set; } = string.Empty;

        [Column("date_relance")]
        public DateTime DateRelance { get; set; }

        [Column("contenu")]
        public string Contenu { get; set; }

        public DossierRecouvrement Dossier { get; set; }
        public ICollection<Communication> Communications { get; set; } = new List<Communication>();
    }
}