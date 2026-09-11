using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Pabili storefront Exclusive Offer creatives (image + redirect).</summary>
public class OperatorAd : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
