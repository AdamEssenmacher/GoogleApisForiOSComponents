using System.Reflection;
using Foundation;
using ObjCRuntime;

namespace FirebaseFoundationE2E;

static partial class FirebaseRuntimeDriftCases
{
    sealed class ObjCExceptionProbe : IDisposable
    {
        bool disposed;

        ObjCExceptionProbe()
        {
            Runtime.MarshalObjectiveCException += OnMarshalObjectiveCException;
        }

        public static ObjCExceptionProbe Attach()
        {
            return new ObjCExceptionProbe();
        }

        public NSException? Exception { get; private set; }

        public MarshalObjectiveCExceptionMode? ExceptionMode { get; private set; }

        public bool HasException => Exception is not null;

        public string Name => FormatDetail(Exception?.Name?.ToString());

        public string Reason => FormatDetail(Exception?.Reason);

        public string Mode => FormatDetail(ExceptionMode?.ToString());

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            Runtime.MarshalObjectiveCException -= OnMarshalObjectiveCException;
            disposed = true;
        }

        void OnMarshalObjectiveCException(object? sender, MarshalObjectiveCExceptionEventArgs args)
        {
            Exception ??= args.Exception;
            ExceptionMode ??= args.ExceptionMode;
        }
    }

    static string FormatNSError(NSError error)
    {
        return $"{error.Domain} ({error.Code}): {error.LocalizedDescription}";
    }

    static void RequireConstructor(Type type, Type[] parameterTypes, string selector)
    {
        var constructor = type.GetConstructor(parameterTypes);
        if (constructor is null)
        {
            throw new InvalidOperationException(
                $"Expected managed constructor '{type.FullName}({string.Join(", ", parameterTypes.Select(parameterType => parameterType.FullName))})' " +
                $"was not found for selector '{selector}'.");
        }
    }

    static void RequireVoidMethod(Type type, string methodName, string selector)
    {
        RequireVoidMethod(type, methodName, Type.EmptyTypes, selector);
    }

    static void RequireVoidMethod(Type type, string methodName, Type[] parameterTypes, string selector)
    {
        var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, binder: null, types: parameterTypes, modifiers: null);
        if (method?.ReturnType != typeof(void))
        {
            throw new InvalidOperationException(
                $"Expected managed API '{type.FullName}.{methodName}({string.Join(", ", parameterTypes.Select(parameterType => parameterType.FullName))})' " +
                $"to return void for selector '{selector}', observed '{method?.ReturnType.FullName ?? "<missing>"}'.");
        }
    }

    static string FormatDetail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<empty>" : value;
    }
}
