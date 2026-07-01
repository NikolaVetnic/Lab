using OperationsCenter.Api.Configuration;
using OperationsCenter.Api.Endpoints;
using OperationsCenter.Application.DependencyInjection;
using OperationsCenter.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseApiDocumentation();

app.MapApiEndpoints();

app.Run();

public partial class Program;
