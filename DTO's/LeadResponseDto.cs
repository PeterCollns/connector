namespace MetaAdsConnector.DTO_s
{
    public class LeadResponseDto
    {
        public string Uuid { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public Dictionary<string, string> Fields { get; set; } = new();
        public string? Status { get; set; } // retrieved from CRM
    }

}
