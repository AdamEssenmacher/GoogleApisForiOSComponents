using System.Reflection;

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

    static string FormatDetail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }
}
