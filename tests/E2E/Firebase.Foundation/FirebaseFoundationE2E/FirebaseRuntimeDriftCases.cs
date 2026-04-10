using System.Reflection;

#if ENABLE_RUNTIME_DRIFT_CASE_ANALYTICS_SESSIONIDWITHCOMPLETION
using Firebase.Analytics;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_DATABASE_SERVERVALUE_INCREMENT
using Firebase.Database;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_UPDATEEXPERIMENTS
using Firebase.ABTesting;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_ACTIVATEEXPERIMENT
using Firebase.ABTesting;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_VALIDATERUNNINGEXPERIMENTS
using Firebase.ABTesting;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_GETQUERYNAMED
using Firebase.CloudFirestore;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFUNCTIONS_USEFUNCTIONSEMULATORORIGIN
using Firebase.CloudFunctions;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CRASHLYTICS_STACKFRAMEWITHADDRESS
using Firebase.Crashlytics;
using ObjCRuntime;
#endif

namespace FirebaseFoundationE2E;

static class FirebaseRuntimeDriftCases
{
    static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(5);

    public static string? GetConfiguredCaseId()
    {
        return GetAssemblyMetadataValue("RuntimeDriftCase");
    }

    public static async Task<string> ExecuteConfiguredCaseAsync()
    {
        var caseId = GetConfiguredCaseId();
        if (string.IsNullOrWhiteSpace(caseId))
        {
            throw new InvalidOperationException("Runtime drift mode was requested without a RuntimeDriftCase value.");
        }

        var methodName = GetAssemblyMetadataValue("RuntimeDriftCaseMethod");
        if (string.IsNullOrWhiteSpace(methodName))
        {
            throw new InvalidOperationException($"Runtime drift case '{caseId}' is missing RuntimeDriftCaseMethod metadata.");
        }

        var method = typeof(FirebaseRuntimeDriftCases).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        if (method is null)
        {
            throw new InvalidOperationException($"Runtime drift case '{caseId}' points at missing method '{methodName}'.");
        }

        if (method.Invoke(null, null) is not Task<string> task)
        {
            throw new InvalidOperationException($"Runtime drift case '{caseId}' method '{methodName}' did not return Task<string>.");
        }

        return await task;
    }

    static string? GetAssemblyMetadataValue(string key)
    {
        return typeof(FirebaseRuntimeDriftCases)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            ?.Value;
    }

#if ENABLE_RUNTIME_DRIFT_CASE_ANALYTICS_SESSIONIDWITHCOMPLETION
    static async Task<string> VerifyAnalyticsSessionIdWithCompletionAsync()
    {
        const string selector = "sessionIDWithCompletion:";

        var signature = typeof(Analytics).GetMethod(
            nameof(Analytics.SessionIdWithCompletion),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(Action<long, NSError>) },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(Analytics.SessionIdWithCompletion)}({typeof(Action<long, NSError>).FullName})' was not found.");
        }

        Analytics.SetAnalyticsCollectionEnabled(true);
        Analytics.SetConsent(new Dictionary<ConsentType, ConsentStatus>
        {
            [ConsentType.AnalyticsStorage] = ConsentStatus.Granted,
            [ConsentType.AdStorage] = ConsentStatus.Denied,
        });

        var callbackInvoked = false;
        long callbackSessionId = 0;
        NSError? callbackError = null;
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                Analytics.SessionIdWithCompletion((sessionId, error) =>
                {
                    callbackInvoked = true;
                    callbackSessionId = sessionId;
                    callbackError = error;
                    completionSource.TrySetResult(true);
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the missing binding is added, but observed {ex.GetType().FullName}. " +
                    $"Completion delegate type: {typeof(Action<long, NSError>).FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(AsyncTimeout));
            if (completedTask != completionSource.Task)
            {
                throw new TimeoutException(
                    $"Selector '{selector}' did not invoke its completion callback within {AsyncTimeout.TotalSeconds} seconds.");
            }

            if (!callbackInvoked)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed without throwing, but the completion callback was never marked as invoked.");
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return
                $"Selector '{selector}' invoked its completion callback without ObjC exception. " +
                $"Completion delegate type: {typeof(Action<long, NSError>).FullName}. " +
                $"SessionId: {callbackSessionId}. " +
                $"NSError: {callbackError?.LocalizedDescription ?? "<null>"}.";
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_DATABASE_SERVERVALUE_INCREMENT
    static Task<string> VerifyDatabaseServerValueIncrementAsync()
    {
        const string selector = "increment:";

        var signature = typeof(ServerValue).GetMethod(
            nameof(ServerValue.Increment),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(NSNumber) },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(ServerValue.Increment)}({typeof(NSNumber).FullName})' was not found.");
        }

        using var delta = NSNumber.FromInt64(1);
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            NSDictionary placeholder;
            try
            {
                placeholder = ServerValue.Increment(delta);
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw for a valid NSNumber delta, but observed {ex.GetType().FullName}. " +
                    $"Managed delta argument type: {delta.GetType().FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            if (placeholder.Count == 0)
            {
                throw new InvalidOperationException("ServerValue.Increment returned an empty placeholder dictionary.");
            }

            return Task.FromResult(
                $"Selector '{selector}' returned a non-empty placeholder dictionary. " +
                $"Managed delta argument type: {delta.GetType().FullName}. Placeholder count: {placeholder.Count}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_UPDATEEXPERIMENTS
    static async Task<string> VerifyABTestingUpdateExperimentsAsync()
    {
        const string selector = "updateExperimentsWithServiceOrigin:events:policy:lastStartTime:payloads:completionHandler:";

        var signature = typeof(ExperimentController).GetMethod(
            nameof(ExperimentController.UpdateExperiments),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[]
            {
                typeof(string),
                typeof(LifecycleEvents),
                typeof(ExperimentPayloadExperimentOverflowPolicy),
                typeof(double),
                typeof(NSData[]),
                typeof(Action<NSError>)
            },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(ExperimentController.UpdateExperiments)}(string, {typeof(LifecycleEvents).FullName}, {typeof(ExperimentPayloadExperimentOverflowPolicy).FullName}, double, {typeof(NSData[]).FullName}, {typeof(Action<NSError>).FullName})' was not found.");
        }

        var parameters = signature.GetParameters();
        if (parameters.Length != 6 || parameters[2].ParameterType != typeof(ExperimentPayloadExperimentOverflowPolicy))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected policy parameter type '{typeof(ExperimentPayloadExperimentOverflowPolicy).FullName}', observed '{parameters.ElementAtOrDefault(2)?.ParameterType.FullName ?? "<missing>"}'.");
        }

        var controller = ExperimentController.SharedInstance;
        if (controller is null)
        {
            throw new InvalidOperationException("Firebase.ABTesting.ExperimentController.SharedInstance returned null after App.Configure().");
        }

        var events = new LifecycleEvents
        {
            SetExperimentEventName = new NSString("codex_set_experiment"),
            ActivateExperimentEventName = new NSString("codex_activate_experiment"),
            ClearExperimentEventName = new NSString("codex_clear_experiment"),
            TimeoutExperimentEventName = new NSString("codex_timeout_experiment"),
            ExpireExperimentEventName = new NSString("codex_expire_experiment"),
        };
        var policy = ExperimentPayloadExperimentOverflowPolicy.DiscardOldest;
        var payloads = Array.Empty<NSData>();
        var completionInvoked = false;
        NSError? completionError = null;
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                controller.UpdateExperiments("codex", events, policy, -1, payloads, error =>
                {
                    completionInvoked = true;
                    completionError = error;
                    completionSource.TrySetResult(true);
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw with the corrected enum binding, but observed {ex.GetType().FullName}. " +
                    $"Managed policy argument type: {policy.GetType().FullName}. Policy value: {(int)policy}. " +
                    $"Payload array type: {payloads.GetType().FullName}. Payload count: {payloads.Length}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(AsyncTimeout));
            if (completedTask != completionSource.Task)
            {
                throw new TimeoutException(
                    $"Selector '{selector}' did not invoke its completion callback within {AsyncTimeout.TotalSeconds} seconds.");
            }

            if (!completionInvoked)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed without throwing, but the completion callback was never marked as invoked.");
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return
                $"Selector '{selector}' completed without ObjC exception. " +
                $"Managed policy argument type: {parameters[2].ParameterType.FullName}. Policy value: {(int)policy}. " +
                $"Payload array type: {payloads.GetType().FullName}. Payload count: {payloads.Length}. " +
                $"CompletionInvoked: {completionInvoked}. CompletionError: {FormatDetail(completionError?.LocalizedDescription)}.";
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_ACTIVATEEXPERIMENT
    static Task<string> VerifyABTestingActivateExperimentAsync()
    {
        const string selector = "activateExperiment:forServiceOrigin:";

        var controller = ExperimentController.SharedInstance;
        if (controller is null)
        {
            throw new InvalidOperationException("Firebase.ABTesting.ExperimentController.SharedInstance returned null after App.Configure().");
        }

        var payload = new ExperimentPayload();
        var origin = "codex";
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                controller.ActivateExperiment(payload, origin);
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Managed payload type: {payload.GetType().FullName}. " +
                    $"Origin argument type: {origin.GetType().FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return Task.FromResult(
                $"Selector '{selector}' completed without ObjC exception after the binding fix. " +
                $"Managed payload type: {payload.GetType().FullName}. " +
                $"Origin argument type: {origin.GetType().FullName}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ABTESTING_VALIDATERUNNINGEXPERIMENTS
    static Task<string> VerifyABTestingValidateRunningExperimentsAsync()
    {
        const string selector = "validateRunningExperimentsForServiceOrigin:runningExperimentPayloads:";

        var controller = ExperimentController.SharedInstance;
        if (controller is null)
        {
            throw new InvalidOperationException("Firebase.ABTesting.ExperimentController.SharedInstance returned null after App.Configure().");
        }

        var signature = typeof(ExperimentController).GetMethod(
            nameof(ExperimentController.ValidateRunningExperiments),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string), typeof(ExperimentPayload[]) },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(ExperimentController.ValidateRunningExperiments)}(string, {typeof(ExperimentPayload[]).FullName})' was not found.");
        }

        var parameters = signature.GetParameters();
        if (parameters.Length != 2 || parameters[1].ParameterType != typeof(ExperimentPayload[]))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected payload parameter type '{typeof(ExperimentPayload[]).FullName}', observed '{parameters.ElementAtOrDefault(1)?.ParameterType.FullName ?? "<missing>"}'.");
        }

        var payloads = Array.Empty<ExperimentPayload>();
        var origin = "codex";
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                controller.ValidateRunningExperiments(origin, payloads);
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Managed payload array type: {payloads.GetType().FullName}. Payload count: {payloads.Length}. " +
                    $"Origin argument type: {origin.GetType().FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return Task.FromResult(
                $"Selector '{selector}' completed without ObjC exception after the binding fix. " +
                $"Managed signature payload type: {parameters[1].ParameterType.FullName}. " +
                $"Managed payload array type: {payloads.GetType().FullName}. Payload count: {payloads.Length}. " +
                $"Origin argument type: {origin.GetType().FullName}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_GETQUERYNAMED
    static async Task<string> VerifyCloudFirestoreGetQueryNamedAsync()
    {
        const string selector = "getQueryNamed:completion:";

        var firestore = Firestore.SharedInstance;
        if (firestore is null)
        {
            throw new InvalidOperationException("Firebase.CloudFirestore.Firestore.SharedInstance returned null after App.Configure().");
        }

        var queryName = "codex-firestore-missing-query";
        var callbackInvoked = false;
        var returnedQueryWasNull = false;
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var completionSource = new TaskCompletionSource<Query?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                firestore.GetQueryNamed(queryName, query =>
                {
                    callbackInvoked = true;
                    returnedQueryWasNull = query is null;
                    completionSource.TrySetResult(query);
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Runtime argument type: {queryName.GetType().FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(AsyncTimeout));
            if (completedTask != completionSource.Task)
            {
                throw new TimeoutException(
                    $"Selector '{selector}' did not invoke its completion callback within {AsyncTimeout.TotalSeconds} seconds after the binding fix.");
            }

            var returnedQuery = await completionSource.Task;
            if (!callbackInvoked)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed without throwing, but the completion callback was never marked as invoked.");
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return
                $"Selector '{selector}' completed without ObjC exception after the binding fix. " +
                $"Runtime argument type: {queryName.GetType().FullName}. " +
                $"CallbackInvoked: {callbackInvoked}. " +
                $"ReturnedQueryWasNull: {returnedQueryWasNull}. " +
                $"ReturnedQueryType: {returnedQuery?.GetType().FullName ?? "<null>"}.";
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFUNCTIONS_USEFUNCTIONSEMULATORORIGIN
    static Task<string> VerifyCloudFunctionsUseFunctionsEmulatorOriginAsync()
    {
        const string staleSelector = "useFunctionsEmulatorOrigin:";
        const string liveSelector = "useEmulatorWithHost:port:";

        var functions = CloudFunctions.DefaultInstance;
        if (functions is null)
        {
            throw new InvalidOperationException("Firebase.CloudFunctions.CloudFunctions.DefaultInstance returned null after App.Configure().");
        }

        var staleMethod = typeof(CloudFunctions).GetMethod(
            "UseFunctionsEmulatorOrigin",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (staleMethod is not null)
        {
            throw new InvalidOperationException(
                $"CloudFunctions still exposes stale managed API '{staleMethod.Name}', so callers can still reach selector '{staleSelector}'.");
        }

        var staleProperty = typeof(CloudFunctions).GetProperty("EmulatorOrigin", BindingFlags.Instance | BindingFlags.Public);
        if (staleProperty is not null)
        {
            throw new InvalidOperationException(
                $"CloudFunctions still exposes stale managed property '{staleProperty.Name}', which no longer maps to a real ObjC surface.");
        }

        if (functions.RespondsToSelector(new Selector(staleSelector)))
        {
            throw new InvalidOperationException(
                $"Native FIRFunctions still responds to stale selector '{staleSelector}', so the runtime drift would still be reachable.");
        }

        if (!functions.RespondsToSelector(new Selector(liveSelector)))
        {
            throw new InvalidOperationException(
                $"Native FIRFunctions does not respond to expected live selector '{liveSelector}'.");
        }

        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                functions.UseEmulatorOriginWithHost("127.0.0.1", 5002);
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{liveSelector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Runtime host argument type: {typeof(string).FullName}. Runtime port argument type: {typeof(uint).FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Cloud Functions emulator API completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return Task.FromResult(
                $"Removed stale managed selector '{staleSelector}' and property 'EmulatorOrigin'. " +
                $"Live selector '{liveSelector}' completed without ObjC exception after the binding fix. " +
                $"Runtime host argument type: {typeof(string).FullName}. Runtime port argument type: {typeof(uint).FullName}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CRASHLYTICS_STACKFRAMEWITHADDRESS
    static Task<string> VerifyCrashlyticsStackFrameWithAddressAsync()
    {
        const string staleSelector = "stackFrameWithAddress:address";
        const string liveSelector = "stackFrameWithAddress:";

        var signature = typeof(StackFrame).GetMethod(
            nameof(StackFrame.Create),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(nuint) },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(StackFrame.Create)}({typeof(nuint).FullName})' was not found.");
        }

        var marshaledExceptionCaptured = false;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledExceptionCaptured = true;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            try
            {
                using var stackFrame = StackFrame.Create((nuint)1);
                if (stackFrame is null)
                {
                    throw new InvalidOperationException(
                        $"Selector '{liveSelector}' returned null after the binding fix. Runtime address argument type: {typeof(nuint).FullName}.");
                }
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{liveSelector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Stale selector was '{staleSelector}'. " +
                    $"Runtime address argument type: {typeof(nuint).FullName}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            if (marshaledExceptionCaptured)
            {
                throw new InvalidOperationException(
                    $"Selector '{liveSelector}' completed, but Runtime.MarshalObjectiveCException captured an unexpected Objective-C exception. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return Task.FromResult(
                $"Corrected stale selector '{staleSelector}' to native selector '{liveSelector}'. " +
                $"Runtime address argument type: {typeof(nuint).FullName}; StackFrame.Create returned without ObjC exception.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

    static string FormatDetail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }
}
