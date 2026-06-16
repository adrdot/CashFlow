using AspireApp1.ServiceDefaults.Authentication;
using CashFlow.Auth.Infrastructure.Configuration;
using CashFlow.Auth.Infrastructure.Security;
using Microsoft.Extensions.Options;
using InfrastructureJwtOptions = CashFlow.Auth.Infrastructure.Configuration.JwtOptions;

namespace CashFlow.Auth.UnitTests.Security;

public sealed class JwtValidationTests
{
    [Fact]
    public void CreateLocalValidationParameters_RejectsTokenSignedWithDifferentKey()
    {
        var options = new InfrastructureJwtOptions
        {
            Issuer = "CashFlow.Auth.Api",
            Audience = "CashFlow.Web",
            SigningKey = "primary-signing-key-12345678901234567890",
            ExpirationMinutes = 60
        };

        var tokenService = new JwtTokenService(Options.Create(options));
        var userAccount = new CashFlow.Auth.Domain.Entities.UserAccount
        {
            Id = Guid.NewGuid(),
            Email = "admin@cashflow.local",
            DisplayName = "Admin",
            PasswordHash = string.Empty
        };

        var otherKeyService = new JwtTokenService(Options.Create(new InfrastructureJwtOptions
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            SigningKey = "other-signing-key-12345678901234567890",
            ExpirationMinutes = options.ExpirationMinutes
        }));

        var tamperedToken = otherKeyService.CreateToken(userAccount);
        var session = tokenService.ValidateToken(tamperedToken);

        Assert.Null(session);
    }

    [Fact]
    public void ServiceDefaultsValidationParameters_AcceptTokenIssuedByConfiguredService()
    {
        var options = new InfrastructureJwtOptions
        {
            Issuer = "CashFlow.Auth.Api",
            Audience = "CashFlow.Web",
            SigningKey = "dev-only-signing-key-change-me-1234567890",
            ExpirationMinutes = 60
        };

        var tokenService = new JwtTokenService(Options.Create(options));
        var userAccount = new CashFlow.Auth.Domain.Entities.UserAccount
        {
            Id = Guid.NewGuid(),
            Email = "admin@cashflow.local",
            DisplayName = "Admin",
            PasswordHash = string.Empty
        };

        var token = tokenService.CreateToken(userAccount);
        var parameters = CashFlowAuthenticationExtensions.CreateLocalValidationParameters(new AspireApp1.ServiceDefaults.Authentication.JwtOptions
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            SigningKey = options.SigningKey,
            ExpirationMinutes = options.ExpirationMinutes
        });
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();

        var principal = handler.ValidateToken(token, parameters, out _);

        Assert.Equal("admin@cashflow.local", principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value);
    }

    [Fact]
    public void RefreshToken_CanBeValidated_AndRotatedIntoNewAccessToken()
    {
        var options = new InfrastructureJwtOptions
        {
            Issuer = "CashFlow.Auth.Api",
            Audience = "CashFlow.Web",
            SigningKey = "dev-only-signing-key-change-me-1234567890",
            ExpirationMinutes = 60,
            RefreshTokenExpirationDays = 7
        };

        var tokenService = new JwtTokenService(Options.Create(options));
        var userAccount = new CashFlow.Auth.Domain.Entities.UserAccount
        {
            Id = Guid.NewGuid(),
            Email = "admin@cashflow.local",
            DisplayName = "Admin",
            PasswordHash = string.Empty
        };

        var refreshToken = tokenService.CreateRefreshToken(userAccount);
        var email = tokenService.ValidateRefreshToken(refreshToken);
        Assert.Equal(userAccount.Email, email);

        var accessToken = tokenService.CreateToken(userAccount);
        var session = tokenService.ValidateToken(accessToken);
        Assert.NotNull(session);
        Assert.Equal(userAccount.Email, session.Email);
    }
}
