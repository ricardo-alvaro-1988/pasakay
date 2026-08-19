using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class AppUser : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? GoogleSubject { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public Guid? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public bool IsMainAdmin { get; set; }
    public Guid? AccessGroupId { get; set; }
    public AccessGroup? AccessGroup { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
