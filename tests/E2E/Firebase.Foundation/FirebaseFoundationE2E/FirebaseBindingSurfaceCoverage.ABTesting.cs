#if ENABLE_BINDING_SURFACE_COVERAGE
namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static Task<BindingSurfaceCoverageTargetResult> VerifyABTestingBindingSurfaceAsync(BindingSurfaceCoverageDocument document) =>
        ExecuteTargetAsync(document, "ABTesting");
}
#endif
