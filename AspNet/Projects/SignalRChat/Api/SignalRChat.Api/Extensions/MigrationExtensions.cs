using Microsoft.EntityFrameworkCore;
using SignalRChat.Api.Data;

namespace SignalRChat.Api.Extensions;

public static class MigrationExtensions
{
    public static IApplicationBuilder ApplyMigrations(this IApplicationBuilder app)
    {
        // Create a scope to safely resolve scoped services like DbContext
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var services = scope.ServiceProvider;

            try
            {
                services.GetRequiredService<ChatDbContext>().Database.Migrate();
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger<ChatDbContext>>();
                logger.LogError(ex, "An error occurred while applying database migrations.");

                throw; // Prevent the app from starting with a broken DB
            }
        }

        return app;
    }
}