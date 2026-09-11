using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class Merchant : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string BusinessName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string PinnedAddress { get; set; } = string.Empty;
    public bool ManagedByMerchant { get; set; }
    public string Mobile { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public string? LogoPath { get; set; }
    public string? BackgroundPath { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<MerchantOperatingHour> OperatingHours { get; set; } = new List<MerchantOperatingHour>();
    public ICollection<MerchantProductCategory> Categories { get; set; } = new List<MerchantProductCategory>();
    public ICollection<MerchantProduct> Products { get; set; } = new List<MerchantProduct>();
    public ICollection<MerchantAddonGroup> AddonGroups { get; set; } = new List<MerchantAddonGroup>();
}
