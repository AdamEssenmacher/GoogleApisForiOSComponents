using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FirebaseBindingAudit;

internal sealed record AuditOptions(
    string RepoRoot,
    string OutputDirectory,
    string GeneratorVersion,
    IReadOnlyList<string>? SelectedTargets,
    bool KeepTemp,
    string? SharpiePath,
    string SharpieVersion,
    bool DisableSharpie,
    bool DisableSuppressions);

internal sealed record AuditFinding(
    string Category,
    string Severity,
    string Message,
    string TypeName,
    string? MemberName,
    string? Selector,
    string? BaselineFile,
    string? GeneratedFile,
    string? ComparisonTypeKey = null,
    string? ComparisonMemberKey = null,
    string? Confidence = null,
    string? ConfidenceSource = null,
    string? SuppressionId = null,
    string? SuppressionReason = null,
    IReadOnlyList<string>? SuppressionEvidence = null);

internal sealed record ConfidenceSummary(
    int Confirmed,
    int Disputed,
    int LowConfidence,
    int NotReviewed);

internal sealed record TargetAuditResult(
    string Target,
    string Xcframework,
    string Status,
    string GenerationStatus,
    string ComparisonSource,
    int FailureCount,
    int InfoCount,
    int SuppressedCount,
    string SharpieStatus,
    ConfidenceSummary ConfidenceSummary,
    IReadOnlyList<AuditFinding> Findings,
    IReadOnlyList<AuditFinding> SuppressedFindings);

internal sealed record AuditRunResult(
    int ExitCode,
    string OutputDirectory,
    IReadOnlyList<TargetAuditResult> Results,
    string? TempDirectory,
    SharpieToolResolution SharpieResolution,
    SuppressionSummary SuppressionSummary);

internal sealed record SharpieToolResolution(
    string Status,
    string ExpectedVersion,
    string? CommandPath,
    string? ResolvedPath,
    string? Version,
    string? Message)
{
    public bool IsReady => string.Equals(Status, "resolved", StringComparison.Ordinal);
}

internal sealed record SharpieRunResult(
    string Status,
    string? FrameworkPath,
    ProcessResult? ProcessResult,
    TargetComparisonResult? Comparison,
    BindingSnapshot? Snapshot,
    IReadOnlyList<string> GeneratedFiles,
    IReadOnlyList<AuditFinding> WarningFindings,
    string? Message);

internal sealed class AuditRunner
{
    private static readonly Regex SwiftFrameworkDependencyRegex = new(
        "<SwiftFrameworkDependency Include=\"([^\"]+)\"\\s+PackageId=\"([^\"]+)\" PackageVersion=\"([^\"]+)\" />",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AuditConfiguration configuration;
    private readonly SuppressionConfiguration suppressionConfiguration;
    private readonly BindingSyntaxParser parser;
    private readonly BindingComparer comparer = new();

    public AuditRunner(AuditConfiguration configuration, SuppressionConfiguration suppressionConfiguration)
    {
        this.configuration = configuration;
        this.suppressionConfiguration = suppressionConfiguration;
        parser = new BindingSyntaxParser(configuration.ManualAttributes, configuration.BindingAttributes);
    }

    public async Task<AuditRunResult> RunAsync(AuditOptions options, CancellationToken cancellationToken = default)
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        var detailsDirectory = Path.Combine(outputDirectory, "details");
        var logsDirectory = Path.Combine(outputDirectory, "logs");
        PrepareOutputDirectory(outputDirectory, detailsDirectory, logsDirectory);

        var selectedTargets = SelectTargets(options.SelectedTargets);
        var tempRoot = CreateTempRootPath();
        Directory.CreateDirectory(tempRoot);
        var sharedNugetPackages = Path.Combine(Path.GetTempPath(), "firebase-binding-audit", "nuget-packages");
        var dotnetEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["NUGET_PACKAGES"] = sharedNugetPackages,
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
        };
        Directory.CreateDirectory(sharedNugetPackages);

        var sharpieResolution = await ResolveSharpieAsync(options, cancellationToken);

        try
        {
            var stagedFrameworksDirectory = Path.Combine(tempRoot, "frameworks");
            StageXcframeworks(options.RepoRoot, stagedFrameworksDirectory);
            var sharpieFrameworkSearchDirectory = Path.Combine(tempRoot, "sharpie-frameworks");
            var sharpieStageXcframeworks = GetSharpieFrameworkSliceStageXcframeworks(options, selectedTargets, configuration.Sharpie);
            if (sharpieStageXcframeworks.Count > 0)
            {
                StageSharpieFrameworkSlices(stagedFrameworksDirectory, sharpieFrameworkSearchDirectory, sharpieStageXcframeworks);
            }

            var sharedAliases = BuildSharedAliases(options.RepoRoot);
            var results = new List<TargetAuditResult>();
            foreach (var target in selectedTargets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await AuditTargetAsync(target, options, tempRoot, logsDirectory, dotnetEnvironment, sharedAliases, sharpieResolution, sharpieFrameworkSearchDirectory, cancellationToken);
                results.Add(result);
            }

            var suppressionApplication = SuppressionEngine.Apply(results, suppressionConfiguration, options.DisableSuppressions);
            var finalResults = suppressionApplication.Results;
            var exitCode = finalResults.Any(static result => result.Status == "failed") ||
                           suppressionApplication.Summary.InvalidRuleCount > 0
                ? 1
                : 0;

            await ReportWriter.WriteAsync(
                outputDirectory,
                detailsDirectory,
                options.GeneratorVersion,
                finalResults,
                tempRoot,
                sharpieResolution,
                suppressionApplication.Summary,
                cancellationToken);

            return new AuditRunResult(
                exitCode,
                outputDirectory,
                finalResults,
                options.KeepTemp ? tempRoot : null,
                sharpieResolution,
                suppressionApplication.Summary);
        }
        finally
        {
            if (!options.KeepTemp && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private IReadOnlyDictionary<string, string> BuildSharedAliases(string repoRoot)
    {
        var comparableFiles = configuration.Targets
            .SelectMany(target => target.BaselineFiles.Select(file => Path.Combine(target.BaselineDirectoryPath(repoRoot), file)))
            .ToList();
        return parser.Parse(comparableFiles, []).TypeAliases;
    }

    private async Task<TargetAuditResult> AuditTargetAsync(
        AuditTargetDefinition target,
        AuditOptions options,
        string tempRoot,
        string logsDirectory,
        IReadOnlyDictionary<string, string> dotnetEnvironment,
        IReadOnlyDictionary<string, string> sharedAliases,
        SharpieToolResolution sharpieResolution,
        string sharpieFrameworkSearchDirectory,
        CancellationToken cancellationToken)
    {
        var diagnosticFindings = new List<AuditFinding>();
        var baselineComparableFiles = target.BaselineFiles
            .Select(file => Path.Combine(target.BaselineDirectoryPath(options.RepoRoot), file))
            .ToList();
        var baselineHelperFiles = target.HelperFiles
            .Select(file => Path.Combine(target.BaselineDirectoryPath(options.RepoRoot), file))
            .ToList();
        var baselineSnapshot = parser.Parse(baselineComparableFiles, baselineHelperFiles);

        var generationStatus = "succeeded";
        var primaryFailed = false;
        var primaryComparableSurfaceEmpty = false;
        var sharpieStatus = options.DisableSharpie ? "disabled" : "not-requested";
        TargetComparisonResult? primaryComparison = null;
        TargetComparisonResult? sharpieComparison = null;
        SharpieRunResult? sharpieRun = null;
        var comparisonSource = "primary";

        var targetProjectsDirectory = Path.Combine(tempRoot, "projects");
        var projectName = $"FirebaseBindingAudit.{target.Id}";
        Directory.CreateDirectory(targetProjectsDirectory);
        var projectDirectory = Path.Combine(targetProjectsDirectory, projectName);
        Directory.CreateDirectory(projectDirectory);
        var stagedFrameworkPath = Path.Combine(tempRoot, "frameworks", $"{target.Xcframework}.xcframework");

        try
        {
            PrepareStagedXcframeworkForTarget(target, stagedFrameworkPath);
            var projectFile = CreateSwiftBindingProject(projectDirectory, projectName, stagedFrameworkPath, options.GeneratorVersion);
            var buildResult = await BuildTargetProjectAsync(projectFile, projectDirectory, dotnetEnvironment, cancellationToken);
            WriteLogs(logsDirectory, $"{target.Id}-build", buildResult);

            var primaryWarnings = BuildProcessWarningFindings(buildResult, target, "dotnet build", "generation-warning");
            diagnosticFindings.AddRange(primaryWarnings);
            if (primaryWarnings.Count > 0)
            {
                generationStatus = "succeeded_with_warnings";
            }

            if (buildResult.ExitCode != 0)
            {
                primaryFailed = true;
                generationStatus = "failed";
                diagnosticFindings.AddRange(BuildGenerationFailureFindings(buildResult, $"Binding generation failed for {target.Id}."));
            }
            else
            {
                var generatedBindingFiles = FindGeneratedBindingFiles(projectDirectory);
                if (generatedBindingFiles.Count == 0)
                {
                    primaryFailed = true;
                    generationStatus = "failed";
                    diagnosticFindings.Add(BuildPrimaryGenerationFinding(
                        category: "generation-failure",
                        severity: "failure",
                        target.PackageId,
                        $"Expected generated binding files were not found for {target.Id}."));
                }
                else
                {
                    var generatedSnapshot = parser.Parse(generatedBindingFiles, []);
                    primaryComparableSurfaceEmpty = CountComparableSurface(generatedSnapshot) == 0;
                    if (primaryComparableSurfaceEmpty)
                    {
                        diagnosticFindings.Add(BuildPrimaryGenerationFinding(
                            category: "generation-warning",
                            severity: "info",
                            target.PackageId,
                            $"Fresh output for {target.Id} contained zero comparable bound types, delegates, or enums."));
                        if (!string.Equals(generationStatus, "failed", StringComparison.Ordinal))
                        {
                            generationStatus = "succeeded_with_warnings";
                        }
                    }

                    primaryComparison = comparer.Compare(baselineSnapshot, generatedSnapshot, sharedAliases);
                }
            }
        }
        catch (Exception exception)
        {
            primaryFailed = true;
            generationStatus = "failed";
            diagnosticFindings.Add(BuildPrimaryGenerationFinding(
                category: "generation-failure",
                severity: "failure",
                target.PackageId,
                $"Failed to create or build the temp binding project for {target.Id}: {exception.Message}"));
        }

        var sharpieRequested = ShouldRequestSharpie(target, options, primaryFailed, primaryComparableSurfaceEmpty);
        if (sharpieRequested)
        {
            if (!sharpieResolution.IsReady)
            {
                sharpieStatus = sharpieResolution.Status;
                diagnosticFindings.Add(BuildSharpieStatusFinding(
                    target,
                    $"Sharpie fallback was requested for {target.Id}, but Sharpie is {sharpieResolution.Status}: {sharpieResolution.Message ?? "no additional details"}"));
            }
            else
            {
                try
                {
                    sharpieRun = await RunSharpieAsync(target, stagedFrameworkPath, tempRoot, logsDirectory, sharedAliases, baselineSnapshot, sharpieResolution, sharpieFrameworkSearchDirectory, cancellationToken);
                }
                catch (Exception exception)
                {
                    sharpieRun = new SharpieRunResult(
                        Status: "failed",
                        FrameworkPath: null,
                        ProcessResult: null,
                        Comparison: null,
                        Snapshot: null,
                        GeneratedFiles: [],
                        WarningFindings: [],
                        Message: $"Sharpie bind failed for {target.Id}: {exception.Message}");
                }

                sharpieStatus = sharpieRun.Status;
                diagnosticFindings.AddRange(sharpieRun.WarningFindings);
                sharpieComparison = sharpieRun.Comparison;

                if (!string.Equals(sharpieRun.Status, "succeeded", StringComparison.Ordinal))
                {
                    diagnosticFindings.Add(BuildSharpieStatusFinding(
                        target,
                        sharpieRun.Message ?? $"Sharpie completed with status '{sharpieRun.Status}' for {target.Id}."));
                }
            }
        }

        var effectiveFindings = new List<AuditFinding>(diagnosticFindings);
        if (ShouldUseSharpieComparisonFallback(
                target,
                target.EffectiveSharpieMode(configuration.Sharpie),
                primaryFailed,
                primaryComparableSurfaceEmpty,
                sharpieRun))
        {
            comparisonSource = "sharpie-fallback";
            generationStatus = GetFallbackGenerationStatus(generationStatus, primaryFailed, primaryComparableSurfaceEmpty);
            effectiveFindings = DemotePrimaryGenerationFailuresForFallback(effectiveFindings);
            effectiveFindings.Add(BuildComparisonFallbackFinding(target, primaryFailed, primaryComparableSurfaceEmpty, sharpieRun!.FrameworkPath));
            effectiveFindings.AddRange(sharpieComparison!.Failures);
            effectiveFindings.AddRange(sharpieComparison.Infos);
        }
        else
        {
            if (primaryComparison is not null)
            {
                effectiveFindings.AddRange(primaryComparison.Failures);
                effectiveFindings.AddRange(primaryComparison.Infos);
            }
        }

        var annotatedFindings = AnnotateFindings(effectiveFindings, sharpieRequested, sharpieStatus, sharpieComparison, comparisonSource);
        var status = annotatedFindings.Any(static finding => finding.Severity == "failure") ? "failed" : "passed";
        return BuildTargetResult(target, status, generationStatus, comparisonSource, sharpieStatus, BuildConfidenceSummary(annotatedFindings), annotatedFindings);
    }

    private bool ShouldRequestSharpie(AuditTargetDefinition target, AuditOptions options, bool primaryFailed, bool primaryComparableSurfaceEmpty)
    {
        if (options.DisableSharpie)
        {
            return false;
        }

        var mode = target.EffectiveSharpieMode(configuration.Sharpie);
        if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(mode, "forceFallback", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return primaryFailed || primaryComparableSurfaceEmpty;
    }

    internal static bool ShouldUseSharpieComparisonFallback(
        AuditTargetDefinition target,
        string sharpieMode,
        bool primaryFailed,
        bool primaryComparableSurfaceEmpty,
        SharpieRunResult? sharpieRun)
    {
        var forceFallback = string.Equals(sharpieMode, "forceFallback", StringComparison.OrdinalIgnoreCase);

        return target.UseSharpieComparisonFallback &&
               (forceFallback || primaryFailed || primaryComparableSurfaceEmpty) &&
               sharpieRun is not null &&
               string.Equals(sharpieRun.Status, "succeeded", StringComparison.Ordinal) &&
               sharpieRun.Comparison is not null;
    }

    internal static bool ShouldStageSharpieFrameworkSlices(
        AuditOptions options,
        IReadOnlyList<AuditTargetDefinition> selectedTargets,
        SharpieConfiguration sharpieConfiguration) =>
        GetSharpieFrameworkSliceStageXcframeworks(options, selectedTargets, sharpieConfiguration).Count > 0;

    internal static IReadOnlyList<string> GetSharpieFrameworkSliceStageXcframeworks(
        AuditOptions options,
        IReadOnlyList<AuditTargetDefinition> selectedTargets,
        SharpieConfiguration sharpieConfiguration)
    {
        if (options.DisableSharpie)
        {
            return [];
        }

        return selectedTargets
            .Where(target => !string.Equals(target.EffectiveSharpieMode(sharpieConfiguration), "off", StringComparison.OrdinalIgnoreCase))
            .Select(static target => target.Xcframework)
            .Where(static xcframework => !string.IsNullOrWhiteSpace(xcframework))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    internal static string CreateTempRootPath() =>
        Path.Combine(Path.GetTempPath(), "firebase-binding-audit", $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}");

    private static ConfidenceSummary BuildConfidenceSummary(IReadOnlyList<AuditFinding> findings)
    {
        return AuditFindingSummary.BuildConfidenceSummary(findings.ToList());
    }

    private static List<AuditFinding> AnnotateFindings(
        IReadOnlyList<AuditFinding> findings,
        bool sharpieRequested,
        string sharpieStatus,
        TargetComparisonResult? sharpieComparison,
        string comparisonSource)
    {
        if (string.Equals(comparisonSource, "sharpie-fallback", StringComparison.Ordinal))
        {
            return findings.Select(finding =>
            {
                if (IsComparisonFailureFinding(finding))
                {
                    return finding with { Confidence = "confirmed", ConfidenceSource = "sharpie-fallback-comparison-source" };
                }

                return finding with { Confidence = "not-reviewed", ConfidenceSource = "sharpie-fallback-note" };
            }).ToList();
        }

        if (!sharpieRequested)
        {
            return findings.Select(finding => finding with
            {
                Confidence = "not-reviewed",
                ConfidenceSource = "sharpie-not-requested"
            }).ToList();
        }

        if (!string.Equals(sharpieStatus, "succeeded", StringComparison.Ordinal) || sharpieComparison is null)
        {
            var (confidence, source) = sharpieStatus switch
            {
                "disabled" => ("not-reviewed", "sharpie-disabled"),
                "not-requested" => ("not-reviewed", "sharpie-not-requested"),
                _ => ("low-confidence", $"sharpie-{sharpieStatus}")
            };

            return findings.Select(finding => ApplyBaseConfidence(finding, confidence, source)).ToList();
        }

        var exactMatches = sharpieComparison.Failures
            .Select(CreateComparisonMatchKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
        var entityMatches = sharpieComparison.Failures
            .Select(CreateComparisonEntityKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);

        return findings.Select(finding =>
        {
            if (IsComparisonFailureFinding(finding))
            {
                var exactKey = CreateComparisonMatchKey(finding);
                if (!string.IsNullOrWhiteSpace(exactKey) && exactMatches.Contains(exactKey))
                {
                    return finding with { Confidence = "confirmed", ConfidenceSource = "sharpie-exact-match" };
                }

                var entityKey = CreateComparisonEntityKey(finding);
                if (!string.IsNullOrWhiteSpace(entityKey) && entityMatches.Contains(entityKey))
                {
                    return finding with { Confidence = "confirmed", ConfidenceSource = "sharpie-entity-match" };
                }

                return finding with { Confidence = "disputed", ConfidenceSource = "sharpie-no-match" };
            }

            if (IsPrimaryGenerationFinding(finding))
            {
                return finding with { Confidence = "disputed", ConfidenceSource = "sharpie-generated-comparable-surface" };
            }

            return finding with { Confidence = "not-reviewed", ConfidenceSource = "sharpie-not-applicable" };
        }).ToList();
    }

    private static AuditFinding ApplyBaseConfidence(AuditFinding finding, string confidence, string source)
    {
        if (IsComparisonFailureFinding(finding) || IsPrimaryGenerationFinding(finding))
        {
            return finding with { Confidence = confidence, ConfidenceSource = source };
        }

        return finding with { Confidence = "not-reviewed", ConfidenceSource = "sharpie-not-applicable" };
    }

    private static bool IsComparisonFailureFinding(AuditFinding finding)
    {
        return string.Equals(finding.Category, "stale-baseline-binding", StringComparison.Ordinal) ||
               string.Equals(finding.Category, "missing-baseline-binding", StringComparison.Ordinal) ||
               string.Equals(finding.Category, "signature-drift", StringComparison.Ordinal) ||
               string.Equals(finding.Category, "attribute-drift", StringComparison.Ordinal);
    }

    private static bool IsPrimaryGenerationFinding(AuditFinding finding)
    {
        return string.Equals(finding.ComparisonTypeKey, "generation", StringComparison.Ordinal);
    }

    private static string? CreateComparisonMatchKey(AuditFinding finding)
    {
        if (string.IsNullOrWhiteSpace(finding.ComparisonTypeKey))
        {
            return null;
        }

        return $"{finding.Category}|{finding.ComparisonTypeKey}|{finding.ComparisonMemberKey ?? "-"}";
    }

    private static string? CreateComparisonEntityKey(AuditFinding finding)
    {
        if (string.IsNullOrWhiteSpace(finding.ComparisonTypeKey))
        {
            return null;
        }

        return $"{finding.ComparisonTypeKey}|{finding.ComparisonMemberKey ?? "-"}";
    }

    private static AuditFinding BuildPrimaryGenerationFinding(string category, string severity, string typeName, string message)
    {
        return new AuditFinding(
            Category: category,
            Severity: severity,
            Message: message,
            TypeName: typeName,
            MemberName: null,
            Selector: null,
            BaselineFile: null,
            GeneratedFile: null,
            ComparisonTypeKey: "generation",
            ComparisonMemberKey: category);
    }

    private static AuditFinding BuildSharpieStatusFinding(AuditTargetDefinition target, string message)
    {
        return new AuditFinding(
            Category: "sharpie-warning",
            Severity: "info",
            Message: message,
            TypeName: target.PackageId,
            MemberName: null,
            Selector: null,
            BaselineFile: null,
            GeneratedFile: null);
    }

    private static List<AuditFinding> DemotePrimaryGenerationFailuresForFallback(IReadOnlyList<AuditFinding> findings)
    {
        return findings.Select(finding =>
        {
            if (!IsPrimaryGenerationFinding(finding))
            {
                return finding;
            }

            return finding with
            {
                Category = "generation-warning",
                Severity = "info"
            };
        }).ToList();
    }

    private static string GetFallbackGenerationStatus(string generationStatus, bool primaryFailed, bool primaryComparableSurfaceEmpty)
    {
        if (primaryFailed)
        {
            return "primary-failed-used-sharpie-fallback";
        }

        if (primaryComparableSurfaceEmpty)
        {
            return "primary-inconclusive-used-sharpie-fallback";
        }

        return generationStatus;
    }

    private static AuditFinding BuildComparisonFallbackFinding(
        AuditTargetDefinition target,
        bool primaryFailed,
        bool primaryComparableSurfaceEmpty,
        string? sharpieFrameworkPath)
    {
        var reason = primaryFailed
            ? "Primary swift-dotnet-bindings generation was not usable"
            : primaryComparableSurfaceEmpty
                ? "Primary swift-dotnet-bindings output had zero comparable surface"
                : "Configured to use Sharpie fallback output";
        var frameworkName = string.IsNullOrWhiteSpace(sharpieFrameworkPath) ? null : Path.GetFileName(sharpieFrameworkPath);
        var suffix = frameworkName is null ? "." : $" via '{frameworkName}'.";

        return new AuditFinding(
            Category: "comparison-fallback",
            Severity: "info",
            Message: $"{reason} for {target.Id}; using Sharpie output as the comparison source{suffix}",
            TypeName: target.PackageId,
            MemberName: null,
            Selector: null,
            BaselineFile: null,
            GeneratedFile: null);
    }

    private static int CountComparableSurface(BindingSnapshot snapshot)
    {
        return snapshot.BoundTypes.Count + snapshot.Delegates.Count + snapshot.Enums.Count;
    }

    private async Task<SharpieRunResult> RunSharpieAsync(
        AuditTargetDefinition target,
        string stagedXcframeworkPath,
        string tempRoot,
        string logsDirectory,
        IReadOnlyDictionary<string, string> sharedAliases,
        BindingSnapshot baselineSnapshot,
        SharpieToolResolution sharpieResolution,
        string sharpieFrameworkSearchDirectory,
        CancellationToken cancellationToken)
    {
        var sharpieDirectory = Path.Combine(tempRoot, "sharpie", target.Id);
        ResetDirectory(sharpieDirectory);
        var warningFindings = new List<AuditFinding>();
        var attemptedFrameworkPath = ResolveSharpieFrameworkPath(stagedXcframeworkPath);

        var firstAttempt = await ExecuteSharpieBindAsync(
            target,
            sharpieResolution,
            attemptedFrameworkPath,
            sharpieDirectory,
            logsDirectory,
            $"{target.Id}-sharpie",
            sharpieFrameworkSearchDirectory,
            includeFrameworkSearchPath: true,
            cancellationToken);

        warningFindings.AddRange(firstAttempt.WarningFindings);
        if (firstAttempt.ProcessResult.ExitCode != 0)
        {
            return new SharpieRunResult(
                Status: "failed",
                FrameworkPath: attemptedFrameworkPath,
                ProcessResult: firstAttempt.ProcessResult,
                Comparison: null,
                Snapshot: null,
                GeneratedFiles: [],
                WarningFindings: warningFindings,
                Message: $"Sharpie bind failed for {target.Id}. Exit code: {firstAttempt.ProcessResult.ExitCode}.");
        }

        var generatedFiles = FindSharpieBindingFiles(sharpieDirectory);
        if (generatedFiles.Count == 0 &&
            TryResolveSharpieCompanionFrameworkPath(attemptedFrameworkPath, Path.GetDirectoryName(stagedXcframeworkPath) ?? string.Empty, out var companionFrameworkPath))
        {
            warningFindings.Add(BuildSharpieStatusFinding(
                target,
                $"Sharpie generated no files for '{Path.GetFileName(attemptedFrameworkPath)}'; retrying with companion framework '{Path.GetFileName(companionFrameworkPath)}'."));

            ResetDirectory(sharpieDirectory);
            attemptedFrameworkPath = companionFrameworkPath;
            var companionAttempt = await ExecuteSharpieBindAsync(
                target,
                sharpieResolution,
                attemptedFrameworkPath,
                sharpieDirectory,
                logsDirectory,
                $"{target.Id}-sharpie-companion",
                sharpieFrameworkSearchDirectory,
                includeFrameworkSearchPath: false,
                cancellationToken);

            warningFindings.AddRange(companionAttempt.WarningFindings);
            if (companionAttempt.ProcessResult.ExitCode != 0)
            {
                return new SharpieRunResult(
                    Status: "failed",
                    FrameworkPath: attemptedFrameworkPath,
                    ProcessResult: companionAttempt.ProcessResult,
                    Comparison: null,
                    Snapshot: null,
                    GeneratedFiles: [],
                    WarningFindings: warningFindings,
                    Message: $"Sharpie bind failed for {target.Id} when retrying companion framework '{Path.GetFileName(attemptedFrameworkPath)}'. Exit code: {companionAttempt.ProcessResult.ExitCode}.");
            }

            generatedFiles = FindSharpieBindingFiles(sharpieDirectory);
        }

        if (generatedFiles.Count == 0)
        {
            return new SharpieRunResult(
                Status: "succeeded_empty",
                FrameworkPath: attemptedFrameworkPath,
                ProcessResult: firstAttempt.ProcessResult,
                Comparison: null,
                Snapshot: null,
                GeneratedFiles: [],
                WarningFindings: warningFindings,
                Message: $"Sharpie completed for {target.Id}, but no binding files were generated.");
        }

        var snapshot = parser.Parse(generatedFiles, []);
        if (CountComparableSurface(snapshot) == 0)
        {
            return new SharpieRunResult(
                Status: "succeeded_empty",
                FrameworkPath: attemptedFrameworkPath,
                ProcessResult: firstAttempt.ProcessResult,
                Comparison: null,
                Snapshot: snapshot,
                GeneratedFiles: generatedFiles,
                WarningFindings: warningFindings,
                Message: $"Sharpie completed for {target.Id}, but its output contained zero comparable bound types, delegates, or enums.");
        }

        var comparison = comparer.Compare(baselineSnapshot, snapshot, sharedAliases);
        return new SharpieRunResult(
            Status: "succeeded",
            FrameworkPath: attemptedFrameworkPath,
            ProcessResult: firstAttempt.ProcessResult,
            Comparison: comparison,
            Snapshot: snapshot,
            GeneratedFiles: generatedFiles,
            WarningFindings: warningFindings,
            Message: null);
    }

    private static async Task<(ProcessResult ProcessResult, IReadOnlyList<AuditFinding> WarningFindings)> ExecuteSharpieBindAsync(
        AuditTargetDefinition target,
        SharpieToolResolution sharpieResolution,
        string frameworkPath,
        string outputDirectory,
        string logsDirectory,
        string logPrefix,
        string sharpieFrameworkSearchDirectory,
        bool includeFrameworkSearchPath,
        CancellationToken cancellationToken)
    {
        var workingDirectory = Directory.GetParent(outputDirectory)?.FullName ?? Path.GetTempPath();
        var arguments = new List<string> { "bind", "--framework", frameworkPath, "--output", outputDirectory };
        if (includeFrameworkSearchPath)
        {
            arguments.AddRange(["-c", "-F", sharpieFrameworkSearchDirectory]);
        }

        var processResult = await RunProcessAsync(
            sharpieResolution.CommandPath ?? "sharpie",
            arguments,
            workingDirectory,
            null,
            cancellationToken);
        WriteLogs(logsDirectory, logPrefix, processResult);
        return (processResult, BuildProcessWarningFindings(processResult, target, "sharpie bind", "sharpie-warning"));
    }

    internal static bool TryResolveSharpieCompanionFrameworkPath(string frameworkPath, string stagedFrameworksDirectory, out string companionFrameworkPath)
    {
        var frameworkName = Path.GetFileNameWithoutExtension(frameworkPath);
        var rootHeaderPath = Path.Combine(frameworkPath, "Headers", $"{frameworkName}.h");
        if (File.Exists(rootHeaderPath))
        {
            foreach (Match importMatch in Regex.Matches(
                         File.ReadAllText(rootHeaderPath),
                         "#import\\s+<([^/]+)/[^>]+>",
                         RegexOptions.CultureInvariant))
            {
                var companionModuleName = importMatch.Groups[1].Value;
                if (!string.Equals(companionModuleName, frameworkName, StringComparison.Ordinal))
                {
                    var companionXcframeworkPath = Path.Combine(stagedFrameworksDirectory, $"{companionModuleName}.xcframework");
                    if (Directory.Exists(companionXcframeworkPath))
                    {
                        companionFrameworkPath = ResolveSharpieFrameworkPath(companionXcframeworkPath);
                        return true;
                    }
                }
            }
        }

        companionFrameworkPath = string.Empty;
        return false;
    }

    private static string ResolveSharpieFrameworkPath(string xcframeworkPath)
    {
        var infoPlistPath = Path.Combine(xcframeworkPath, "Info.plist");
        if (File.Exists(infoPlistPath))
        {
            var libraries = ParseAvailableLibraries(infoPlistPath);
            var preferredLibrary = libraries
                .OrderBy(static library => RankSharpieLibraryCandidate(library))
                .FirstOrDefault(static library => string.Equals(GetLibraryValue(library, "SupportedPlatform"), "ios", StringComparison.OrdinalIgnoreCase));

            if (preferredLibrary is not null)
            {
                var libraryIdentifier = GetLibraryValue(preferredLibrary, "LibraryIdentifier");
                var libraryPath = GetLibraryValue(preferredLibrary, "LibraryPath");
                if (!string.IsNullOrWhiteSpace(libraryIdentifier) && !string.IsNullOrWhiteSpace(libraryPath))
                {
                    var frameworkPath = Path.Combine(xcframeworkPath, libraryIdentifier, libraryPath);
                    if (Directory.Exists(frameworkPath))
                    {
                        return frameworkPath;
                    }
                }
            }
        }

        var fallbackFramework = Directory.EnumerateDirectories(xcframeworkPath, "*.framework", SearchOption.AllDirectories)
            .OrderBy(path => RankSharpieFrameworkPath(path))
            .FirstOrDefault();

        if (fallbackFramework is not null)
        {
            return fallbackFramework;
        }

        throw new InvalidOperationException($"Unable to resolve a concrete framework slice for '{xcframeworkPath}'.");
    }

    private static List<Dictionary<string, string>> ParseAvailableLibraries(string plistPath)
    {
        var document = XDocument.Load(plistPath);
        var rootDictionary = document.Root?.Element("dict");
        if (rootDictionary is null)
        {
            return [];
        }

        foreach (var (key, value) in EnumerateDictionaryEntries(rootDictionary))
        {
            if (!string.Equals(key, "AvailableLibraries", StringComparison.Ordinal) || !string.Equals(value.Name.LocalName, "array", StringComparison.Ordinal))
            {
                continue;
            }

            return value.Elements("dict")
                .Select(ParseStringDictionary)
                .ToList();
        }

        return [];
    }

    private static IEnumerable<(string Key, XElement Value)> EnumerateDictionaryEntries(XElement dictionaryElement)
    {
        string? currentKey = null;
        foreach (var child in dictionaryElement.Elements())
        {
            if (string.Equals(child.Name.LocalName, "key", StringComparison.Ordinal))
            {
                currentKey = child.Value;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                yield return (currentKey, child);
                currentKey = null;
            }
        }
    }

    private static Dictionary<string, string> ParseStringDictionary(XElement dictionaryElement)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in EnumerateDictionaryEntries(dictionaryElement))
        {
            values[key] = value.Name.LocalName switch
            {
                "true" => "true",
                "false" => "false",
                _ => value.Value
            };
        }

        return values;
    }

    private static int RankSharpieLibraryCandidate(IReadOnlyDictionary<string, string> library)
    {
        var identifier = GetLibraryValue(library, "LibraryIdentifier");
        var variant = GetLibraryValue(library, "SupportedPlatformVariant");
        if (string.Equals(identifier, "ios-arm64", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(variant))
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(identifier) &&
            identifier.StartsWith("ios-", StringComparison.OrdinalIgnoreCase) &&
            !identifier.Contains("simulator", StringComparison.OrdinalIgnoreCase) &&
            !identifier.Contains("maccatalyst", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(variant))
        {
            return 2;
        }

        return 3;
    }

    private static string? GetLibraryValue(IReadOnlyDictionary<string, string> library, string key)
    {
        return library.TryGetValue(key, out var value) ? value : null;
    }

    private static int RankSharpieFrameworkPath(string frameworkPath)
    {
        var normalizedPath = frameworkPath.Replace('\\', '/');
        if (normalizedPath.Contains("/ios-arm64/", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedPath.Contains("/ios-", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.Contains("simulator", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.Contains("maccatalyst", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }

    private static List<string> FindSharpieBindingFiles(string outputDirectory)
    {
        var generatedFiles = new List<string>();
        var apiDefinitionPath = Directory.EnumerateFiles(outputDirectory, "ApiDefinition.cs", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
        var structsAndEnumsPath = Directory.EnumerateFiles(outputDirectory, "StructsAndEnums.cs", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();

        if (apiDefinitionPath is not null)
        {
            generatedFiles.Add(apiDefinitionPath);
        }

        if (structsAndEnumsPath is not null)
        {
            generatedFiles.Add(structsAndEnumsPath);
        }

        return generatedFiles;
    }

    private async Task<SharpieToolResolution> ResolveSharpieAsync(AuditOptions options, CancellationToken cancellationToken)
    {
        if (options.DisableSharpie)
        {
            return new SharpieToolResolution(
                Status: "disabled",
                ExpectedVersion: options.SharpieVersion,
                CommandPath: options.SharpiePath,
                ResolvedPath: options.SharpiePath,
                Version: null,
                Message: "Sharpie was disabled via --disable-sharpie.");
        }

        var commandPath = options.SharpiePath ?? "sharpie";
        string? resolvedPath = options.SharpiePath;
        try
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                resolvedPath = await ResolveCommandPathAsync(commandPath, options.RepoRoot, cancellationToken);
            }

            var versionResult = await RunProcessAsync(commandPath, ["--version"], options.RepoRoot, null, cancellationToken);
            var version = versionResult.AllLines().FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line))?.Trim();
            if (versionResult.ExitCode != 0 || string.IsNullOrWhiteSpace(version))
            {
                return new SharpieToolResolution(
                    Status: "error",
                    ExpectedVersion: options.SharpieVersion,
                    CommandPath: commandPath,
                    ResolvedPath: resolvedPath,
                    Version: version,
                    Message: $"Unable to determine Sharpie version. Exit code: {versionResult.ExitCode}.");
            }

            if (version.Contains(options.SharpieVersion, StringComparison.Ordinal))
            {
                return new SharpieToolResolution(
                    Status: "resolved",
                    ExpectedVersion: options.SharpieVersion,
                    CommandPath: commandPath,
                    ResolvedPath: resolvedPath,
                    Version: version,
                    Message: null);
            }

            return new SharpieToolResolution(
                Status: "incompatible",
                ExpectedVersion: options.SharpieVersion,
                CommandPath: commandPath,
                ResolvedPath: resolvedPath,
                Version: version,
                Message: $"Expected Sharpie {options.SharpieVersion}, but found '{version}'.");
        }
        catch (Exception exception)
        {
            return new SharpieToolResolution(
                Status: "missing",
                ExpectedVersion: options.SharpieVersion,
                CommandPath: commandPath,
                ResolvedPath: resolvedPath,
                Version: null,
                Message: exception.Message);
        }
    }

    private static async Task<string?> ResolveCommandPathAsync(string commandName, string workingDirectory, CancellationToken cancellationToken)
    {
        var whichResult = await RunProcessAsync("/usr/bin/which", [commandName], workingDirectory, null, cancellationToken);
        return whichResult.ExitCode == 0
            ? whichResult.StandardOutput.Trim()
            : null;
    }

    private static async Task<ProcessResult> BuildTargetProjectAsync(
        string projectFile,
        string projectDirectory,
        IReadOnlyDictionary<string, string> dotnetEnvironment,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var knownDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ProcessResult? lastResult = null;

        while (attempt < 4)
        {
            lastResult = await RunProcessAsync(
                "dotnet",
                ["build", "-nologo", "-v", "minimal", "-p:RestoreSources=https://api.nuget.org/v3/index.json", "-p:NuGetAudit=false"],
                projectDirectory,
                dotnetEnvironment,
                cancellationToken);

            var addedDependencies = ExtractSuggestedDependencies(lastResult)
                .Where(dependency => knownDependencies.Add(dependency.Include))
                .ToList();

            if (addedDependencies.Count == 0)
            {
                return lastResult;
            }

            UpsertSwiftFrameworkDependencies(projectFile, addedDependencies);
            attempt++;
        }

        return lastResult ?? throw new InvalidOperationException("Build process did not produce a result.");
    }

    private static IEnumerable<SwiftFrameworkDependencySpec> ExtractSuggestedDependencies(ProcessResult result)
    {
        var content = $"{result.StandardOutput}{Environment.NewLine}{result.StandardError}";
        foreach (Match match in SwiftFrameworkDependencyRegex.Matches(content))
        {
            yield return new SwiftFrameworkDependencySpec(
                Include: match.Groups[1].Value,
                PackageId: match.Groups[2].Value,
                PackageVersion: match.Groups[3].Value);
        }
    }

    private static void UpsertSwiftFrameworkDependencies(string projectFile, IEnumerable<SwiftFrameworkDependencySpec> dependencies)
    {
        var document = XDocument.Load(projectFile, LoadOptions.PreserveWhitespace);
        var projectElement = document.Root ?? throw new InvalidOperationException($"The project file '{projectFile}' is invalid.");
        var existingDependencies = new HashSet<string>(
            projectElement
                .Descendants("SwiftFrameworkDependency")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(static value => !string.IsNullOrWhiteSpace(value))!,
            StringComparer.OrdinalIgnoreCase);

        var newDependencies = dependencies
            .Where(dependency => existingDependencies.Add(dependency.Include))
            .ToList();

        if (newDependencies.Count == 0)
        {
            return;
        }

        var itemGroup = new XElement("ItemGroup");
        foreach (var dependency in newDependencies)
        {
            itemGroup.Add(new XElement(
                "SwiftFrameworkDependency",
                new XAttribute("Include", dependency.Include),
                new XAttribute("PackageId", dependency.PackageId),
                new XAttribute("PackageVersion", dependency.PackageVersion)));
        }

        projectElement.Add(itemGroup);
        document.Save(projectFile);
    }

    private TargetAuditResult BuildTargetResult(
        AuditTargetDefinition target,
        string status,
        string generationStatus,
        string comparisonSource,
        string sharpieStatus,
        ConfidenceSummary confidenceSummary,
        IReadOnlyList<AuditFinding> findings)
    {
        return new TargetAuditResult(
            Target: target.Id,
            Xcframework: target.Xcframework,
            Status: status,
            GenerationStatus: generationStatus,
            ComparisonSource: comparisonSource,
            FailureCount: findings.Count(static finding => finding.Severity == "failure"),
            InfoCount: findings.Count(static finding => finding.Severity == "info"),
            SuppressedCount: 0,
            SharpieStatus: sharpieStatus,
            ConfidenceSummary: confidenceSummary,
            Findings: findings,
            SuppressedFindings: []);
    }

    private List<AuditTargetDefinition> SelectTargets(IReadOnlyList<string>? selectedTargets)
    {
        if (selectedTargets is null || selectedTargets.Count == 0)
        {
            return configuration.Targets;
        }

        var lookup = new HashSet<string>(selectedTargets, StringComparer.OrdinalIgnoreCase);
        var matches = configuration.Targets
            .Where(target => lookup.Contains(target.Id))
            .ToList();

        var missing = selectedTargets
            .Where(selection => matches.All(match => !string.Equals(match.Id, selection, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Unknown target(s): {string.Join(", ", missing)}");
        }

        return matches;
    }

    private static void PrepareOutputDirectory(string outputDirectory, string detailsDirectory, string logsDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        if (Directory.Exists(detailsDirectory))
        {
            Directory.Delete(detailsDirectory, recursive: true);
        }

        if (Directory.Exists(logsDirectory))
        {
            Directory.Delete(logsDirectory, recursive: true);
        }

        Directory.CreateDirectory(detailsDirectory);
        Directory.CreateDirectory(logsDirectory);
    }

    private static void StageXcframeworks(string repoRoot, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var externalsDirectory = Path.Combine(repoRoot, "externals");
        foreach (var xcframeworkDirectory in Directory.EnumerateDirectories(externalsDirectory, "*.xcframework", SearchOption.TopDirectoryOnly))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(xcframeworkDirectory));
            TryCreateDirectorySymlink(destinationPath, xcframeworkDirectory);
        }
    }

    internal static void PrepareStagedXcframeworkForTarget(AuditTargetDefinition target, string stagedXcframeworkPath)
    {
        if (target.ObjcUmbrellaHeaderImports.Length == 0)
        {
            return;
        }

        MaterializeStagedDirectory(stagedXcframeworkPath);
        AddObjcUmbrellaHeaderImports(stagedXcframeworkPath, target.ObjcUmbrellaHeaderImports);
    }

    private static void MaterializeStagedDirectory(string stagedDirectoryPath)
    {
        var directoryInfo = new DirectoryInfo(stagedDirectoryPath);
        if (!directoryInfo.Exists || (directoryInfo.Attributes & FileAttributes.ReparsePoint) == 0)
        {
            return;
        }

        var linkTarget = directoryInfo.LinkTarget;
        if (string.IsNullOrWhiteSpace(linkTarget))
        {
            throw new InvalidOperationException($"Unable to resolve symlink target for '{stagedDirectoryPath}'.");
        }

        var sourcePath = Path.IsPathRooted(linkTarget)
            ? linkTarget
            : Path.GetFullPath(Path.Combine(directoryInfo.Parent?.FullName ?? Directory.GetCurrentDirectory(), linkTarget));
        if (!Directory.Exists(sourcePath))
        {
            throw new InvalidOperationException($"Symlink target '{sourcePath}' does not exist for '{stagedDirectoryPath}'.");
        }

        Directory.Delete(stagedDirectoryPath);
        CopyDirectory(sourcePath, stagedDirectoryPath);
    }

    private static void AddObjcUmbrellaHeaderImports(string xcframeworkPath, IReadOnlyList<string> imports)
    {
        var importLines = imports
            .Select(NormalizeObjcImportLine)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (importLines.Count == 0)
        {
            return;
        }

        foreach (var umbrellaHeaderPath in Directory.EnumerateFiles(xcframeworkPath, "*-umbrella.h", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(umbrellaHeaderPath);
            var additions = importLines
                .Where(importLine => !content.Contains(importLine, StringComparison.Ordinal))
                .ToList();
            if (additions.Count == 0)
            {
                continue;
            }

            var builder = new StringBuilder(content);
            if (builder.Length > 0 && builder[^1] != '\n')
            {
                builder.AppendLine();
            }

            foreach (var addition in additions)
            {
                builder.AppendLine(addition);
            }

            File.WriteAllText(umbrellaHeaderPath, builder.ToString());
        }
    }

    private static string NormalizeObjcImportLine(string import)
    {
        var trimmedImport = import.Trim();
        if (trimmedImport.StartsWith("#import ", StringComparison.Ordinal))
        {
            return trimmedImport;
        }

        if (trimmedImport.StartsWith("<", StringComparison.Ordinal) || trimmedImport.StartsWith("\"", StringComparison.Ordinal))
        {
            return $"#import {trimmedImport}";
        }

        return $"#import <{trimmedImport}>";
    }

    private static void StageSharpieFrameworkSlices(
        string stagedFrameworksDirectory,
        string destinationDirectory,
        IReadOnlyList<string> xcframeworkNames)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var xcframeworkName in xcframeworkNames)
        {
            var xcframeworkDirectory = Path.Combine(stagedFrameworksDirectory, $"{xcframeworkName}.xcframework");
            if (!Directory.Exists(xcframeworkDirectory))
            {
                continue;
            }

            string frameworkPath;
            try
            {
                frameworkPath = ResolveSharpieFrameworkPath(xcframeworkDirectory);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(frameworkPath));
            TryCreateDirectorySymlink(destinationPath, frameworkPath);
        }
    }

    private static void TryCreateDirectorySymlink(string destinationPath, string sourcePath)
    {
        if (Directory.Exists(destinationPath))
        {
            return;
        }

        try
        {
            Directory.CreateSymbolicLink(destinationPath, sourcePath);
        }
        catch
        {
            CopyDirectory(sourcePath, destinationPath);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }
    }

    private static void ResetDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }

        Directory.CreateDirectory(directoryPath);
    }

    private static string CreateSwiftBindingProject(string projectDirectory, string projectName, string xcframeworkPath, string generatorVersion)
    {
        var projectFile = Path.Combine(projectDirectory, $"{projectName}.csproj");
        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = true
        };

        using var stringWriter = new StringWriter();
        using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
        {
            xmlWriter.WriteStartElement("Project");
            xmlWriter.WriteAttributeString("Sdk", $"SwiftBindings.Sdk/{generatorVersion}");

            xmlWriter.WriteStartElement("PropertyGroup");
            xmlWriter.WriteElementString("TargetFramework", "net10.0-ios");
            xmlWriter.WriteElementString("SwiftFrameworkType", "ObjC");
            xmlWriter.WriteElementString("SwiftWrapperRequired", "false");
            xmlWriter.WriteEndElement();

            xmlWriter.WriteStartElement("ItemGroup");
            xmlWriter.WriteStartElement("SwiftFramework");
            xmlWriter.WriteAttributeString("Include", xcframeworkPath);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndElement();

            xmlWriter.WriteEndElement();
        }

        File.WriteAllText(projectFile, stringWriter.ToString());
        return projectFile;
    }

    private static string? FindGeneratedFile(string projectDirectory, string fileName)
    {
        return Directory.EnumerateFiles(projectDirectory, fileName, SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}swift-binding{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static List<string> FindGeneratedBindingFiles(string projectDirectory)
    {
        var generatedFiles = new List<string>();
        var apiDefinitionPath = FindGeneratedFile(projectDirectory, "ApiDefinition.cs");
        var structsAndEnumsPath = FindGeneratedFile(projectDirectory, "StructsAndEnums.cs");

        if (apiDefinitionPath is not null)
        {
            generatedFiles.Add(apiDefinitionPath);
        }

        if (structsAndEnumsPath is not null)
        {
            generatedFiles.Add(structsAndEnumsPath);
        }

        return generatedFiles;
    }

    private static List<AuditFinding> BuildGenerationFailureFindings(ProcessResult result, string summary)
    {
        var findings = new List<AuditFinding>
        {
            new(
                Category: "generation-failure",
                Severity: "failure",
                Message: $"{summary} Exit code: {result.ExitCode}.",
                TypeName: "generation",
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: null,
                ComparisonTypeKey: "generation",
                ComparisonMemberKey: "generation-failure")
        };

        var firstDiagnosticLine = result.AllLines().FirstOrDefault(IsActionableWarningLine);
        if (!string.IsNullOrWhiteSpace(firstDiagnosticLine))
        {
            findings.Add(new AuditFinding(
                Category: "generation-warning",
                Severity: "info",
                Message: $"dotnet build: {firstDiagnosticLine}",
                TypeName: "generation",
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: null));
        }

        return findings;
    }

    private static List<AuditFinding> BuildProcessWarningFindings(ProcessResult processResult, AuditTargetDefinition target, string commandName, string category)
    {
        return processResult.AllLines()
            .Where(IsActionableWarningLine)
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .Select(line => new AuditFinding(
                Category: category,
                Severity: "info",
                Message: $"{commandName}: {line}",
                TypeName: target.PackageId,
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: null))
            .ToList();
    }

    private static bool IsActionableWarningLine(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (trimmed.EndsWith("Warning(s)", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Contains(": warning ", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteLogs(string logsDirectory, string prefix, ProcessResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Command: {result.Command}");
        builder.AppendLine($"Working directory: {result.WorkingDirectory}");
        builder.AppendLine($"Exit code: {result.ExitCode}");
        builder.AppendLine();
        builder.AppendLine("STDOUT:");
        builder.AppendLine(result.StandardOutput);
        builder.AppendLine();
        builder.AppendLine("STDERR:");
        builder.AppendLine(result.StandardError);

        File.WriteAllText(Path.Combine(logsDirectory, $"{prefix}.log"), builder.ToString());
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        var commandBuilder = new StringBuilder(fileName);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
            commandBuilder.Append(' ');
            commandBuilder.Append(argument.Contains(' ') ? $"\"{argument}\"" : argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var environmentVariable in environmentVariables)
            {
                startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(
            Command: commandBuilder.ToString(),
            WorkingDirectory: workingDirectory,
            ExitCode: process.ExitCode,
            StandardOutput: await standardOutputTask,
            StandardError: await standardErrorTask);
    }
}

internal sealed record SwiftFrameworkDependencySpec(
    string Include,
    string PackageId,
    string PackageVersion);

internal sealed record ProcessResult(
    string Command,
    string WorkingDirectory,
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public IEnumerable<string> AllLines()
    {
        return StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Concat(StandardError.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }
}
