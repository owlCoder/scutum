using System.Collections.Concurrent;
using T08.Application.Exposures;
using T08.Domain.Exposures;

namespace T08.Infrastructure.Persistence;

/// <summary>
/// Privremeni repozitorijum u memoriji do prave baze. Čuva i vraća kopije i proverava verziju pri upisu,
/// pa se ponaša kao pravi (konkurentna izmena = konflikt, a ne tiho prepisivanje).
/// </summary>
public sealed class InMemoryExposureRepository : IExposureRepository
{
    private readonly ConcurrentDictionary<string, Exposure> _store = new();
    private readonly object _gate = new();

    /// <summary>Broj upisanih exposure-a (za testove: potvrda da odbijen zahtev nije ništa upisao).</summary>
    public int Count => _store.Count;

    /// <summary>Sinhroni seed za testove; registracija ide preko <see cref="AddAsync"/>.</summary>
    public void Add(Exposure exposure)
    {
        if (!_store.TryAdd(exposure.Id, exposure.Copy()))
            throw new InvalidOperationException($"Exposure '{exposure.Id}' već postoji.");
    }

    public Task<bool> AddAsync(Exposure exposure, CancellationToken ct = default) =>
        Task.FromResult(_store.TryAdd(exposure.Id, exposure.Copy()));

    public Task<Exposure?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_store.TryGetValue(id, out var e) ? e.Copy() : null);

    public Task<bool> SaveAsync(Exposure exposure, int expectedVersion, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (!_store.TryGetValue(exposure.Id, out var current) || current.Version != expectedVersion)
                return Task.FromResult(false);

            _store[exposure.Id] = exposure.Copy();
            return Task.FromResult(true);
        }
    }
}
