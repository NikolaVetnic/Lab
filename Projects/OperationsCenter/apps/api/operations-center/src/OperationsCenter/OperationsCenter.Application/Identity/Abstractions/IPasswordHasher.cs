namespace OperationsCenter.Application.Identity.Abstractions;

public interface IPasswordHasher
{
    bool Verify(string hashedPassword, string providedPassword);

    string Hash(string password);
}
