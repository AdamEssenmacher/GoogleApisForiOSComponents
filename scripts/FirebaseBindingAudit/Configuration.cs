using System.Text.Json;

namespace FirebaseBindingAudit;

internal sealed class AuditConfiguration
{
    public string[] ManualAttributes { get; set; } = [];

    public string[] BindingAttributes { get; set; } = [];

    public SharpieConfiguration Sharpie { get; set; } = new();

    public List<AuditTargetDefinition> Targets { get; set; } = [];
}

internal sealed class SharpieConfiguration
{
    public string ExpectedVersion { get; set; } = "26.3.0.11";

    public string DefaultMode { get; set; } = "auto";
}

internal sealed class AuditTargetDefinition
{
    public string Id { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string Xcframework { get; set; } = string.Empty;

    public string BaselineDirectory { get; set; } = string.Empty;

    public string[] BaselineFiles { get; set; } = [];

    public string[] HelperFiles { get; set; } = [];

    public string? SharpieMode { get; set; }

    public bool UseSharpieComparisonFallback { get; set; }

    public string BaselineDirectoryPath(string repoRoot) => Path.Combine(repoRoot, BaselineDirectory);

    public string EffectiveSharpieMode(SharpieConfiguration sharpieConfiguration) =>
        string.IsNullOrWhiteSpace(SharpieMode) ? sharpieConfiguration.DefaultMode : SharpieMode!;
}

internal static class ConfigurationLoader
{
    public static AuditConfiguration Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Configuration file not found.", configPath);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var config = JsonSerializer.Deserialize<AuditConfiguration>(File.ReadAllText(configPath), options);
        if (config is null)
        {
            throw new InvalidOperationException($"Unable to deserialize configuration at '{configPath}'.");
        }

        if (config.Targets.Count == 0)
        {
            throw new InvalidOperationException("The audit configuration does not define any targets.");
        }

        var validModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auto",
            "forceFallback",
            "off"
        };

        if (!validModes.Contains(config.Sharpie.DefaultMode))
        {
            throw new InvalidOperationException($"Unsupported sharpie.defaultMode '{config.Sharpie.DefaultMode}'.");
        }

        foreach (var target in config.Targets)
        {
            if (!string.IsNullOrWhiteSpace(target.SharpieMode) && !validModes.Contains(target.SharpieMode))
            {
                throw new InvalidOperationException($"Unsupported sharpieMode '{target.SharpieMode}' for target '{target.Id}'.");
            }
        }

        return config;
    }
}
