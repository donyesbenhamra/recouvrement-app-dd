namespace RecouvrementAPI.DTOs
{
    public class CreateClientsDto
    {
        public string Client { get; set; }      // "Nom Prenom"
        public string Telephone { get; set; }
        public string Email { get; set; }
        public string Agence { get; set; }      // ville agence
        public string TypeEmprunt { get; set; }
        public double MontantDu { get; set; }
        public int Retard { get; set; }
        
    }
}