using System.Text.Json;

namespace FirebaseBindingAudit;

internal sealed class SuppressionConfiguration
{
    public List<SuppressionRule> Suppressions { get; set; } = [];
}

internal sealed class SuppressionRule
{
    public string Id { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? TypeName { get; set; }

    public string? MemberName { get; set; }

    public string? Selector { get; set; }

    public string? ComparisonTypeKey { get; set; }

    public string? ComparisonMemberKey { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string[] Evidence { get; set; } = [];
}

internal sealed record StaleSuppressionRule(
    string Id,
    string Target,
    string Category,
    string Reason);

internal sealed record InvalidSuppressionRule(
    string Id,
    string Target,
    string Category,
    string Message);

internal sealed record SuppressionSummary(
    bool Enabled,
    int RuleCount,
    int MatchedRuleCount,
    int StaleRuleCount,
    IReadOnlyList<StaleSuppressionRule> StaleRules,
    int InvalidRuleCount,
    IReadOnlyList<InvalidSuppressionRule> InvalidRules);

internal sealed record SuppressionApplicationResult(
    IReadOnlyList<TargetAuditResult> Results,
    SuppressionSummary Summary);

internal static class SuppressionLoader
{
    private static readonly HashSet<string> SupportedCategories = new(StringComparer.Ordinal)
    {
        "stale-baseline-binding",
        "missing-baseline-binding",
        "signature-drift",
        "attribute-drift"
    };

    public static SuppressionConfiguration Load(string suppressionPath)
    {
        if (!File.Exists(suppressionPath))
        {
            throw new FileNotFoundException("Suppression file not found.", suppressionPath);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var config = JsonSerializer.Deserialize<SuppressionConfiguration>(File.ReadAllText(suppressionPath), options);
        if (config is null)
        {
            throw new InvalidOperationException($"Unable to deserialize suppressions at '{suppressionPath}'.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < config.Suppressions.Count; index++)
        {
            var rule = config.Suppressions[index];
            rule.Id = Normalize(rule.Id);
            rule.Target = Normalize(rule.Target);
            rule.Category = Normalize(rule.Category);
            rule.TypeName = NormalizeNullable(rule.TypeName);
            rule.MemberName = NormalizeNullable(rule.MemberName);
            rule.Selector = NormalizeNullable(rule.Selector);
            rule.ComparisonTypeKey = NormalizeNullable(rule.ComparisonTypeKey);
            rule.ComparisonMemberKey = NormalizeNullable(rule.ComparisonMemberKey);
            rule.Reason = Normalize(rule.Reason);
            rule.Evidence = rule.Evidence
                .Select(NormalizeNullable)
                .Where(static evidence => !string.IsNullOrWhiteSpace(evidence))
                .Cast<string>()
                .ToArray();

            if (string.IsNullOrWhiteSpace(rule.Id))
            {
                throw new InvalidOperationException($"Suppression rule at index {index} is missing required field 'id'.");
            }

            if (!ids.Add(rule.Id))
            {
                throw new InvalidOperationException($"Duplicate suppression rule id '{rule.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(rule.Target))
            {
                throw new InvalidOperationException($"Suppression rule '{rule.Id}' is missing required field 'target'.");
            }

            if (string.IsNullOrWhiteSpace(rule.Category))
            {
                throw new InvalidOperationException($"Suppression rule '{rule.Id}' is missing required field 'category'.");
            }

            if (!SupportedCategories.Contains(rule.Category))
            {
                throw new InvalidOperationException(
                    $"Suppression rule '{rule.Id}' uses unsupported category '{rule.Category}'.");
            }

            if (string.IsNullOrWhiteSpace(rule.Reason))
            {
                throw new InvalidOperationException($"Suppression rule '{rule.Id}' is missing required field 'reason'.");
            }

            if (rule.Evidence.Length == 0)
            {
                throw new InvalidOperationException($"Suppression rule '{rule.Id}' must include at least one evidence entry.");
            }

            if (string.IsNullOrWhiteSpace(rule.ComparisonTypeKey) && string.IsNullOrWhiteSpace(rule.TypeName))
            {
                throw new InvalidOperationException(
                    $"Suppression rule '{rule.Id}' must include either 'comparisonTypeKey' or 'typeName'.");
            }

            if (!string.IsNullOrWhiteSpace(rule.Selector) &&
                string.IsNullOrWhiteSpace(rule.ComparisonMemberKey) &&
                string.IsNullOrWhiteSpace(rule.MemberName))
            {
                throw new InvalidOperationException(
                    $"Suppression rule '{rule.Id}' uses 'selector' and must also include 'comparisonMemberKey' or 'memberName'.");
            }
        }

        return config;
    }

    private static string Normalize(string value) => value.Trim();

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class SuppressionEngine
{
    private static readonly HashSet<string> SupportedCategories = new(StringComparer.Ordinal)
    {
        "stale-baseline-binding",
        "missing-baseline-binding",
        "signature-drift",
        "attribute-drift"
    };

    public static SuppressionApplicationResult Apply(
        IReadOnlyList<TargetAuditResult> results,
        SuppressionConfiguration suppressionConfiguration,
        bool disableSuppressions)
    {
        if (disableSuppressions)
        {
            return new SuppressionApplicationResult(
                Results: results,
                Summary: new SuppressionSummary(
                    Enabled: false,
                    RuleCount: suppressionConfiguration.Suppressions.Count,
                    MatchedRuleCount: 0,
                    StaleRuleCount: 0,
                    StaleRules: [],
                    InvalidRuleCount: 0,
                    InvalidRules: []));
        }

        var candidates = results
            .SelectMany(result => result.Findings.Select((finding, index) => new SuppressionCandidate(result.Target, index, finding)))
            .Where(static candidate => SupportedCategories.Contains(candidate.Finding.Category))
            .ToList();

        var staleRules = new List<StaleSuppressionRule>();
        var invalidRules = new List<InvalidSuppressionRule>();
        var appliedRules = new HashSet<string>(StringComparer.Ordinal);
        var suppressionMap = new Dictionary<(string Target, int Index), SuppressionRule>();

        foreach (var rule in suppressionConfiguration.Suppressions)
        {
            var matches = candidates
                .Where(candidate => Matches(rule, candidate))
                .ToList();

            if (matches.Count == 0)
            {
                staleRules.Add(new StaleSuppressionRule(rule.Id, rule.Target, rule.Category, rule.Reason));
                continue;
            }

            if (matches.Count > 1)
            {
                invalidRules.Add(new InvalidSuppressionRule(
                    rule.Id,
                    rule.Target,
                    rule.Category,
                    $"Suppression rule '{rule.Id}' matched {matches.Count} findings and is too broad."));
                continue;
            }

            var key = (matches[0].Target, matches[0].Index);
            if (suppressionMap.TryGetValue(key, out var existingRule))
            {
                invalidRules.Add(new InvalidSuppressionRule(
                    rule.Id,
                    rule.Target,
                    rule.Category,
                    $"Suppression rule '{rule.Id}' overlaps with existing suppression rule '{existingRule.Id}'."));
                continue;
            }

            suppressionMap[key] = rule;
            appliedRules.Add(rule.Id);
        }

        var updatedResults = results
            .Select(result =>
            {
                var activeFindings = new List<AuditFinding>(result.Findings.Count);
                var suppressedFindings = new List<AuditFinding>();

                for (var index = 0; index < result.Findings.Count; index++)
                {
                    var finding = result.Findings[index];
                    if (suppressionMap.TryGetValue((result.Target, index), out var rule))
                    {
                        suppressedFindings.Add(finding with
                        {
                            SuppressionId = rule.Id,
                            SuppressionReason = rule.Reason,
                            SuppressionEvidence = rule.Evidence
                        });
                    }
                    else
                    {
                        activeFindings.Add(finding);
                    }
                }

                return result with
                {
                    Status = activeFindings.Any(static finding => finding.Severity == "failure") ? "failed" : "passed",
                    FailureCount = activeFindings.Count(static finding => finding.Severity == "failure"),
                    InfoCount = activeFindings.Count(static finding => finding.Severity == "info"),
                    ConfidenceSummary = AuditFindingSummary.BuildConfidenceSummary(activeFindings),
                    Findings = activeFindings,
                    SuppressedCount = suppressedFindings.Count,
                    SuppressedFindings = suppressedFindings
                };
            })
            .ToList();

        return new SuppressionApplicationResult(
            Results: updatedResults,
            Summary: new SuppressionSummary(
                Enabled: true,
                RuleCount: suppressionConfiguration.Suppressions.Count,
                MatchedRuleCount: appliedRules.Count,
                StaleRuleCount: staleRules.Count,
                StaleRules: staleRules,
                InvalidRuleCount: invalidRules.Count,
                InvalidRules: invalidRules));
    }

    private static bool Matches(SuppressionRule rule, SuppressionCandidate candidate)
    {
        var finding = candidate.Finding;

        if (!string.Equals(rule.Target, candidate.Target, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(rule.Category, finding.Category, StringComparison.Ordinal))
        {
            return false;
        }

        return MatchesOptional(rule.TypeName, finding.TypeName) &&
               MatchesOptional(rule.MemberName, finding.MemberName) &&
               MatchesOptional(rule.Selector, finding.Selector) &&
               MatchesOptional(rule.ComparisonTypeKey, finding.ComparisonTypeKey) &&
               MatchesOptional(rule.ComparisonMemberKey, finding.ComparisonMemberKey);
    }

    private static bool MatchesOptional(string? expected, string? actual) =>
        string.IsNullOrWhiteSpace(expected) || string.Equals(expected, actual, StringComparison.Ordinal);

    private sealed record SuppressionCandidate(string Target, int Index, AuditFinding Finding);
}

internal static class AuditFindingSummary
{
    public static ConfidenceSummary BuildConfidenceSummary(IReadOnlyCollection<AuditFinding> findings)
    {
        return new ConfidenceSummary(
            Confirmed: findings.Count(static finding => string.Equals(finding.Confidence, "confirmed", StringComparison.Ordinal)),
            Disputed: findings.Count(static finding => string.Equals(finding.Confidence, "disputed", StringComparison.Ordinal)),
            LowConfidence: findings.Count(static finding => string.Equals(finding.Confidence, "low-confidence", StringComparison.Ordinal)),
            NotReviewed: findings.Count(static finding => string.Equals(finding.Confidence, "not-reviewed", StringComparison.Ordinal)));
    }
}
