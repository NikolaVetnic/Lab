using OperationsCenter.Api.Configuration;
using OperationsCenter.Api.Endpoints;
using OperationsCenter.Application.Incidents.UseCases;
using OperationsCenter.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddApiDocumentation();
builder.Services.AddScoped<CreateIncidentUseCase>();
builder.Services.AddScoped<ListIncidentsUseCase>();
builder.Services.AddScoped<GetIncidentByIdUseCase>();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseApiDocumentation();

app.MapApiEndpoints();

app.Run();

public partial class Program;
