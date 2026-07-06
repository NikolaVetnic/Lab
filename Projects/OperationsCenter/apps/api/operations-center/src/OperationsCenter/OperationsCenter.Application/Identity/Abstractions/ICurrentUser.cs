using OperationsCenter.Domain.Identity;

namespace OperationsCenter.Application.Identity.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email { get; }

    SystemRole? Role { get; }
}
