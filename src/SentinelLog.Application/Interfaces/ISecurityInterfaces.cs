using SentinelLog.Domain.Entities;

namespace SentinelLog.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenGenerator
{
    (string Token, int ExpiresIn) GenerateToken(User user);
}
