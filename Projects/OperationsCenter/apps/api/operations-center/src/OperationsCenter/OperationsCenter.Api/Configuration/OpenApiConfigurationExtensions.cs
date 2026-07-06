using Microsoft.OpenApi;

namespace OperationsCenter.Api.Configuration;

public static class OpenApiConfigurationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                var components = document.Components ??= new OpenApiComponents();
                components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Description = "JWT Bearer token authentication"
                };

                return Task.CompletedTask;
            });

        });

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
                            <style>
                                body {
                                    margin: 0;
                                    font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
                                }

                                .auth-panel {
                                    display: grid;
                                    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                                    gap: 8px;
                                    align-items: end;
                                    padding: 12px;
                                    border-bottom: 1px solid #e5e7eb;
                                    background: #f8fafc;
                                }

                                .auth-field {
                                    display: flex;
                                    flex-direction: column;
                                    gap: 4px;
                                }

                                .auth-field label {
                                    font-size: 12px;
                                    color: #334155;
                                }

                                .auth-field input {
                                    padding: 8px;
                                    border: 1px solid #cbd5e1;
                                    border-radius: 6px;
                                    font-size: 14px;
                                }

                                .auth-actions {
                                    display: flex;
                                    gap: 8px;
                                    flex-wrap: wrap;
                                }

                                .auth-actions button {
                                    border: 0;
                                    border-radius: 6px;
                                    padding: 8px 12px;
                                    cursor: pointer;
                                    font-weight: 600;
                                }

                                .btn-primary {
                                    background: #0f766e;
                                    color: #fff;
                                }

                                .btn-secondary {
                                    background: #475569;
                                    color: #fff;
                                }

                                .auth-status {
                                    font-size: 12px;
                                    color: #334155;
                                    min-height: 16px;
                                }
                            </style>
                        </head>
                        <body>
                            <div class="auth-panel">
                                <div class="auth-field">
                                    <label for="oc-email">Email</label>
                                    <input id="oc-email" type="email" placeholder="admin@operationscenter.local" />
                                </div>
                                <div class="auth-field">
                                    <label for="oc-password">Password</label>
                                    <input id="oc-password" type="password" placeholder="Your password" />
                                </div>
                                <div class="auth-actions">
                                    <button id="oc-login" class="btn-primary" type="button">Login</button>
                                    <button id="oc-clear" class="btn-secondary" type="button">Clear Token</button>
                                </div>
                                <div class="auth-field" style="grid-column: 1 / -1;">
                                    <label for="oc-token">JWT Token (optional paste)</label>
                                    <input id="oc-token" type="text" placeholder="Paste token here or use Login" />
                                </div>
                                <div id="oc-status" class="auth-status" style="grid-column: 1 / -1;"></div>
                            </div>
                            <div id="swagger-ui"></div>
                            <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js"></script>
                            <script>
                                const tokenInput = document.getElementById('oc-token');
                                const emailInput = document.getElementById('oc-email');
                                const passwordInput = document.getElementById('oc-password');
                                const loginButton = document.getElementById('oc-login');
                                const clearButton = document.getElementById('oc-clear');
                                const statusLabel = document.getElementById('oc-status');
                                const storageKey = 'operationscenter.swagger.jwt';

                                function getToken() {
                                    return tokenInput.value.trim();
                                }

                                function setStatus(message, isError) {
                                    statusLabel.textContent = message;
                                    statusLabel.style.color = isError ? '#b91c1c' : '#334155';
                                }

                                function saveToken(token) {
                                    tokenInput.value = token;
                                    localStorage.setItem(storageKey, token);
                                    setStatus('Token ready. Requests will include Authorization header.', false);
                                }

                                const savedToken = localStorage.getItem(storageKey);
                                if (savedToken) {
                                    tokenInput.value = savedToken;
                                }

                                tokenInput.addEventListener('change', () => {
                                    const token = getToken();
                                    if (token) {
                                        localStorage.setItem(storageKey, token);
                                        setStatus('Token updated from input.', false);
                                    }
                                });

                                clearButton.addEventListener('click', () => {
                                    tokenInput.value = '';
                                    localStorage.removeItem(storageKey);
                                    setStatus('Token cleared.', false);
                                });

                                loginButton.addEventListener('click', async () => {
                                    const email = emailInput.value.trim();
                                    const password = passwordInput.value;

                                    if (!email || !password) {
                                        setStatus('Email and password are required.', true);
                                        return;
                                    }

                                    setStatus('Logging in...', false);

                                    try {
                                        const response = await fetch('/auth/login', {
                                            method: 'POST',
                                            headers: { 'Content-Type': 'application/json' },
                                            body: JSON.stringify({ email, password })
                                        });

                                        if (!response.ok) {
                                            setStatus('Login failed. Check credentials and try again.', true);
                                            return;
                                        }

                                        const payload = await response.json();
                                        if (!payload.accessToken) {
                                            setStatus('Login succeeded but no accessToken was returned.', true);
                                            return;
                                        }

                                        saveToken(payload.accessToken);
                                    } catch {
                                        setStatus('Login request failed.', true);
                                    }
                                });

                                window.ui = SwaggerUIBundle({
                                    url: '/openapi/v1.json',
                                    dom_id: '#swagger-ui',
                                    requestInterceptor: function(request) {
                                        const token = getToken();

                                        if (token && !request.url.endsWith('/auth/login')) {
                                            request.headers = request.headers || {};
                                            request.headers.Authorization = `Bearer ${token}`;
                                        }

                                        return request;
                                    }
                                });
                            </script>
                        </body>
                        </html>
                        """,
                    "text/html"));

        return app;
    }
}
