using Microsoft.EntityFrameworkCore;

namespace Scutum.T10.Data;

// Jedan DbContext za ceo servis. Entiteti se dodaju kao DbSet-ovi, a njihove konfiguracije kao
// IEntityTypeConfiguration<T> klase u Configurations/<Celina>/ (učitavaju se automatski).
public sealed class T10DbContext(DbContextOptions<T10DbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(T10DbContext).Assembly);
    }
}
