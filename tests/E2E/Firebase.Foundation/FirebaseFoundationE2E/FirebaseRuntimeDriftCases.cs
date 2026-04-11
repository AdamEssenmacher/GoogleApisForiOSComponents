using System.Reflection;

#if ENABLE_RUNTIME_DRIFT_CASE_ANALYTICS_SESSIONIDWITHCOMPLETION
using Firebase.Analytics;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_ANALYTICS_ONDEVICECONVERSION
using Firebase.Analytics;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_REMOTECONFIG_REALTIME_CUSTOMSIGNALS
using Firebase.RemoteConfig;
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

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_FIELDVALUE_VECTORWITHARRAY
using Firebase.CloudFirestore;
using Foundation;
using ObjCRuntime;
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_AGGREGATE_QUERY
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

#if ENABLE_RUNTIME_DRIFT_CASE_ANALYTICS_ONDEVICECONVERSION
    static Task<string> VerifyAnalyticsOnDeviceConversionAsync()
    {
        var expectedSignatures = new (string MethodName, Type[] ParameterTypes, string Selector)[]
        {
            (
                nameof(Analytics.InitiateOnDeviceConversionMeasurementWithEmailAddress),
                new[] { typeof(string) },
                "initiateOnDeviceConversionMeasurementWithEmailAddress:"),
            (
                nameof(Analytics.InitiateOnDeviceConversionMeasurementWithPhoneNumber),
                new[] { typeof(string) },
                "initiateOnDeviceConversionMeasurementWithPhoneNumber:"),
            (
                nameof(Analytics.InitiateOnDeviceConversionMeasurementWithHashedEmailAddress),
                new[] { typeof(NSData) },
                "initiateOnDeviceConversionMeasurementWithHashedEmailAddress:"),
            (
                nameof(Analytics.InitiateOnDeviceConversionMeasurementWithHashedPhoneNumber),
                new[] { typeof(NSData) },
                "initiateOnDeviceConversionMeasurementWithHashedPhoneNumber:"),
        };

        foreach (var (methodName, parameterTypes, selector) in expectedSignatures)
        {
            var signature = typeof(Analytics).GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            if (signature is null)
            {
                throw new InvalidOperationException(
                    $"Expected managed API '{methodName}({string.Join(", ", parameterTypes.Select(type => type.FullName))})' was not found for selector '{selector}'.");
            }
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
            using var hashedEmailAddress = NSData.FromArray(new byte[]
            {
                0x9a, 0x79, 0x2f, 0x3d, 0xa4, 0x18, 0x7e, 0x1b,
                0x02, 0xb7, 0x83, 0xfd, 0x0b, 0x41, 0x77, 0x57,
                0x97, 0x7a, 0x2a, 0xaf, 0x62, 0x94, 0x6d, 0x12,
                0x9d, 0x4c, 0xd2, 0x5a, 0x73, 0xb1, 0x3f, 0x31,
            });
            using var hashedPhoneNumber = NSData.FromArray(new byte[]
            {
                0x2d, 0x71, 0x16, 0x42, 0xb7, 0x26, 0xb0, 0x44,
                0x01, 0x62, 0x7c, 0xa9, 0xfb, 0xac, 0x32, 0xf5,
                0xc8, 0x53, 0x0f, 0x8d, 0x89, 0xc4, 0x6c, 0x2e,
                0x42, 0xb8, 0x6e, 0xfd, 0xb0, 0x33, 0x84, 0xa8,
            });

            try
            {
                Analytics.InitiateOnDeviceConversionMeasurementWithEmailAddress("codex@example.com");
                Analytics.InitiateOnDeviceConversionMeasurementWithPhoneNumber("+15555550100");
                Analytics.InitiateOnDeviceConversionMeasurementWithHashedEmailAddress(hashedEmailAddress);
                Analytics.InitiateOnDeviceConversionMeasurementWithHashedPhoneNumber(hashedPhoneNumber);
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    "Analytics on-device conversion selectors should not throw after the missing bindings are added, " +
                    $"but observed {ex.GetType().FullName}. " +
                    $"String argument type: {typeof(string).FullName}. Hashed argument type: {typeof(NSData).FullName}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledExceptionCaptured)
            {
                throw new InvalidOperationException(
                    "Analytics on-device conversion selectors completed, but Runtime.MarshalObjectiveCException captured an unexpected Objective-C exception. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            return Task.FromResult(
                "Analytics on-device conversion selectors completed without ObjC exception. " +
                $"String argument type: {typeof(string).FullName}. Hashed argument type: {typeof(NSData).FullName}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_REMOTECONFIG_REALTIME_CUSTOMSIGNALS
    static async Task<string> VerifyRemoteConfigRealtimeCustomSignalsAsync()
    {
        const string listenerSelector = "addOnConfigUpdateListener:";
        const string customSignalsSelector = "setCustomSignals:withCompletion:";

        var listenerSignature = typeof(RemoteConfig).GetMethod(
            nameof(RemoteConfig.AddOnConfigUpdateListener),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(RemoteConfigUpdateCompletionHandler) },
            modifiers: null);
        if (listenerSignature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(RemoteConfig.AddOnConfigUpdateListener)}({typeof(RemoteConfigUpdateCompletionHandler).FullName})' was not found.");
        }

        if (listenerSignature.ReturnType != typeof(ConfigUpdateListenerRegistration))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(RemoteConfig.AddOnConfigUpdateListener)}' to return '{typeof(ConfigUpdateListenerRegistration).FullName}', observed '{listenerSignature.ReturnType.FullName}'.");
        }

        var customSignalsSignature = typeof(RemoteConfig).GetMethod(
            nameof(RemoteConfig.SetCustomSignals),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(NSDictionary<NSString, NSObject>), typeof(Action<NSError>) },
            modifiers: null);
        if (customSignalsSignature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(RemoteConfig.SetCustomSignals)}({typeof(NSDictionary<NSString, NSObject>).FullName}, {typeof(Action<NSError>).FullName})' was not found.");
        }

        var remoteConfig = RemoteConfig.SharedInstance;
        if (remoteConfig is null)
        {
            throw new InvalidOperationException("Firebase.RemoteConfig.RemoteConfig.SharedInstance returned null after App.Configure().");
        }

        if (!remoteConfig.RespondsToSelector(new Selector(listenerSelector)))
        {
            throw new InvalidOperationException($"Native FIRRemoteConfig does not respond to expected selector '{listenerSelector}'.");
        }

        if (!remoteConfig.RespondsToSelector(new Selector(customSignalsSelector)))
        {
            throw new InvalidOperationException($"Native FIRRemoteConfig does not respond to expected selector '{customSignalsSelector}'.");
        }

        var listenerInvoked = false;
        var listenerUpdateWasNull = false;
        NSError? listenerError = null;
        var customSignalsCompletionInvoked = false;
        NSError? customSignalsCompletionError = null;
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var customSignalsCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            using var signalKey = new NSString("codex_signal");
            using var signalValue = new NSString("enabled");
            using var customSignals = NSDictionary<NSString, NSObject>.FromObjectsAndKeys(
                new NSObject[] { signalValue },
                new[] { signalKey },
                1);

            ConfigUpdateListenerRegistration registration;
            try
            {
                registration = remoteConfig.AddOnConfigUpdateListener((configUpdate, error) =>
                {
                    listenerInvoked = true;
                    listenerUpdateWasNull = configUpdate is null;
                    listenerError = error;
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{listenerSelector}' should not throw after the missing binding is added, but observed {ex.GetType().FullName}. " +
                    $"Listener delegate type: {typeof(RemoteConfigUpdateCompletionHandler).FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            using (registration)
            {
                if (registration is null)
                {
                    throw new InvalidOperationException($"Selector '{listenerSelector}' returned null registration.");
                }

                registration.Remove();
            }

            try
            {
                remoteConfig.SetCustomSignals(customSignals, error =>
                {
                    customSignalsCompletionInvoked = true;
                    customSignalsCompletionError = error;
                    customSignalsCompletionSource.TrySetResult(true);
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{customSignalsSelector}' should not throw after the missing binding is added, but observed {ex.GetType().FullName}. " +
                    $"Signals dictionary type: {customSignals.GetType().FullName}. Completion delegate type: {typeof(Action<NSError>).FullName}. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            var completedTask = await Task.WhenAny(customSignalsCompletionSource.Task, Task.Delay(AsyncTimeout));
            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"RemoteConfig missing-surface selectors completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            if (completedTask != customSignalsCompletionSource.Task)
            {
                throw new TimeoutException(
                    $"Selector '{customSignalsSelector}' did not invoke its completion callback within {AsyncTimeout.TotalSeconds} seconds. " +
                    $"Signals dictionary type: {customSignals.GetType().FullName}. Completion delegate type: {typeof(Action<NSError>).FullName}.");
            }

            return
                $"Selectors '{listenerSelector}' and '{customSignalsSelector}' completed without ObjC exception after the missing bindings were added. " +
                $"Listener delegate type: {typeof(RemoteConfigUpdateCompletionHandler).FullName}. " +
                $"Registration type: {typeof(ConfigUpdateListenerRegistration).FullName}. " +
                $"Update type: {typeof(RemoteConfigUpdate).FullName}. " +
                $"Signals dictionary type: {customSignals.GetType().FullName}. " +
                $"Custom signals callback observed: {completedTask == customSignalsCompletionSource.Task}. " +
                $"Custom signals callback invoked: {customSignalsCompletionInvoked}. " +
                $"Custom signals NSError: {FormatDetail(customSignalsCompletionError?.LocalizedDescription)}. " +
                $"Listener invoked: {listenerInvoked}. Listener update was null: {listenerUpdateWasNull}. " +
                $"Listener NSError: {FormatDetail(listenerError?.LocalizedDescription)}.";
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

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_FIELDVALUE_VECTORWITHARRAY
    static Task<string> VerifyCloudFirestoreFieldValueVectorWithArrayAsync()
    {
        const string selector = "vectorWithArray:";

        var signature = typeof(FieldValue).GetMethod(
            nameof(FieldValue.VectorWithArray),
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(NSNumber[]) },
            modifiers: null);
        if (signature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(FieldValue.VectorWithArray)}({typeof(NSNumber[]).FullName})' was not found.");
        }

        if (signature.ReturnType != typeof(VectorValue))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(FieldValue.VectorWithArray)}' to return '{typeof(VectorValue).FullName}', observed '{signature.ReturnType.FullName}'.");
        }

        var vectorConstructor = typeof(VectorValue).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(NSNumber[]) },
            modifiers: null);
        if (vectorConstructor is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(VectorValue)}({typeof(NSNumber[]).FullName})' was not found.");
        }

        using var first = NSNumber.FromInt64(1);
        using var second = NSNumber.FromInt64(2);
        var values = new[] { first, second };
        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var vectorArrayLength = 0;

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
                using var fieldValue = FieldValue.VectorWithArray(values);
                if (fieldValue is null)
                {
                    throw new InvalidOperationException(
                        $"Selector '{selector}' returned null for a valid NSNumber array.");
                }

                using var vectorValue = new VectorValue(values);
                var vectorArray = vectorValue.Array;
                vectorArrayLength = vectorArray.Length;
                if (vectorArrayLength != values.Length)
                {
                    throw new InvalidOperationException(
                        $"VectorValue.Array returned {vectorArrayLength} values, expected {values.Length}.");
                }
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the missing binding is added, but observed {ex.GetType().FullName}. " +
                    $"Managed vector argument type: {values.GetType().FullName}. " +
                    $"Vector value type: {typeof(VectorValue).FullName}. " +
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
                $"Selector '{selector}' returned a VectorValue without ObjC exception after the missing binding was added. " +
                $"Managed vector argument type: {values.GetType().FullName}. " +
                $"Return type: {signature.ReturnType.FullName}. Vector array length: {vectorArrayLength}.");
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }
#endif

#if ENABLE_RUNTIME_DRIFT_CASE_CLOUDFIRESTORE_AGGREGATE_QUERY
    static async Task<string> VerifyCloudFirestoreAggregateQueryAsync()
    {
        const string queryCountSelector = "count";
        const string aggregateSelector = "aggregate:";
        const string aggregateQueryQuerySelector = "query";
        const string getAggregationSelector = "aggregationWithSource:completion:";

        var queryCountProperty = typeof(Query).GetProperty(
            nameof(Query.Count),
            BindingFlags.Instance | BindingFlags.Public);
        if (queryCountProperty is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(Query.Count)}' was not found on '{typeof(Query).FullName}'.");
        }

        if (queryCountProperty.PropertyType != typeof(AggregateQuery))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(Query.Count)}' to return '{typeof(AggregateQuery).FullName}', observed '{queryCountProperty.PropertyType.FullName}'.");
        }

        var aggregateSignature = typeof(Query).GetMethod(
            nameof(Query.Aggregate),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(AggregateField[]) },
            modifiers: null);
        if (aggregateSignature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(Query.Aggregate)}({typeof(AggregateField[]).FullName})' was not found.");
        }

        if (aggregateSignature.ReturnType != typeof(AggregateQuery))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(Query.Aggregate)}' to return '{typeof(AggregateQuery).FullName}', observed '{aggregateSignature.ReturnType.FullName}'.");
        }

        var getAggregationSignature = typeof(AggregateQuery).GetMethod(
            nameof(AggregateQuery.GetAggregation),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(AggregateSource), typeof(AggregateQuerySnapshotHandler) },
            modifiers: null);
        if (getAggregationSignature is null)
        {
            throw new InvalidOperationException(
                $"Expected managed API '{nameof(AggregateQuery.GetAggregation)}({typeof(AggregateSource).FullName}, {typeof(AggregateQuerySnapshotHandler).FullName})' was not found.");
        }

        var snapshotCountProperty = typeof(AggregateQuerySnapshot).GetProperty(
            nameof(AggregateQuerySnapshot.Count),
            BindingFlags.Instance | BindingFlags.Public);
        if (snapshotCountProperty?.PropertyType != typeof(NSNumber))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(AggregateQuerySnapshot.Count)}' to return '{typeof(NSNumber).FullName}', observed '{snapshotCountProperty?.PropertyType.FullName ?? "<missing>"}'.");
        }

        var snapshotValueSignature = typeof(AggregateQuerySnapshot).GetMethod(
            nameof(AggregateQuerySnapshot.GetValue),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: new[] { typeof(AggregateField) },
            modifiers: null);
        if (snapshotValueSignature?.ReturnType != typeof(NSObject))
        {
            throw new InvalidOperationException(
                $"Managed signature regression: expected '{nameof(AggregateQuerySnapshot.GetValue)}' to return '{typeof(NSObject).FullName}', observed '{snapshotValueSignature?.ReturnType.FullName ?? "<missing>"}'.");
        }

        var firestore = Firestore.SharedInstance;
        if (firestore is null)
        {
            throw new InvalidOperationException("Firebase.CloudFirestore.Firestore.SharedInstance returned null after App.Configure().");
        }

        var query = firestore.GetCollection("codex-aggregate-e2e");
        if (query is null)
        {
            throw new InvalidOperationException("Firebase.CloudFirestore.Firestore.GetCollection returned null.");
        }

        if (!query.RespondsToSelector(new Selector(queryCountSelector)))
        {
            throw new InvalidOperationException($"Native FIRQuery does not respond to expected selector '{queryCountSelector}'.");
        }

        if (!query.RespondsToSelector(new Selector(aggregateSelector)))
        {
            throw new InvalidOperationException($"Native FIRQuery does not respond to expected selector '{aggregateSelector}'.");
        }

        NSException? marshaledException = null;
        MarshalObjectiveCExceptionMode? marshaledExceptionMode = null;
        var seedWriteCompletionSource = new TaskCompletionSource<NSError?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverCountCompletionSource = new TaskCompletionSource<(AggregateQuerySnapshot? Snapshot, NSError? Error)>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            marshaledException ??= args.Exception;
            marshaledExceptionMode ??= args.ExceptionMode;
        }

        Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        try
        {
            AggregateField countField;
            AggregateField sumByNameField;
            AggregateField sumByPathField;
            AggregateField averageByNameField;
            AggregateField averageByPathField;
            AggregateQuery countQuery;
            AggregateQuery aggregateQuery;
            Query countUnderlyingQuery;
            Query aggregateUnderlyingQuery;
            var seedWriteCompletionInvoked = false;
            NSError? seedWriteError = null;
            var serverCountCompletionInvoked = false;
            AggregateQuerySnapshot? serverCountSnapshot = null;
            NSError? serverCountError = null;

            try
            {
                var seedDocument = query.GetDocument("aggregate-count-seed");
                using var scoreKey = new NSString("score");
                using var markerKey = new NSString("marker");
                using var scoreValue = NSNumber.FromInt32(1);
                using var markerValue = new NSString("aggregate-count");
                using var seedData = NSDictionary<NSString, NSObject>.FromObjectsAndKeys(
                    new NSObject[] { scoreValue, markerValue },
                    new[] { scoreKey, markerKey },
                    2);
                seedDocument.SetData(seedData, error =>
                {
                    seedWriteCompletionInvoked = true;
                    seedWriteError = error;
                    seedWriteCompletionSource.TrySetResult(error);
                });

                var completedSeedWriteTask = await Task.WhenAny(seedWriteCompletionSource.Task, Task.Delay(AsyncTimeout));
                if (completedSeedWriteTask != seedWriteCompletionSource.Task)
                {
                    throw new TimeoutException(
                        "Cloud Firestore seed document write did not invoke its completion callback " +
                        $"within {AsyncTimeout.TotalSeconds} seconds.");
                }

                if (!seedWriteCompletionInvoked)
                {
                    throw new InvalidOperationException(
                        "Cloud Firestore seed document write completed without throwing, but the completion callback was never marked as invoked.");
                }

                var completedSeedWriteError = await seedWriteCompletionSource.Task;
                if (!ReferenceEquals(seedWriteError, completedSeedWriteError))
                {
                    throw new InvalidOperationException("Cloud Firestore seed document write callback state did not match the completed task payload.");
                }

                if (completedSeedWriteError is not null)
                {
                    throw new InvalidOperationException(
                        $"Cloud Firestore seed document write reached native completion with Firebase error {FormatNSError(completedSeedWriteError)}.");
                }

                using var fieldPath = new FieldPath(new[] { "score" });
                countField = AggregateField.Count;
                sumByNameField = AggregateField.Sum("score");
                sumByPathField = AggregateField.Sum(fieldPath);
                averageByNameField = AggregateField.Average("score");
                averageByPathField = AggregateField.Average(fieldPath);

                countQuery = query.Count;
                aggregateQuery = query.Aggregate(new[]
                {
                    countField,
                    sumByNameField,
                    sumByPathField,
                    averageByNameField,
                    averageByPathField,
                });
                countUnderlyingQuery = countQuery.Query;
                aggregateUnderlyingQuery = aggregateQuery.Query;

                countQuery.GetAggregation(AggregateSource.Server, (snapshot, error) =>
                {
                    serverCountCompletionInvoked = true;
                    serverCountSnapshot = snapshot;
                    serverCountError = error;
                    serverCountCompletionSource.TrySetResult((snapshot, error));
                });
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Firestore aggregate selectors should not throw after the missing bindings are added, but observed {ex.GetType().FullName}. " +
                    $"Selectors exercised: setData:completion:, '{queryCountSelector}', '{aggregateSelector}', '{aggregateQueryQuerySelector}', '{getAggregationSelector}', aggregateFieldForCount, aggregateFieldForSumOfField:, aggregateFieldForSumOfFieldPath:, aggregateFieldForAverageOfField:, aggregateFieldForAverageOfFieldPath:. " +
                    $"NSException.Name: {FormatDetail(marshaledException?.Name?.ToString())}. " +
                    $"NSException.Reason: {FormatDetail(marshaledException?.Reason)}. " +
                    $"Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.",
                    ex);
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Firestore aggregate selectors completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            if (countQuery is null)
            {
                throw new InvalidOperationException($"Selector '{queryCountSelector}' returned null.");
            }

            if (aggregateQuery is null)
            {
                throw new InvalidOperationException($"Selector '{aggregateSelector}' returned null.");
            }

            if (countUnderlyingQuery is null)
            {
                throw new InvalidOperationException($"Selector '{aggregateQueryQuerySelector}' returned null for the count aggregate query.");
            }

            if (aggregateUnderlyingQuery is null)
            {
                throw new InvalidOperationException($"Selector '{aggregateQueryQuerySelector}' returned null for the multi-field aggregate query.");
            }

            if (!countQuery.RespondsToSelector(new Selector(getAggregationSelector)))
            {
                throw new InvalidOperationException($"Native FIRAggregateQuery does not respond to expected selector '{getAggregationSelector}'.");
            }

            var completedTask = await Task.WhenAny(serverCountCompletionSource.Task, Task.Delay(AsyncTimeout));
            if (completedTask != serverCountCompletionSource.Task)
            {
                throw new TimeoutException(
                    $"Selector '{getAggregationSelector}' did not invoke its completion callback within {AsyncTimeout.TotalSeconds} seconds.");
            }

            if (!serverCountCompletionInvoked)
            {
                throw new InvalidOperationException(
                    $"Selector '{getAggregationSelector}' completed without throwing, but the completion callback was never marked as invoked.");
            }

            var (completedServerCountSnapshot, completedServerCountError) = await serverCountCompletionSource.Task;
            if (!ReferenceEquals(serverCountSnapshot, completedServerCountSnapshot) || !ReferenceEquals(serverCountError, completedServerCountError))
            {
                throw new InvalidOperationException("Server count aggregation callback state did not match the completed task payload.");
            }

            if (marshaledException is not null)
            {
                throw new InvalidOperationException(
                    $"Firestore aggregate server query completed, but Runtime.MarshalObjectiveCException captured unexpected NSException.Name '{marshaledException.Name}'. " +
                    $"Reason: {FormatDetail(marshaledException.Reason)}. Marshal mode: {FormatDetail(marshaledExceptionMode?.ToString())}.");
            }

            var serverResultDetail = completedServerCountError is not null
                ? $"Server count aggregation reached native Firestore completion with Firebase error {FormatNSError(completedServerCountError)}."
                : completedServerCountSnapshot is not null
                    ? $"Server count aggregation returned snapshot count {completedServerCountSnapshot.Count.Int64Value} with query type {completedServerCountSnapshot.Query.GetType().FullName}."
                    : "Server count aggregation completed without either a snapshot or an NSError.";

            if (completedServerCountSnapshot is null && completedServerCountError is null)
            {
                throw new InvalidOperationException(
                    $"Selector '{getAggregationSelector}' completed without either a snapshot or an NSError.");
            }

            if (completedServerCountError is not null)
            {
                throw new InvalidOperationException(serverResultDetail);
            }

            var serverCount = completedServerCountSnapshot!.Count.Int64Value;
            if (serverCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Server count aggregation returned {serverCount}; expected a non-zero count after seeding document 'aggregate-count-seed'.");
            }

            return
                $"Selectors '{queryCountSelector}' and '{aggregateSelector}' returned aggregate query objects without ObjC exception. " +
                $"Aggregate field types: {countField.GetType().FullName}, {sumByNameField.GetType().FullName}, {sumByPathField.GetType().FullName}, {averageByNameField.GetType().FullName}, {averageByPathField.GetType().FullName}. " +
                $"Count query type: {countQuery.GetType().FullName}. Aggregate query type: {aggregateQuery.GetType().FullName}. " +
                $"Underlying query types: {countUnderlyingQuery.GetType().FullName}, {aggregateUnderlyingQuery.GetType().FullName}. " +
                $"Seed document write completed without Firebase error. Aggregate query get selector present: {getAggregationSelector}. " +
                serverResultDetail;
        }
        finally
        {
            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
        }
    }

    static string FormatNSError(NSError error)
    {
        return $"{error.Domain} ({error.Code}): {error.LocalizedDescription}";
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
