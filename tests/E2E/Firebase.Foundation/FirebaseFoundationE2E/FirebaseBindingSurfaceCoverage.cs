#if ENABLE_BINDING_SURFACE_COVERAGE
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Foundation;
using ObjCRuntime;

namespace FirebaseFoundationE2E;

public static partial class FirebaseBindingSurfaceCoverage
{
    const string CoverageResourceName = "binding-surface-coverage.generated";
    static readonly IReadOnlyDictionary<Type, string> CSharpTypeAliases = new Dictionary<Type, string>
    {
        [typeof(bool)] = "bool",
        [typeof(byte)] = "byte",
        [typeof(sbyte)] = "sbyte",
        [typeof(char)] = "char",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(object)] = "object",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(string)] = "string",
        [typeof(void)] = "void",
        [typeof(IntPtr)] = "nint",
        [typeof(UIntPtr)] = "nuint",
    };

    public static async Task<string> VerifyConfiguredAsync(FirebaseE2ERunResult runResult)
    {
        await Task.Yield();

        var selectedTarget = GetConfiguredTarget();
        if (string.IsNullOrWhiteSpace(selectedTarget))
        {
            throw new InvalidOperationException("Binding surface coverage target metadata was not set.");
        }

        var document = LoadCoverageDocument();
        var targetResults = new List<BindingSurfaceCoverageTargetResult>();
        foreach (var target in SelectTargets(document, selectedTarget))
        {
            targetResults.Add(await ExecuteConfiguredTargetAsync(document, target.Id));
        }

        var summary = new BindingSurfaceCoverageRunResult
        {
            Target = selectedTarget,
            SurfaceCount = targetResults.Sum(static target => target.SurfaceCount),
            ExercisedCount = targetResults.Sum(static target => target.ExercisedCount),
            WaivedCount = targetResults.Sum(static target => target.WaivedCount),
            FailedCount = targetResults.Sum(static target => target.FailedCount),
        };
        summary.Targets.AddRange(targetResults);
        runResult.BindingSurfaceCoverage = summary;

        if (summary.FailedCount > 0)
        {
            var failures = targetResults
                .SelectMany(static target => target.Failures)
                .Take(12)
                .Select(static failure => $"{failure.SurfaceId}: {failure.Message}");
            throw new InvalidOperationException(
                $"Binding surface coverage failed for {summary.FailedCount} surface(s): {string.Join("; ", failures)}");
        }

        return $"Binding surface coverage passed for {summary.SurfaceCount} surface(s): {summary.ExercisedCount} exercised, {summary.WaivedCount} waived.";
    }

    static Task<BindingSurfaceCoverageTargetResult> ExecuteConfiguredTargetAsync(
        BindingSurfaceCoverageDocument document,
        string targetId) =>
        targetId switch
        {
            "ABTesting" => VerifyABTestingBindingSurfaceAsync(document),
            "Analytics" => VerifyAnalyticsBindingSurfaceAsync(document),
            "AppCheck" => VerifyAppCheckBindingSurfaceAsync(document),
            "Auth" => VerifyAuthBindingSurfaceAsync(document),
            "CloudFirestore" => VerifyCloudFirestoreBindingSurfaceAsync(document),
            "CloudFunctions" => VerifyCloudFunctionsBindingSurfaceAsync(document),
            "CloudMessaging" => VerifyCloudMessagingBindingSurfaceAsync(document),
            "Core" => VerifyCoreBindingSurfaceAsync(document),
            "Crashlytics" => VerifyCrashlyticsBindingSurfaceAsync(document),
            "Database" => VerifyDatabaseBindingSurfaceAsync(document),
            "Installations" => VerifyInstallationsBindingSurfaceAsync(document),
            "PerformanceMonitoring" => VerifyPerformanceMonitoringBindingSurfaceAsync(document),
            "RemoteConfig" => VerifyRemoteConfigBindingSurfaceAsync(document),
            "Storage" => VerifyStorageBindingSurfaceAsync(document),
            _ => throw new InvalidOperationException($"No binding surface coverage method exists for target '{targetId}'.")
        };

    static Task<BindingSurfaceCoverageTargetResult> ExecuteTargetAsync(
        BindingSurfaceCoverageDocument document,
        string targetId)
    {
        var target = document.Targets.FirstOrDefault(target =>
            string.Equals(target.Id, targetId, StringComparison.Ordinal));
        if (target is null)
        {
            throw new InvalidOperationException($"Binding surface coverage document does not contain target '{targetId}'.");
        }

        var waivers = document.Waivers
            .Where(waiver => string.Equals(waiver.Target, targetId, StringComparison.Ordinal))
            .ToDictionary(static waiver => waiver.SurfaceId, StringComparer.Ordinal);
        var result = new BindingSurfaceCoverageTargetResult
        {
            Target = target.Id,
            SurfaceCount = target.Surfaces.Count,
            WaivedCount = target.Surfaces.Count(surface => waivers.ContainsKey(surface.SurfaceId)),
        };

        var exercised = new HashSet<string>(StringComparer.Ordinal);
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var surface in target.Surfaces)
        {
            if (waivers.ContainsKey(surface.SurfaceId))
            {
                continue;
            }

            attempted.Add(surface.SurfaceId);
            try
            {
                ExerciseSurface(surface);
                exercised.Add(surface.SurfaceId);
            }
            catch (Exception ex) when (IsBindingLayerFailure(ex))
            {
                result.Failures.Add(new BindingSurfaceCoverageFailure
                {
                    Target = target.Id,
                    SurfaceId = surface.SurfaceId,
                    Kind = surface.Kind,
                    TypeName = surface.TypeName,
                    MemberName = surface.MemberName,
                    Selector = surface.NativeSelectors.FirstOrDefault()?.Selector ?? surface.BindingValue,
                    Message = $"{ex.GetType().FullName}: {ex.Message}",
                });
            }
        }

        var knownSurfaces = target.Surfaces.Select(static surface => surface.SurfaceId).ToHashSet(StringComparer.Ordinal);
        foreach (var staleExercise in exercised.Where(surfaceId => !knownSurfaces.Contains(surfaceId)))
        {
            result.Failures.Add(new BindingSurfaceCoverageFailure
            {
                Target = target.Id,
                SurfaceId = staleExercise,
                Kind = "stale-exerciser",
                TypeName = string.Empty,
                Message = "Exerciser recorded a surface ID not present in the generated source inventory.",
            });
        }

        foreach (var missedSurface in target.Surfaces.Where(surface => !waivers.ContainsKey(surface.SurfaceId) && !attempted.Contains(surface.SurfaceId)))
        {
            result.Failures.Add(new BindingSurfaceCoverageFailure
            {
                Target = target.Id,
                SurfaceId = missedSurface.SurfaceId,
                Kind = missedSurface.Kind,
                TypeName = missedSurface.TypeName,
                MemberName = missedSurface.MemberName,
                Selector = missedSurface.NativeSelectors.FirstOrDefault()?.Selector ?? missedSurface.BindingValue,
                Message = "Surface was neither exercised nor waived.",
            });
        }

        result.ExercisedCount = exercised.Count;
        result.FailedCount = result.Failures.Count;
        return Task.FromResult(result);
    }

    static void ExerciseSurface(BindingSurfaceDescriptor surface)
    {
        var type = ResolveManagedType(surface);
        ResolveManagedMember(type, surface);
        if (surface.IsProtocol)
        {
            ProbeProtocolSurface(surface);
            return;
        }

        if (!string.IsNullOrWhiteSpace(surface.ObjectiveCName) &&
            string.Equals(surface.Kind, "bound-type", StringComparison.Ordinal))
        {
            ProbeNativeClass(surface);
        }

        if (!string.IsNullOrWhiteSpace(surface.ObjectiveCName) && surface.NativeSelectors.Count > 0)
        {
            ProbeNativeClass(surface);
            foreach (var selector in surface.NativeSelectors)
            {
                ProbeClassSelector(surface, selector);
            }
        }

        if (string.Equals(surface.Kind, "enum-member", StringComparison.Ordinal))
        {
            ResolveEnumMember(type, surface.MemberName);
            if (string.Equals(surface.BindingAttribute, "Field", StringComparison.Ordinal))
            {
                ProbeNativeSymbol(surface);
            }
        }
        else if (string.Equals(surface.BindingAttribute, "Field", StringComparison.Ordinal) ||
                 string.Equals(surface.BindingAttribute, "Notification", StringComparison.Ordinal))
        {
            ProbeNativeSymbol(surface);
            TouchStaticMemberIfAvailable(type, surface.MemberName);
        }
        else if (surface.Kind.StartsWith("manual", StringComparison.Ordinal))
        {
            TouchManualSurface(type, surface);
        }
    }

    static Type ResolveManagedType(BindingSurfaceDescriptor surface)
    {
        var type = ResolveManagedType(surface.RuntimeTypeName, surface.AssemblyName);
        if (type is null && surface.RuntimeTypeName.StartsWith("Firebase.", StringComparison.Ordinal))
        {
            var runtimeParts = surface.RuntimeTypeName.Split('.');
            if (runtimeParts.Length > 2 && !runtimeParts[^1].StartsWith('I'))
            {
                var interfaceName = string.Join('.', runtimeParts.Take(runtimeParts.Length - 1).Append($"I{runtimeParts[^1]}"));
                type = ResolveManagedType(interfaceName, surface.AssemblyName);
            }
        }

        return type ?? throw new TypeLoadException($"Managed type '{surface.RuntimeTypeName}' was not found in assembly '{surface.AssemblyName}'.");
    }

    static void ResolveManagedMember(Type type, BindingSurfaceDescriptor surface)
    {
        if (string.IsNullOrWhiteSpace(surface.MemberName))
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (string.Equals(surface.Kind, "property", StringComparison.Ordinal))
        {
            var property = type.GetProperty(surface.MemberName, flags);
            if (property is null)
            {
                throw new MissingMemberException(type.FullName, surface.MemberName);
            }

            if (surface.HasGetter && property.GetMethod is null)
            {
                throw new MissingMemberException(type.FullName, "get_" + surface.MemberName);
            }

            if (surface.HasSetter && property.SetMethod is null)
            {
                throw new MissingMemberException(type.FullName, "set_" + surface.MemberName);
            }

            return;
        }

        if (string.Equals(surface.Kind, "method", StringComparison.Ordinal))
        {
            if (!type.GetMethods(flags).Any(method =>
                    string.Equals(method.Name, surface.MemberName, StringComparison.Ordinal) &&
                    method.GetParameters().Length == surface.ParameterCount))
            {
                throw new MissingMethodException(type.FullName, surface.MemberName);
            }

            return;
        }

        if (string.Equals(surface.Kind, "constructor", StringComparison.Ordinal) &&
            !type.GetConstructors(flags).Any(constructor => constructor.GetParameters().Length == surface.ParameterCount))
        {
            throw new MissingMethodException(type.FullName, ".ctor");
        }
    }

    static Type? ResolveManagedType(string runtimeTypeName, string assemblyName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(runtimeTypeName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            var assembly = Assembly.Load(new AssemblyName(assemblyName));
            return assembly.GetType(runtimeTypeName, throwOnError: false);
        }

        return null;
    }

    static void ProbeNativeClass(BindingSurfaceDescriptor surface)
    {
        if (string.IsNullOrWhiteSpace(surface.ObjectiveCName))
        {
            return;
        }

        var nativeClass = objc_getClass(surface.ObjectiveCName);
        if (nativeClass == IntPtr.Zero)
        {
            throw new TypeLoadException($"Objective-C class '{surface.ObjectiveCName}' was not found for '{surface.SurfaceId}'.");
        }
    }

    static void ProbeProtocolSurface(BindingSurfaceDescriptor surface)
    {
        if (string.IsNullOrWhiteSpace(surface.ObjectiveCName))
        {
            return;
        }

        var protocol = objc_getProtocol(surface.ObjectiveCName);
        if (protocol == IntPtr.Zero)
        {
            throw new TypeLoadException($"Objective-C protocol '{surface.ObjectiveCName}' was not found for '{surface.SurfaceId}'.");
        }

        foreach (var selector in surface.NativeSelectors)
        {
            var selectorHandle = sel_registerName(selector.Selector);
            var isInstanceMethod = selector.IsStatic ? (byte)0 : (byte)1;
            var requiredMethod = protocol_getMethodDescription(protocol, selectorHandle, 1, isInstanceMethod);
            var optionalMethod = protocol_getMethodDescription(protocol, selectorHandle, 0, isInstanceMethod);
            if (requiredMethod.Name == IntPtr.Zero && optionalMethod.Name == IntPtr.Zero)
            {
                throw new MissingMethodException($"Objective-C protocol '{surface.ObjectiveCName}' does not expose selector '{selector.Selector}'.");
            }
        }
    }

    static void ProbeClassSelector(BindingSurfaceDescriptor surface, BindingSurfaceNativeSelector selector)
    {
        if (string.IsNullOrWhiteSpace(surface.ObjectiveCName))
        {
            return;
        }

        var nativeClass = objc_getClass(surface.ObjectiveCName);
        if (nativeClass == IntPtr.Zero)
        {
            throw new TypeLoadException($"Objective-C class '{surface.ObjectiveCName}' was not found for selector '{selector.Selector}'.");
        }

        var selectorHandle = sel_registerName(selector.Selector);
        var method = selector.IsStatic
            ? class_getClassMethod(nativeClass, selectorHandle)
            : class_getInstanceMethod(nativeClass, selectorHandle);
        if (method == IntPtr.Zero)
        {
            throw new MissingMethodException($"Objective-C class '{surface.ObjectiveCName}' does not expose selector '{selector.Selector}'.");
        }
    }

    static void ProbeNativeSymbol(BindingSurfaceDescriptor surface)
    {
        if (string.IsNullOrWhiteSpace(surface.BindingValue))
        {
            return;
        }

        var handle = Dlfcn.dlopen(null, 0);
        if (handle == IntPtr.Zero)
        {
            throw new DllNotFoundException("Unable to open the current process for native symbol lookup.");
        }

        try
        {
            var symbol = Dlfcn.dlsym(handle, surface.BindingValue);
            if (symbol == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException($"Native symbol '{surface.BindingValue}' was not found for '{surface.SurfaceId}'.");
            }
        }
        finally
        {
            Dlfcn.dlclose(handle);
        }
    }

    static void ResolveEnumMember(Type type, string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        if (!type.IsEnum || !Enum.GetNames(type).Contains(memberName, StringComparer.Ordinal))
        {
            throw new MissingMemberException(type.FullName, memberName);
        }
    }

    static void TouchManualSurface(Type type, BindingSurfaceDescriptor surface)
    {
        var memberName = surface.MemberName;
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        var property = type.GetProperty(memberName, flags);
        if (property?.GetMethod?.IsStatic == true)
        {
            _ = property.GetValue(null);
            return;
        }

        if (property is not null)
        {
            return;
        }

        var field = type.GetField(memberName, flags);
        if (field?.IsStatic == true)
        {
            _ = field.GetValue(null);
            return;
        }

        if (field is not null)
        {
            return;
        }

        if (type.GetMethods(flags).Any(method => MethodMatchesManualSurface(method, surface)))
        {
            return;
        }

        throw new MissingMethodException(type.FullName, surface.Signature);
    }

    static bool MethodMatchesManualSurface(MethodInfo method, BindingSurfaceDescriptor surface)
    {
        if (!string.Equals(method.Name, surface.MemberName, StringComparison.Ordinal))
        {
            return false;
        }

        var parameters = method.GetParameters();
        if (parameters.Length != surface.ParameterCount)
        {
            return false;
        }

        if (surface.ParameterTypes.Count == 0)
        {
            return true;
        }

        if (parameters.Length != surface.ParameterTypes.Count)
        {
            return false;
        }

        return parameters
            .Zip(surface.ParameterTypes, static (parameter, expectedType) => TypeMatches(parameter.ParameterType, expectedType))
            .All(static matches => matches);
    }

    static bool TypeMatches(Type actualType, string expectedType)
    {
        var normalizedExpected = NormalizeTypeName(expectedType);
        return CreateTypeNameCandidates(actualType)
            .Any(candidate => string.Equals(candidate, normalizedExpected, StringComparison.Ordinal));
    }

    static IReadOnlyList<string> CreateTypeNameCandidates(Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType() ?? type;
        }

        if (type.IsArray)
        {
            return CreateTypeNameCandidates(type.GetElementType() ?? type)
                .Select(static candidate => NormalizeTypeName($"{candidate}[]"))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        var candidates = new List<string>();
        if (type.IsGenericType)
        {
            var genericDefinition = type.GetGenericTypeDefinition();
            var genericArguments = type.GetGenericArguments();
            foreach (var genericTypeName in CreateGenericTypeNameCandidates(genericDefinition))
            {
                var useFullArgumentNames = genericTypeName.Contains('.', StringComparison.Ordinal);
                var argumentNames = genericArguments
                    .Select(argument => SelectTypeNameCandidate(argument, useFullArgumentNames))
                    .ToList();
                AddTypeNameCandidate(candidates, $"{genericTypeName}<{string.Join(", ", argumentNames)}>");
            }
        }
        else
        {
            if (CSharpTypeAliases.TryGetValue(type, out var alias))
            {
                AddTypeNameCandidate(candidates, alias);
            }

            AddTypeNameCandidate(candidates, type.Name);
            AddTypeNameCandidate(candidates, type.FullName);
        }

        return candidates;
    }

    static IEnumerable<string> CreateGenericTypeNameCandidates(Type genericDefinition)
    {
        yield return StripGenericArity(genericDefinition.Name);
        if (!string.IsNullOrWhiteSpace(genericDefinition.FullName))
        {
            yield return StripGenericArity(genericDefinition.FullName);
        }
    }

    static string SelectTypeNameCandidate(Type type, bool preferFullName)
    {
        var candidates = CreateTypeNameCandidates(type);
        return preferFullName
            ? candidates.FirstOrDefault(static candidate => candidate.Contains('.', StringComparison.Ordinal)) ?? candidates[0]
            : candidates[0];
    }

    static void AddTypeNameCandidate(List<string> candidates, string? typeName)
    {
        var normalizedTypeName = NormalizeTypeName(typeName);
        if (!string.IsNullOrWhiteSpace(normalizedTypeName) &&
            !candidates.Contains(normalizedTypeName, StringComparer.Ordinal))
        {
            candidates.Add(normalizedTypeName);
        }
    }

    static string NormalizeTypeName(string? typeName)
    {
        return typeName?
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace("+", ".", StringComparison.Ordinal)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim() ?? string.Empty;
    }

    static string StripGenericArity(string typeName)
    {
        var tickIndex = typeName.IndexOf('`', StringComparison.Ordinal);
        return tickIndex < 0 ? typeName : typeName[..tickIndex];
    }

    static void TouchStaticMemberIfAvailable(Type type, string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        var property = type.GetProperty(memberName, flags);
        if (property?.GetMethod is not null)
        {
            _ = property.GetValue(null);
            return;
        }

        var field = type.GetField(memberName, flags);
        if (field is not null)
        {
            _ = field.GetValue(null);
        }
    }

    static BindingSurfaceCoverageDocument LoadCoverageDocument()
    {
        var path = NSBundle.MainBundle.PathForResource(CoverageResourceName, "json");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("Generated binding surface coverage document was not bundled.", CoverageResourceName + ".json");
        }

        return JsonSerializer.Deserialize<BindingSurfaceCoverageDocument>(
                   File.ReadAllText(path),
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException("Unable to deserialize generated binding surface coverage document.");
    }

    static IEnumerable<BindingSurfaceCoverageTargetDocument> SelectTargets(
        BindingSurfaceCoverageDocument document,
        string selectedTarget)
    {
        if (string.Equals(selectedTarget, "all", StringComparison.OrdinalIgnoreCase))
        {
            return document.Targets.OrderBy(static target => target.Id, StringComparer.Ordinal);
        }

        var target = document.Targets.FirstOrDefault(target =>
            string.Equals(target.Id, selectedTarget, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            throw new InvalidOperationException($"Generated binding surface coverage document does not contain target '{selectedTarget}'.");
        }

        return [target];
    }

    public static string? GetConfiguredTarget() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static attribute => string.Equals(attribute.Key, "BindingSurfaceCoverageTarget", StringComparison.Ordinal))
            ?.Value;

    static bool IsBindingLayerFailure(Exception exception) =>
        exception is ObjCException ||
        exception is DllNotFoundException ||
        exception is EntryPointNotFoundException ||
        exception is TypeLoadException ||
        exception is MissingMethodException ||
        exception is MissingMemberException ||
        exception is TargetInvocationException ||
        exception is InvalidCastException ||
        exception is MarshalDirectiveException ||
        exception is SEHException ||
        exception is InvalidOperationException;

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_getClass")]
    static extern IntPtr objc_getClass(string name);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "objc_getProtocol")]
    static extern IntPtr objc_getProtocol(string name);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "sel_registerName")]
    static extern IntPtr sel_registerName(string name);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "class_getInstanceMethod")]
    static extern IntPtr class_getInstanceMethod(IntPtr cls, IntPtr selector);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "class_getClassMethod")]
    static extern IntPtr class_getClassMethod(IntPtr cls, IntPtr selector);

    [DllImport(Constants.ObjectiveCLibrary, EntryPoint = "protocol_getMethodDescription")]
    static extern ObjCMethodDescription protocol_getMethodDescription(
        IntPtr protocol,
        IntPtr selector,
        byte isRequiredMethod,
        byte isInstanceMethod);

    [StructLayout(LayoutKind.Sequential)]
    readonly struct ObjCMethodDescription
    {
        public readonly IntPtr Name;
        public readonly IntPtr Types;
    }

    sealed class BindingSurfaceCoverageDocument
    {
        public List<BindingSurfaceCoverageTargetDocument> Targets { get; set; } = [];
        public List<BindingSurfaceWaiver> Waivers { get; set; } = [];
    }

    sealed class BindingSurfaceCoverageTargetDocument
    {
        public string Id { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
        public string CoverageCaseMethod { get; set; } = string.Empty;
        public List<string> SourceFiles { get; set; } = [];
        public List<BindingSurfacePackageReference> RequiredPackages { get; set; } = [];
        public List<BindingSurfaceDescriptor> Surfaces { get; set; } = [];
    }

    sealed class BindingSurfacePackageReference
    {
        public string Id { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }

    sealed class BindingSurfaceWaiver
    {
        public string Target { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
    }

    sealed class BindingSurfaceDescriptor
    {
        public string Target { get; set; } = string.Empty;
        public string SurfaceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string RuntimeTypeName { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public string? ObjectiveCName { get; set; }
        public string? ContainerKind { get; set; }
        public bool IsProtocol { get; set; }
        public bool IsStatic { get; set; }
        public string? MemberName { get; set; }
        public string? BindingAttribute { get; set; }
        public string? BindingValue { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public int ParameterCount { get; set; }
        public List<string> ParameterTypes { get; set; } = [];
        public List<BindingSurfaceNativeSelector> NativeSelectors { get; set; } = [];
        public string SourceFile { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }

    sealed class BindingSurfaceNativeSelector
    {
        public string Selector { get; set; } = string.Empty;
        public bool IsStatic { get; set; }
        public bool IsProtocol { get; set; }
    }
}
#endif
