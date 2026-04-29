using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ExceptionMonitor.Api.Auth;

public interface IApiKeyHasher
{
    string Hash(string plaintextKey);
}

public sealed class HmacApiKeyHasher(IOptions<SecurityOptions> options) : IApiKeyHasher
{
    public string Hash(string plaintextKey)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKeyHashSecret))
        {
            throw new InvalidOperationException("Security:ApiKeyHashSecret must be configured.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.ApiKeyHashSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(plaintextKey))).ToLowerInvariant();
    }
}
