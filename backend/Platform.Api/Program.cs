using Platform.Api.Modules.Laboratories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLaboratories(builder.Environment);
var app = builder.Build();

// Starter endpoint only. Add domain APIs together with server-side authorization.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "security-lab-api",
    note = "Initial scaffold; not a production-ready laboratory platform"
}));

app.MapLaboratories();

app.Run();

public partial class Program { }
