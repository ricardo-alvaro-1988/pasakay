using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class MerchantOperatingHour : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant Merchant { get; set; } = null!;
    /// <summary>0 = Sunday … 6 = Saturday (System.DayOfWeek).</summary>
    public int DayOfWeek { get; set; }
    public bool IsClosed { get; set; }
    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }
}
