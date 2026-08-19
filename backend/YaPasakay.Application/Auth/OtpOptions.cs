namespace YaPasakay.Application.Auth;

public class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>Fixed = always 1234. Used for admin, operator, and rider sign-in.</summary>
    public string Mode { get; set; } = "Fixed";

    /// <summary>When true, code 1234 still validates for staff apps.</summary>
    public bool AllowDevBypass { get; set; } = true;
}

public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>OAuth 2.0 Web client ID from Google Cloud Console (Google Identity Services).</summary>
    public string? ClientId { get; set; }

    /// <summary>Used only for server-side OAuth. GIS ID-token sign-in does not need this.</summary>
    public string? ClientSecret { get; set; }
}

public class FcmOptions
{
    public const string SectionName = "Fcm";

    /// <summary>Legacy FCM server key. When empty, pushes are logged only.</summary>
    public string? ServerKey { get; set; }
}
