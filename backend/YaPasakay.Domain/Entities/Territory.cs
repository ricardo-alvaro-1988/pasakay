using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class Province : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<Municipality> Municipalities { get; set; } = new List<Municipality>();
}

public class Municipality : BaseEntity
{
    public Guid ProvinceId { get; set; }
    public Province Province { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ICollection<Barangay> Barangays { get; set; } = new List<Barangay>();
}

public class Barangay : BaseEntity
{
    public Guid MunicipalityId { get; set; }
    public Municipality Municipality { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}

public class OperatorBarangay : BaseEntity
{
    public Guid OperatorId { get; set; }
    public Operator Operator { get; set; } = null!;
    public Guid BarangayId { get; set; }
    public Barangay Barangay { get; set; } = null!;
}
