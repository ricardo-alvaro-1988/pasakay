using YaPasakay.Domain.Enums;

namespace YaPasakay.Api.Models;

public class CreateRiderForm
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Password { get; set; }
    public VehicleType VehicleType { get; set; } = VehicleType.Motorcycle;
    public string PlateNumber { get; set; } = string.Empty;
    public string? VehicleModel { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty;
    public Guid AddressBarangayId { get; set; }
    public string AddressDetails { get; set; } = string.Empty;
    public IFormFile? ProfilePhoto { get; set; }
    public IFormFile? LicensePhoto { get; set; }
    public List<PaymentMethod> AcceptedPaymentMethods { get; set; } = [];
}
