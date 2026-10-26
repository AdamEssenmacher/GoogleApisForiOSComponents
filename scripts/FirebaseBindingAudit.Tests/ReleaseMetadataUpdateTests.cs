using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

public class ReleaseMetadataUpdateTests
{
    // Execute the production Cake transforms and archive verifier with only the
    // filesystem/logging aliases stubbed. No vendor downloads or Apple SDK needed.
    static readonly Lazy<Type> Helpers = new(CompileHelpers);

    static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "firebase_release.cake"))) return dir.FullName;
        throw new InvalidOperationException("Repository root not found");
    }

    static Type CompileHelpers()
    {
        CompilationUnitSyntax Parse(string file) => CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(RepositoryRoot(), file)),
            new CSharpParseOptions(kind: SourceCodeKind.Script)).GetCompilationUnitRoot();
        var release = Parse("firebase_release.cake");
        var downloads = Parse("custom_externals_download.cake");
        string Method(CompilationUnitSyntax root, string name) => root.Members.OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == name)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
            .NormalizeWhitespace().ToFullString();
        string Class(CompilationUnitSyntax root, string name) => root.Members.OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.ValueText == name)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .NormalizeWhitespace().ToFullString();
        var source = """
            using System;
            using System.IO;
            using System.Collections.Generic;
            using System.Text.RegularExpressions;
            """ + Class(release, "FirebaseReleaseDownloadInfo") + Class(downloads, "ExternalDownloadSource") + """
            public record FilePath(string FullPath);
            public static class CakeHelpers {
              public static void Information(string message) { }
            """ + Method(release, "UpdateFirebaseReleaseExternalDownloadsCake")
                + Method(release, "ReplaceFirebaseReleasePackageReferenceVersion")
                + Method(downloads, "VerifyArchiveHash") + """
              public static string UpdateDownload(string text, string version, string key, string hash) =>
                UpdateFirebaseReleaseExternalDownloadsCake(text, new Dictionary<string, FirebaseReleaseDownloadInfo> {
                  ["FirebaseAnalytics"] = new FirebaseReleaseDownloadInfo { Id="FirebaseAnalytics", Version=version, ArchiveKey=key, ExpectedSha256=hash }
                });
              public static void VerifyFile(string path, string hash) =>
                VerifyArchiveHash(new ExternalDownloadSource("Fixture", "1.0.0", "key", expectedSha256: hash), new FilePath(path));
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("CakeHelpers" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return Assembly.Load(output.ToArray()).GetType("CakeHelpers")!;
    }

    static object? Invoke(string name, params object?[] args)
    {
        try { return Helpers.Value.GetMethod(name)!.Invoke(null, args); }
        catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
    }

    [Theory]
    [InlineData("12.12.0")]
    [InlineData("[12.12.0]")]
    [InlineData("[12.12.0,12.13.0)")]
    public void MeasurementVersionUpdatesKeepTheNativePairExact(string previous)
    {
        var text = $"<Project><ItemGroup><PackageReference Include=\"AdamE.Google.iOS.GoogleAppMeasurement\" Version=\"{previous}\"/>"
            + "<PackageReference Include=\"Other\" Version=\"1.0.0\"/></ItemGroup></Project>";
        foreach (var version in new[] { "12.13.0", "12.14.0" })
        {
            text = (string)Invoke("ReplaceFirebaseReleasePackageReferenceVersion", text, "AdamE.Google.iOS.GoogleAppMeasurement", version)!;
            var refs = XDocument.Parse(text).Descendants("PackageReference").ToArray();
            Assert.Equal($"[{version}]", refs[0].Attribute("Version")!.Value);
            Assert.Equal("1.0.0", refs[1].Attribute("Version")!.Value);
        }
    }

    [Fact]
    public void UnrelatedPackageUpdatesRetainTheirMinimumVersionSemantics()
    {
        var text = "<PackageReference Include=\"Other\" Version=\"1.0.0\"/>";
        var updated = (string)Invoke("ReplaceFirebaseReleasePackageReferenceVersion", text, "Other", "2.0.0")!;
        Assert.Equal("2.0.0", XElement.Parse(updated).Attribute("Version")!.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchiveUpdatesMigrateOrReplaceTheHashTogetherWithCoordinates(bool alreadyPinned)
    {
        var oldHash = new string('a', 64); var newHash = new string('b', 64);
        var text = "{ \"FirebaseAnalytics\", new ExternalDownloadSource (\"FirebaseAnalytics\", \"12.12.0\", \"old-key\""
            + (alreadyPinned ? $", expectedSha256: \"{oldHash}\"" : "") + ") },\n"
            + "{ \"GooglePlaces\", new ExternalDownloadSource (\"GooglePlaces\", \"7.4.0\", \"places-key\") },";
        var updated = (string)Invoke("UpdateDownload", text, "12.13.0", "new-key", newHash)!;
        Assert.Contains($"\"12.13.0\", \"new-key\", expectedSha256: \"{newHash}\"", updated);
        Assert.DoesNotContain(oldHash, updated);
        Assert.DoesNotContain("old-key", updated);
        Assert.Contains("\"GooglePlaces\", \"7.4.0\", \"places-key\"", updated);
        Assert.Equal(updated, Invoke("UpdateDownload", updated, "12.13.0", "new-key", newHash));
    }

    [Fact]
    public void ArchiveUpdaterRejectsMissingHashesAndMissingDeclarations()
    {
        Assert.Contains("invalid archive SHA-256", Assert.Throws<Exception>(() => Invoke("UpdateDownload", "", "12.13.0", "key", "")).Message);
        Assert.Contains("Expected one", Assert.Throws<Exception>(() => Invoke("UpdateDownload", "", "12.13.0", "key", new string('a', 64))).Message);
    }

    [Fact]
    public void ArchiveVerifierAcceptsMatchingBytesAndRejectsCorruption()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "reviewed archive fixture");
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            Invoke("VerifyFile", path, hash); // Uppercase pins are supported.
            File.AppendAllText(path, "corrupt response");
            Assert.Contains("SHA-256 mismatch", Assert.Throws<Exception>(() => Invoke("VerifyFile", path, hash)).Message);
        }
        finally { File.Delete(path); }
    }
}
