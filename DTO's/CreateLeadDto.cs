using System.ComponentModel.DataAnnotations;

namespace MetaAdsConnector.DTO_s
{
    public class CreateLeadDto
    {
        public string? Uuid { get; set; }

        [Required]
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}