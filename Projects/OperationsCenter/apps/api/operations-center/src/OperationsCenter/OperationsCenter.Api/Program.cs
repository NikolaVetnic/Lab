using OperationsCenter.Api.Configuration;
using OperationsCenter.Api.Infrastructure;
using OperationsCenter.Application.DependencyInjection;
using OperationsCenter.Application.Identity.Abstractions;
using OperationsCenter.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

if (await builder.TryRunDevelopmentSeedAsync(args))
{
    return;
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseApiDocumentation();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
