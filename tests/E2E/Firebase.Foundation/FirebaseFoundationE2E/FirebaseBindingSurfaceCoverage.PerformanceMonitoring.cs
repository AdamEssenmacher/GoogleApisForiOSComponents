#if ENABLE_BINDING_SURFACE_COVERAGE
namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static Task<BindingSurfaceCoverageTargetResult> VerifyPerformanceMonitoringBindingSurfaceAsync(BindingSurfaceCoverageDocument document) =>
        ExecuteTargetAsync(document, "PerformanceMonitoring");
}
#endif
