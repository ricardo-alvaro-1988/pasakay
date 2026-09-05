using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class OperatorCashInBankAccount : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string BankName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? QrImagePath { get; set; }
    public int SortOrder { get; set; }
}
