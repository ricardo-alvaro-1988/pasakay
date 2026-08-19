using YaPasakay.Domain.Common;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain.Entities;

public class OperatorBill : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public string Number { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal MotorcycleAmount { get; set; }
    public decimal TricycleAmount { get; set; }
    public int TripCount { get; set; }
    public DateTime PeriodFromUtc { get; set; }
    public DateTime PeriodToUtc { get; set; }
    public bool DisabledOperator { get; set; }
    public DateTime NotifiedAtUtc { get; set; }
    public string? Note { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Issued;
    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
}
