using System.Security.Claims;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Domain.Identity;

namespace OperationsCenter.Api.Infrastructure;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }

    public string? Email => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public SystemRole? Role
    {
        get
        {
            var roleValue = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
            if (string.IsNullOrWhiteSpace(roleValue))
            {
                return null;
            }

            return Enum.TryParse<SystemRole>(roleValue, ignoreCase: true, out var role)
                ? role
                : null;
        }
    }
}
