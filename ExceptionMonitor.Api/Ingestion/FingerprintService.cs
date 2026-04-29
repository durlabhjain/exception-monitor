using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ExceptionMonitor.Api.Ingestion;

public interface IFingerprintService
{
    string Compute(Guid applicationId, string environment, string? exceptionType, string message, string stackTrace, string? route, string? customFingerprint);
}

public sealed partial class FingerprintService : IFingerprintService
{
    public string Compute(Guid applicationId, string environment, string? exceptionType, string message, string stackTrace, string? route, string? customFingerprint)
    {
        if (!string.IsNullOrWhiteSpace(customFingerprint))
        {
            return Hash($"custom|{applicationId}|{environment}|{customFingerprint.Trim().ToLowerInvariant()}");
        }

        var normalizedMessage = NormalizeMessage(message);
        var topFrame = ExtractTopFrame(stackTrace);
        return Hash($"auto|{applicationId}|{environment.ToLowerInvariant()}|{exceptionType?.ToLowerInvariant()}|{normalizedMessage}|{topFrame}|{route?.ToLowerInvariant()}");
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string NormalizeMessage(string message)
    {
        var normalized = GuidRegex().Replace(message, "{guid}");
        normalized = NumberRegex().Replace(normalized, "{number}");
        return normalized.Trim().ToLowerInvariant();
    }

    private static string ExtractTopFrame(string stackTrace)
    {
        return stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex("\\b\\d+\\b")]
    private static partial Regex NumberRegex();
}
