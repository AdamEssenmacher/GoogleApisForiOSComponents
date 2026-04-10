using System.Reflection;

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
        const string selector = "useFunctionsEmulatorOrigin:";

        var functions = CloudFunctions.DefaultInstance;
        if (functions is null)
        {
            throw new InvalidOperationException("Firebase.CloudFunctions.CloudFunctions.DefaultInstance returned null after App.Configure().");
        }

        const string origin = "localhost:5001";
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
                functions.UseFunctionsEmulatorOrigin(origin);
                var cachedOrigin = functions.EmulatorOrigin;
                if (!string.Equals(cachedOrigin, origin, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CloudFunctions.EmulatorOrigin returned '{FormatDetail(cachedOrigin)}' after UseFunctionsEmulatorOrigin, expected '{origin}'.");
                }

                var recreatedWrapper = (CloudFunctions?)Activator.CreateInstance(
                    typeof(CloudFunctions),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: new object?[] { functions.Handle },
                    culture: null);
                if (recreatedWrapper is null)
                {
                    throw new InvalidOperationException("Failed to create a second managed CloudFunctions wrapper for the same native handle.");
                }

                var recreatedOrigin = recreatedWrapper.EmulatorOrigin;
                if (!string.Equals(recreatedOrigin, origin, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CloudFunctions.EmulatorOrigin returned '{FormatDetail(recreatedOrigin)}' from a recreated wrapper, expected '{origin}'.");
                }

                functions.UseEmulatorOriginWithHost("127.0.0.1", 5002);
                cachedOrigin = functions.EmulatorOrigin;
                if (!string.Equals(cachedOrigin, "127.0.0.1:5002", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CloudFunctions.EmulatorOrigin returned '{FormatDetail(cachedOrigin)}' after UseEmulatorOriginWithHost, expected '127.0.0.1:5002'.");
                }

                recreatedOrigin = recreatedWrapper.EmulatorOrigin;
                if (!string.Equals(recreatedOrigin, "127.0.0.1:5002", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CloudFunctions.EmulatorOrigin returned '{FormatDetail(recreatedOrigin)}' from a recreated wrapper after UseEmulatorOriginWithHost, expected '127.0.0.1:5002'.");
                }
            }
            catch (ObjCException ex)
            {
                throw new InvalidOperationException(
                    $"Selector '{selector}' should not throw after the binding fix, but observed {ex.GetType().FullName}. " +
                    $"Runtime argument type: {origin.GetType().FullName}. " +
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
                $"Selector '{selector}' completed without ObjC exception after the binding fix. " +
                $"Runtime argument type: {origin.GetType().FullName}. " +
                $"Cached origin after legacy method: {origin}. " +
                $"Recreated wrapper origin after legacy method: {origin}. " +
                $"Cached origin after host/port method: 127.0.0.1:5002. " +
                $"Recreated wrapper origin after host/port method: 127.0.0.1:5002.");
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
