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
    public void ShouldStageSharpieFrameworkSlices_SkipsWhenSharpieIsDisabled()
    {
        var selectedTargets = new[] { CreateTarget("Auth", "FirebaseAuth") };

        Assert.False(AuditRunner.ShouldStageSharpieFrameworkSlices(
            CreateAuditOptions(disableSharpie: true),
            selectedTargets,
            CreateSharpieConfiguration()));
        Assert.True(AuditRunner.ShouldStageSharpieFrameworkSlices(
            CreateAuditOptions(disableSharpie: false),
            selectedTargets,
            CreateSharpieConfiguration()));
    }

    [Fact]
    public void ShouldStageSharpieFrameworkSlices_SkipsTargetsWithSharpieOff()
    {
        var selectedTargets = new[] { CreateTarget("Auth", "FirebaseAuth", sharpieMode: "off") };

        var shouldStage = AuditRunner.ShouldStageSharpieFrameworkSlices(
            CreateAuditOptions(disableSharpie: false),
            selectedTargets,
            CreateSharpieConfiguration());

        Assert.False(shouldStage);
    }

    [Fact]
    public void ShouldStageSharpieFrameworkSlices_IncludesAutoAndForceFallbackTargets()
    {
        var selectedTargets = new[]
        {
            CreateTarget("Auth", "FirebaseAuth", sharpieMode: "auto"),
            CreateTarget("CloudFirestore", "FirebaseFirestore", sharpieMode: "forceFallback")
        };

        var stagedXcframeworks = AuditRunner.GetSharpieFrameworkSliceStageXcframeworks(
            CreateAuditOptions(disableSharpie: false),
            selectedTargets,
            CreateSharpieConfiguration());

        Assert.Equal(["FirebaseAuth", "FirebaseFirestore"], stagedXcframeworks);
    }

    [Fact]
    public void GetSharpieFrameworkSliceStageXcframeworks_ExcludesUnselectedTargets()
    {
        var selectedTargets = new[] { CreateTarget("Auth", "FirebaseAuth") };

        var stagedXcframeworks = AuditRunner.GetSharpieFrameworkSliceStageXcframeworks(
            CreateAuditOptions(disableSharpie: false),
            selectedTargets,
            CreateSharpieConfiguration());

        Assert.Equal(["FirebaseAuth"], stagedXcframeworks);
        Assert.DoesNotContain("UnsupportedUnselectedFramework", stagedXcframeworks);
    }

    [Fact]
    public void CreateTempRootPath_ReturnsUniqueAuditWorkspacePaths()
    {
        var firstPath = AuditRunner.CreateTempRootPath();
        var secondPath = AuditRunner.CreateTempRootPath();

        Assert.NotEqual(firstPath, secondPath);
        Assert.EndsWith("firebase-binding-audit", Directory.GetParent(firstPath)!.Name);
        Assert.Contains("-", Path.GetFileName(firstPath), StringComparison.Ordinal);
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

    [Fact]
    public void PrepareStagedXcframeworkForTarget_MaterializesSymlinkBeforeAddingUmbrellaImports()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"firebase-binding-audit-umbrella-{Guid.NewGuid():N}");

        try
        {
            var sourceXcframeworkPath = Path.Combine(tempRoot, "externals", "FirebaseFunctions.xcframework");
            var sourceHeaderPath = Path.Combine(sourceXcframeworkPath, "ios-arm64", "FirebaseFunctions.framework", "Headers", "FirebaseFunctions-umbrella.h");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceHeaderPath)!);
            File.WriteAllText(sourceHeaderPath, "FOUNDATION_EXPORT double FirebaseFunctionsVersionNumber;\n");

            var stagedXcframeworkPath = Path.Combine(tempRoot, "frameworks", "FirebaseFunctions.xcframework");
            Directory.CreateDirectory(Path.GetDirectoryName(stagedXcframeworkPath)!);
            Directory.CreateSymbolicLink(stagedXcframeworkPath, sourceXcframeworkPath);

            AuditRunner.PrepareStagedXcframeworkForTarget(
                new AuditTargetDefinition
                {
                    Id = "CloudFunctions",
                    Xcframework = "FirebaseFunctions",
                    ObjcUmbrellaHeaderImports = ["FirebaseFunctions/FirebaseFunctions-Swift.h"]
                },
                stagedXcframeworkPath);

            var stagedHeaderPath = Path.Combine(stagedXcframeworkPath, "ios-arm64", "FirebaseFunctions.framework", "Headers", "FirebaseFunctions-umbrella.h");
            Assert.Contains("#import <FirebaseFunctions/FirebaseFunctions-Swift.h>", File.ReadAllText(stagedHeaderPath));
            Assert.DoesNotContain("#import <FirebaseFunctions/FirebaseFunctions-Swift.h>", File.ReadAllText(sourceHeaderPath));
            Assert.False((new DirectoryInfo(stagedXcframeworkPath).Attributes & FileAttributes.ReparsePoint) != 0);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static AuditOptions CreateAuditOptions(bool disableSharpie) =>
        new(
            RepoRoot: string.Empty,
            OutputDirectory: string.Empty,
            GeneratorVersion: "0.7.0",
            SelectedTargets: null,
            KeepTemp: false,
            SharpiePath: null,
            SharpieVersion: "26.3.0.11",
            DisableSharpie: disableSharpie,
            DisableSuppressions: false);

    private static SharpieConfiguration CreateSharpieConfiguration() =>
        new()
        {
            DefaultMode = "auto"
        };

    private static AuditTargetDefinition CreateTarget(string id, string xcframework, string? sharpieMode = null) =>
        new()
        {
            Id = id,
            Xcframework = xcframework,
            SharpieMode = sharpieMode
        };
}
