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
