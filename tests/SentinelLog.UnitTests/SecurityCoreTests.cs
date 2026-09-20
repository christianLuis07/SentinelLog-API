using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;
using SentinelLog.Infrastructure.Authentication;
using Xunit;

namespace SentinelLog.UnitTests;

public class SecurityCoreTests
{
    [Fact]
    public void PasswordHasher_ShouldProduceDifferentHashesForSamePassword()
    {
        var hasher = new PasswordHasher();
        var password = "SecureAdminPassword!123";

        var hash1 = hasher.Hash(password);
        var hash2 = hasher.Hash(password);

        hash1.Should().NotBe(hash2, "BCrypt generates unique cryptographic salts per hash invocation.");
        hasher.Verify(password, hash1).Should().BeTrue();
        hasher.Verify(password, hash2).Should().BeTrue();
    }

    [Fact]
    public void PasswordHasher_WithIncorrectPassword_ShouldReturnFalse()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("RealPassword123!");

        hasher.Verify("WrongPassword456!", hash).Should().BeFalse();
        hasher.Verify("", hash).Should().BeFalse();
        hasher.Verify("RealPassword123!", "").Should().BeFalse();
    }

    [Fact]
    public void JwtTokenGenerator_ShouldIncludeRequiredSecurityClaims()
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Secret = "SentinelLogSuperSecretTestKeyThatMeetsMinimumLengthRequirement123456",
            Issuer = "SentinelLog.Test",
            Audience = "SentinelLog.Client.Test",
            ExpirationMinutes = 120
        });

        var generator = new JwtTokenGenerator(jwtOptions);
        var user = new User
        {
            Id = 42,
            Username = "secops_analyst",
            Email = "analyst@sentinellog.local",
            Role = UserRole.SecurityAnalyst,
            IsActive = true
        };

        var (token, expiresIn) = generator.GenerateToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        expiresIn.Should().Be(120 * 60);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("SentinelLog.Test");
        jwt.Audiences.Should().Contain("SentinelLog.Client.Test");

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == "secops_analyst");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "analyst@sentinellog.local");
        jwt.Claims.Should().Contain(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && c.Value == "SecurityAnalyst");
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
    }
}
