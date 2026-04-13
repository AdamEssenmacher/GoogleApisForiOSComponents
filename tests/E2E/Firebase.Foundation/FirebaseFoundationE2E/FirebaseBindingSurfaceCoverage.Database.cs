#if ENABLE_BINDING_SURFACE_COVERAGE
namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static Task<BindingSurfaceCoverageTargetResult> VerifyDatabaseBindingSurfaceAsync(BindingSurfaceCoverageDocument document) =>
        ExecuteTargetAsync(document, "Database");
}
#endif
