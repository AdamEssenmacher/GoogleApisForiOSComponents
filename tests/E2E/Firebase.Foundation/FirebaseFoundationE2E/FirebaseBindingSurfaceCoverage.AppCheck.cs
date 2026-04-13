#if ENABLE_BINDING_SURFACE_COVERAGE
namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static Task<BindingSurfaceCoverageTargetResult> VerifyAppCheckBindingSurfaceAsync(BindingSurfaceCoverageDocument document) =>
        ExecuteTargetAsync(document, "AppCheck");
}
#endif
