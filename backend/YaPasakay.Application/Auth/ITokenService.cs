using YaPasakay.Domain.Entities;

namespace YaPasakay.Application.Auth;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(AppUser user);
    string CreateRefreshToken();
}
