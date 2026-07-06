using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Infrastructure.Development;
using OperationsCenter.Infrastructure.Identity;
using OperationsCenter.Application.Persistence;
using OperationsCenter.Domain.Identity;

namespace OperationsCenter.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OperationsCenterDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'OperationsCenterDatabase' is not configured.");
        }

        services.AddDbContext<OperationsCenterDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IOperationsCenterDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<OperationsCenterDbContext>());

        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT options are not configured.");

        services.AddOptions<JwtOptions>()
            .Bind(jwtSection)
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && !string.IsNullOrWhiteSpace(options.SigningKey)
                && options.AccessTokenExpiryMinutes > 0,
                "JWT options must include issuer, audience, signing key and positive expiry.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("Incidents.Read", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRole.Admin.ToString(), SystemRole.Operator.ToString(), SystemRole.Viewer.ToString());
            })
            .AddPolicy("Incidents.Write", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRole.Admin.ToString(), SystemRole.Operator.ToString());
            });

        services.AddScoped<DevelopmentDataSeeder>();

        return services;
    }
}
