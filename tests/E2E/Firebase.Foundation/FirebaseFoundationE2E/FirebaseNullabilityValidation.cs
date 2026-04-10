#if ENABLE_NULLABILITY_VALIDATION
using Firebase.Analytics;
using Firebase.AppCheck;
using Firebase.CloudFirestore;
using Firebase.CloudMessaging;
using Firebase.Crashlytics;
using Firebase.Database;
using Firebase.PerformanceMonitoring;
using FirebaseCoreApp = Firebase.Core.App;
using FirebaseCoreOptions = Firebase.Core.Options;
using FirebaseMessagingClient = Firebase.CloudMessaging.Messaging;
using FirebasePerformanceHttpMethod = Firebase.PerformanceMonitoring.HttpMethod;
using Foundation;
using ObjCRuntime;

namespace FirebaseFoundationE2E;

static class FirebaseNullabilityValidation
{
    static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(5);

    public static Task<string> VerifyCoreNullabilityAsync()
    {
        var defaultApp = FirebaseCoreApp.DefaultInstance ?? throw new InvalidOperationException("Firebase.Core.App.DefaultInstance returned null during nullability validation.");
        var defaultOptions = FirebaseCoreOptions.DefaultInstance ?? throw new InvalidOperationException("Firebase.Core.Options.DefaultInstance returned null during nullability validation.");
        var bundleId = defaultOptions.BundleId;

        if (string.IsNullOrWhiteSpace(bundleId))
        {
            throw new InvalidOperationException("Firebase.Core.Options.BundleId unexpectedly returned null or empty.");
        }

        var currentBundleId = NSBundle.MainBundle.BundleIdentifier ?? string.Empty;
        if (!string.Equals(bundleId, currentBundleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Firebase.Core.Options.BundleId returned '{bundleId}', expected '{currentBundleId}'.");
        }

        return Task.FromResult($"Default app '{defaultApp.Name}' resolved successfully. BundleId: {bundleId}.");
    }

    public static Task<string> VerifyAnalyticsNullabilityAsync()
    {
        var appInstanceId = Analytics.AppInstanceId;
        var state = string.IsNullOrWhiteSpace(appInstanceId) ? "<null-or-empty>" : appInstanceId;
        return Task.FromResult($"Analytics.AppInstanceId completed without throwing. Value: {state}.");
    }

    public static Task<string> VerifyAppCheckNullabilityAsync()
    {
        var defaultApp = FirebaseCoreApp.DefaultInstance ?? throw new InvalidOperationException("Firebase.Core.App.DefaultInstance returned null before App Check validation.");
        var perAppInstance = AppCheck.Create(defaultApp);

        var handlerObservedNullPayload = false;
        TokenCompletionHandler tokenHandler = (token, error) =>
        {
            handlerObservedNullPayload = token is null && error is null;
        };
        tokenHandler(null, null);

        if (!handlerObservedNullPayload)
        {
            throw new InvalidOperationException("AppCheck TokenCompletionHandler did not accept null token/error values.");
        }

        var debugProvider = new AppCheckDebugProvider(defaultApp);
        var deviceCheckProvider = new DeviceCheckProvider(defaultApp);
        var appAttestProvider = new AppAttestProvider(defaultApp);

        return Task.FromResult(
            $"AppCheck.Create returned {(perAppInstance is null ? "<null>" : "instance")}. " +
            $"DebugProvider handle zero: {debugProvider.Handle == NativeHandle.Zero}. " +
            $"DeviceCheckProvider handle zero: {deviceCheckProvider.Handle == NativeHandle.Zero}. " +
            $"AppAttestProvider handle zero: {appAttestProvider.Handle == NativeHandle.Zero}. " +
            $"Token handler accepted null payloads.");
    }

    public static Task<string> VerifyCloudMessagingNullabilityAsync()
    {
        var messaging = FirebaseMessagingClient.SharedInstance ?? throw new InvalidOperationException("Firebase.CloudMessaging.Messaging.SharedInstance returned null.");
        var delegateProbe = new MessagingDelegateProbe();

        messaging.Delegate = delegateProbe;
        delegateProbe.DidReceiveRegistrationToken(messaging, null);

        if (!delegateProbe.Invoked || delegateProbe.ObservedToken is not null)
        {
            throw new InvalidOperationException("Firebase.CloudMessaging.MessagingDelegate did not accept a null registration token.");
        }

        messaging.Subscribe("codex-nullability-e2e", null);
        messaging.Unsubscribe("codex-nullability-e2e", null);

        return Task.FromResult("Cloud Messaging accepted a null registration token and null topic-operation completions without throwing.");
    }

    public static async Task<string> VerifyCloudFirestoreNullabilityAsync()
    {
        var firestore = Firestore.SharedInstance ?? throw new InvalidOperationException("Firebase.CloudFirestore.Firestore.SharedInstance returned null.");
        var collection = firestore.GetCollection("codex-nullability-e2e");
        var addedDocument = collection.AddDocument(
            new Dictionary<object, object>
            {
                ["marker"] = new NSString("nullability"),
            },
            null);

        if (addedDocument is null)
        {
            throw new InvalidOperationException("Firebase.CloudFirestore.CollectionReference.AddDocument returned null.");
        }

        var snapshotCompletion = new TaskCompletionSource<(DocumentSnapshot? Snapshot, NSError? Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        IListenerRegistration? registration = null;

        registration = addedDocument.AddSnapshotListener((snapshot, error) =>
        {
            registration?.Remove();
            snapshotCompletion.TrySetResult((snapshot, error));
        });

        var (snapshot, error) = await WaitForCompletionAsync(snapshotCompletion.Task, "Cloud Firestore document listener");
        if (error is not null)
        {
            return $"Cloud Firestore accepted a null AddDocument completion and reached native listener callback with Firebase error {FormatNSError(error)} for '{addedDocument.Path}'.";
        }

        if (snapshot is null)
        {
            throw new InvalidOperationException("Cloud Firestore document listener completed without either a snapshot or an error.");
        }

        var snapshotData = snapshot.Data;
        var dataState = snapshotData is null ? "null" : $"count={snapshotData.Count}";
        return $"Cloud Firestore accepted a null AddDocument completion and reached native listener callback for '{addedDocument.Path}'. Exists={snapshot.Exists}. Data={dataState}.";
    }

    public static async Task<string> VerifyCrashlyticsNullabilityAsync()
    {
        var crashlytics = Crashlytics.SharedInstance ?? throw new InvalidOperationException("Firebase.Crashlytics.Crashlytics.SharedInstance returned null.");

        crashlytics.SetCustomValue(null, "codex-nullability-value");
        crashlytics.SetUserId(null);
        crashlytics.Log("codex-nullability-crashlytics");

        var reportCompletion = new TaskCompletionSource<CrashlyticsReport?>(TaskCreationOptions.RunContinuationsAsynchronously);
        crashlytics.CheckAndUpdateUnsentReportsWithCompletion(report => reportCompletion.TrySetResult(report));

        var completedTask = await Task.WhenAny(reportCompletion.Task, Task.Delay(AsyncTimeout));
        if (completedTask != reportCompletion.Task)
        {
            return "Crashlytics accepted null custom value/user ID without throwing. Unsent-report callback did not complete within the validation timeout.";
        }

        var report = await reportCompletion.Task;
        if (report is not null)
        {
            report.SetCustomValue(null, "codex-nullability-report-value");
            report.SetUserID(null);
            return $"Crashlytics accepted null values on both the shared instance and CrashlyticsReport '{report.ReportID}'.";
        }

        return "Crashlytics accepted null custom value/user ID without throwing and reported a null unsent report.";
    }

    public static Task<string> VerifyDatabaseNullabilityAsync()
    {
        var projectId = FirebaseCoreOptions.DefaultInstance?.ProjectId ?? throw new InvalidOperationException("Firebase.Core.Options.ProjectId returned null before Database validation.");
        var database = Firebase.Database.Database.From($"https://{projectId}-default-rtdb.firebaseio.com");
        var root = database.GetRootReference();

        if (root.Key is not null)
        {
            throw new InvalidOperationException($"Firebase.Database.DatabaseReference.Key returned '{root.Key}' for the root reference.");
        }

        var child = root.GetChild("codex-nullability-e2e");
        child.RunTransaction(_ => TransactionResult.Abort(), null, false);

        return Task.FromResult($"Realtime Database root key was null as expected and RunTransaction accepted a null completion block on '{child.Url}'.");
    }

    public static Task<string> VerifyPerformanceNullabilityAsync()
    {
        var url = new NSUrl("https://example.com/codex-nullability");
        var metric = new HttpMetric(url, FirebasePerformanceHttpMethod.Get);
        if (metric is null)
        {
            throw new InvalidOperationException("Firebase.PerformanceMonitoring.HttpMetric returned null for a valid URL.");
        }

        metric.ResponseContentType = null;
        metric.Start();
        metric.Stop();

        return Task.FromResult("PerformanceMonitoring.HttpMetric accepted a valid constructor call and a null ResponseContentType without throwing.");
    }

    static async Task<T> WaitForCompletionAsync<T>(Task<T> task, string operation)
    {
        var completedTask = await Task.WhenAny(task, Task.Delay(AsyncTimeout));
        if (completedTask != task)
        {
            throw new TimeoutException($"{operation} did not complete within {AsyncTimeout.TotalSeconds} seconds.");
        }

        return await task;
    }

    static string FormatNSError(NSError error)
    {
        return $"{error.Domain} ({error.Code}): {error.LocalizedDescription}";
    }

    sealed class MessagingDelegateProbe : MessagingDelegate
    {
        public bool Invoked { get; private set; }
        public string? ObservedToken { get; private set; }

        public override void DidReceiveRegistrationToken(FirebaseMessagingClient messaging, string? fcmToken)
        {
            Invoked = true;
            ObservedToken = fcmToken;
        }
    }
}
#endif
