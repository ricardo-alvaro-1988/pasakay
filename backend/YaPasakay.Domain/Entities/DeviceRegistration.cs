using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class DeviceRegistration : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "Unknown";
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
