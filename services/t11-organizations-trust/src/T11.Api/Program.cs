var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();

// Needed so the test project can use WebApplicationFactory<Program>.
public partial class Program { }
