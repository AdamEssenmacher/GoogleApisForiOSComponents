using Xunit;

namespace FirebaseBindingAudit.Tests;

public sealed class AuditRunnerTests
{
    [Fact]
    public void ShouldUseSharpieComparisonFallback_UsesForceFallbackWhenPrimaryComparisonSucceeded()
    {
        var target = new AuditTargetDefinition
        {
            UseSharpieComparisonFallback = true
        };

        var sharpieRun = new SharpieRunResult(
            Status: "succeeded",
            FrameworkPath: null,
            ProcessResult: null,
            Comparison: new TargetComparisonResult([], []),
            Snapshot: null,
            GeneratedFiles: [],
            WarningFindings: [],
            Message: null);

        var shouldFallback = AuditRunner.ShouldUseSharpieComparisonFallback(
            target,
            sharpieMode: "forceFallback",
            primaryFailed: false,
            primaryComparableSurfaceEmpty: false,
            sharpieRun);

        Assert.True(shouldFallback);
    }

    [Fact]
    public void ShouldUseSharpieComparisonFallback_DoesNotUseAutoWhenPrimaryComparisonSucceeded()
    {
        var target = new AuditTargetDefinition
        {
            UseSharpieComparisonFallback = true
        };

        var sharpieRun = new SharpieRunResult(
            Status: "succeeded",
            FrameworkPath: null,
            ProcessResult: null,
            Comparison: new TargetComparisonResult([], []),
            Snapshot: null,
            GeneratedFiles: [],
            WarningFindings: [],
            Message: null);

        var shouldFallback = AuditRunner.ShouldUseSharpieComparisonFallback(
            target,
            sharpieMode: "auto",
            primaryFailed: false,
            primaryComparableSurfaceEmpty: false,
            sharpieRun);

        Assert.False(shouldFallback);
    }

    [Fact]
    public void TryResolveSharpieCompanionFrameworkPath_UsesLaterExistingImport()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"firebase-binding-audit-companion-{Guid.NewGuid():N}");

        try
        {
            var stagedFrameworksDirectory = Path.Combine(tempRoot, "frameworks");
            var primaryFrameworkPath = Path.Combine(stagedFrameworksDirectory, "FirebaseCore.xcframework", "ios-arm64", "FirebaseCore.framework");
            var primaryHeadersDirectory = Path.Combine(primaryFrameworkPath, "Headers");
            var companionFrameworkPath = Path.Combine(stagedFrameworksDirectory, "FirebaseSessions.xcframework", "ios-arm64", "FirebaseSessions.framework");

            Directory.CreateDirectory(primaryHeadersDirectory);
            Directory.CreateDirectory(companionFrameworkPath);
            File.WriteAllText(
                Path.Combine(primaryHeadersDirectory, "FirebaseCore.h"),
                """
                #import <Foundation/Foundation.h>
                #import <FirebaseCore/FirebaseCore.h>
                #import <FirebaseSessions/FirebaseSessions.h>
                """);

            var resolved = AuditRunner.TryResolveSharpieCompanionFrameworkPath(
                primaryFrameworkPath,
                stagedFrameworksDirectory,
                out var resolvedCompanionFrameworkPath);

            Assert.True(resolved);
            Assert.Equal(companionFrameworkPath, resolvedCompanionFrameworkPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
