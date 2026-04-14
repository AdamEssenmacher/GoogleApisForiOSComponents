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
    public void Build_RecordsBoundMethodParameterModifiers()
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
                    [Export("signOut:")]
                    bool SignOut([NullAllowed] out NSError error);
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "SignOut");

            Assert.Equal("method", surface.Kind);
            Assert.Equal(1, surface.ParameterCount);
            Assert.Equal(["out NSError"], surface.ParameterTypes);
            Assert.Equal("SignOut(out NSError) -> bool", surface.Signature);
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
    public void Build_PreservesManualExportStaticness()
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
                    [Static]
                    [Async]
                    [Export("fetchSignInMethodsForEmail:completion:")]
                    void FetchSignInMethods(string email, Action completion);
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "FetchSignInMethods");

            Assert.Equal("manual-method", surface.Kind);
            Assert.Equal("FIRAuth", surface.ObjectiveCName);
            Assert.True(surface.IsStatic);
            var nativeSelector = Assert.Single(surface.NativeSelectors);
            Assert.Equal("fetchSignInMethodsForEmail:completion:", nativeSelector.Selector);
            Assert.True(nativeSelector.IsStatic);
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
    public void Build_IncludesDelegatesReferencedByManualMembers()
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

                public delegate void SignInMethodQueryHandler(string[] methods, NSError error);

                [BaseType(typeof(NSObject), Name = "FIRAuth")]
                public interface Auth
                {
                    [Async]
                    [Export("fetchSignInMethodsForEmail:completion:")]
                    void FetchSignInMethods(string email, SignInMethodQueryHandler completion);
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surfaces = document.Targets.Single().Surfaces;
            Assert.Contains(
                surfaces,
                static surface => surface.Kind == "delegate" &&
                                  surface.TypeName == "Firebase.Auth.SignInMethodQueryHandler" &&
                                  surface.ParameterTypes.SequenceEqual(["string[]", "NSError"]));
            Assert.Contains(
                surfaces,
                static surface => surface.Kind == "manual-method" &&
                                  surface.MemberName == "FetchSignInMethods" &&
                                  surface.ParameterTypes.SequenceEqual(["string", "SignInMethodQueryHandler"]));
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
    public void Build_PreservesManualExportPropertyKind()
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

                [BaseType(typeof(NSObject), Name = "FIRLifecycleEvents")]
                public interface LifecycleEvents
                {
                    [Advice("Prefer the default event name.")]
                    [Export("setExperimentEventName", ArgumentSemantic = ArgumentSemantic.Copy)]
                    NSString SetExperimentEventName { get; set; }
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "SetExperimentEventName");

            Assert.Equal("manual-property", surface.Kind);
            Assert.Equal("FIRLifecycleEvents", surface.ObjectiveCName);
            Assert.Equal("Export", surface.BindingAttribute);
            Assert.Equal("setExperimentEventName", surface.BindingValue);
            Assert.True(surface.HasGetter);
            Assert.True(surface.HasSetter);
            Assert.Equal("NSString", surface.ReturnType);
            Assert.Equal(
                ["setExperimentEventName", "setSetExperimentEventName:"],
                surface.NativeSelectors.Select(static selector => selector.Selector).ToArray());
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
    public void Build_SynthesizesNamedIsPropertySetterSelector()
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
                    [Export("isTokenAutoRefreshEnabled")]
                    bool IsTokenAutoRefreshEnabled { get; set; }
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "IsTokenAutoRefreshEnabled");

            Assert.Equal(
                ["isTokenAutoRefreshEnabled", "setIsTokenAutoRefreshEnabled:"],
                surface.NativeSelectors.Select(static selector => selector.Selector).ToArray());
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
    public void Build_UsesGetterBindForBooleanGetterSelector()
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
                    [Export("tokenAutoRefreshEnabled")]
                    bool TokenAutoRefreshEnabled { [Bind("isTokenAutoRefreshEnabled")] get; set; }
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "TokenAutoRefreshEnabled");

            Assert.Equal(
                ["isTokenAutoRefreshEnabled", "setTokenAutoRefreshEnabled:"],
                surface.NativeSelectors.Select(static selector => selector.Selector).ToArray());
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
    public void Build_RecordsEnumUnderlyingType()
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

                [Native]
                public enum AuthErrorCode : long
                {
                    Unknown = 0
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.Kind == "enum");

            Assert.Equal("Firebase.Auth.AuthErrorCode", surface.RuntimeTypeName);
            Assert.Equal("long", surface.UnderlyingType);
            Assert.Equal("enum", surface.ContainerKind);
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
    public void Build_PreservesPublicHelperTypeShape()
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
                """);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Extension.cs"),
                """
                namespace Firebase.Auth;

                public static class AuthHelpers
                {
                    public static string Normalize(string value) => value;
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration(["Extension.cs"])).Build(
                repoRoot,
                CreateManifest("Extension.cs"),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.Kind == "manual-type");

            Assert.Equal("Firebase.Auth.AuthHelpers", surface.RuntimeTypeName);
            Assert.Equal("static class", surface.ContainerKind);
            Assert.True(surface.IsStatic);
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
    public void Build_PreservesApiDefinitionManualTypeShape()
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

                public interface AuthMarker
                {
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.Kind == "manual-type");

            Assert.Equal("Firebase.Auth.AuthMarker", surface.RuntimeTypeName);
            Assert.Equal("interface", surface.ContainerKind);
            Assert.False(surface.IsStatic);
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
    public void Build_DoesNotTreatApiDefinitionHelperExportsAsNativeClasses()
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

                public interface InstallationIdChangedEventArgs
                {
                    [Export("kFIRInstallationIDDidChangeNotificationAppNameKey")]
                    string AppName { get; set; }
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration()).Build(
                repoRoot,
                CreateManifest(),
                "Auth");

            var surface = Assert.Single(
                document.Targets.Single().Surfaces,
                static surface => surface.MemberName == "AppName");

            Assert.Equal("manual-property", surface.Kind);
            Assert.Null(surface.ObjectiveCName);
            Assert.Empty(surface.NativeSelectors);
            Assert.Equal("Export", surface.BindingAttribute);
            Assert.Equal("kFIRInstallationIDDidChangeNotificationAppNameKey", surface.BindingValue);
            Assert.True(surface.HasGetter);
            Assert.False(surface.HasSetter);
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
    public void Build_IncludesImplicitlyPublicInterfaceHelperMembers()
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
                """);
            File.WriteAllText(
                Path.Combine(sourceDirectory, "Extension.cs"),
                """
                namespace Firebase.Auth;

                public interface IAuthTokenSource
                {
                    string Token { get; }

                    string this[int index] { get; }

                    bool TryGetToken(out NSError error);

                    private string HiddenToken() => "";
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration(["Extension.cs"])).Build(
                repoRoot,
                CreateManifest("Extension.cs"),
                "Auth");

            var surfaces = document.Targets.Single().Surfaces;
            var helperType = Assert.Single(surfaces, static surface => surface.Kind == "manual-type");
            Assert.Equal("Firebase.Auth.IAuthTokenSource", helperType.RuntimeTypeName);
            Assert.Equal("interface", helperType.ContainerKind);

            var helperProperty = Assert.Single(surfaces, static surface => surface.Kind == "manual-property");
            Assert.Equal("Token", helperProperty.MemberName);
            Assert.Equal("string", helperProperty.ReturnType);

            var helperMethod = Assert.Single(surfaces, static surface => surface.Kind == "manual-method");
            Assert.Equal("TryGetToken", helperMethod.MemberName);
            Assert.Equal(["out NSError"], helperMethod.ParameterTypes);
            Assert.Equal("bool", helperMethod.ReturnType);

            var helperIndexer = Assert.Single(surfaces, static surface => surface.Kind == "manual-indexer");
            Assert.Equal("Item", helperIndexer.MemberName);
            Assert.Equal(["int"], helperIndexer.ParameterTypes);
            Assert.Equal("string", helperIndexer.ReturnType);

            Assert.DoesNotContain(surfaces, static surface => surface.MemberName == "HiddenToken");
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
                    public delegate string TokenFactory(int index, ref NSError error);

                    public Auth(string name)
                    {
                    }

                    public string this[int index] => "";

                    public bool TryGetToken(out NSError error)
                    {
                        error = null;
                        return false;
                    }
                }
                """);

            var document = new BindingSurfaceCoverageBuilder(CreateConfiguration(["Extension.cs"])).Build(
                repoRoot,
                CreateManifest("Extension.cs"),
                "Auth");

            var surfaces = document.Targets.Single().Surfaces;
            var fieldConstant = Assert.Single(surfaces, static surface => surface.BindingValue == "FIRAuthErrorDomain");
            Assert.True(fieldConstant.IsStatic);

            var helperType = Assert.Single(surfaces, static surface => surface.Kind == "manual-type");
            Assert.Equal("class", helperType.ContainerKind);
            Assert.False(helperType.IsStatic);

            var helperDelegate = Assert.Single(surfaces, static surface => surface.Kind == "manual-delegate");
            Assert.Equal("Firebase.Auth.Auth+TokenFactory", helperDelegate.RuntimeTypeName);
            Assert.Equal("delegate", helperDelegate.ContainerKind);
            Assert.Equal(["int", "ref NSError"], helperDelegate.ParameterTypes);
            Assert.Equal("string", helperDelegate.ReturnType);

            var helperMethod = Assert.Single(surfaces, static surface => surface.MemberName == "TryGetToken");
            Assert.Equal(["out NSError"], helperMethod.ParameterTypes);
            Assert.Equal("bool", helperMethod.ReturnType);

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
            ManualAttributes = ["Wrap", "Advice", "Async"],
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
