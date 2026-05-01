using EscapeRoom.Domain.Entities;

namespace EscapeRoom.Application.Abstractions;

public interface IJwtTokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashRefreshToken(string refreshToken);
}
