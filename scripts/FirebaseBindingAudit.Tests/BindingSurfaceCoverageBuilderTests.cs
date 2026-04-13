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

    private static AuditConfiguration CreateConfiguration() =>
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
                    HelperFiles = []
                }
            ]
        };

    private static BindingSurfaceCoverageManifest CreateManifest() =>
        new()
        {
            Targets =
            [
                new BindingSurfaceCoverageTargetManifest
                {
                    Id = "Auth",
                    PackageId = "AdamE.Firebase.iOS.Auth",
                    CoverageCaseMethod = "VerifyAuthBindingSurfaceAsync",
                    SourceFiles = [Path.Combine("source", "Firebase", "Auth", "ApiDefinition.cs")]
                }
            ]
        };
}
