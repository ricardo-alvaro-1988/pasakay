namespace YaPasakay.Api.Models;

public class CreateOperatorForm
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Password { get; set; }
    public Guid AddressBarangayId { get; set; }
    public string AddressDetails { get; set; } = string.Empty;
    public string GovernmentIdType { get; set; } = string.Empty;
    public string GovernmentId { get; set; } = string.Empty;
    public List<Guid> BarangayIds { get; set; } = [];
    public IFormFile? ProfilePhoto { get; set; }
    public IFormFile? GovernmentIdPhoto { get; set; }
    public decimal? MotorcycleCommissionPercent { get; set; }
    public decimal? TricycleCommissionPercent { get; set; }
}
