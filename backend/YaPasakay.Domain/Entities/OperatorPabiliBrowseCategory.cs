using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

/// <summary>Operator-configured Pabili home quick-category chips shown under search.</summary>
public class OperatorPabiliBrowseCategory : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
