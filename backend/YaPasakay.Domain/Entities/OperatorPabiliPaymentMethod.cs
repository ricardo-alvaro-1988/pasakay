using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

/// <summary>Operator-configured Pabili checkout payment methods (Cash / GCash / Maya / Other) with optional QR.</summary>
public class OperatorPabiliPaymentMethod : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    /// <summary>Optional display label (mainly for Other).</summary>
    public string Label { get; set; } = string.Empty;
    public string? QrImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
