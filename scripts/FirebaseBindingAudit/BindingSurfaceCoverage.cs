using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FirebaseBindingAudit;

internal sealed class BindingSurfaceCoverageManifest
{
    public List<BindingSurfaceCoverageTargetManifest> Targets { get; set; } = [];

    public List<BindingSurfaceWaiver> Waivers { get; set; } = [];
}

internal sealed class BindingSurfaceCoverageTargetManifest
{
    public string Id { get; set; } = string.Empty;

    public string PackageId { get; set; } = string.Empty;

    public string CoverageCaseMethod { get; set; } = string.Empty;

    public string[] SourceFiles { get; set; } = [];

    public List<BindingSurfacePackageReference> RequiredExtraPackages { get; set; } = [];
}

internal sealed class BindingSurfacePackageReference
{
    public string Id { get; set; } = string.Empty;

    public string Version { get; set; } = "12.6.0";
}

internal sealed class BindingSurfaceWaiver
{
    public string Target { get; set; } = string.Empty;

    public string SurfaceId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Evidence { get; set; } = string.Empty;
}

internal sealed record BindingSurfaceCoverageDocument(
    IReadOnlyList<BindingSurfaceCoverageTargetDocument> Targets,
    IReadOnlyList<BindingSurfaceWaiver> Waivers);

internal sealed record BindingSurfaceCoverageTargetDocument(
    string Id,
    string PackageId,
    string CoverageCaseMethod,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<BindingSurfacePackageReference> RequiredPackages,
    IReadOnlyList<BindingSurfaceDescriptor> Surfaces);

internal sealed record BindingSurfaceDescriptor(
    string Target,
    string SurfaceId,
    string Kind,
    string TypeName,
    string RuntimeTypeName,
    string AssemblyName,
    string? ObjectiveCName,
    string? ContainerKind,
    bool IsProtocol,
    bool IsStatic,
    string? MemberName,
    string? BindingAttribute,
    string? BindingValue,
    bool HasGetter,
    bool HasSetter,
    int ParameterCount,
    IReadOnlyList<string> ParameterTypes,
    string? ReturnType,
    IReadOnlyList<BindingSurfaceNativeSelector> NativeSelectors,
    string SourceFile,
    string Signature);

internal sealed record BindingSurfaceNativeSelector(
    string Selector,
    bool IsStatic,
    bool IsProtocol);

internal sealed record BindingSurfaceExerciseRecord(
    string Target,
    string SurfaceId);

internal sealed record BindingSurfaceCoverageValidationResult(
    IReadOnlyList<string> UnclaimedSurfaceIds,
    IReadOnlyList<string> StaleWaiverSurfaceIds,
    IReadOnlyList<string> StaleExerciseSurfaceIds)
{
    public bool IsValid => UnclaimedSurfaceIds.Count == 0 &&
                           StaleWaiverSurfaceIds.Count == 0 &&
                           StaleExerciseSurfaceIds.Count == 0;
}

internal static class BindingSurfaceCoverageManifestLoader
{
    public static BindingSurfaceCoverageManifest Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Binding surface coverage manifest not found.", manifestPath);
        }

        var manifest = JsonSerializer.Deserialize<BindingSurfaceCoverageManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest is null)
        {
            throw new InvalidOperationException($"Unable to deserialize binding surface coverage manifest at '{manifestPath}'.");
        }

        return manifest;
    }
}

internal sealed class BindingSurfaceCoverageBuilder
{
    private static readonly BindingSurfaceNativeSelector[] EmptySelectors = [];
    private readonly AuditConfiguration auditConfiguration;
    private readonly BindingSyntaxParser parser;

    public BindingSurfaceCoverageBuilder(AuditConfiguration auditConfiguration)
    {
        this.auditConfiguration = auditConfiguration;
        parser = new BindingSyntaxParser(auditConfiguration.ManualAttributes, auditConfiguration.BindingAttributes);
    }

    public BindingSurfaceCoverageDocument Build(
        string repoRoot,
        BindingSurfaceCoverageManifest manifest,
        string selectedTarget)
    {
        var manifestTargetsById = manifest.Targets.ToDictionary(static target => target.Id, StringComparer.OrdinalIgnoreCase);
        var selectedTargets = SelectTargets(manifest.Targets, selectedTarget);
        var targetDocuments = new List<BindingSurfaceCoverageTargetDocument>();

        foreach (var targetManifest in selectedTargets)
        {
            var auditTarget = auditConfiguration.Targets.FirstOrDefault(target =>
                string.Equals(target.Id, targetManifest.Id, StringComparison.OrdinalIgnoreCase));
            if (auditTarget is null)
            {
                throw new InvalidOperationException($"Coverage target '{targetManifest.Id}' is not present in scripts/firebase-binding-audit.json.");
            }

            var sourceFiles = targetManifest.SourceFiles
                .Select(sourceFile => Path.Combine(repoRoot, sourceFile))
                .ToList();
            var sourceFileSet = targetManifest.SourceFiles.ToHashSet(StringComparer.Ordinal);
            var expectedSourceFiles = auditTarget.BaselineFiles
                .Concat(auditTarget.HelperFiles)
                .Select(file => Path.Combine(auditTarget.BaselineDirectory, file))
                .ToHashSet(StringComparer.Ordinal);
            if (!sourceFileSet.SetEquals(expectedSourceFiles))
            {
                throw new InvalidOperationException(
                    $"Coverage source files for '{targetManifest.Id}' must exactly match the audit config source files.");
            }

            foreach (var sourceFile in sourceFiles)
            {
                if (!File.Exists(sourceFile))
                {
                    throw new FileNotFoundException($"Coverage source file for '{targetManifest.Id}' was not found.", sourceFile);
                }
            }

            var comparableFiles = auditTarget.BaselineFiles
                .Select(file => Path.Combine(auditTarget.BaselineDirectoryPath(repoRoot), file))
                .ToList();
            var helperFiles = auditTarget.HelperFiles
                .Select(file => Path.Combine(auditTarget.BaselineDirectoryPath(repoRoot), file))
                .ToList();
            var helperFileSet = helperFiles.ToHashSet(StringComparer.Ordinal);
            var snapshot = parser.Parse(comparableFiles, helperFiles);
            var surfaces = BuildSurfaces(targetManifest.Id, snapshot, helperFileSet)
                .Concat(BuildPublicHelperSurfaces(targetManifest.Id, helperFiles))
                .OrderBy(static surface => surface.SurfaceId, StringComparer.Ordinal)
                .ToList();
            var duplicateSurfaceIds = surfaces
                .GroupBy(static surface => surface.SurfaceId, StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Select(static group => group.Key)
                .ToList();
            if (duplicateSurfaceIds.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Coverage target '{targetManifest.Id}' generated duplicate surface ids: {string.Join(", ", duplicateSurfaceIds.Take(20))}");
            }

            var requiredPackages = new Dictionary<string, BindingSurfacePackageReference>(StringComparer.Ordinal);
            AddPackage(requiredPackages, targetManifest.PackageId, "12.6.0");
            foreach (var package in targetManifest.RequiredExtraPackages)
            {
                AddPackage(requiredPackages, package.Id, package.Version);
            }

            targetDocuments.Add(new BindingSurfaceCoverageTargetDocument(
                targetManifest.Id,
                targetManifest.PackageId,
                targetManifest.CoverageCaseMethod,
                targetManifest.SourceFiles,
                requiredPackages.Values.OrderBy(static package => package.Id, StringComparer.Ordinal).ToList(),
                surfaces));
        }

        var selectedTargetIds = selectedTargets.Select(static target => target.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedWaivers = manifest.Waivers
            .Where(waiver => selectedTargetIds.Contains(waiver.Target))
            .OrderBy(static waiver => waiver.Target, StringComparer.Ordinal)
            .ThenBy(static waiver => waiver.SurfaceId, StringComparer.Ordinal)
            .ToList();

        foreach (var waiver in selectedWaivers)
        {
            if (!manifestTargetsById.ContainsKey(waiver.Target))
            {
                throw new InvalidOperationException($"Coverage waiver '{waiver.SurfaceId}' references unknown target '{waiver.Target}'.");
            }
        }

        return new BindingSurfaceCoverageDocument(targetDocuments, selectedWaivers);
    }

    public static async Task WriteAsync(
        BindingSurfaceCoverageDocument document,
        string coverageOutputPath,
        string propsOutputPath,
        string selectedTarget,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(coverageOutputPath))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(propsOutputPath))!);

        var json = JsonSerializer.Serialize(
            document,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        await File.WriteAllTextAsync(coverageOutputPath, json + Environment.NewLine, cancellationToken);

        var props = BuildProps(document, coverageOutputPath, selectedTarget);
        await File.WriteAllTextAsync(propsOutputPath, props.ToString(SaveOptions.DisableFormatting) + Environment.NewLine, cancellationToken);
    }

    private static XDocument BuildProps(
        BindingSurfaceCoverageDocument document,
        string coverageOutputPath,
        string selectedTarget)
    {
        var defaultPackageIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "AdamE.Firebase.iOS.Analytics",
            "AdamE.Firebase.iOS.Core",
            "AdamE.Firebase.iOS.Installations",
            "AdamE.Google.iOS.GoogleAppMeasurement",
            "AdamE.Google.iOS.GoogleDataTransport",
            "AdamE.Google.iOS.GoogleUtilities",
            "AdamE.Google.iOS.Nanopb",
            "AdamE.Google.iOS.PromisesObjC"
        };

        var packages = document.Targets
            .SelectMany(static target => target.RequiredPackages)
            .Where(package => !defaultPackageIds.Contains(package.Id))
            .GroupBy(static package => package.Id, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(package => package.Version, StringComparer.Ordinal).First())
            .OrderBy(static package => package.Id, StringComparer.Ordinal)
            .ToList();
        var assemblyNames = document.Targets
            .SelectMany(static target => target.Surfaces)
            .Select(static surface => surface.AssemblyName)
            .Where(static assemblyName => !string.IsNullOrWhiteSpace(assemblyName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static assemblyName => assemblyName, StringComparer.Ordinal)
            .ToList();
        var defineConstants = string.Join(
            ";",
            new[] { "$(DefineConstants)", "ENABLE_BINDING_SURFACE_COVERAGE" }
                .Concat(document.Targets.Select(static target => CreateTargetCompileConstant(target.Id))));

        var project = new XElement("Project",
            new XElement("PropertyGroup",
                new XElement("BindingSurfaceCoverageTarget", selectedTarget),
                new XElement("DefineConstants", defineConstants)),
            new XElement("ItemGroup",
                new XElement("BundleResource",
                    new XAttribute("Include", Path.GetFullPath(coverageOutputPath)),
                    new XAttribute("Link", "binding-surface-coverage.generated.json"))),
            new XElement("ItemGroup",
                packages.Select(package =>
                    new XElement("PackageReference",
                        new XAttribute("Include", package.Id),
                        new XAttribute("Version", package.Version)))),
            new XElement("ItemGroup",
                assemblyNames.Select(assemblyName =>
                    new XElement("TrimmerRootAssembly",
                        new XAttribute("Include", assemblyName)))));

        return new XDocument(project);
    }

    private static string CreateTargetCompileConstant(string targetId)
    {
        var builder = new StringBuilder("ENABLE_BINDING_SURFACE_COVERAGE_");
        foreach (var character in targetId)
        {
            builder.Append(char.IsLetterOrDigit(character)
                ? char.ToUpperInvariant(character)
                : '_');
        }

        return builder.ToString();
    }

    private static IReadOnlyList<BindingSurfaceCoverageTargetManifest> SelectTargets(
        IReadOnlyList<BindingSurfaceCoverageTargetManifest> targets,
        string selectedTarget)
    {
        if (string.Equals(selectedTarget, "all", StringComparison.OrdinalIgnoreCase))
        {
            return targets.OrderBy(static target => target.Id, StringComparer.Ordinal).ToList();
        }

        var target = targets.FirstOrDefault(target =>
            string.Equals(target.Id, selectedTarget, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            var available = string.Join(", ", targets.Select(static target => target.Id).OrderBy(static id => id, StringComparer.Ordinal));
            throw new InvalidOperationException($"Unknown binding surface target '{selectedTarget}'. Available targets: all, {available}");
        }

        return [target];
    }

    private static IEnumerable<BindingSurfaceDescriptor> BuildSurfaces(
        string target,
        BindingSnapshot snapshot,
        IReadOnlySet<string> helperFiles)
    {
        foreach (var boundType in snapshot.BoundTypes.Values)
        {
            yield return new BindingSurfaceDescriptor(
                Target: target,
                SurfaceId: $"{target}:type:{boundType.ComparisonKey}",
                Kind: boundType.IsProtocol ? "protocol" : "bound-type",
                TypeName: boundType.DisplayName,
                RuntimeTypeName: boundType.DisplayName,
                AssemblyName: ResolveAssemblyName(boundType.Namespace),
                ObjectiveCName: boundType.ObjectiveCName,
                ContainerKind: boundType.ContainerKind,
                IsProtocol: boundType.IsProtocol,
                IsStatic: boundType.IsStatic,
                MemberName: null,
                BindingAttribute: null,
                BindingValue: null,
                HasGetter: false,
                HasSetter: false,
                ParameterCount: 0,
                ParameterTypes: [],
                ReturnType: null,
                NativeSelectors: EmptySelectors,
                SourceFile: boundType.SourceFile,
                Signature: $"{boundType.ContainerKind} {boundType.Name}");

            foreach (var member in boundType.Members.Values)
            {
                var bindingAttribute = member.BindingAttribute;
                var bindingValue = member.BindingValue;
                yield return new BindingSurfaceDescriptor(
                    Target: target,
                    SurfaceId: $"{target}:member:{boundType.ComparisonKey}:{member.Key}",
                    Kind: member.Kind,
                    TypeName: boundType.DisplayName,
                    RuntimeTypeName: boundType.DisplayName,
                    AssemblyName: ResolveAssemblyName(boundType.Namespace),
                    ObjectiveCName: boundType.ObjectiveCName,
                    ContainerKind: boundType.ContainerKind,
                    IsProtocol: boundType.IsProtocol,
                    IsStatic: member.IsStatic,
                    MemberName: member.Name,
                    BindingAttribute: bindingAttribute,
                    BindingValue: bindingValue,
                    HasGetter: member.HasGetter,
                    HasSetter: member.HasSetter,
                    ParameterCount: member.Parameters.Count,
                    ParameterTypes: member.Parameters.Select(static parameter => parameter.Type).ToList(),
                    ReturnType: member.ReturnType,
                    NativeSelectors: BuildNativeSelectors(boundType, member).ToList(),
                    SourceFile: member.SourceFile,
                    Signature: member.Signature);
            }
        }

        var usedDelegateNames = FindUsedDelegateNames(snapshot);
        foreach (var delegateSurface in snapshot.Delegates.Values)
        {
            if (!IsDelegateUsedByBoundSurface(delegateSurface, usedDelegateNames))
            {
                continue;
            }

            yield return new BindingSurfaceDescriptor(
                Target: target,
                SurfaceId: $"{target}:delegate:{delegateSurface.ComparisonKey}",
                Kind: "delegate",
                TypeName: delegateSurface.DisplayName,
                RuntimeTypeName: delegateSurface.DisplayName,
                AssemblyName: ResolveAssemblyName(delegateSurface.Namespace),
                ObjectiveCName: null,
                ContainerKind: "delegate",
                IsProtocol: false,
                IsStatic: false,
                MemberName: null,
                BindingAttribute: null,
                BindingValue: null,
                HasGetter: false,
                HasSetter: false,
                ParameterCount: delegateSurface.Parameters.Count,
                ParameterTypes: delegateSurface.Parameters.Select(static parameter => parameter.Type).ToList(),
                ReturnType: delegateSurface.ReturnType,
                NativeSelectors: EmptySelectors,
                SourceFile: delegateSurface.SourceFile,
                Signature: delegateSurface.Signature);
        }

        foreach (var enumSurface in snapshot.Enums.Values)
        {
            yield return new BindingSurfaceDescriptor(
                Target: target,
                SurfaceId: $"{target}:enum:{enumSurface.ComparisonKey}",
                Kind: "enum",
                TypeName: enumSurface.DisplayName,
                RuntimeTypeName: enumSurface.DisplayName,
                AssemblyName: ResolveAssemblyName(enumSurface.Namespace),
                ObjectiveCName: null,
                ContainerKind: "enum",
                IsProtocol: false,
                IsStatic: false,
                MemberName: null,
                BindingAttribute: null,
                BindingValue: null,
                HasGetter: false,
                HasSetter: false,
                ParameterCount: 0,
                ParameterTypes: [],
                ReturnType: null,
                NativeSelectors: EmptySelectors,
                SourceFile: enumSurface.SourceFile,
                Signature: $"enum {enumSurface.Name}");

            foreach (var enumMember in enumSurface.Members.Values)
            {
                yield return new BindingSurfaceDescriptor(
                    Target: target,
                    SurfaceId: $"{target}:enum-member:{enumSurface.ComparisonKey}:{enumMember.Name}",
                    Kind: "enum-member",
                    TypeName: enumSurface.DisplayName,
                    RuntimeTypeName: enumSurface.DisplayName,
                    AssemblyName: ResolveAssemblyName(enumSurface.Namespace),
                    ObjectiveCName: null,
                    ContainerKind: "enum",
                    IsProtocol: false,
                    IsStatic: false,
                    MemberName: enumMember.Name,
                    BindingAttribute: enumMember.FieldValue is null ? null : "Field",
                    BindingValue: enumMember.FieldValue,
                    HasGetter: false,
                    HasSetter: false,
                    ParameterCount: 0,
                    ParameterTypes: [],
                    ReturnType: null,
                    NativeSelectors: EmptySelectors,
                    SourceFile: enumMember.SourceFile,
                    Signature: $"{enumSurface.Name}.{enumMember.Name}");
            }
        }

        foreach (var manualItem in snapshot.ManualItems)
        {
            if (helperFiles.Contains(manualItem.SourceFile))
            {
                continue;
            }

            if (manualItem.ManualAttributes.Any(static attribute =>
                    string.Equals(attribute, "Internal", StringComparison.Ordinal)))
            {
                continue;
            }

            var (bindingAttribute, bindingValue) = ParseManualMemberKey(manualItem.MatchMemberKey);
            yield return new BindingSurfaceDescriptor(
                Target: target,
                SurfaceId: CreateManualSurfaceId(target, manualItem),
                Kind: manualItem.Kind,
                TypeName: manualItem.TypeName,
                RuntimeTypeName: manualItem.TypeName,
                AssemblyName: ResolveAssemblyName(ExtractNamespace(manualItem.TypeName)),
                ObjectiveCName: null,
                ContainerKind: "manual",
                IsProtocol: false,
                IsStatic: manualItem.IsStatic,
                MemberName: manualItem.MemberName,
                BindingAttribute: bindingAttribute,
                BindingValue: bindingValue,
                HasGetter: manualItem.HasGetter,
                HasSetter: manualItem.HasSetter,
                ParameterCount: manualItem.ParameterTypes.Count,
                ParameterTypes: manualItem.ParameterTypes,
                ReturnType: manualItem.ReturnType,
                NativeSelectors: string.Equals(bindingAttribute, "Export", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(bindingValue)
                    ? [new BindingSurfaceNativeSelector(bindingValue!, manualItem.IsStatic, IsProtocol: false)]
                    : EmptySelectors,
                SourceFile: manualItem.SourceFile,
                Signature: manualItem.Signature);
        }
    }

    private static HashSet<string> FindUsedDelegateNames(BindingSnapshot snapshot)
    {
        var usedDelegateNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var boundType in snapshot.BoundTypes.Values)
        {
            foreach (var member in boundType.Members.Values)
            {
                AddTypeNameVariants(usedDelegateNames, member.ReturnType);
                foreach (var parameter in member.Parameters)
                {
                    AddTypeNameVariants(usedDelegateNames, parameter.Type);
                }
            }
        }

        return usedDelegateNames;
    }

    private static bool IsDelegateUsedByBoundSurface(DelegateSurface delegateSurface, IReadOnlySet<string> usedDelegateNames) =>
        usedDelegateNames.Contains(delegateSurface.Name) ||
        usedDelegateNames.Contains(delegateSurface.DisplayName) ||
        usedDelegateNames.Contains(delegateSurface.ComparisonKey);

    private static void AddTypeNameVariants(HashSet<string> typeNames, string typeName)
    {
        var normalizedTypeName = NormalizeCoverageTypeName(typeName);
        if (string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            return;
        }

        typeNames.Add(normalizedTypeName);
        typeNames.Add(normalizedTypeName.Split('.').Last());
    }

    private static string NormalizeCoverageTypeName(string typeName)
    {
        var normalized = typeName
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Trim()
            .TrimEnd('?');

        while (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized[..^2].TrimEnd();
        }

        return normalized;
    }

    private static IEnumerable<BindingSurfaceDescriptor> BuildPublicHelperSurfaces(string target, IReadOnlyList<string> helperFiles)
    {
        foreach (var helperFile in helperFiles)
        {
            if (!File.Exists(helperFile))
            {
                continue;
            }

            var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(helperFile), path: helperFile);
            var root = syntaxTree.GetCompilationUnitRoot();
            foreach (var delegateDeclaration in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
            {
                if (!IsEffectivelyPublic(delegateDeclaration))
                {
                    continue;
                }

                var namespaceName = GetContainingNamespace(delegateDeclaration);
                var typeName = GetQualifiedTypeName(delegateDeclaration, delegateDeclaration.Identifier.Text);
                var parameterTypes = GetParameterTypes(delegateDeclaration.ParameterList);
                var signature = $"delegate {delegateDeclaration.Identifier.Text}({string.Join(", ", parameterTypes)}) -> {delegateDeclaration.ReturnType.WithoutTrivia()}";
                yield return CreatePublicHelperSurface(
                    target,
                    helperFile,
                    typeName,
                    namespaceName,
                    surfaceId: $"{target}:manual-delegate:{typeName}:{CreateSurfaceIdKey(signature)}",
                    memberName: null,
                    signature: signature,
                    parameterTypes: parameterTypes,
                    returnType: delegateDeclaration.ReturnType.WithoutTrivia().ToString(),
                    isStatic: false,
                    kind: "manual-delegate");
            }

            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (!IsEffectivelyPublic(typeDeclaration))
                {
                    continue;
                }

                var namespaceName = GetContainingNamespace(typeDeclaration);
                var typeName = GetQualifiedTypeName(typeDeclaration, typeDeclaration.Identifier.Text);
                yield return CreatePublicHelperSurface(
                    target,
                    helperFile,
                    typeName,
                    namespaceName,
                    surfaceId: $"{target}:manual-type:{typeName}",
                    memberName: null,
                    signature: $"{GetTypeDeclarationKind(typeDeclaration)} {typeDeclaration.Identifier.Text}",
                    parameterTypes: [],
                    returnType: null,
                    isStatic: typeDeclaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)));

                foreach (var property in typeDeclaration.Members.OfType<PropertyDeclarationSyntax>())
                {
                    if (!IsPublic(property))
                    {
                        continue;
                    }

                    yield return CreatePublicHelperSurface(
                        target,
                        helperFile,
                        typeName,
                        namespaceName,
                        surfaceId: $"{target}:manual:{typeName}:{property.Identifier.Text}:{CreateSurfaceIdKey($"{property.Type.WithoutTrivia()} {property.Identifier.Text}")}",
                        memberName: property.Identifier.Text,
                        signature: $"{property.Type.WithoutTrivia()} {property.Identifier.Text} {{ get; }}",
                        parameterTypes: [],
                        returnType: property.Type.WithoutTrivia().ToString(),
                        isStatic: property.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)),
                        kind: "manual-property",
                        hasGetter: HasGetter(property.AccessorList),
                        hasSetter: HasSetter(property.AccessorList));
                }

                foreach (var indexer in typeDeclaration.Members.OfType<IndexerDeclarationSyntax>())
                {
                    if (!IsPublic(indexer))
                    {
                        continue;
                    }

                    var parameterTypes = GetParameterTypes(indexer.ParameterList);
                    var signature = $"{indexer.Type.WithoutTrivia()} this[{string.Join(", ", parameterTypes)}] {{ get; }}";
                    yield return CreatePublicHelperSurface(
                        target,
                        helperFile,
                        typeName,
                        namespaceName,
                        surfaceId: $"{target}:manual-indexer:{typeName}:{CreateSurfaceIdKey(signature)}",
                        memberName: "Item",
                        signature: signature,
                        parameterTypes: parameterTypes,
                        returnType: indexer.Type.WithoutTrivia().ToString(),
                        isStatic: false,
                        kind: "manual-indexer",
                        hasGetter: HasGetter(indexer.AccessorList),
                        hasSetter: HasSetter(indexer.AccessorList));
                }

                foreach (var constructor in typeDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
                {
                    if (!IsPublic(constructor))
                    {
                        continue;
                    }

                    var parameterTypes = GetParameterTypes(constructor.ParameterList);
                    var signature = $"{constructor.Identifier.Text}({string.Join(", ", parameterTypes)})";
                    yield return CreatePublicHelperSurface(
                        target,
                        helperFile,
                        typeName,
                        namespaceName,
                        surfaceId: $"{target}:manual-constructor:{typeName}:{constructor.ParameterList.Parameters.Count}:{CreateSurfaceIdKey(signature)}",
                        memberName: ".ctor",
                        signature: signature,
                        parameterTypes: parameterTypes,
                        returnType: null,
                        isStatic: false,
                        kind: "manual-constructor");
                }

                foreach (var method in typeDeclaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (!IsPublic(method))
                    {
                        continue;
                    }

                    var parameterTypes = GetParameterTypes(method.ParameterList);
                    var signature = $"{method.Identifier.Text}({string.Join(", ", parameterTypes)}) -> {method.ReturnType.WithoutTrivia()}";
                    yield return CreatePublicHelperSurface(
                        target,
                        helperFile,
                        typeName,
                        namespaceName,
                        surfaceId: $"{target}:manual:{typeName}:{method.Identifier.Text}:{method.ParameterList.Parameters.Count}:{CreateSurfaceIdKey(signature)}",
                        memberName: method.Identifier.Text,
                        signature: signature,
                        parameterTypes: parameterTypes,
                        returnType: method.ReturnType.WithoutTrivia().ToString(),
                        isStatic: method.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)),
                        kind: "manual-method");
                }

                foreach (var field in typeDeclaration.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (!IsPublic(field))
                    {
                        continue;
                    }

                    foreach (var variable in field.Declaration.Variables)
                    {
                        yield return CreatePublicHelperSurface(
                            target,
                            helperFile,
                            typeName,
                            namespaceName,
                            surfaceId: $"{target}:manual-field:{typeName}:{variable.Identifier.Text}:{CreateSurfaceIdKey($"{field.Declaration.Type.WithoutTrivia()} {variable.Identifier.Text}")}",
                            memberName: variable.Identifier.Text,
                            signature: $"{field.Declaration.Type.WithoutTrivia()} {variable.Identifier.Text}",
                            parameterTypes: [],
                            returnType: field.Declaration.Type.WithoutTrivia().ToString(),
                            isStatic: field.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)) ||
                                      field.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ConstKeyword)),
                            kind: "manual-field");
                    }
                }
            }
        }
    }

    private static BindingSurfaceDescriptor CreatePublicHelperSurface(
        string target,
        string helperFile,
        string typeName,
        string namespaceName,
        string surfaceId,
        string? memberName,
        string signature,
        IReadOnlyList<string> parameterTypes,
        string? returnType,
        bool isStatic,
        string? kind = null,
        bool hasGetter = false,
        bool hasSetter = false) =>
        new(
            Target: target,
            SurfaceId: surfaceId,
            Kind: kind ?? (memberName is null ? "manual" : "manual-member"),
            TypeName: typeName,
            RuntimeTypeName: typeName,
            AssemblyName: ResolveAssemblyName(namespaceName),
            ObjectiveCName: null,
            ContainerKind: "manual",
            IsProtocol: false,
            IsStatic: isStatic,
            MemberName: memberName,
            BindingAttribute: null,
            BindingValue: null,
            HasGetter: hasGetter,
            HasSetter: hasSetter,
            ParameterCount: parameterTypes.Count,
            ParameterTypes: parameterTypes,
            ReturnType: returnType,
            NativeSelectors: EmptySelectors,
            SourceFile: helperFile,
            Signature: signature);

    private static IReadOnlyList<string> GetParameterTypes(BaseParameterListSyntax parameterList) =>
        parameterList.Parameters
            .Select(GetParameterType)
            .ToList();

    private static string GetParameterType(ParameterSyntax parameter)
    {
        var parameterType = parameter.Type?.WithoutTrivia().ToString() ?? "object";
        var modifier = parameter.Modifiers.FirstOrDefault(static modifier =>
            modifier.IsKind(SyntaxKind.RefKeyword) ||
            modifier.IsKind(SyntaxKind.OutKeyword) ||
            modifier.IsKind(SyntaxKind.InKeyword));
        return modifier.RawKind == 0 ? parameterType : $"{modifier.Text} {parameterType}";
    }

    private static bool IsEffectivelyPublic(MemberDeclarationSyntax member) =>
        IsPublic(member) &&
        member.Ancestors().OfType<TypeDeclarationSyntax>().All(IsPublic);

    private static bool IsPublic(MemberDeclarationSyntax member) =>
        member.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PublicKeyword));

    private static bool HasGetter(AccessorListSyntax? accessorList) =>
        accessorList is null ||
        accessorList.Accessors.Any(static accessor => accessor.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.GetAccessorDeclaration));

    private static bool HasSetter(AccessorListSyntax? accessorList) =>
        accessorList?.Accessors.Any(static accessor => accessor.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SetAccessorDeclaration)) == true;

    private static string GetQualifiedTypeName(SyntaxNode node, string typeName)
    {
        var containingTypes = new List<string>();
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax typeDeclaration)
            {
                containingTypes.Insert(0, typeDeclaration.Identifier.Text);
            }
        }

        containingTypes.Add(typeName);
        var nestedTypeName = string.Join("+", containingTypes);
        var namespaceName = GetContainingNamespace(node);
        return string.IsNullOrWhiteSpace(namespaceName)
            ? nestedTypeName
            : $"{namespaceName}.{nestedTypeName}";
    }

    private static string GetTypeDeclarationKind(TypeDeclarationSyntax typeDeclaration) =>
        typeDeclaration switch
        {
            ClassDeclarationSyntax classDeclaration when classDeclaration.Modifiers.Any(static modifier => modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StaticKeyword)) => "static class",
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            RecordDeclarationSyntax => "record",
            _ => "type"
        };

    private static IEnumerable<BindingSurfaceNativeSelector> BuildNativeSelectors(BoundTypeSurface boundType, BindingMemberSurface member)
    {
        if (!string.Equals(member.BindingAttribute, "Export", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(member.BindingValue))
        {
            yield break;
        }

        if (string.Equals(member.Kind, "property", StringComparison.Ordinal))
        {
            if (member.HasGetter)
            {
                yield return new BindingSurfaceNativeSelector(
                    member.GetterBind ?? member.BindingValue,
                    member.IsStatic,
                    boundType.IsProtocol);
            }

            if (member.HasSetter)
            {
                yield return new BindingSurfaceNativeSelector(
                    member.SetterBind ?? CreateSetterSelector(member.BindingValue),
                    member.IsStatic,
                    boundType.IsProtocol);
            }

            yield break;
        }

        yield return new BindingSurfaceNativeSelector(member.BindingValue, member.IsStatic, boundType.IsProtocol);
    }

    private static string CreateSetterSelector(string getterSelector)
    {
        var getter = getterSelector.TrimEnd(':');
        if (getter.Length == 0)
        {
            return "set:";
        }

        return $"set{char.ToUpperInvariant(getter[0])}{getter[1..]}:";
    }

    private static string CreateManualSurfaceId(string target, ManualSurfaceItem manualItem)
    {
        var memberKey = manualItem.MatchMemberKey;
        if (!string.IsNullOrWhiteSpace(memberKey))
        {
            return $"{target}:manual:{manualItem.MatchTypeKey}:{memberKey}";
        }

        if (!string.IsNullOrWhiteSpace(manualItem.MemberName))
        {
            return $"{target}:manual:{manualItem.MatchTypeKey}:{manualItem.MemberName}:{CreateSurfaceIdKey(manualItem.Signature)}";
        }

        return $"{target}:manual-type:{manualItem.MatchTypeKey}";
    }

    private static string CreateSurfaceIdKey(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) ||
                character is ':' or '.' or '_' or '-' or '<' or '>' or '[' or ']' or ',')
            {
                builder.Append(character);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    private static (string? BindingAttribute, string? BindingValue) ParseManualMemberKey(string? matchMemberKey)
    {
        if (string.IsNullOrWhiteSpace(matchMemberKey))
        {
            return (null, null);
        }

        var separatorIndex = matchMemberKey.IndexOf('|', StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            return (null, null);
        }

        var rawAttribute = matchMemberKey[..separatorIndex];
        var value = matchMemberKey[(separatorIndex + 1)..];
        var attribute = rawAttribute switch
        {
            "export" => "Export",
            "field" => "Field",
            "notification" => "Notification",
            _ => null
        };

        return (attribute, attribute is null ? null : value);
    }

    private static void AddPackage(
        Dictionary<string, BindingSurfacePackageReference> packages,
        string packageId,
        string version)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return;
        }

        packages.TryAdd(packageId, new BindingSurfacePackageReference
        {
            Id = packageId,
            Version = string.IsNullOrWhiteSpace(version) ? "12.6.0" : version
        });
    }

    private static string ResolveAssemblyName(string? namespaceName)
    {
        if (string.IsNullOrWhiteSpace(namespaceName))
        {
            return string.Empty;
        }

        var parts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : parts[0];
    }

    private static string ExtractNamespace(string typeName)
    {
        var lastDotIndex = typeName.LastIndexOf('.');
        return lastDotIndex < 0 ? string.Empty : typeName[..lastDotIndex];
    }

    private static string GetContainingNamespace(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                return namespaceDeclaration.Name.WithoutTrivia().ToString();
            }
        }

        return string.Empty;
    }
}

internal static class BindingSurfaceCoverageValidator
{
    public static BindingSurfaceCoverageValidationResult Validate(
        BindingSurfaceCoverageDocument document,
        IEnumerable<BindingSurfaceExerciseRecord>? exercisedSurfaces = null)
    {
        var surfacesByTarget = document.Targets.ToDictionary(
            static target => target.Id,
            target => target.Surfaces.Select(static surface => surface.SurfaceId).ToHashSet(StringComparer.Ordinal),
            StringComparer.OrdinalIgnoreCase);
        var waiversByTarget = document.Waivers
            .GroupBy(static waiver => waiver.Target, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var staleWaivers = new List<string>();
        var exercisedByTarget = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (exercisedSurfaces is not null)
        {
            foreach (var exercise in exercisedSurfaces)
            {
                if (!exercisedByTarget.TryGetValue(exercise.Target, out var targetExercises))
                {
                    targetExercises = new HashSet<string>(StringComparer.Ordinal);
                    exercisedByTarget[exercise.Target] = targetExercises;
                }

                targetExercises.Add(exercise.SurfaceId);
            }
        }

        var staleExercises = new List<string>();
        foreach (var exerciseTarget in exercisedByTarget)
        {
            if (!surfacesByTarget.TryGetValue(exerciseTarget.Key, out var knownSurfaces))
            {
                staleExercises.AddRange(exerciseTarget.Value.Select(surfaceId => FormatTargetSurfaceId(exerciseTarget.Key, surfaceId)));
                continue;
            }

            staleExercises.AddRange(exerciseTarget.Value
                .Where(surfaceId => !knownSurfaces.Contains(surfaceId))
                .Select(surfaceId => FormatTargetSurfaceId(exerciseTarget.Key, surfaceId)));
        }

        var unclaimed = new List<string>();
        foreach (var target in document.Targets)
        {
            var waivedSurfaces = waiversByTarget.TryGetValue(target.Id, out var targetWaivers)
                ? targetWaivers.Select(static waiver => waiver.SurfaceId).ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            foreach (var waivedSurface in waivedSurfaces)
            {
                if (!surfacesByTarget[target.Id].Contains(waivedSurface))
                {
                    staleWaivers.Add(FormatTargetSurfaceId(target.Id, waivedSurface));
                }
            }

            if (exercisedSurfaces is null)
            {
                continue;
            }

            var exercised = exercisedByTarget.TryGetValue(target.Id, out var targetExercises)
                ? targetExercises
                : new HashSet<string>(StringComparer.Ordinal);

            unclaimed.AddRange(target.Surfaces
                .Select(static surface => surface.SurfaceId)
                .Where(surfaceId => !waivedSurfaces.Contains(surfaceId) && !exercised.Contains(surfaceId))
                .Select(surfaceId => FormatTargetSurfaceId(target.Id, surfaceId)));
        }

        return new BindingSurfaceCoverageValidationResult(
            unclaimed.OrderBy(static id => id, StringComparer.Ordinal).ToList(),
            staleWaivers.OrderBy(static id => id, StringComparer.Ordinal).ToList(),
            staleExercises.OrderBy(static id => id, StringComparer.Ordinal).ToList());
    }

    private static string FormatTargetSurfaceId(string target, string surfaceId) =>
        surfaceId.StartsWith($"{target}:", StringComparison.Ordinal) ? surfaceId : $"{target}:{surfaceId}";

    public static void ThrowIfInvalid(BindingSurfaceCoverageValidationResult validationResult)
    {
        if (validationResult.IsValid)
        {
            return;
        }

        var messages = new List<string>();
        if (validationResult.UnclaimedSurfaceIds.Count > 0)
        {
            messages.Add("Unclaimed surfaces: " + string.Join(", ", validationResult.UnclaimedSurfaceIds.Take(25)));
        }

        if (validationResult.StaleWaiverSurfaceIds.Count > 0)
        {
            messages.Add("Stale waivers: " + string.Join(", ", validationResult.StaleWaiverSurfaceIds.Take(25)));
        }

        if (validationResult.StaleExerciseSurfaceIds.Count > 0)
        {
            messages.Add("Stale exercised surfaces: " + string.Join(", ", validationResult.StaleExerciseSurfaceIds.Take(25)));
        }

        throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
    }
}
