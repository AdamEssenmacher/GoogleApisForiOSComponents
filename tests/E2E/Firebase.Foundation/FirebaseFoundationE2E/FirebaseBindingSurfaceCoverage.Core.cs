#if ENABLE_BINDING_SURFACE_COVERAGE
namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static Task<BindingSurfaceCoverageTargetResult> VerifyCoreBindingSurfaceAsync(BindingSurfaceCoverageDocument document) =>
        ExecuteTargetAsync(document, "Core");
}
#endif
