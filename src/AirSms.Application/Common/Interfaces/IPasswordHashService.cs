namespace AirSms.Application.Common.Interfaces;

public interface IPasswordHashService
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}
