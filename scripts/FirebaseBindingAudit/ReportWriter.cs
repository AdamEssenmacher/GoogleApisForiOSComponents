using System.Text;
using System.Text.Json;

namespace FirebaseBindingAudit;

internal static class ReportWriter
{
    public static async Task WriteAsync(
        string outputDirectory,
        string detailsDirectory,
        string generatorVersion,
        IReadOnlyList<TargetAuditResult> results,
        string tempDirectory,
        SharpieToolResolution sharpieResolution,
        SuppressionSummary suppressionSummary,
        CancellationToken cancellationToken)
    {
        foreach (var result in results)
        {
            await WriteDetailReportAsync(detailsDirectory, result, cancellationToken);
        }

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "report.md"),
            BuildAggregateMarkdown(generatorVersion, results, tempDirectory, sharpieResolution, suppressionSummary),
            cancellationToken);

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            generatorVersion,
            tempDirectory,
            sharpie = new
            {
                status = sharpieResolution.Status,
                expectedVersion = sharpieResolution.ExpectedVersion,
                commandPath = sharpieResolution.CommandPath,
                resolvedPath = sharpieResolution.ResolvedPath,
                version = sharpieResolution.Version,
                message = sharpieResolution.Message
            },
            suppressions = new
            {
                enabled = suppressionSummary.Enabled,
                ruleCount = suppressionSummary.RuleCount,
                matchedRuleCount = suppressionSummary.MatchedRuleCount,
                staleRuleCount = suppressionSummary.StaleRuleCount,
                staleRules = suppressionSummary.StaleRules.Select(rule => new
                {
                    id = rule.Id,
                    target = rule.Target,
                    category = rule.Category,
                    reason = rule.Reason
                }),
                invalidRuleCount = suppressionSummary.InvalidRuleCount,
                invalidRules = suppressionSummary.InvalidRules.Select(rule => new
                {
                    id = rule.Id,
                    target = rule.Target,
                    category = rule.Category,
                    message = rule.Message
                })
            },
            results = results.Select(result => new
            {
                target = result.Target,
                xcframework = result.Xcframework,
                status = result.Status,
                generationStatus = result.GenerationStatus,
                comparisonSource = result.ComparisonSource,
                failureCount = result.FailureCount,
                infoCount = result.InfoCount,
                suppressedCount = result.SuppressedCount,
                sharpieStatus = result.SharpieStatus,
                confidenceSummary = new
                {
                    confirmed = result.ConfidenceSummary.Confirmed,
                    disputed = result.ConfidenceSummary.Disputed,
                    lowConfidence = result.ConfidenceSummary.LowConfidence,
                    notReviewed = result.ConfidenceSummary.NotReviewed
                },
                findings = result.Findings.Select(finding => new
                {
                    category = finding.Category,
                    severity = finding.Severity,
                    message = finding.Message,
                    typeName = finding.TypeName,
                    memberName = finding.MemberName,
                    selector = finding.Selector,
                    baselineFile = finding.BaselineFile,
                    generatedFile = finding.GeneratedFile,
                    comparisonTypeKey = finding.ComparisonTypeKey,
                    comparisonMemberKey = finding.ComparisonMemberKey,
                    confidence = finding.Confidence,
                    confidenceSource = finding.ConfidenceSource
                }),
                suppressedFindings = result.SuppressedFindings.Select(finding => new
                {
                    category = finding.Category,
                    severity = finding.Severity,
                    message = finding.Message,
                    typeName = finding.TypeName,
                    memberName = finding.MemberName,
                    selector = finding.Selector,
                    baselineFile = finding.BaselineFile,
                    generatedFile = finding.GeneratedFile,
                    comparisonTypeKey = finding.ComparisonTypeKey,
                    comparisonMemberKey = finding.ComparisonMemberKey,
                    confidence = finding.Confidence,
                    confidenceSource = finding.ConfidenceSource,
                    suppressionId = finding.SuppressionId,
                    suppressionReason = finding.SuppressionReason,
                    suppressionEvidence = finding.SuppressionEvidence
                })
            })
        };

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "report.json"),
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static async Task WriteDetailReportAsync(string detailsDirectory, TargetAuditResult result, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {result.Target}");
        builder.AppendLine();
        builder.AppendLine($"- Status: `{result.Status}`");
        builder.AppendLine($"- Generation status: `{result.GenerationStatus}`");
        builder.AppendLine($"- Comparison source: `{result.ComparisonSource}`");
        builder.AppendLine($"- Failures: `{result.FailureCount}`");
        builder.AppendLine($"- Infos: `{result.InfoCount}`");
        builder.AppendLine($"- Suppressed: `{result.SuppressedCount}`");
        builder.AppendLine($"- Sharpie status: `{result.SharpieStatus}`");
        builder.AppendLine($"- Confidence: `confirmed={result.ConfidenceSummary.Confirmed}`, `disputed={result.ConfidenceSummary.Disputed}`, `low-confidence={result.ConfidenceSummary.LowConfidence}`, `not-reviewed={result.ConfidenceSummary.NotReviewed}`");
        builder.AppendLine();

        var failureFindings = result.Findings.Where(static finding => finding.Severity == "failure").ToList();
        var infoFindings = result.Findings.Where(static finding => finding.Severity == "info").ToList();
        var suppressedFindings = result.SuppressedFindings.ToList();

        builder.AppendLine("## Failures");
        builder.AppendLine();
        if (failureFindings.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
        }
        else
        {
            foreach (var finding in failureFindings)
            {
                AppendFinding(builder, finding);
            }
        }

        builder.AppendLine("## Suppressed Findings");
        builder.AppendLine();
        if (suppressedFindings.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
        }
        else
        {
            foreach (var finding in suppressedFindings)
            {
                AppendFinding(builder, finding);
            }
        }

        builder.AppendLine("## Infos");
        builder.AppendLine();
        if (infoFindings.Count == 0)
        {
            builder.AppendLine("None.");
            builder.AppendLine();
        }
        else
        {
            foreach (var finding in infoFindings)
            {
                AppendFinding(builder, finding);
            }
        }

        await File.WriteAllTextAsync(Path.Combine(detailsDirectory, $"{result.Target}.md"), builder.ToString(), cancellationToken);
    }

    private static string BuildAggregateMarkdown(
        string generatorVersion,
        IReadOnlyList<TargetAuditResult> results,
        string tempDirectory,
        SharpieToolResolution sharpieResolution,
        SuppressionSummary suppressionSummary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Firebase Binding Drift Audit");
        builder.AppendLine();
        builder.AppendLine($"- Generator version: `{generatorVersion}`");
        builder.AppendLine($"- Sharpie status: `{sharpieResolution.Status}`");
        builder.AppendLine($"- Sharpie expected version: `{sharpieResolution.ExpectedVersion}`");
        if (!string.IsNullOrWhiteSpace(sharpieResolution.Version))
        {
            builder.AppendLine($"- Sharpie resolved version: `{sharpieResolution.Version}`");
        }
        if (!string.IsNullOrWhiteSpace(sharpieResolution.ResolvedPath))
        {
            builder.AppendLine($"- Sharpie resolved path: `{sharpieResolution.ResolvedPath}`");
        }
        if (!string.IsNullOrWhiteSpace(sharpieResolution.Message))
        {
            builder.AppendLine($"- Sharpie note: `{sharpieResolution.Message}`");
        }
        builder.AppendLine($"- Suppressions enabled: `{suppressionSummary.Enabled}`");
        builder.AppendLine($"- Suppression rules: `{suppressionSummary.RuleCount}`");
        builder.AppendLine($"- Matched suppressions: `{suppressionSummary.MatchedRuleCount}`");
        builder.AppendLine($"- Stale suppressions: `{suppressionSummary.StaleRuleCount}`");
        builder.AppendLine($"- Invalid suppressions: `{suppressionSummary.InvalidRuleCount}`");
        builder.AppendLine($"- Generated at: `{DateTime.UtcNow:O}`");
        builder.AppendLine($"- Temp workspace: `{tempDirectory}`");
        builder.AppendLine();
        builder.AppendLine("| Target | Status | Generation | Compare | Sharpie | Confidence | Failures | Suppressed | Infos | Detail |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | --- |");

        foreach (var result in results.OrderBy(static result => result.Target, StringComparer.Ordinal))
        {
            builder.AppendLine($"| {result.Target} | `{result.Status}` | `{result.GenerationStatus}` | `{result.ComparisonSource}` | `{result.SharpieStatus}` | `C:{result.ConfidenceSummary.Confirmed} D:{result.ConfidenceSummary.Disputed} LC:{result.ConfidenceSummary.LowConfidence} NR:{result.ConfidenceSummary.NotReviewed}` | {result.FailureCount} | {result.SuppressedCount} | {result.InfoCount} | `details/{result.Target}.md` |");
        }

        builder.AppendLine();

        var totalFailures = results.Sum(static result => result.FailureCount);
        var totalSuppressed = results.Sum(static result => result.SuppressedCount);
        var totalInfos = results.Sum(static result => result.InfoCount);
        builder.AppendLine($"- Total failures: `{totalFailures}`");
        builder.AppendLine($"- Total suppressed: `{totalSuppressed}`");
        builder.AppendLine($"- Total infos: `{totalInfos}`");

        builder.AppendLine();
        builder.AppendLine("## Stale Suppressions");
        builder.AppendLine();
        if (suppressionSummary.StaleRules.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            foreach (var rule in suppressionSummary.StaleRules.OrderBy(static rule => rule.Id, StringComparer.Ordinal))
            {
                builder.AppendLine($"- `{rule.Id}` target=`{rule.Target}` category=`{rule.Category}` reason=`{rule.Reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Invalid Suppressions");
        builder.AppendLine();
        if (suppressionSummary.InvalidRules.Count == 0)
        {
            builder.AppendLine("None.");
        }
        else
        {
            foreach (var rule in suppressionSummary.InvalidRules.OrderBy(static rule => rule.Id, StringComparer.Ordinal))
            {
                builder.AppendLine($"- `{rule.Id}` target=`{rule.Target}` category=`{rule.Category}` message=`{rule.Message}`");
            }
        }

        return builder.ToString();
    }

    private static void AppendFinding(StringBuilder builder, AuditFinding finding)
    {
        builder.Append("- `");
        builder.Append(finding.Category);
        builder.Append("` ");
        builder.AppendLine(finding.Message);

        builder.Append("  - Type: `");
        builder.Append(finding.TypeName);
        builder.AppendLine("`");

        if (!string.IsNullOrWhiteSpace(finding.MemberName))
        {
            builder.Append("  - Member: `");
            builder.Append(finding.MemberName);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.Selector))
        {
            builder.Append("  - Selector: `");
            builder.Append(finding.Selector);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.BaselineFile))
        {
            builder.Append("  - Baseline file: `");
            builder.Append(finding.BaselineFile);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.GeneratedFile))
        {
            builder.Append("  - Generated file: `");
            builder.Append(finding.GeneratedFile);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.ComparisonTypeKey))
        {
            builder.Append("  - Comparison type key: `");
            builder.Append(finding.ComparisonTypeKey);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.ComparisonMemberKey))
        {
            builder.Append("  - Comparison member key: `");
            builder.Append(finding.ComparisonMemberKey);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.Confidence))
        {
            builder.Append("  - Confidence: `");
            builder.Append(finding.Confidence);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.ConfidenceSource))
        {
            builder.Append("  - Confidence source: `");
            builder.Append(finding.ConfidenceSource);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.SuppressionId))
        {
            builder.Append("  - Suppression id: `");
            builder.Append(finding.SuppressionId);
            builder.AppendLine("`");
        }

        if (!string.IsNullOrWhiteSpace(finding.SuppressionReason))
        {
            builder.Append("  - Suppression reason: `");
            builder.Append(finding.SuppressionReason);
            builder.AppendLine("`");
        }

        if (finding.SuppressionEvidence is { Count: > 0 })
        {
            builder.Append("  - Suppression evidence: `");
            builder.Append(string.Join("`, `", finding.SuppressionEvidence));
            builder.AppendLine("`");
        }

        builder.AppendLine();
    }
}
