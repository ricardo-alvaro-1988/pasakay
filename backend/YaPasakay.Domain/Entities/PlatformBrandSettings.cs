using YaPasakay.Domain.Common;

namespace YaPasakay.Domain.Entities;

public class PlatformBrandSettings : BaseEntity
{
    public string BrandName { get; set; } = "Ya! Pasakay";
    public string ShortName { get; set; } = "Pasakay";
    public string? LogoPath { get; set; }
    public string? FaviconPath { get; set; }
    public string ThemeId { get; set; } = "pasakay-red";
}
