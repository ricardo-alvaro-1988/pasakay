using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class CustomerProfile : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender? Gender { get; set; }
    public string? PhotoPath { get; set; }
    public string? PinHash { get; set; }
    public DeleteAccountStatus DeleteStatus { get; set; } = DeleteAccountStatus.None;
    public DateTime? DeleteRequestedAtUtc { get; set; }
    public string? DeleteRequestReason { get; set; }
    public DateTime? DeleteResolvedAtUtc { get; set; }
    public string? DeleteResolutionNote { get; set; }
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public Guid? HailRiderId { get; set; }
    public RiderProfile? HailRider { get; set; }
    public DateTime? HailAtUtc { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
            ? AppUser.FullName
            : $"{FirstName} {LastName}".Trim();
}
