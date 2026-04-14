using Xunit;

namespace FirebaseBindingAudit.Tests;

public sealed class SuppressionEngineTests
{
    [Fact]
    public void Apply_UsesComparisonKeysForExactMemberLevelMatch()
    {
        var result = CreateResult(
            "CloudMessaging",
            CreateFailureFinding(
                typeName: "Firebase.CloudMessaging.Messaging",
                memberName: "ApnsToken",
                selector: "APNSToken",
                comparisonTypeKey: "firmessaging",
                comparisonMemberKey: "property:APNSToken"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-1",
                    Target = "CloudMessaging",
                    Category = "attribute-drift",
                    ComparisonTypeKey = "firmessaging",
                    ComparisonMemberKey = "property:APNSToken",
                    Reason = "known false positive",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);
        var target = application.Results.Single();

        Assert.Equal("passed", target.Status);
        Assert.Equal(0, target.FailureCount);
        Assert.Empty(target.Findings);
        Assert.Equal(1, target.SuppressedCount);
        Assert.Equal("rule-1", target.SuppressedFindings.Single().SuppressionId);
        Assert.Equal(1, application.Summary.MatchedRuleCount);
    }

    [Fact]
    public void Apply_FallsBackToTypeMemberAndSelectorMatch()
    {
        var result = CreateResult(
            "RemoteConfig",
            CreateFailureFinding(
                typeName: "Firebase.RemoteConfig.RemoteConfigValue",
                memberName: "JsonValue",
                selector: "JSONValue"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-2",
                    Target = "RemoteConfig",
                    Category = "attribute-drift",
                    TypeName = "Firebase.RemoteConfig.RemoteConfigValue",
                    MemberName = "JsonValue",
                    Selector = "JSONValue",
                    Reason = "known false positive",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);
        var target = application.Results.Single();

        Assert.Equal(1, target.SuppressedCount);
        Assert.Empty(target.Findings);
        Assert.Equal("rule-2", target.SuppressedFindings.Single().SuppressionId);
    }

    [Fact]
    public void Apply_SuppressedFindingsDoNotCountTowardFailureCountOrStatus()
    {
        var result = CreateResult(
            "Core",
            CreateFailureFinding(
                typeName: "Firebase.Core.Options",
                memberName: "ApiKey",
                selector: "APIKey"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-3",
                    Target = "Core",
                    Category = "attribute-drift",
                    TypeName = "Firebase.Core.Options",
                    MemberName = "ApiKey",
                    Selector = "APIKey",
                    Reason = "known false positive",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);
        var target = application.Results.Single();

        Assert.Equal("passed", target.Status);
        Assert.Equal(0, target.FailureCount);
        Assert.Equal(0, target.InfoCount);
        Assert.Equal(1, target.SuppressedCount);
    }

    [Fact]
    public void Apply_ReportsOverBroadRuleAsInvalidAndLeavesFindingsActive()
    {
        var result = CreateResult(
            "CloudMessaging",
            CreateFailureFinding(
                typeName: "Firebase.CloudMessaging.Messaging",
                memberName: "ApnsToken",
                selector: "APNSToken"),
            CreateFailureFinding(
                typeName: "Firebase.CloudMessaging.Messaging",
                memberName: "FcmToken",
                selector: "FCMToken"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-4",
                    Target = "CloudMessaging",
                    Category = "attribute-drift",
                    TypeName = "Firebase.CloudMessaging.Messaging",
                    Reason = "too broad",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);
        var target = application.Results.Single();

        Assert.Equal(1, application.Summary.InvalidRuleCount);
        Assert.Equal(0, target.SuppressedCount);
        Assert.Equal(2, target.FailureCount);
        Assert.Equal("failed", target.Status);
    }

    [Fact]
    public void Apply_ReportsUnusedRuleAsStaleInfoOnly()
    {
        var result = CreateResult(
            "Core",
            CreateFailureFinding(
                typeName: "Firebase.Core.App",
                memberName: "DefaultInstance",
                selector: "defaultApp"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-5",
                    Target = "Core",
                    Category = "attribute-drift",
                    TypeName = "Firebase.Core.Options",
                    MemberName = "ApiKey",
                    Selector = "APIKey",
                    Reason = "unused",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);

        Assert.Equal(1, application.Summary.StaleRuleCount);
        Assert.Equal(0, application.Summary.InvalidRuleCount);
        Assert.Equal(0, application.Summary.MatchedRuleCount);
        Assert.Single(application.Results.Single().Findings);
    }

    [Fact]
    public void Apply_DoesNotReportRulesForUnauditedTargetsAsStale()
    {
        var result = CreateResult(
            "Core",
            CreateFailureFinding(
                typeName: "Firebase.Core.App",
                memberName: "DefaultInstance",
                selector: "defaultApp"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-unselected",
                    Target = "RemoteConfig",
                    Category = "attribute-drift",
                    TypeName = "Firebase.RemoteConfig.RemoteConfigValue",
                    MemberName = "JsonValue",
                    Selector = "JSONValue",
                    Reason = "target was not part of this audit run",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: false);

        Assert.Equal(1, application.Summary.RuleCount);
        Assert.Equal(0, application.Summary.StaleRuleCount);
        Assert.Equal(0, application.Summary.InvalidRuleCount);
        Assert.Equal(0, application.Summary.MatchedRuleCount);
        Assert.Single(application.Results.Single().Findings);
    }

    [Fact]
    public void Apply_DisableSuppressionsLeavesFindingsUnsuppressed()
    {
        var result = CreateResult(
            "Core",
            CreateFailureFinding(
                typeName: "Firebase.Core.Options",
                memberName: "ApiKey",
                selector: "APIKey"));

        var config = new SuppressionConfiguration
        {
            Suppressions =
            [
                new SuppressionRule
                {
                    Id = "rule-6",
                    Target = "Core",
                    Category = "attribute-drift",
                    TypeName = "Firebase.Core.Options",
                    MemberName = "ApiKey",
                    Selector = "APIKey",
                    Reason = "known false positive",
                    Evidence = ["header"]
                }
            ]
        };

        var application = SuppressionEngine.Apply([result], config, disableSuppressions: true);
        var target = application.Results.Single();

        Assert.False(application.Summary.Enabled);
        Assert.Equal(1, target.FailureCount);
        Assert.Equal(0, target.SuppressedCount);
        Assert.Single(target.Findings);
    }

    private static TargetAuditResult CreateResult(string target, params AuditFinding[] findings)
    {
        return new TargetAuditResult(
            Target: target,
            Xcframework: $"{target}.xcframework",
            Status: "failed",
            GenerationStatus: "succeeded",
            ComparisonSource: "primary",
            FailureCount: findings.Count(finding => finding.Severity == "failure"),
            InfoCount: findings.Count(finding => finding.Severity == "info"),
            SuppressedCount: 0,
            SharpieStatus: "not-requested",
            ConfidenceSummary: AuditFindingSummary.BuildConfidenceSummary(findings),
            Findings: findings,
            SuppressedFindings: []);
    }

    private static AuditFinding CreateFailureFinding(
        string typeName,
        string memberName,
        string selector,
        string? comparisonTypeKey = null,
        string? comparisonMemberKey = null)
    {
        return new AuditFinding(
            Category: "attribute-drift",
            Severity: "failure",
            Message: $"{typeName}.{memberName}",
            TypeName: typeName,
            MemberName: memberName,
            Selector: selector,
            BaselineFile: "baseline.cs",
            GeneratedFile: "generated.cs",
            ComparisonTypeKey: comparisonTypeKey,
            ComparisonMemberKey: comparisonMemberKey,
            Confidence: "not-reviewed",
            ConfidenceSource: "test");
    }
}
