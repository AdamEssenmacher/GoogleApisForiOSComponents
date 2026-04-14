namespace FirebaseBindingAudit;

internal static class ProgramEntry
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var parsedArguments = ParseArguments(args);
            if (parsedArguments.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            var repoRoot = parsedArguments.RepoRoot ?? throw new InvalidOperationException("--repo-root is required.");
            var configPath = Path.Combine(repoRoot, "scripts", "firebase-binding-audit.json");
            var suppressionPath = Path.Combine(repoRoot, "scripts", "firebase-binding-audit-suppressions.json");

            var configuration = ConfigurationLoader.Load(configPath);
            if (parsedArguments.GenerateBindingSurfaceCoverage)
            {
                var coverageManifestPath = parsedArguments.CoverageManifest ?? throw new InvalidOperationException("--coverage-manifest is required.");
                var coverageOutputPath = parsedArguments.CoverageOutput ?? throw new InvalidOperationException("--coverage-output is required.");
                var coveragePropsOutputPath = parsedArguments.CoveragePropsOutput ?? throw new InvalidOperationException("--coverage-props-output is required.");
                var bindingSurfaceTarget = parsedArguments.BindingSurfaceTarget ?? throw new InvalidOperationException("--binding-surface-target is required.");
                var manifest = BindingSurfaceCoverageManifestLoader.Load(coverageManifestPath);
                var document = new BindingSurfaceCoverageBuilder(configuration).Build(repoRoot, manifest, bindingSurfaceTarget);
                BindingSurfaceCoverageValidator.ThrowIfInvalid(BindingSurfaceCoverageValidator.Validate(document));
                await BindingSurfaceCoverageBuilder.WriteAsync(
                    document,
                    coverageOutputPath,
                    coveragePropsOutputPath,
                    bindingSurfaceTarget);

                Console.WriteLine($"Binding surface coverage document: {coverageOutputPath}");
                Console.WriteLine($"Binding surface coverage props: {coveragePropsOutputPath}");
                foreach (var package in document.Targets.SelectMany(static target => target.RequiredPackages).DistinctBy(static package => package.Id).OrderBy(static package => package.Id, StringComparer.Ordinal))
                {
                    Console.WriteLine($"Package: {package.Id} {package.Version}");
                }

                return 0;
            }

            var outputDirectory = parsedArguments.OutputDirectory ?? throw new InvalidOperationException("--output-dir is required.");
            var suppressions = File.Exists(suppressionPath)
                ? SuppressionLoader.Load(suppressionPath)
                : new SuppressionConfiguration();

            var runner = new AuditRunner(configuration, suppressions);
            var result = await runner.RunAsync(
                new AuditOptions(
                    RepoRoot: repoRoot,
                    OutputDirectory: outputDirectory,
                    GeneratorVersion: parsedArguments.GeneratorVersion ?? "0.7.0",
                    SelectedTargets: parsedArguments.Targets,
                    KeepTemp: parsedArguments.KeepTemp,
                    SharpiePath: parsedArguments.SharpiePath,
                    SharpieVersion: parsedArguments.SharpieVersion ?? configuration.Sharpie.ExpectedVersion,
                    DisableSharpie: parsedArguments.DisableSharpie,
                    DisableSuppressions: parsedArguments.DisableSuppressions));

            Console.WriteLine($"Aggregate report: {Path.Combine(result.OutputDirectory, "report.md")}");
            Console.WriteLine($"JSON report: {Path.Combine(result.OutputDirectory, "report.json")}");
            Console.WriteLine($"Sharpie status: {result.SharpieResolution.Status}");
            if (!string.IsNullOrWhiteSpace(result.SharpieResolution.Version))
            {
                Console.WriteLine($"Sharpie version: {result.SharpieResolution.Version}");
            }
            if (!string.IsNullOrWhiteSpace(result.TempDirectory))
            {
                Console.WriteLine($"Temp workspace kept at: {result.TempDirectory}");
            }
            Console.WriteLine($"Suppressions enabled: {!parsedArguments.DisableSuppressions}");
            Console.WriteLine($"Suppression rules matched: {result.SuppressionSummary.MatchedRuleCount}");
            if (result.SuppressionSummary.StaleRuleCount > 0)
            {
                Console.WriteLine($"Stale suppression rules: {result.SuppressionSummary.StaleRuleCount}");
            }
            if (result.SuppressionSummary.InvalidRuleCount > 0)
            {
                Console.WriteLine($"Invalid suppression rules: {result.SuppressionSummary.InvalidRuleCount}");
            }

            return result.ExitCode;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static ParsedArguments ParseArguments(string[] args)
    {
        var parsed = new ParsedArguments();
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--repo-root":
                    parsed.RepoRoot = GetRequiredValue(args, ++index, "--repo-root");
                    break;
                case "--output-dir":
                    parsed.OutputDirectory = GetRequiredValue(args, ++index, "--output-dir");
                    break;
                case "--generator-version":
                    parsed.GeneratorVersion = GetRequiredValue(args, ++index, "--generator-version");
                    break;
                case "--targets":
                    parsed.Targets = GetRequiredValue(args, ++index, "--targets")
                        .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                case "--keep-temp":
                    parsed.KeepTemp = true;
                    break;
                case "--sharpie-path":
                    parsed.SharpiePath = GetRequiredValue(args, ++index, "--sharpie-path");
                    break;
                case "--sharpie-version":
                    parsed.SharpieVersion = GetRequiredValue(args, ++index, "--sharpie-version");
                    break;
                case "--disable-sharpie":
                    parsed.DisableSharpie = true;
                    break;
                case "--disable-suppressions":
                    parsed.DisableSuppressions = true;
                    break;
                case "--generate-binding-surface-coverage":
                    parsed.GenerateBindingSurfaceCoverage = true;
                    break;
                case "--coverage-manifest":
                    parsed.CoverageManifest = GetRequiredValue(args, ++index, "--coverage-manifest");
                    break;
                case "--coverage-output":
                    parsed.CoverageOutput = GetRequiredValue(args, ++index, "--coverage-output");
                    break;
                case "--coverage-props-output":
                    parsed.CoveragePropsOutput = GetRequiredValue(args, ++index, "--coverage-props-output");
                    break;
                case "--binding-surface-target":
                    parsed.BindingSurfaceTarget = GetRequiredValue(args, ++index, "--binding-surface-target");
                    break;
                case "--help":
                case "-h":
                    parsed.ShowHelp = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument '{args[index]}'.");
            }
        }

        return parsed;
    }

    private static string GetRequiredValue(string[] args, int index, string optionName)
    {
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new InvalidOperationException($"{optionName} requires a value.");
        }

        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: FirebaseBindingAudit --repo-root <path> --output-dir <path> [--targets a,b] [--generator-version 0.7.0] [--keep-temp] [--sharpie-path /path/to/sharpie] [--sharpie-version 26.3.0.11] [--disable-sharpie] [--disable-suppressions]");
        Console.WriteLine("       FirebaseBindingAudit --generate-binding-surface-coverage --repo-root <path> --coverage-manifest <path> --coverage-output <path> --coverage-props-output <path> --binding-surface-target <target|all>");
    }

    private sealed class ParsedArguments
    {
        public string? RepoRoot { get; set; }

        public string? OutputDirectory { get; set; }

        public string? GeneratorVersion { get; set; }

        public string[]? Targets { get; set; }

        public bool KeepTemp { get; set; }

        public string? SharpiePath { get; set; }

        public string? SharpieVersion { get; set; }

        public bool DisableSharpie { get; set; }

        public bool DisableSuppressions { get; set; }

        public bool GenerateBindingSurfaceCoverage { get; set; }

        public string? CoverageManifest { get; set; }

        public string? CoverageOutput { get; set; }

        public string? CoveragePropsOutput { get; set; }

        public string? BindingSurfaceTarget { get; set; }

        public bool ShowHelp { get; set; }
    }
}
