using Xunit;

namespace FirebaseBindingAudit.Tests;

public sealed class BindingSurfaceCoverageValidatorTests
{
    [Fact]
    public void Validate_DetectsUnclaimedSurfaces()
    {
        var document = CreateDocument(CreateSurface("Core:type:FIRApp"));

        var result = BindingSurfaceCoverageValidator.Validate(document, []);

        Assert.Equal(["Core:type:FIRApp"], result.UnclaimedSurfaceIds);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DetectsStaleWaivers()
    {
        var document = CreateDocument(
            CreateSurface("Core:type:FIRApp"),
            waivers: [CreateWaiver("Core:type:Missing")]);

        var result = BindingSurfaceCoverageValidator.Validate(document);

        Assert.Equal(["Core:type:Missing"], result.StaleWaiverSurfaceIds);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_DetectsStaleExerciserSurfaceIds()
    {
        var document = CreateDocument(CreateSurface("Core:type:FIRApp"));

        var result = BindingSurfaceCoverageValidator.Validate(
            document,
            [new BindingSurfaceExerciseRecord("Core", "Core:type:Missing")]);

        Assert.Equal(["Core:type:Missing"], result.StaleExerciseSurfaceIds);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_AcceptsExactManifestCoverage()
    {
        var document = CreateDocument(CreateSurface("Core:type:FIRApp"));

        var result = BindingSurfaceCoverageValidator.Validate(
            document,
            [new BindingSurfaceExerciseRecord("Core", "Core:type:FIRApp")]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_TreatsWaivedSurfacesAsReportedButNotFailed()
    {
        var document = CreateDocument(
            CreateSurface("Core:type:FIRApp"),
            waivers: [CreateWaiver("Core:type:FIRApp")]);

        var result = BindingSurfaceCoverageValidator.Validate(document, []);

        Assert.True(result.IsValid);
    }

    private static BindingSurfaceCoverageDocument CreateDocument(
        BindingSurfaceDescriptor surface,
        IReadOnlyList<BindingSurfaceWaiver>? waivers = null) =>
        new(
            [
                new BindingSurfaceCoverageTargetDocument(
                    "Core",
                    "AdamE.Firebase.iOS.Core",
                    "VerifyCoreBindingSurfaceAsync",
                    ["source/Firebase/Core/ApiDefinition.cs"],
                    [new BindingSurfacePackageReference { Id = "AdamE.Firebase.iOS.Core", Version = "12.6.0" }],
                    [surface])
            ],
            waivers ?? []);

    private static BindingSurfaceDescriptor CreateSurface(string surfaceId) =>
        new(
            Target: "Core",
            SurfaceId: surfaceId,
            Kind: "bound-type",
            TypeName: "Firebase.Core.App",
            RuntimeTypeName: "Firebase.Core.App",
            AssemblyName: "Firebase.Core",
            ObjectiveCName: "FIRApp",
            ContainerKind: "interface",
            IsProtocol: false,
            IsStatic: false,
            MemberName: null,
            BindingAttribute: null,
            BindingValue: null,
            HasGetter: false,
            HasSetter: false,
            ParameterCount: 0,
            ParameterTypes: [],
            ReturnType: null,
            UnderlyingType: null,
            NativeSelectors: [],
            SourceFile: "source/Firebase/Core/ApiDefinition.cs",
            Signature: "interface App");

    private static BindingSurfaceWaiver CreateWaiver(string surfaceId) =>
        new()
        {
            Target = "Core",
            SurfaceId = surfaceId,
            Kind = "native-owned-callback-type",
            Reason = "unit test",
            Evidence = "unit test",
        };
}
