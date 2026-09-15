using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class OperatorBillVehicleLine : BaseEntity
{
    public Guid OperatorBillId { get; set; }
    public OperatorBill OperatorBill { get; set; } = null!;
    public Guid? VehicleCategoryId { get; set; }
    public VehicleCategory? VehicleCategory { get; set; }
    public string VehicleCode { get; set; } = string.Empty;
    public string VehicleName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
