using FirebaseReleaseFingerprint;
using Xunit;

public class ReleaseBuildFingerprintTests
{
    const string Components = """
        Artifact MAPS = new Artifact("Google.Maps", "1.0");
        Artifact FIREBASE = new Artifact("Firebase.Core", "12.14");
        var ARTIFACTS = new Dictionary<string, Artifact> { { "Google.Maps", MAPS }, { "Firebase.Core", FIREBASE } };
        void SetPins() {
            MAPS.PodSpecs = new[] { PodSpec.Create("GoogleMaps", "1.0") };
            FIREBASE.PodSpecs = new[] { PodSpec.Create("FirebaseCore", "12.14") };
            MAPS.Dependencies = new[] { FIREBASE };
            SharedSetup("shared");
        }
        """;

    [Fact]
    public void FoundationScopeIgnoresUnselectedVersionPinsAndRegistryEntries()
    {
        var old = BuildInputs.NormalizeComponents(Components, ["FIREBASE"]);
        Assert.Equal(old, BuildInputs.NormalizeComponents(Components.Replace("12.14", "12.15"), ["FIREBASE"]));
        Assert.Equal(old, BuildInputs.NormalizeComponents("// comment\n" + Components, ["FIREBASE"]));
        Assert.Equal(old, BuildInputs.NormalizeComponents(Components.Replace("Firebase.Core", "Firebase.Other"), ["FIREBASE"]));
        Assert.NotEqual(old, BuildInputs.NormalizeComponents(Components + "\n#if IOS\nSharedSetup(\"changed\");\n#endif", ["FIREBASE"]));
        Assert.NotEqual(old, BuildInputs.NormalizeComponents(Components.Replace("1.0", "2.0"), ["FIREBASE"]));
        Assert.NotEqual(old, BuildInputs.NormalizeComponents(Components.Replace("shared", "changed"), ["FIREBASE"]));
        Assert.NotEqual(old, BuildInputs.NormalizeComponents(Components.Replace("new[] { FIREBASE }", "new Artifact[0]"), ["FIREBASE"]));
    }

    [Fact]
    public void FoundationScopeIncludesSelectedTransitiveDependencyPins()
    {
        Assert.NotEqual(BuildInputs.NormalizeComponents(Components, []),
            BuildInputs.NormalizeComponents(Components.Replace("12.14", "12.15"), []));
        Assert.Throws<InvalidOperationException>(() => BuildInputs.NormalizeComponents("invalid {{{", []));
    }

    const string Build = """
        Task("nuget").Does(() => {
            if (ARTIFACTS_TO_BUILD.Contains(GOOGLE_MAPS_ARTIFACT)) MapsOnly();
            if (ARTIFACTS_TO_BUILD.Contains(GOOGLE_SIGN_IN_ARTIFACT)) SignInOnly();
            SharedPack("Release");
        });
        void MapsOnly() { MapsHelper("maps-v1"); }
        void MapsHelper(string value) { Information(value); }
        void SignInOnly() { Information("signin-v1"); }
        void SharedPack(string value) { DotNetPack(value); }
        """;

    static string Fingerprint(string build, string common = "") => BuildInputs.Normalize(
        new Dictionary<string, string> { ["build.cake"] = build, ["common.cake"] = common },
        ["GOOGLE_MAPS_ARTIFACT", "GOOGLE_SIGN_IN_ARTIFACT"]);

    [Theory]
    [InlineData("maps-v1", "maps-v2")]
    [InlineData("signin-v1", "signin-v2")]
    [InlineData("MapsOnly();", "{ MapsOnly(); Information(\"maps only\"); }")]
    [InlineData("SignInOnly();", "{ SignInOnly(); Information(\"signin only\"); }")]
    public void ExcludedComponentBodiesAndHelpersDoNotAffectFingerprint(string before, string after)
        => Assert.Equal(Fingerprint(Build), Fingerprint(Build.Replace(before, after)));

    [Fact]
    public void CommentsAndWhitespaceDoNotAffectFingerprint()
        => Assert.Equal(Fingerprint(Build), Fingerprint("// heading\n" + Build.Replace("SharedPack(\"Release\");", "/* comment */ SharedPack ( \"Release\" ) ; // note")));

    [Fact]
    public void DocumentationCommentsDoNotAffectFingerprint()
        => Assert.Equal(Fingerprint(Build), Fingerprint(Build.Replace("void SharedPack", "/// <summary>Pack shared outputs.</summary>\nvoid SharedPack")));

    [Theory]
    [InlineData("Release", "Debug")]
    [InlineData("DotNetPack(value)", "DotNetBuild(value)")]
    [InlineData("GOOGLE_MAPS_ARTIFACT", "FIREBASE_CORE_ARTIFACT")]
    [InlineData("GOOGLE_MAPS_ARTIFACT", "UNKNOWN_ARTIFACT")]
    public void SharedPackagingAndUnknownOrSelectedGuardsRemainInputs(string before, string after)
        => Assert.NotEqual(Fingerprint(Build), Fingerprint(Build.Replace(before, after)));

    [Fact]
    public void HelperAlsoUsedBySharedCodeRemainsAnInput()
    {
        var shared = Build.Replace("SharedPack(\"Release\");", "SharedPack(\"Release\"); MapsHelper(\"shared\");");
        Assert.NotEqual(Fingerprint(shared), Fingerprint(shared.Replace("Information(value)", "Warning(value)")));
    }

    [Fact]
    public void CrossFileExclusiveHelpersAreExcludedButSharedHelpersAreRetained()
    {
        var build = "if (ARTIFACTS_TO_BUILD.Contains(GOOGLE_MAPS_ARTIFACT)) MapsOnly(); Shared();";
        var common = "void MapsOnly() { Helper(); } void Helper() { Information(1); } void Shared() { Information(2); }";
        Assert.Equal(Fingerprint(build, common), Fingerprint(build, common.Replace("Information(1)", "Information(3)")));
        Assert.NotEqual(Fingerprint(build, common), Fingerprint(build, common.Replace("Information(2)", "Information(3)")));
    }

    [Fact]
    public void ElseBranchAndCommentLikeStringsRemainInputs()
    {
        var build = "if (ARTIFACTS_TO_BUILD.Contains(GOOGLE_MAPS_ARTIFACT)) MapsOnly(); else Information(\"https://a/*text*/\");";
        Assert.NotEqual(Fingerprint(build), Fingerprint(build.Replace("https://a", "https://b")));
    }

    [Fact]
    public void ToolchainDirectivesAndInvalidSyntaxAreNotSilentlyIgnored()
    {
        Assert.NotEqual(Fingerprint("#load \"one.cake\"\n" + Build), Fingerprint("#load \"two.cake\"\n" + Build));
        Assert.Throws<InvalidOperationException>(() => Fingerprint("if ("));
    }
}
