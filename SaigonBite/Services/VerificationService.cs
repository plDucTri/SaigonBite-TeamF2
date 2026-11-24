using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace SaigonBite.Services;

/// <summary>Register as a singleton: the codes live in IMemoryCache.</summary>
public class VerificationService : IVerificationService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private const int MaxWrongAttempts = 5;

    private readonly IMemoryCache _cache;
    private readonly ICodeSender _sender;
    private readonly TimeProvider _time;

    public VerificationService(IMemoryCache cache, ICodeSender sender, TimeProvider time)
    {
        _cache = cache;
        _sender = sender;
        _time = time;
    }

    public string? SendCode(string identifier, string type)
    {
        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var entry = new CodeEntry(code, _time.GetUtcNow().Add(CodeLifetime));
        _cache.Set(Key(identifier), entry, CodeLifetime);
        return _sender.Send(identifier, type, code);
    }

    public bool VerifyCode(string identifier, string code)
    {
        string key = Key(identifier);
        if (!_cache.TryGetValue(key, out CodeEntry? entry) || entry is null)
            return false;

        lock (entry)
        {
            if (_time.GetUtcNow() > entry.ExpiresAt)
            {
                _cache.Remove(key);
                return false;
            }

            bool match = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(entry.Code),
                Encoding.UTF8.GetBytes(code ?? string.Empty));

            if (match)
            {
                _cache.Remove(key); // a code can be used only once
                return true;
            }

            entry.WrongAttempts++;
            if (entry.WrongAttempts >= MaxWrongAttempts)
                _cache.Remove(key);
            return false;
        }
    }

    private static string Key(string identifier) => $"verify:{identifier}";

    private sealed class CodeEntry
    {
        public CodeEntry(string code, DateTimeOffset expiresAt)
        {
            Code = code;
            ExpiresAt = expiresAt;
        }

        public string Code { get; }
        public DateTimeOffset ExpiresAt { get; }
        public int WrongAttempts { get; set; }
    }
}
