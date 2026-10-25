using System.Text.Json;
using System.Text.Json.Serialization;
using Scutum.T03.Application;
using Scutum.T03.Infrastructure;

var json = new JsonSerializerOptions { WriteIndented = true };
json.Converters.Add(new JsonStringEnumConverter());
var fixture = new DemoFixture();
var step = 0;
Guid Correlation() => Guid.Parse($"00000000-0000-0000-0000-{++step:000000000000}");
void Show(string scenario, OperationResult result, OperationOutcome expected)
{
    Console.WriteLine($"\nSCENARIO: {scenario}");
    Console.WriteLine(JsonSerializer.Serialize(result, json));
    if (result.Outcome != expected) throw new InvalidOperationException("Neočekivan demo ishod.");
}

Show("1. Ana čita svoj resurs", fixture.For(fixture.Ana).Read(new("res-001", Correlation())), OperationOutcome.Granted);
Show("2. Marko zna ID, ali nije vlasnik", fixture.For(fixture.Marko).Read(new("res-001", Correlation())), OperationOutcome.Denied);

Console.WriteLine("\nDEMO PASS: 2/2 očekivana ishoda.");
