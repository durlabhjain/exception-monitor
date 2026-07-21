using System.Text.Json;

namespace ExceptionMonitor.Api.Ingestion;

public sealed record ExceptionRequest(
    string? Error,
    string? Message,
    string? StackTrace,
    string? ExceptionType,
    DateTimeOffset? OccurredAt,
    string? Severity,
    string? Environment,
    string? Source,
    string? Release,
    string? CorrelationId,
    string? TraceId,
    string? SpanId,
    string? UserId,
    string? UserHash,
    ExceptionRequestInfo? Request,
    IReadOnlyDictionary<string, string>? Tags,
    JsonElement? Metadata,
    string? Fingerprint,
    // Flat alternatives — NLog/loggers that can't produce nested JSON send these at root level
    string? Method,
    string? Url,
    string? Route,
    string? Referrer,
    int? StatusCode,
    string? UserName,
    // Sent flat by pino-http-send.mjs alongside the fields above
    JsonElement? RequestHeaders,
    JsonElement? RequestParams,
    JsonElement? RequestBody,
    JsonElement? QueryString);

public sealed record ExceptionRequestInfo(
    string? Method,
    string? Url,
    string? Route,
    string? Referrer,
    int? StatusCode,
    string? IpAddress);

public sealed record NormalizedExceptionEvent(
    string Message,
    string StackTrace,
    string? ExceptionType,
    DateTimeOffset OccurredAt,
    string Severity,
    string Environment,
    string? Source,
    string? Release,
    string? CorrelationId,
    string? TraceId,
    string? SpanId,
    string? UserHash,
    string? RequestMethod,
    string? RequestUrl,
    string? RequestRoute,
    string? RequestReferrer,
    int? RequestStatusCode,
    string? UserName,
    JsonElement? RequestHeaders,
    JsonElement? RequestParams,
    JsonElement? RequestBody,
    JsonElement? QueryString,
    string PayloadFormat,
    int PayloadSize,
    IReadOnlyDictionary<string, string> Tags,
    JsonElement Metadata,
    JsonElement? RawPayload,
    string Fingerprint);

public sealed record IngestResponse(Guid EventId, Guid GroupId, string Fingerprint, string Status);
