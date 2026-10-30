using Team21.Acc.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddTeam21Acc();
var app = builder.Build();
app.MapTeam21Acc();

// Starter endpoint only. Add domain APIs together with server-side authorization.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "security-lab-api",
    note = "Initial scaffold; not a production-ready laboratory platform"
}));

app.Run();

public partial class Program { }
