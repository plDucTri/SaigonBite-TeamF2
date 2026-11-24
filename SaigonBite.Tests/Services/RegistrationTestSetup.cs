using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SaigonBite.Helpers;
using SaigonBite.Models.Entities;
using SaigonBite.Repositories;
using SaigonBite.Services;
using SaigonBite.Validators;
namespace SaigonBite.Tests.Services;

/// <summary>A clock the tests can move forward (used for the expired code test).</summary>
public sealed class FakeClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan time) => _now += time;
}

/// <summary>Remembers what was "sent" so tests can read the code.</summary>
public sealed class FakeCodeSender : ICodeSender
{
    public List<(string Identifier, string Type, string Code)> Sent { get; } = new();

    public string? Send(string identifier, string type, string code)
    {
        Sent.Add((identifier, type, code));
        return code;
    }
}

/// <summary>Real validator, hasher, repository and service on an EF InMemory database.</summary>
public sealed class RegistrationTestSetup : IDisposable
{
    public RegistrationTestSetup()
    {
        var options = new DbContextOptionsBuilder<SaigonBiteContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new SaigonBiteContext(options);
        Cache = new MemoryCache(new MemoryCacheOptions());
        Clock = new FakeClock();
        Sender = new FakeCodeSender();
        Hasher = new PasswordHasher();
        Verification = new VerificationService(Cache, Sender, Clock);
        Service = new AccountService(
            new AccountValidator(), new UserRepository(Db), Verification, Hasher, Cache);
    }

    public SaigonBiteContext Db { get; }
    public IMemoryCache Cache { get; }
    public FakeClock Clock { get; }
    public FakeCodeSender Sender { get; }
    public IPasswordHasher Hasher { get; }
    public VerificationService Verification { get; }
    public AccountService Service { get; }

    public void Dispose()
    {
        Db.Dispose();
        Cache.Dispose();
    }
}
