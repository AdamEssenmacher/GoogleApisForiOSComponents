using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

public class NativeCacheTests : IDisposable
{
    static readonly Lazy<Type> HarnessType = new(Compile);
    readonly string root = Path.Combine(Path.GetTempPath(), "native-cache-" + Guid.NewGuid().ToString("N"));
    readonly object harness;

    public NativeCacheTests()
    {
        Directory.CreateDirectory(root);
        harness = Activator.CreateInstance(HarnessType.Value, root)!;
    }
    public void Dispose() => Directory.Delete(root, true);
    void Set(string name, object value) => HarnessType.Value.GetField(name)!.SetValue(harness, value);
    int Count(string name) => (int)HarnessType.Value.GetField(name)!.GetValue(harness)!;
    void Run(string method)
    {
        try { HarnessType.Value.GetMethod(method)!.Invoke(harness, null); }
        catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
    }
    string Output(string name) => Path.Combine(root, "externals", name);
    string Archive => Path.Combine(root, "build/Widget/iphoneos.xcarchive");

    [Fact]
    public void Download_UnchangedInputReusesVerifiedOutputs()
    {
        Run("Download"); Run("Download");
        Assert.Equal(1, Count("Downloads"));
        Assert.Equal(2, Count("Copies"));
    }

    [Theory]
    [InlineData("Version", "2.0")]
    [InlineData("Key", "other-key")]
    [InlineData("Hash", "uppercase")]
    [InlineData("UrlPrefix", "https://other.example")]
    public void Download_ChangedSourceInvalidates(string field, string value)
    {
        Run("Download");
        // SHA spelling is part of source identity even when its verified value is identical.
        Set(field, value); Run("Download");
        Assert.Equal(2, Count("Downloads"));
    }

    [Theory]
    [InlineData("stamp")]
    [InlineData("framework")]
    [InlineData("binary")]
    [InlineData("bundle")]
    [InlineData("corruption")]
    [InlineData("empty")]
    public void Download_MissingOrIncompleteOutputIsNotAHit(string damage)
    {
        Run("Download");
        if (damage == "stamp") File.Delete(Output("GoogleAppMeasurement.download-cache"));
        if (damage == "framework") Directory.Delete(Output("GoogleAppMeasurementIdentitySupport.xcframework"), true);
        if (damage == "binary") File.Delete(Output("GoogleAppMeasurement.xcframework/ios/Measurement.framework/Measurement"));
        if (damage == "bundle") File.Delete(Output("GoogleAppMeasurement.xcframework/ios/Measurement.framework/Resources/Privacy.bundle/Info.plist"));
        if (damage == "corruption") File.WriteAllText(Output("GoogleAppMeasurementIdentitySupport.xcframework/Info.plist"), "truncated");
        if (damage == "empty") { Directory.Delete(Output("GoogleAppMeasurement.xcframework"), true); Directory.CreateDirectory(Output("GoogleAppMeasurement.xcframework")); }
        Run("Download");
        Assert.Equal(2, Count("Downloads"));
        Run("Download"); Assert.Equal(2, Count("Downloads"));
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("partial")]
    [InlineData("second")]
    public void Download_InterruptedOrSilentPartialCopyCannotBecomeAHit(string failure)
    {
        Run("Download"); Set("Version", "2.0"); Set("CopyFailure", failure);
        Assert.ThrowsAny<Exception>(() => Run("Download"));
        Assert.False(File.Exists(Output("GoogleAppMeasurement.download-cache")));
        Set("CopyFailure", ""); Run("Download"); Run("Download");
        Assert.Equal(3, Count("Downloads"));
    }

    [Fact]
    public void Download_HashFailureLeavesNoCompletionRecord()
    {
        Set("Hash", "wrong"); Assert.ThrowsAny<Exception>(() => Run("Download"));
        Assert.Equal(0, Count("Copies"));
        Assert.False(File.Exists(Output("GoogleAppMeasurement.download-cache")));
        Set("Hash", ""); Run("Download"); Run("Download");
        Assert.Equal(2, Count("Downloads"));
    }

    [Fact]
    public void Download_ChangedHashCannotAcceptPreviouslyVerifiedOutputs()
    {
        Run("Download"); Set("Hash", "wrong");
        Assert.ThrowsAny<Exception>(() => Run("Download"));
        Assert.Equal(2, Count("Downloads"));
        Assert.False(File.Exists(Output("GoogleAppMeasurement.download-cache")));
        Set("Hash", ""); Run("Download"); Run("Download");
        Assert.Equal(3, Count("Downloads"));
    }

    [Fact]
    public void Analytics_UsesSameCompletionContract()
    {
        Run("Analytics"); Run("Analytics"); Assert.Equal(1, Count("Downloads"));
        File.Delete(Output("FirebaseAnalytics.xcframework/ios/Analytics.framework/Analytics"));
        Run("Analytics"); Assert.Equal(2, Count("Downloads"));
    }

    [Fact]
    public void Archive_UnchangedInputReusesButStillCreatesXcframework()
    {
        Run("Build"); Run("Build");
        Assert.Equal(1, Count("Builds")); Assert.Equal(2, Count("Creates"));
    }

    [Fact]
    public void Archive_CatalystResourcesPlistIsRequiredAndCanBeCached()
    {
        Set("Catalyst", true);
        Run("Build");
        Run("Build");
        Assert.Equal(1, Count("Builds"));

        File.Delete(Path.Combine(root, "build/Widget/maccatalyst.xcarchive/Products/Library/Frameworks/Widget.framework/Resources/Info.plist"));
        Run("Build");
        Assert.Equal(2, Count("Builds"));
    }

    [Theory]
    [InlineData("Version")]
    [InlineData("Settings")]
    [InlineData("Architectures")]
    [InlineData("XcodeId")]
    [InlineData("Lock")]
    [InlineData("Manifest")]
    [InlineData("Project")]
    [InlineData("Podfile")]
    [InlineData("Support")]
    public void Archive_ChangedBuildInputInvalidates(string change)
    {
        Run("Build");
        if (change == "Lock") File.AppendAllText(Path.Combine(root, "build/Podfile.lock"), "changed");
        else if (change == "Manifest") File.AppendAllText(Path.Combine(root, "build/Pods/Manifest.lock"), "changed");
        else if (change == "Podfile") File.AppendAllText(Path.Combine(root, "build/Podfile"), "changed");
        else if (change == "Support") File.AppendAllText(Path.Combine(root, "build/Pods/Target Support Files/Widget/Widget.xcconfig"), "changed");
        else if (change == "Project") File.AppendAllText(Path.Combine(root, "build/Pods/Pods.xcodeproj/project.pbxproj"), "changed");
        else Set(change, "changed");
        Run("Build"); Assert.Equal(2, Count("Builds"));
        Assert.Equal(change == "Version" ? "changed" : "1.0", File.ReadAllText(Path.Combine(Archive, "Products/Library/Frameworks/Widget.framework/Widget")));
        Run("Build"); Assert.Equal(2, Count("Builds"));
    }

    [Theory]
    [InlineData("stamp")]
    [InlineData("binary")]
    [InlineData("plist")]
    [InlineData("bundle")]
    [InlineData("corruption")]
    public void Archive_UnstampedOrIncompleteArchiveRebuilds(string damage)
    {
        Run("Build");
        var framework = Path.Combine(Archive, "Products/Library/Frameworks/Widget.framework");
        if (damage == "stamp") File.Delete(Archive + ".cache");
        if (damage == "binary") File.Delete(Path.Combine(framework, "Widget"));
        if (damage == "plist") File.Delete(Path.Combine(framework, "Info.plist"));
        if (damage == "bundle") File.Delete(Path.Combine(framework, "Resources/Privacy.bundle/Info.plist"));
        if (damage == "corruption") File.WriteAllText(Path.Combine(framework, "Widget"), "bad");
        Run("Build"); Assert.Equal(2, Count("Builds"));
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("partial")]
    public void Archive_FailedRebuildCannotReuseOldStamp(string failure)
    {
        Run("Build"); Set("Version", "2.0"); Set("BuildFailure", failure);
        Assert.ThrowsAny<Exception>(() => Run("Build"));
        Assert.False(File.Exists(Archive + ".cache"));
        Set("BuildFailure", ""); Run("Build"); Run("Build");
        Assert.Equal(3, Count("Builds"));
    }

    [Theory]
    [InlineData("unlocked")]
    [InlineData("local")]
    public void Archive_InputsWithoutStableSourceIdentityAlwaysRebuild(string mode)
    {
        if (mode == "unlocked") File.Delete(Path.Combine(root, "build/Podfile.lock"));
        else Set("Local", true);
        Run("Build"); Run("Build"); Assert.Equal(2, Count("Builds"));
        Assert.False(File.Exists(Archive + ".cache"));
    }

    static Type Compile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "common.cake"))) dir = dir.Parent;
        Assert.NotNull(dir);
        CompilationUnitSyntax Parse(string path) => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(dir.FullName, path)),
            new CSharpParseOptions(kind: SourceCodeKind.Script)).GetCompilationUnitRoot();
        var common = Parse("common.cake"); var downloads = Parse("custom_externals_download.cake"); var poco = Parse("poco.cake");
        string Class(CompilationUnitSyntax source, string name) => source.Members.OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString();
        string Method(CompilationUnitSyntax source, string name) => source.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name).ToFullString();
        var code = """
            using System;
            using System.IO;
            using System.Linq;
            using System.Collections.Generic;
            """ + string.Join("\n", poco.Members.OfType<StructDeclarationSyntax>().Where(c => new[] { "Arch", "Sdk", "PlatformV2" }.Contains(c.Identifier.ValueText)).Select(c => c.ToFullString()))
            + Class(common, "NativeCacheRecord") + Class(common, "XCodeBuildSettings") + Class(downloads, "ExternalDownloadSource")
            + File.ReadAllText(Path.Combine(dir.FullName, "scripts/FirebaseBindingAudit.Tests/Fixtures/NativeCacheHarness.cs.txt"))
            + Method(common, "BuildXcodeXcframework")
            + string.Join("\n", new[] { "DownloadAndExtract", "GetArchivePath", "GetExtractionRoot", "VerifyArchiveHash", "FirebaseAnalyticsDownload", "GoogleAppMeasurementDownload" }.Select(n => Method(downloads, n))) + "\n}";
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("NativeCache" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(code)], refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream(); var result = compilation.Emit(output);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return Assembly.Load(output.ToArray()).GetType("Harness")!;
    }
}
