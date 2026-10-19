namespace YaPasakay.Application.Auth;

public interface IOtpSender
{
    Task SendAsync(string phone, string code, CancellationToken cancellationToken = default);
}

public interface IOtpStore
{
    void Save(string phone, string code, TimeSpan lifetime);
    bool TryValidate(string phone, string code);
}

public interface IPhilSmsSender
{
    Task SendAsync(string phone, string code, CancellationToken cancellationToken = default);
}

public interface ICustomerOtpStore
{
    bool IsCoolingDown(string phone, TimeSpan cooldown);
    void Save(string phone, string code, TimeSpan lifetime);
    void MarkSent(string phone);
    bool TryValidate(string phone, string code);
    string IssueRegistration(string phone, TimeSpan lifetime);
    bool TryReadRegistration(string token, out string phone);
    void ConsumeRegistration(string token);
}
