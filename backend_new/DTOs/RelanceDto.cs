namespace RecouvrementAPI.DTOs
{
    public class RelanceDto
    {
        public int IdRelance { get; set; }   
        public DateTime DateRelance { get; set; }
        public string Moyen { get; set; } = string.Empty;
        public string Statut { get; set; } = string.Empty;
        public string? contenu { get; set; }
    }
}