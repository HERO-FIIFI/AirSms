using AirSms.Domain.Entities;

namespace AirSms.Application.Common.Interfaces;

public interface IAccessTokenService
{
    AccessTokenResult Create(User user);
}

public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
