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
            "AppDistribution" => VerifyAppDistributionBindingSurfaceAsync(document),
            "InAppMessaging" => VerifyInAppMessagingBindingSurfaceAsync(document),
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
            if (IsManualSurface(surface))
            {
                TouchManualSurface(type, surface);
            }

            ProbeNativeSymbol(surface);
            if (!IsManualSurface(surface))
            {
                TouchStaticMemberIfAvailable(type, surface.MemberName);
            }
        }
        else if (IsManualSurface(surface))
        {
            TouchManualSurface(type, surface);
        }
    }

    static Type ResolveManagedType(BindingSurfaceDescriptor surface)
    {
        foreach (var runtimeTypeName in CreateRuntimeTypeNameCandidates(surface))
        {
            var type = ResolveManagedType(runtimeTypeName, surface.AssemblyName);
            if (type is not null)
            {
                return type;
            }
        }

        throw new TypeLoadException($"Managed type '{surface.RuntimeTypeName}' was not found in assembly '{surface.AssemblyName}'.");
    }

    static IEnumerable<string> CreateRuntimeTypeNameCandidates(BindingSurfaceDescriptor surface)
    {
        yield return surface.RuntimeTypeName;

        if (!surface.RuntimeTypeName.StartsWith("Firebase.", StringComparison.Ordinal))
        {
            yield break;
        }

        var runtimeParts = surface.RuntimeTypeName.Split('.');
        if (runtimeParts.Length <= 2 || IsInterfaceStyleName(runtimeParts[^1]))
        {
            yield break;
        }

        var runtimeNamespace = string.Join('.', runtimeParts.Take(runtimeParts.Length - 1));
        yield return $"{runtimeNamespace}.I{runtimeParts[^1]}";

        if (surface.IsProtocol)
        {
            yield return $"{runtimeNamespace}.{runtimeParts[^1]}Wrapper";
        }
    }

    static bool IsInterfaceStyleName(string typeName) =>
        typeName.Length > 1 &&
        typeName[0] == 'I' &&
        char.IsUpper(typeName[1]);

    static void ResolveManagedMember(Type type, BindingSurfaceDescriptor surface)
    {
        VerifyManagedTypeShape(type, surface);

        if (IsDelegateSurface(surface))
        {
            VerifyDelegateSurface(type, surface);
            return;
        }

        if (string.IsNullOrWhiteSpace(surface.MemberName))
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (string.Equals(surface.Kind, "property", StringComparison.Ordinal))
        {
            var property = FindProperty(type, surface, flags);
            if (property is null)
            {
                throw new MissingMemberException(type.FullName, surface.MemberName);
            }

            return;
        }

        if (string.Equals(surface.Kind, "method", StringComparison.Ordinal))
        {
            if (!type.GetMethods(flags).Any(method => MethodMatchesSurface(method, surface)))
            {
                throw new MissingMethodException(type.FullName, surface.Signature);
            }

            return;
        }

        if (string.Equals(surface.Kind, "constructor", StringComparison.Ordinal) &&
            !type.GetConstructors(flags).Any(constructor => ParametersMatch(constructor.GetParameters(), surface)))
        {
            throw new MissingMethodException(type.FullName, ".ctor");
        }
    }

    static void VerifyManagedTypeShape(Type type, BindingSurfaceDescriptor surface)
    {
        if (string.Equals(surface.Kind, "enum", StringComparison.Ordinal) ||
            string.Equals(surface.Kind, "enum-member", StringComparison.Ordinal) ||
            string.Equals(surface.ContainerKind, "enum", StringComparison.Ordinal))
        {
            VerifyEnumSurface(type, surface);
            return;
        }

        if (IsDelegateSurface(surface))
        {
            if (!typeof(Delegate).IsAssignableFrom(type))
            {
                throw new TypeLoadException($"Managed type '{type.FullName}' is not a delegate.");
            }

            return;
        }

        // Binding-definition interfaces and protocols can generate managed classes; only
        // helper/manual type shapes are expected to map 1:1 to reflection Type shape here.
        if (!string.Equals(surface.Kind, "manual-type", StringComparison.Ordinal))
        {
            return;
        }

        VerifyManualTypeShape(type, surface);
    }

    static void VerifyEnumSurface(Type type, BindingSurfaceDescriptor surface)
    {
        if (!type.IsEnum)
        {
            throw new TypeLoadException($"Managed enum '{surface.RuntimeTypeName}' resolved to non-enum type '{type.FullName}'.");
        }

        if (!string.IsNullOrWhiteSpace(surface.UnderlyingType) &&
            !TypeMatches(Enum.GetUnderlyingType(type), surface.UnderlyingType))
        {
            throw new TypeLoadException(
                $"Managed enum '{type.FullName}' has underlying type '{Enum.GetUnderlyingType(type).FullName}', expected '{surface.UnderlyingType}'.");
        }
    }

    static void VerifyManualTypeShape(Type type, BindingSurfaceDescriptor surface)
    {
        switch (surface.ContainerKind)
        {
            case "static class":
                if (!IsStaticClass(type))
                {
                    throw new TypeLoadException($"Managed helper type '{type.FullName}' is not a static class.");
                }
                break;
            case "class":
                if (!type.IsClass || IsStaticClass(type))
                {
                    throw new TypeLoadException($"Managed helper type '{type.FullName}' is not an instance class.");
                }
                break;
            case "struct":
                if (!type.IsValueType || type.IsEnum)
                {
                    throw new TypeLoadException($"Managed helper type '{type.FullName}' is not a struct.");
                }
                break;
            case "interface":
                if (!type.IsInterface)
                {
                    throw new TypeLoadException($"Managed helper type '{type.FullName}' is not an interface.");
                }
                break;
        }
    }

    static bool IsStaticClass(Type type) => type.IsAbstract && type.IsSealed;

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
        if (string.Equals(surface.Kind, "manual-delegate", StringComparison.Ordinal))
        {
            VerifyDelegateSurface(type, surface);
            return;
        }

        var memberName = surface.MemberName;
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return;
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        if (string.Equals(surface.Kind, "manual-constructor", StringComparison.Ordinal))
        {
            if (!type.GetConstructors(flags).Any(constructor => ParametersMatch(constructor.GetParameters(), surface)))
            {
                throw new MissingMethodException(type.FullName, surface.Signature);
            }

            return;
        }

        if (IsManualPropertySurface(surface))
        {
            var property = FindProperty(type, surface, flags);
            if (property?.GetMethod?.IsStatic == true && property.GetIndexParameters().Length == 0)
            {
                _ = property.GetValue(null);
                return;
            }

            if (property is not null)
            {
                return;
            }

            throw new MissingMemberException(type.FullName, surface.MemberName);
        }

        if (IsManualFieldSurface(surface))
        {
            var field = FindField(type, surface, flags);
            if (field?.IsStatic == true)
            {
                _ = field.GetValue(null);
                return;
            }

            if (field is not null)
            {
                return;
            }

            throw new MissingFieldException(type.FullName, surface.MemberName);
        }

        if (IsManualMethodSurface(surface) &&
            type.GetMethods(flags).Any(method => MethodMatchesSurface(method, surface)))
        {
            return;
        }

        throw new MissingMethodException(type.FullName, surface.Signature);
    }

    static PropertyInfo? FindProperty(Type type, BindingSurfaceDescriptor surface, BindingFlags flags) =>
        type.GetProperties(flags).FirstOrDefault(property => PropertyMatchesSurface(property, surface));

    static bool PropertyMatchesSurface(PropertyInfo property, BindingSurfaceDescriptor surface)
    {
        if (!string.Equals(property.Name, surface.MemberName, StringComparison.Ordinal))
        {
            return false;
        }

        return PropertyIsStatic(property) == surface.IsStatic &&
               ReturnTypeMatches(property.PropertyType, surface) &&
               ParametersMatch(property.GetIndexParameters(), surface) &&
               (!surface.HasGetter || property.GetMethod is not null) &&
               (!surface.HasSetter || property.SetMethod is not null);
    }

    static bool PropertyIsStatic(PropertyInfo property) =>
        property.GetMethod?.IsStatic ??
        property.SetMethod?.IsStatic ??
        false;

    static FieldInfo? FindField(Type type, BindingSurfaceDescriptor surface, BindingFlags flags) =>
        type.GetFields(flags).FirstOrDefault(field => FieldMatchesSurface(field, surface));

    static bool FieldMatchesSurface(FieldInfo field, BindingSurfaceDescriptor surface)
    {
        return string.Equals(field.Name, surface.MemberName, StringComparison.Ordinal) &&
               field.IsStatic == surface.IsStatic &&
               ReturnTypeMatches(field.FieldType, surface);
    }

    static bool MethodMatchesSurface(MethodInfo method, BindingSurfaceDescriptor surface)
    {
        if (!string.Equals(method.Name, surface.MemberName, StringComparison.Ordinal))
        {
            return false;
        }

        return method.IsStatic == surface.IsStatic &&
               ParametersMatch(method.GetParameters(), surface) &&
               ReturnTypeMatches(method.ReturnType, surface);
    }

    static bool IsManualPropertySurface(BindingSurfaceDescriptor surface) =>
        string.Equals(surface.Kind, "manual-property", StringComparison.Ordinal) ||
        string.Equals(surface.Kind, "manual-indexer", StringComparison.Ordinal);

    static bool IsManualFieldSurface(BindingSurfaceDescriptor surface) =>
        string.Equals(surface.Kind, "manual-field", StringComparison.Ordinal);

    static bool IsManualMethodSurface(BindingSurfaceDescriptor surface) =>
        !IsManualPropertySurface(surface) &&
        !IsManualFieldSurface(surface) &&
        !string.Equals(surface.Kind, "manual-constructor", StringComparison.Ordinal) &&
        !IsDelegateSurface(surface);

    static bool IsManualSurface(BindingSurfaceDescriptor surface) =>
        surface.Kind.StartsWith("manual", StringComparison.Ordinal);

    static bool IsDelegateSurface(BindingSurfaceDescriptor surface) =>
        string.Equals(surface.Kind, "delegate", StringComparison.Ordinal) ||
        string.Equals(surface.Kind, "manual-delegate", StringComparison.Ordinal);

    static void VerifyDelegateSurface(Type type, BindingSurfaceDescriptor surface)
    {
        if (!typeof(Delegate).IsAssignableFrom(type))
        {
            throw new TypeLoadException($"Managed type '{type.FullName}' is not a delegate.");
        }

        var invokeMethod = type.GetMethod(
            "Invoke",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (invokeMethod is null ||
            invokeMethod.IsStatic != surface.IsStatic ||
            !ParametersMatch(invokeMethod.GetParameters(), surface) ||
            !ReturnTypeMatches(invokeMethod.ReturnType, surface))
        {
            throw new MissingMethodException(type.FullName, surface.Signature);
        }
    }

    static bool ReturnTypeMatches(Type returnType, BindingSurfaceDescriptor surface)
    {
        return string.IsNullOrWhiteSpace(surface.ReturnType) ||
               TypeMatches(returnType, surface.ReturnType);
    }

    static bool ParametersMatch(ParameterInfo[] parameters, BindingSurfaceDescriptor surface)
    {
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
            .Zip(surface.ParameterTypes, static (parameter, expectedType) => ParameterMatches(parameter, expectedType))
            .All(static matches => matches);
    }

    static bool ParameterMatches(ParameterInfo parameter, string expectedParameterType)
    {
        var (expectedModifier, expectedType) = SplitParameterType(expectedParameterType);
        return string.Equals(GetParameterModifier(parameter), expectedModifier, StringComparison.Ordinal) &&
               TypeMatches(parameter.ParameterType, expectedType);
    }

    static (string Modifier, string Type) SplitParameterType(string parameterType)
    {
        foreach (var modifier in new[] { "ref", "out", "in" })
        {
            var prefix = $"{modifier} ";
            if (parameterType.StartsWith(prefix, StringComparison.Ordinal))
            {
                return (modifier, parameterType[prefix.Length..]);
            }
        }

        return (string.Empty, parameterType);
    }

    static string GetParameterModifier(ParameterInfo parameter)
    {
        if (!parameter.ParameterType.IsByRef)
        {
            return string.Empty;
        }

        if (parameter.IsOut)
        {
            return "out";
        }

        return parameter.IsIn ? "in" : "ref";
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
            AddTypeNameSuffixCandidates(candidates, type.FullName);
        }

        return candidates;
    }

    static void AddTypeNameSuffixCandidates(List<string> candidates, string? typeName)
    {
        var normalizedTypeName = NormalizeTypeName(typeName);
        if (string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            return;
        }

        var parts = normalizedTypeName.Split('.');
        for (var index = 1; index < parts.Length - 1; index++)
        {
            AddTypeNameCandidate(candidates, string.Join('.', parts[index..]));
        }
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
        var property = type
            .GetProperties(flags)
            .FirstOrDefault(property =>
                string.Equals(property.Name, memberName, StringComparison.Ordinal) &&
                property.GetIndexParameters().Length == 0);
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
        public string? ReturnType { get; set; }
        public string? UnderlyingType { get; set; }
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
