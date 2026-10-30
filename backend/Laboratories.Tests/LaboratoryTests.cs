using Platform.Api.Modules.Laboratories.Domain;

namespace Laboratories.Tests;

public class LaboratoryTests
{
    [Test]
    public void Rename_KeepsSameId()
    {
        Guid id = Guid.NewGuid();
        Laboratory lab = new Laboratory(id, "SQL injection");

        lab.Rename("SQL injection - napredno");

        Assert.That(lab.Id, Is.EqualTo(id));
        Assert.That(lab.Title, Is.EqualTo("SQL injection - napredno"));
    }
}