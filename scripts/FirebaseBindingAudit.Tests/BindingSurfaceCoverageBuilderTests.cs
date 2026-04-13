using Xunit;

namespace FirebaseBindingAudit.Tests;

public sealed class BindingSurfaceCoverageBuilderTests
{
    [Fact]
    public void Build_RecordsManualMethodParameterTypes()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"firebase-binding-surface-builder-{Guid.NewGuid():N}");

        try
        {
            var sourceDirectory = Path.Combine(repoRoot, "source", "Firebase", "Auth");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "ApiDefinition.cs"),
                """
                namespace Firebase.Auth;

                public partial class AuthManualSurface
                {
                    [Wrap("SignInWithEmail")]
                    public string SignIn(string email, bool createUser) => email;
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "SignIn");

            Assert.Equal(2, surface.ParameterCount);
            Assert.Equal(["string", "bool"], surface.ParameterTypes);
            Assert.Equal("string", surface.ReturnType);
            Assert.Equal("SignIn(string, bool) -> string", surface.Signature);
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Build_RecordsPublicHelperDelegatesConstructorsAndIndexers()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"firebase-binding-surface-builder-{Guid.NewGuid():N}");

        try
        {
            var sourceDirectory = Path.Combine(repoRoot, "source", "Firebase", "Auth");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "ApiDefinition.cs"),
                """
                namespace Firebase.Auth;

                [BaseType(typeof(NSObject), Name = "FIRAuth")]
                public interface Auth
                {
                    [Field("FIRAuthErrorDomain", "__Internal")]
                    NSString ErrorDomain { get; }
                }
                """);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Extension.cs"),
                """
                namespace Firebase.Auth;

                public partial class Auth
                {
                    public delegate string TokenFactory(int index);

                    public Auth(string name)
                    {
                    }

                    public string this[int index] => "";
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration(["Extension.cs"])).Build(
                repoRoot,
                CreateManifest("Extension.cs"),
                "Auth");

            var surfaces = document.Targets.Single().Surfaces;
            var fieldConstant = Assert.Single(surfaces, static surface => surface.BindingValue == "FIRAuthErrorDomain");
            Assert.True(fieldConstant.IsStatic);

            var helperDelegate = Assert.Single(surfaces, static surface => surface.Kind == "manual-delegate");
            Assert.Equal("Firebase.Auth.Auth+TokenFactory", helperDelegate.RuntimeTypeName);
            Assert.Equal(["int"], helperDelegate.ParameterTypes);
            Assert.Equal("string", helperDelegate.ReturnType);

            var helperConstructor = Assert.Single(surfaces, static surface => surface.Kind == "manual-constructor");
            Assert.Equal("Firebase.Auth.Auth", helperConstructor.RuntimeTypeName);
            Assert.Equal(["string"], helperConstructor.ParameterTypes);

            var helperIndexer = Assert.Single(surfaces, static surface => surface.Kind == "manual-indexer");
            Assert.Equal("Item", helperIndexer.MemberName);
            Assert.Equal(["int"], helperIndexer.ParameterTypes);
            Assert.Equal("string", helperIndexer.ReturnType);
            Assert.True(helperIndexer.HasGetter);
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    private static AuditConfiguration CreateConfiguration(string[]? helperFiles = null) =>
        new()
        {
            ManualAttributes = ["Wrap"],
            BindingAttributes = ["Export", "Field", "Notification"],
            Targets =
            [
                new AuditTargetDefinition
                {
                    Id = "Auth",
                    PackageId = "AdamE.Firebase.iOS.Auth",
                    BaselineDirectory = Path.Combine("source", "Firebase", "Auth"),
                    BaselineFiles = ["ApiDefinition.cs"],
                    HelperFiles = helperFiles ?? []
                }
            ]
        };

    private static BindingSurfaceCoverageManifest CreateManifest(params string[] helperFiles) =>
        new()
        {
            Targets =
            [
                new BindingSurfaceCoverageTargetManifest
                {
                    Id = "Auth",
                    PackageId = "AdamE.Firebase.iOS.Auth",
                    CoverageCaseMethod = "VerifyAuthBindingSurfaceAsync",
                    SourceFiles = [Path.Combine("source", "Firebase", "Auth", "ApiDefinition.cs"), .. helperFiles.Select(static file => Path.Combine("source", "Firebase", "Auth", file))]
                }
            ]
        };
}
