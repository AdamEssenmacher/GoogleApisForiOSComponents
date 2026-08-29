namespace GoogleFoundationE2E;

public sealed class GoogleE2ERunResult
{
    public string BundleId { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? PackageVersion { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string? FatalError { get; set; }
    public List<GoogleE2ETestCaseResult> Cases { get; } = new();
}

public sealed class GoogleE2ETestCaseResult
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public string? Message { get; set; }
    public string? ExceptionType { get; set; }
    public string? Detail { get; set; }
}
