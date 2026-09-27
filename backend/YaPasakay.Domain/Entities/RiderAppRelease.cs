using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class RiderAppRelease : BaseEntity
{
    public string Version { get; set; } = string.Empty;
    public string ApkPath { get; set; } = string.Empty;
    public string? ReleaseNotes { get; set; }
    public bool IsLatest { get; set; }
}
