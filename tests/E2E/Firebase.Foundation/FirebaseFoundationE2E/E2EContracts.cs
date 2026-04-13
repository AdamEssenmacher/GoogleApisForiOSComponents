namespace FirebaseFoundationE2E;

public sealed class FirebaseE2ERunResult
{
    public string BundleId { get; set; } = string.Empty;
    public string? DefaultAppName { get; set; }
    public string? GoogleAppId { get; set; }
    public string? ProjectId { get; set; }
    public string? FirebaseVersion { get; set; }
    public string? InstallationsIdPreview { get; set; }
    public BindingSurfaceCoverageRunResult? BindingSurfaceCoverage { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string? FatalError { get; set; }
    public List<FirebaseE2ETestCaseResult> Cases { get; } = new();
}

public sealed class FirebaseE2ETestCaseResult
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public string? Message { get; set; }
    public string? ExceptionType { get; set; }
    public string? Detail { get; set; }
}

public sealed class BindingSurfaceCoverageRunResult
{
    public string Target { get; set; } = string.Empty;
    public int SurfaceCount { get; set; }
    public int ExercisedCount { get; set; }
    public int WaivedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BindingSurfaceCoverageTargetResult> Targets { get; } = new();
}

public sealed class BindingSurfaceCoverageTargetResult
{
    public string Target { get; set; } = string.Empty;
    public int SurfaceCount { get; set; }
    public int ExercisedCount { get; set; }
    public int WaivedCount { get; set; }
    public int FailedCount { get; set; }
    public List<BindingSurfaceCoverageFailure> Failures { get; } = new();
}

public sealed class BindingSurfaceCoverageFailure
{
    public string Target { get; set; } = string.Empty;
    public string SurfaceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? MemberName { get; set; }
    public string? Selector { get; set; }
    public string Message { get; set; } = string.Empty;
}
