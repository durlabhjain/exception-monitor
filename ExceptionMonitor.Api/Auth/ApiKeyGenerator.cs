using System.Security.Cryptography;

namespace ExceptionMonitor.Api.Auth;

public interface IApiKeyGenerator
{
    GeneratedApiKey Generate();
}

public sealed record GeneratedApiKey(string Plaintext, string Prefix);

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    public GeneratedApiKey Generate()
    {
        var prefixBytes = RandomNumberGenerator.GetBytes(6);
        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var prefix = Convert.ToHexString(prefixBytes).ToLowerInvariant();
        var secret = Base64UrlEncode(secretBytes);
        return new GeneratedApiKey($"exm_{prefix}_{secret}", prefix);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
