using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExceptionMonitor.Api.Auth;

namespace ExceptionMonitor.Api.Ingestion;

public interface IIngestionNormalizer
{
    Task<NormalizedExceptionEvent?> NormalizeJsonAsync(HttpContext context, ApiKeyContext apiKey, CancellationToken cancellationToken);
    Task<NormalizedExceptionEvent?> NormalizeFormAsync(HttpContext context, ApiKeyContext apiKey, CancellationToken cancellationToken);
}

public sealed class IngestionNormalizer(IFingerprintService fingerprints) : IIngestionNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<NormalizedExceptionEvent?> NormalizeJsonAsync(HttpContext context, ApiKeyContext apiKey, CancellationToken cancellationToken)
    {
        var request = await JsonSerializer.DeserializeAsync<ExceptionRequest>(context.Request.Body, JsonOptions, cancellationToken);
        if (request is null) return null;
        var raw = JsonSerializer.SerializeToElement(request, JsonOptions);
        return Normalize(request, apiKey, "json", (int)(context.Request.ContentLength ?? 0), raw);
    }

    public async Task<NormalizedExceptionEvent?> NormalizeFormAsync(HttpContext context, ApiKeyContext apiKey, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        string? Get(params string[] names) => names.Select(name => form.TryGetValue(name, out var value) ? value.ToString() : null).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var metadata = new Dictionary<string, object?>();
        foreach (var field in form)
        {
            metadata[field.Key] = field.Value.ToString();
        }

        var request = new ExceptionRequest(
            Error: Get("error", "Exception", "Error", "Message"),
            Message: Get("message", "Message", "Exception"),
            StackTrace: Get("stackTrace", "StackTrace", "Stack Trace"),
            ExceptionType: Get("exceptionType", "ExceptionType", "Type"),
            OccurredAt: DateTimeOffset.TryParse(Get("occurredAt", "OccurredAt", "CreatedOn", "Date"), out var occurredAt) ? occurredAt : null,
            Severity: Get("severity", "Severity", "Level"),
            Environment: Get("environment", "Environment"),
            Source: Get("source", "Source", "Machine Name"),
            Release: Get("release", "Release", "Version"),
            CorrelationId: Get("correlationId", "CorrelationId", "RequestId"),
            TraceId: Get("traceId", "TraceId"),
            SpanId: Get("spanId", "SpanId"),
            UserId: Get("userId", "UserId"),
            UserHash: Get("userHash", "UserHash"),
            Request: new ExceptionRequestInfo(
                Get("method", "HttpMethod", "Request Method"),
                Get("url", "Url", "URL", "Absolute Url", "AbsoluteUrl"),
                Get("route", "Route", "Virtual Path", "VirtualPath"),
                Get("referrer", "UrlReferrer", "Referer", "Referrer"),
                int.TryParse(Get("statusCode", "StatusCode"), out var status) ? status : null,
                Get("ipAddress", "IpAddress", "RemoteIp")),
            Tags: null,
            Metadata: JsonSerializer.SerializeToElement(metadata, JsonOptions),
            Fingerprint: Get("fingerprint", "Fingerprint"),
            Method: null, Url: null, Route: null, Referrer: null, StatusCode: null, UserName: Get("userName", "UserName", "Username"));

        return Normalize(request, apiKey, "form", (int)(context.Request.ContentLength ?? 0), JsonSerializer.SerializeToElement(metadata, JsonOptions));
    }

    private NormalizedExceptionEvent Normalize(ExceptionRequest request, ApiKeyContext apiKey, string format, int payloadSize, JsonElement rawPayload)
    {
        var message = FirstNonEmpty(request.Error, request.Message) ?? "Unhandled exception";
        var stackTrace = request.StackTrace ?? string.Empty;
        var environment = string.IsNullOrWhiteSpace(request.Environment) ? apiKey.DefaultEnvironment : request.Environment.Trim().ToLowerInvariant();
        var severity = string.IsNullOrWhiteSpace(request.Severity) ? "Error" : request.Severity.Trim();
        var userHash = !string.IsNullOrWhiteSpace(request.UserHash) ? request.UserHash : HashUserId(request.UserId);
        var metadata = request.Metadata ?? JsonSerializer.SerializeToElement(new Dictionary<string, object?>(), JsonOptions);
        // Prefer the nested request object; fall back to flat top-level fields (NLog / simple HTTP clients)
        var requestInfo = request.Request
            ?? (FirstNonEmpty(request.Method, request.Url) is not null
                ? new ExceptionRequestInfo(request.Method, request.Url, request.Route, request.Referrer, request.StatusCode, null)
                : null);

        var fingerprint = fingerprints.Compute(apiKey.ApplicationId, environment, request.ExceptionType, message, stackTrace, requestInfo?.Route, request.Fingerprint);

        return new NormalizedExceptionEvent(
            message,
            stackTrace,
            request.ExceptionType,
            request.OccurredAt ?? DateTimeOffset.UtcNow,
            severity,
            environment,
            request.Source,
            request.Release,
            request.CorrelationId,
            request.TraceId,
            request.SpanId,
            userHash,
            requestInfo?.Method,
            requestInfo?.Url,
            requestInfo?.Route,
            requestInfo?.Referrer,
            requestInfo?.StatusCode,
            request.UserName,
            format,
            payloadSize,
            request.Tags ?? new Dictionary<string, string>(),
            metadata,
            rawPayload,
            fingerprint);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? HashUserId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
    }
}
