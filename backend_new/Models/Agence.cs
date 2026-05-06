using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RecouvrementAPI.Models
{
    [Table("agence")]
    public class Agence
    {
        [Key]
        [Column("id_agence")]
        public int IdAgence { get; set; }

        [Column("nom_agence")]
        public string NomAgence { get; set; }

        [Column("ville")]
        public string Ville { get; set; }

        [Column("adresse")]
        public string Adresse { get; set; }

        public ICollection<Client> Clients { get; set; }
        public ICollection<UtilisateurBack> Utilisateurs { get; set; }
    }
}
