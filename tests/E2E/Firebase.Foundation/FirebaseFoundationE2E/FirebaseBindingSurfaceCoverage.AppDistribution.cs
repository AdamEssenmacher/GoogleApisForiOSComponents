#if ENABLE_BINDING_SURFACE_COVERAGE
#if ENABLE_BINDING_SURFACE_COVERAGE_APPDISTRIBUTION
using FirebaseAppDistributionClient = Firebase.AppDistribution.AppDistribution;
using Foundation;
using UIKit;
#endif

namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    static async Task<BindingSurfaceCoverageTargetResult> VerifyAppDistributionBindingSurfaceAsync(BindingSurfaceCoverageDocument document)
    {
        var result = await ExecuteTargetAsync(document, "AppDistribution");
#if ENABLE_BINDING_SURFACE_COVERAGE_APPDISTRIBUTION
        await ExerciseAppDistributionBoundaryAsync();
#endif
        return result;
    }

#if ENABLE_BINDING_SURFACE_COVERAGE_APPDISTRIBUTION
    static async Task ExerciseAppDistributionBoundaryAsync()
    {
        var appDistribution = FirebaseAppDistributionClient.SharedInstance
            ?? throw new InvalidOperationException("Firebase.AppDistribution.AppDistribution.SharedInstance returned null.");

        _ = appDistribution.IsTesterSignedIn;

        using var url = new NSUrl("firebase-appdistribution://codex-binding-surface-e2e");
        using var options = new NSDictionary<NSString, NSObject>();
        _ = appDistribution.OpenUrl(UIApplication.SharedApplication, url, options);

        var updateCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        appDistribution.CheckForUpdate((release, error) =>
        {
            try
            {
                if (release is not null)
                {
                    _ = release.DisplayVersion;
                    _ = release.BuildVersion;
                    _ = release.ReleaseNotes;
                    _ = release.DownloadUrl;
                    _ = release.IsExpired;
                }

                updateCompletion.TrySetResult(error is null
                    ? "completed without Firebase error"
                    : $"completed with Firebase error {error.Domain} ({error.Code})");
            }
            catch (Exception ex)
            {
                updateCompletion.TrySetException(ex);
            }
        });

        var completedTask = await Task.WhenAny(updateCompletion.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        if (completedTask == updateCompletion.Task)
        {
            _ = await updateCompletion.Task;
        }
    }
#endif
}
#endif
