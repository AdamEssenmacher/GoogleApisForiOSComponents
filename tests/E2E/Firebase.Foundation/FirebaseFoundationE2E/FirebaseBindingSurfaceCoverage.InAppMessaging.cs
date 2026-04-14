#if ENABLE_BINDING_SURFACE_COVERAGE
#if ENABLE_BINDING_SURFACE_COVERAGE_INAPPMESSAGING
using FirebaseInAppMessagingClient = Firebase.InAppMessaging.InAppMessaging;
#endif

namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static async Task<BindingSurfaceCoverageTargetResult> VerifyInAppMessagingBindingSurfaceAsync(BindingSurfaceCoverageDocument document)
    {
        var result = await ExecuteTargetAsync(document, "InAppMessaging");
#if ENABLE_BINDING_SURFACE_COVERAGE_INAPPMESSAGING
        ExerciseInAppMessagingBoundary();
#endif
        return result;
    }

#if ENABLE_BINDING_SURFACE_COVERAGE_INAPPMESSAGING
    static void ExerciseInAppMessagingBoundary()
    {
        var inAppMessaging = FirebaseInAppMessagingClient.SharedInstance
            ?? throw new InvalidOperationException("Firebase.InAppMessaging.InAppMessaging.SharedInstance returned null.");

        var isSuppressed = inAppMessaging.MessageDisplaySuppressed;
        inAppMessaging.MessageDisplaySuppressed = isSuppressed;

        var automaticCollection = inAppMessaging.AutomaticDataCollectionEnabled;
        inAppMessaging.AutomaticDataCollectionEnabled = automaticCollection;

        inAppMessaging.TriggerEvent("codex-binding-surface-e2e");
    }
#endif
}
#endif
