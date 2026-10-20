using Scutum.T10.Data;
using Scutum.T10.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddT10Data(builder.Configuration);
builder.Services.AddT10Services();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json — izvor za 'npm run gen:api' u frontendu
}

// Konvencija repoa: GET /health vraća 200 kada servis radi.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Endpointi po celini idu u Endpoints/<Celina>/ i mapiraju se ovde.

app.Run();

// Omogućava WebApplicationFactory<Program> u integracionim testovima.
public partial class Program;
