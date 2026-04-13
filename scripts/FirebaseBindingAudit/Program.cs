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
            var outputDirectory = parsedArguments.OutputDirectory ?? throw new InvalidOperationException("--output-dir is required.");
            var configPath = Path.Combine(repoRoot, "scripts", "firebase-binding-audit.json");
            var suppressionPath = Path.Combine(repoRoot, "scripts", "firebase-binding-audit-suppressions.json");

            var configuration = ConfigurationLoader.Load(configPath);
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

        public bool ShowHelp { get; set; }
    }
}
