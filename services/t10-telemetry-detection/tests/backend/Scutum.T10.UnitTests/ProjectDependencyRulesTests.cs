using System.Reflection;

namespace Scutum.T10.UnitTests;

// Čuva dogovoreni smer zavisnosti: Api → Services → Data → Domain.
// Domain mora ostati bez EF Core-a, ASP.NET-a i ostalih slojeva, da bi se pravila testirala bez baze.
public class ProjectDependencyRulesTests
{
    [TestCase("Scutum.T10.Domain", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Scutum.T10.Data", "Scutum.T10.Services", "Scutum.T10.Api")]
    [TestCase("Scutum.T10.Data", "Microsoft.AspNetCore", "Scutum.T10.Services", "Scutum.T10.Api")]
    [TestCase("Scutum.T10.Services", "Microsoft.AspNetCore", "Scutum.T10.Api")]
    public void SlojNeZavisiOdZabranjenihSklopova(string assemblyName, params string[] forbiddenPrefixes)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(r => r.Name ?? string.Empty);

        var violations = references.Where(r => forbiddenPrefixes.Any(p => r.StartsWith(p, StringComparison.Ordinal)));

        Assert.That(violations, Is.Empty, $"{assemblyName} ne sme da zavisi od: {string.Join(", ", forbiddenPrefixes)}");
    }
}
