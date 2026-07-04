namespace OperationsCenter.Api.Configuration;

public static class OpenApiConfigurationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }

    public static IApplicationBuilder UseApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi("/openapi/v1.json");
        app.MapGet("/swagger", () => Results.Redirect("/swagger/index.html", permanent: false));
        app.MapGet(
            "/swagger/index.html",
            () =>
                Results.Content(
                    """
                        <!doctype html>
                        <html lang="en">
                        <head>
                            <meta charset="utf-8" />
                            <meta name="viewport" content="width=device-width, initial-scale=1" />
                            <title>OperationsCenter API - Swagger UI</title>
                            <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css" />
                        </head>
                        <body>
                            <div id="swagger-ui"></div>
                            <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
                            <script>
                                window.ui = SwaggerUIBundle({
                                    url: '/openapi/v1.json',
                                    dom_id: '#swagger-ui'
                                });
                            </script>
                        </body>
                        </html>
                        """,
                    "text/html"));

        return app;
    }
}
