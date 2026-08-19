using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class AccessGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<AccessGroupPage> Pages { get; set; } = new List<AccessGroupPage>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}

public class AccessGroupPage : BaseEntity
{
    public Guid AccessGroupId { get; set; }
    public AccessGroup AccessGroup { get; set; } = null!;
    public string PageId { get; set; } = string.Empty;
}
