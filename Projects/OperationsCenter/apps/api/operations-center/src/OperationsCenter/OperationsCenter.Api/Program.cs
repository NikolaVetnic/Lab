using OperationsCenter.Api.Configuration;
using OperationsCenter.Application.DependencyInjection;
using OperationsCenter.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);

if (await builder.TryRunDevelopmentSeedAsync(args))
{
    return;
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseApiDocumentation();

app.MapControllers();

app.Run();

public partial class Program;
