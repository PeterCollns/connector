using System.ComponentModel.DataAnnotations;

namespace MetaAdsConnector.Entities
{
    public class Lead
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Uuid { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<LeadField> Fields { get; set; } = new List<LeadField>();
    }

}
