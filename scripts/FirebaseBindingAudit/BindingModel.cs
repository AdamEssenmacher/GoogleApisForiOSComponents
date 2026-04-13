using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FirebaseBindingAudit;

internal sealed record BindingSnapshot(
    IReadOnlyDictionary<string, BoundTypeSurface> BoundTypes,
    IReadOnlyDictionary<string, DelegateSurface> Delegates,
    IReadOnlyDictionary<string, EnumSurface> Enums,
    IReadOnlyList<ManualSurfaceItem> ManualItems,
    IReadOnlyDictionary<string, string> TypeAliases);

internal sealed record BoundTypeSurface(
    string ComparisonKey,
    string DisplayName,
    string Namespace,
    string Name,
    string ObjectiveCName,
    string ContainerKind,
    string? BaseType,
    bool IsProtocol,
    bool IsStatic,
    string SourceFile,
    IReadOnlyDictionary<string, BindingMemberSurface> Members);

internal sealed record BindingMemberSurface(
    string Key,
    string Kind,
    string Name,
    string? BindingAttribute,
    string? BindingValue,
    bool IsNotification,
    bool IsStatic,
    string ReturnType,
    bool IsReturnNullAllowed,
    bool HasGetter,
    bool HasSetter,
    string? GetterBind,
    string? SetterBind,
    IReadOnlyList<BindingParameterSurface> Parameters,
    string SourceFile,
    string Signature);

internal sealed record BindingParameterSurface(
    string Type,
    bool IsNullAllowed);

internal sealed record DelegateSurface(
    string ComparisonKey,
    string DisplayName,
    string Namespace,
    string Name,
    string ReturnType,
    bool IsReturnNullAllowed,
    IReadOnlyList<BindingParameterSurface> Parameters,
    string SourceFile,
    string Signature);

internal sealed record EnumSurface(
    string ComparisonKey,
    string DisplayName,
    string Namespace,
    string Name,
    string? UnderlyingType,
    bool IsNative,
    string SourceFile,
    IReadOnlyDictionary<string, EnumMemberSurface> Members);

internal sealed record EnumMemberSurface(
    string Name,
    string? FieldValue,
    string SourceFile);

internal sealed record ManualSurfaceItem(
    string Category,
    string TypeName,
    string MatchTypeKey,
    string? MatchMemberKey,
    string? MemberName,
    string Signature,
    string SourceFile);

internal sealed class BindingSyntaxParser
{
    private readonly HashSet<string> manualAttributes;
    private readonly List<string> bindingAttributes;

    public BindingSyntaxParser(IEnumerable<string> manualAttributes, IEnumerable<string> bindingAttributes)
    {
        this.manualAttributes = new HashSet<string>(manualAttributes, StringComparer.Ordinal);
        this.bindingAttributes = bindingAttributes
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public BindingSnapshot Parse(IEnumerable<string> comparableFiles, IEnumerable<string> helperFiles)
    {
        var boundTypes = new Dictionary<string, BoundTypeSurface>(StringComparer.Ordinal);
        var delegates = new Dictionary<string, DelegateSurface>(StringComparer.Ordinal);
        var enums = new Dictionary<string, EnumSurface>(StringComparer.Ordinal);
        var manualItems = new List<ManualSurfaceItem>();

        foreach (var file in comparableFiles.Distinct(StringComparer.Ordinal))
        {
            ParseFile(file, isHelperFile: false, boundTypes, delegates, enums, manualItems);
        }

        foreach (var file in helperFiles.Distinct(StringComparer.Ordinal))
        {
            ParseFile(file, isHelperFile: true, boundTypes, delegates, enums, manualItems);
        }

        return new BindingSnapshot(
            BoundTypes: boundTypes,
            Delegates: delegates,
            Enums: enums,
            ManualItems: manualItems,
            TypeAliases: BuildTypeAliases(boundTypes.Values, delegates.Values, enums.Values));
    }

    private void ParseFile(
        string filePath,
        bool isHelperFile,
        Dictionary<string, BoundTypeSurface> boundTypes,
        Dictionary<string, DelegateSurface> delegates,
        Dictionary<string, EnumSurface> enums,
        List<ManualSurfaceItem> manualItems)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath);
        var root = syntaxTree.GetCompilationUnitRoot();
        VisitMembers(root.Members, string.Empty, filePath, isHelperFile, boundTypes, delegates, enums, manualItems);
    }

    private void VisitMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, BoundTypeSurface> boundTypes,
        Dictionary<string, DelegateSurface> delegates,
        Dictionary<string, EnumSurface> enums,
        List<ManualSurfaceItem> manualItems)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    VisitMembers(
                        namespaceDeclaration.Members,
                        CombineNamespace(currentNamespace, namespaceDeclaration.Name.ToString()),
                        filePath,
                        isHelperFile,
                        boundTypes,
                        delegates,
                        enums,
                        manualItems);
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespaceDeclaration:
                    VisitMembers(
                        fileScopedNamespaceDeclaration.Members,
                        CombineNamespace(currentNamespace, fileScopedNamespaceDeclaration.Name.ToString()),
                        filePath,
                        isHelperFile,
                        boundTypes,
                        delegates,
                        enums,
                        manualItems);
                    break;
                case InterfaceDeclarationSyntax interfaceDeclaration:
                    ParseType(
                        interfaceDeclaration.Identifier.Text,
                        "interface",
                        interfaceDeclaration.Modifiers,
                        interfaceDeclaration.AttributeLists,
                        interfaceDeclaration.Members,
                        currentNamespace,
                        filePath,
                        isHelperFile,
                        boundTypes,
                        manualItems);
                    break;
                case ClassDeclarationSyntax classDeclaration:
                    ParseType(
                        classDeclaration.Identifier.Text,
                        "class",
                        classDeclaration.Modifiers,
                        classDeclaration.AttributeLists,
                        classDeclaration.Members,
                        currentNamespace,
                        filePath,
                        isHelperFile,
                        boundTypes,
                        manualItems);
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    ParseDelegate(delegateDeclaration, currentNamespace, filePath, isHelperFile, delegates, manualItems);
                    break;
                case EnumDeclarationSyntax enumDeclaration:
                    ParseEnum(enumDeclaration, currentNamespace, filePath, isHelperFile, enums, manualItems);
                    break;
            }
        }
    }

    private void ParseType(
        string typeName,
        string containerKind,
        SyntaxTokenList modifiers,
        SyntaxList<AttributeListSyntax> attributeLists,
        SyntaxList<MemberDeclarationSyntax> members,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, BoundTypeSurface> boundTypes,
        List<ManualSurfaceItem> manualItems)
    {
        var isProtocol = HasAttribute(attributeLists, "Protocol");
        var baseTypeName = GetBaseTypeName(attributeLists);
        var objectiveCName = GetTypeBindingName(typeName, attributeLists);
        var isStatic = modifiers.Any(static token => token.IsKind(SyntaxKind.StaticKeyword)) || HasAttribute(attributeLists, "Static");
        var bindingMembers = new Dictionary<string, BindingMemberSurface>(StringComparer.Ordinal);

        foreach (var member in members)
        {
            switch (member)
            {
                case MethodDeclarationSyntax methodDeclaration:
                    ParseMethod(methodDeclaration, typeName, objectiveCName, currentNamespace, filePath, isHelperFile, bindingMembers, manualItems);
                    break;
                case PropertyDeclarationSyntax propertyDeclaration:
                    ParseProperty(propertyDeclaration, typeName, objectiveCName, currentNamespace, filePath, isHelperFile, bindingMembers, manualItems);
                    break;
            }
        }

        if (isHelperFile)
        {
            AddManualTypeItems(typeName, objectiveCName, containerKind, currentNamespace, filePath, bindingMembers.Values, manualItems);
            return;
        }

        if (baseTypeName is null && !isProtocol)
        {
            AddManualTypeItems(typeName, objectiveCName, containerKind, currentNamespace, filePath, bindingMembers.Values, manualItems);
            return;
        }

        boundTypes[objectiveCName] = new BoundTypeSurface(
            ComparisonKey: objectiveCName,
            DisplayName: QualifyType(currentNamespace, typeName),
            Namespace: currentNamespace,
            Name: typeName,
            ObjectiveCName: objectiveCName,
            ContainerKind: containerKind,
            BaseType: baseTypeName,
            IsProtocol: isProtocol,
            IsStatic: isStatic,
            SourceFile: filePath,
            Members: bindingMembers);
    }

    private static void AddManualTypeItems(
        string typeName,
        string typeMatchKey,
        string containerKind,
        string currentNamespace,
        string filePath,
        IEnumerable<BindingMemberSurface> bindingMembers,
        List<ManualSurfaceItem> manualItems)
    {
        var typeDisplayName = QualifyType(currentNamespace, typeName);
        var comparableMembers = bindingMembers.ToList();
        if (comparableMembers.Count == 0)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: typeDisplayName,
                MatchTypeKey: typeMatchKey,
                MatchMemberKey: null,
                MemberName: null,
                Signature: $"{containerKind} {typeName}",
                SourceFile: filePath));
            return;
        }

        foreach (var member in comparableMembers)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: typeDisplayName,
                MatchTypeKey: typeMatchKey,
                MatchMemberKey: member.Key,
                MemberName: member.Name,
                Signature: member.Signature,
                SourceFile: member.SourceFile));
        }
    }

    private void ParseMethod(
        MethodDeclarationSyntax methodDeclaration,
        string containingType,
        string containingTypeMatchKey,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, BindingMemberSurface> bindingMembers,
        List<ManualSurfaceItem> manualItems)
    {
        var bindingAttribute = GetPrimaryBindingAttribute(methodDeclaration.AttributeLists);
        var bindingValue = bindingAttribute is null ? null : GetBindingValue(methodDeclaration.AttributeLists, bindingAttribute);
        var isManual = isHelperFile || HasAnyAttribute(methodDeclaration.AttributeLists, manualAttributes);
        var signature = BuildMethodSignature(methodDeclaration);

        if (isManual)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: QualifyType(currentNamespace, containingType),
                MatchTypeKey: containingTypeMatchKey,
                MatchMemberKey: bindingAttribute is null ? null : CreateMemberKey(bindingAttribute, bindingValue, methodDeclaration.Identifier.Text, methodDeclaration.ParameterList.Parameters.Count),
                MemberName: methodDeclaration.Identifier.Text,
                Signature: signature,
                SourceFile: filePath));
            return;
        }

        if (bindingAttribute is null)
        {
            return;
        }

        var parameters = methodDeclaration.ParameterList.Parameters
            .Select(CreateParameter)
            .ToList();
        var memberKey = CreateMemberKey(bindingAttribute, bindingValue, methodDeclaration.Identifier.Text, parameters.Count);

        bindingMembers[memberKey] = new BindingMemberSurface(
            Key: memberKey,
            Kind: methodDeclaration.Identifier.Text == "Constructor" ? "constructor" : "method",
            Name: methodDeclaration.Identifier.Text,
            BindingAttribute: bindingAttribute,
            BindingValue: bindingValue,
            IsNotification: HasAttribute(methodDeclaration.AttributeLists, "Notification"),
            IsStatic: methodDeclaration.Modifiers.Any(static token => token.IsKind(SyntaxKind.StaticKeyword)) || HasAttribute(methodDeclaration.AttributeLists, "Static"),
            ReturnType: NormalizeType(methodDeclaration.ReturnType),
            IsReturnNullAllowed: HasNullAllowed(methodDeclaration.AttributeLists, "return") || HasNullAllowed(methodDeclaration.AttributeLists, null),
            HasGetter: false,
            HasSetter: false,
            GetterBind: null,
            SetterBind: null,
            Parameters: parameters,
            SourceFile: filePath,
            Signature: signature);
    }

    private void ParseProperty(
        PropertyDeclarationSyntax propertyDeclaration,
        string containingType,
        string containingTypeMatchKey,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, BindingMemberSurface> bindingMembers,
        List<ManualSurfaceItem> manualItems)
    {
        var bindingAttribute = GetPrimaryBindingAttribute(propertyDeclaration.AttributeLists);
        var bindingValue = bindingAttribute is null ? null : GetBindingValue(propertyDeclaration.AttributeLists, bindingAttribute);
        var isManual = isHelperFile || HasAnyAttribute(propertyDeclaration.AttributeLists, manualAttributes);
        var signature = BuildPropertySignature(propertyDeclaration);

        if (isManual)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: QualifyType(currentNamespace, containingType),
                MatchTypeKey: containingTypeMatchKey,
                MatchMemberKey: bindingAttribute is null ? null : CreateMemberKey(bindingAttribute, bindingValue, propertyDeclaration.Identifier.Text, 0),
                MemberName: propertyDeclaration.Identifier.Text,
                Signature: signature,
                SourceFile: filePath));
            return;
        }

        if (bindingAttribute is null)
        {
            return;
        }

        var getter = propertyDeclaration.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        var setter = propertyDeclaration.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration));
        var memberKey = CreateMemberKey(bindingAttribute, bindingValue, propertyDeclaration.Identifier.Text, 0);

        bindingMembers[memberKey] = new BindingMemberSurface(
            Key: memberKey,
            Kind: "property",
            Name: propertyDeclaration.Identifier.Text,
            BindingAttribute: bindingAttribute,
            BindingValue: bindingValue,
            IsNotification: HasAttribute(propertyDeclaration.AttributeLists, "Notification"),
            IsStatic: propertyDeclaration.Modifiers.Any(static token => token.IsKind(SyntaxKind.StaticKeyword)) || HasAttribute(propertyDeclaration.AttributeLists, "Static"),
            ReturnType: NormalizeType(propertyDeclaration.Type),
            IsReturnNullAllowed: HasNullAllowed(propertyDeclaration.AttributeLists, "return") || HasNullAllowed(propertyDeclaration.AttributeLists, null),
            HasGetter: getter is not null,
            HasSetter: setter is not null,
            GetterBind: getter is null ? null : GetAttributeStringValue(getter.AttributeLists, "Bind"),
            SetterBind: setter is null ? null : GetAttributeStringValue(setter.AttributeLists, "Bind"),
            Parameters: Array.Empty<BindingParameterSurface>(),
            SourceFile: filePath,
            Signature: signature);
    }

    private void ParseDelegate(
        DelegateDeclarationSyntax delegateDeclaration,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, DelegateSurface> delegates,
        List<ManualSurfaceItem> manualItems)
    {
        if (isHelperFile)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: QualifyType(currentNamespace, delegateDeclaration.Identifier.Text),
                MatchTypeKey: delegateDeclaration.Identifier.Text,
                MatchMemberKey: null,
                MemberName: null,
                Signature: BuildDelegateSignature(delegateDeclaration),
                SourceFile: filePath));
            return;
        }

        var comparisonKey = delegateDeclaration.Identifier.Text;
        delegates[comparisonKey] = new DelegateSurface(
            ComparisonKey: comparisonKey,
            DisplayName: QualifyType(currentNamespace, delegateDeclaration.Identifier.Text),
            Namespace: currentNamespace,
            Name: delegateDeclaration.Identifier.Text,
            ReturnType: NormalizeType(delegateDeclaration.ReturnType),
            IsReturnNullAllowed: HasNullAllowed(delegateDeclaration.AttributeLists, "return") || HasNullAllowed(delegateDeclaration.AttributeLists, null),
            Parameters: delegateDeclaration.ParameterList.Parameters.Select(CreateParameter).ToList(),
            SourceFile: filePath,
            Signature: BuildDelegateSignature(delegateDeclaration));
    }

    private void ParseEnum(
        EnumDeclarationSyntax enumDeclaration,
        string currentNamespace,
        string filePath,
        bool isHelperFile,
        Dictionary<string, EnumSurface> enums,
        List<ManualSurfaceItem> manualItems)
    {
        if (isHelperFile)
        {
            manualItems.Add(new ManualSurfaceItem(
                Category: "manual-surface",
                TypeName: QualifyType(currentNamespace, enumDeclaration.Identifier.Text),
                MatchTypeKey: enumDeclaration.Identifier.Text,
                MatchMemberKey: null,
                MemberName: null,
                Signature: $"enum {enumDeclaration.Identifier.Text}",
                SourceFile: filePath));
            return;
        }

        var members = new Dictionary<string, EnumMemberSurface>(StringComparer.Ordinal);
        foreach (var member in enumDeclaration.Members)
        {
            members[member.Identifier.Text] = new EnumMemberSurface(
                Name: member.Identifier.Text,
                FieldValue: GetAttributeStringValue(member.AttributeLists, "Field"),
                SourceFile: filePath);
        }

        var comparisonKey = enumDeclaration.Identifier.Text;
        enums[comparisonKey] = new EnumSurface(
            ComparisonKey: comparisonKey,
            DisplayName: QualifyType(currentNamespace, enumDeclaration.Identifier.Text),
            Namespace: currentNamespace,
            Name: enumDeclaration.Identifier.Text,
            UnderlyingType: enumDeclaration.BaseList?.Types.FirstOrDefault()?.Type.WithoutTrivia().ToString(),
            IsNative: HasAttribute(enumDeclaration.AttributeLists, "Native"),
            SourceFile: filePath,
            Members: members);
    }

    private static string CreateMemberKey(string bindingAttribute, string? bindingValue, string memberName, int parameterCount)
    {
        if (!string.IsNullOrWhiteSpace(bindingValue))
        {
            return $"{bindingAttribute.ToLowerInvariant()}|{bindingValue}";
        }

        return $"{bindingAttribute.ToLowerInvariant()}|{memberName}|{parameterCount}";
    }

    private static string GetTypeBindingName(string typeName, SyntaxList<AttributeListSyntax> attributeLists)
    {
        var protocolName = GetAttributeNamedStringValue(attributeLists, "Protocol", "Name");
        if (!string.IsNullOrWhiteSpace(protocolName))
        {
            return protocolName;
        }

        var baseTypeName = GetAttributeNamedStringValue(attributeLists, "BaseType", "Name");
        return string.IsNullOrWhiteSpace(baseTypeName) ? typeName : baseTypeName;
    }

    private string? GetPrimaryBindingAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var bindingAttribute in bindingAttributes)
        {
            if (HasAttribute(attributeLists, bindingAttribute))
            {
                return bindingAttribute;
            }
        }

        return null;
    }

    private static string? GetBindingValue(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
    {
        return GetAttributeStringValue(attributeLists, attributeName);
    }

    private static BindingParameterSurface CreateParameter(ParameterSyntax parameter)
    {
        return new BindingParameterSurface(
            Type: NormalizeType(parameter.Type),
            IsNullAllowed: HasNullAllowed(parameter.AttributeLists, null));
    }

    private static Dictionary<string, string> BuildTypeAliases(
        IEnumerable<BoundTypeSurface> boundTypes,
        IEnumerable<DelegateSurface> delegates,
        IEnumerable<EnumSurface> enums)
    {
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var boundType in boundTypes)
        {
            AddExpandedAliases(aliases, boundType.ComparisonKey, boundType.ComparisonKey);
            AddExpandedAliases(aliases, boundType.DisplayName, boundType.ComparisonKey);
            AddExpandedAliases(aliases, boundType.Name, boundType.ComparisonKey);
            AddExpandedAliases(aliases, boundType.ObjectiveCName, boundType.ComparisonKey);
            AddExpandedAliases(aliases, CreateNamespacePrefixedAlias(boundType.Namespace, boundType.Name), boundType.ComparisonKey);
        }

        foreach (var delegateSurface in delegates)
        {
            AddExpandedAliases(aliases, delegateSurface.ComparisonKey, delegateSurface.ComparisonKey);
            AddExpandedAliases(aliases, delegateSurface.DisplayName, delegateSurface.ComparisonKey);
            AddExpandedAliases(aliases, delegateSurface.Name, delegateSurface.ComparisonKey);
            AddExpandedAliases(aliases, CreateNamespacePrefixedAlias(delegateSurface.Namespace, delegateSurface.Name), delegateSurface.ComparisonKey);
        }

        foreach (var enumSurface in enums)
        {
            AddExpandedAliases(aliases, enumSurface.ComparisonKey, enumSurface.ComparisonKey);
            AddExpandedAliases(aliases, enumSurface.DisplayName, enumSurface.ComparisonKey);
            AddExpandedAliases(aliases, enumSurface.Name, enumSurface.ComparisonKey);
            AddExpandedAliases(aliases, CreateNamespacePrefixedAlias(enumSurface.Namespace, enumSurface.Name), enumSurface.ComparisonKey);
        }

        return aliases;
    }

    private static string? CreateNamespacePrefixedAlias(string namespaceName, string typeName)
    {
        var namespaceTail = namespaceName
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(namespaceTail) ||
            typeName.StartsWith(namespaceTail, StringComparison.Ordinal))
        {
            return null;
        }

        return $"{namespaceTail}{typeName}";
    }

    private static void AddExpandedAliases(IDictionary<string, string> aliases, string? alias, string comparisonKey)
    {
        AddAlias(aliases, alias, comparisonKey);
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        foreach (var expandedAlias in ExpandAliasVariants(alias))
        {
            AddAlias(aliases, expandedAlias, comparisonKey);
        }
    }

    private static IEnumerable<string> ExpandAliasVariants(string alias)
    {
        var simpleName = alias.Split('.').Last();
        foreach (var simpleVariant in ExpandSimpleAliasVariants(simpleName))
        {
            yield return simpleVariant;
            if (alias.Contains('.', StringComparison.Ordinal))
            {
                yield return $"{string.Join(".", alias.Split('.').SkipLast(1))}.{simpleVariant}";
            }
        }
    }

    private static IEnumerable<string> ExpandSimpleAliasVariants(string simpleName)
    {
        var variants = new HashSet<string>(StringComparer.Ordinal);
        AddSimpleAliasVariant(variants, simpleName);

        if (TryStripObjectiveCPrefix(simpleName, out var withoutObjectiveCPrefix))
        {
            AddSimpleAliasVariant(variants, withoutObjectiveCPrefix);
        }

        if (TryStripInterfaceObjectiveCPrefix(simpleName, out var withoutInterfaceObjectiveCPrefix))
        {
            AddSimpleAliasVariant(variants, withoutInterfaceObjectiveCPrefix);
        }

        return variants;
    }

    private static void AddSimpleAliasVariant(HashSet<string> variants, string simpleName)
    {
        if (!variants.Add(simpleName))
        {
            return;
        }

        if (simpleName.EndsWith("Block", StringComparison.Ordinal))
        {
            variants.Add($"{simpleName[..^"Block".Length]}Handler");
        }

        if (simpleName.EndsWith("Callback", StringComparison.Ordinal))
        {
            variants.Add($"{simpleName[..^"Callback".Length]}Handler");
        }

        if (simpleName.EndsWith("Completion", StringComparison.Ordinal))
        {
            variants.Add($"{simpleName}Handler");
        }
    }

    private static bool TryStripObjectiveCPrefix(string alias, out string result)
    {
        foreach (var prefix in new[] { "FIR", "ABT" })
        {
            if (alias.Length > prefix.Length &&
                alias.StartsWith(prefix, StringComparison.Ordinal) &&
                char.IsUpper(alias[prefix.Length]))
            {
                result = alias[prefix.Length..];
                return true;
            }
        }

        result = string.Empty;
        return false;
    }

    private static bool TryStripInterfaceObjectiveCPrefix(string alias, out string result)
    {
        if (alias.Length > 4 &&
            alias.StartsWith("IFIR", StringComparison.Ordinal) &&
            char.IsUpper(alias[4]))
        {
            result = $"I{alias[4..]}";
            return true;
        }

        if (alias.Length > 4 &&
            alias.StartsWith("IABT", StringComparison.Ordinal) &&
            char.IsUpper(alias[4]))
        {
            result = $"I{alias[4..]}";
            return true;
        }

        result = string.Empty;
        return false;
    }

    private static void AddAlias(IDictionary<string, string> aliases, string? alias, string comparisonKey)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        var normalizedAlias = NormalizeAlias(alias);
        if (!aliases.ContainsKey(normalizedAlias))
        {
            aliases[normalizedAlias] = comparisonKey;
        }
    }

    private static string BuildMethodSignature(MethodDeclarationSyntax methodDeclaration)
    {
        var builder = new StringBuilder();
        builder.Append(methodDeclaration.Identifier.Text);
        builder.Append('(');
        builder.Append(string.Join(", ", methodDeclaration.ParameterList.Parameters.Select(static parameter => NormalizeType(parameter.Type))));
        builder.Append(')');
        builder.Append(" -> ");
        builder.Append(NormalizeType(methodDeclaration.ReturnType));
        return builder.ToString();
    }

    private static string BuildPropertySignature(PropertyDeclarationSyntax propertyDeclaration)
    {
        var builder = new StringBuilder();
        builder.Append(NormalizeType(propertyDeclaration.Type));
        builder.Append(' ');
        builder.Append(propertyDeclaration.Identifier.Text);
        builder.Append(" { ");
        if (propertyDeclaration.AccessorList?.Accessors.Any(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration)) == true)
        {
            builder.Append("get; ");
        }

        if (propertyDeclaration.AccessorList?.Accessors.Any(static accessor => accessor.IsKind(SyntaxKind.SetAccessorDeclaration)) == true)
        {
            builder.Append("set; ");
        }

        builder.Append('}');
        return builder.ToString();
    }

    private static string BuildDelegateSignature(DelegateDeclarationSyntax delegateDeclaration)
    {
        var builder = new StringBuilder();
        builder.Append(delegateDeclaration.Identifier.Text);
        builder.Append('(');
        builder.Append(string.Join(", ", delegateDeclaration.ParameterList.Parameters.Select(static parameter => NormalizeType(parameter.Type))));
        builder.Append(')');
        builder.Append(" -> ");
        builder.Append(NormalizeType(delegateDeclaration.ReturnType));
        return builder.ToString();
    }

    private static string NormalizeType(TypeSyntax? typeSyntax)
    {
        return typeSyntax?.WithoutTrivia().ToString().Replace("global::", string.Empty, StringComparison.Ordinal) ?? "void";
    }

    private static string QualifyType(string currentNamespace, string typeName)
    {
        return string.IsNullOrWhiteSpace(currentNamespace) ? typeName : $"{currentNamespace}.{typeName}";
    }

    private static string CombineNamespace(string currentNamespace, string childNamespace)
    {
        return string.IsNullOrWhiteSpace(currentNamespace) ? childNamespace : $"{currentNamespace}.{childNamespace}";
    }

    private bool HasAnyAttribute(SyntaxList<AttributeListSyntax> attributeLists, HashSet<string> attributeNames)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (attributeNames.Contains(NormalizeAttributeName(attribute.Name)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (string.Equals(NormalizeAttributeName(attribute.Name), attributeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasNullAllowed(SyntaxList<AttributeListSyntax> attributeLists, string? attributeTarget)
    {
        foreach (var attributeList in attributeLists)
        {
            var target = attributeList.Target?.Identifier.Text;
            if (attributeTarget is not null && !string.Equals(attributeTarget, target, StringComparison.Ordinal))
            {
                continue;
            }

            if (attributeTarget is null && target is not null)
            {
                continue;
            }

            foreach (var attribute in attributeList.Attributes)
            {
                if (string.Equals(NormalizeAttributeName(attribute.Name), "NullAllowed", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? GetBaseTypeName(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!string.Equals(NormalizeAttributeName(attribute.Name), "BaseType", StringComparison.Ordinal))
                {
                    continue;
                }

                var firstArgument = attribute.ArgumentList?.Arguments.FirstOrDefault();
                if (firstArgument?.Expression is TypeOfExpressionSyntax typeOfExpression)
                {
                    return NormalizeType(typeOfExpression.Type);
                }
            }
        }

        return null;
    }

    private static string? GetAttributeStringValue(SyntaxList<AttributeListSyntax> attributeLists, string attributeName)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!string.Equals(NormalizeAttributeName(attribute.Name), attributeName, StringComparison.Ordinal))
                {
                    continue;
                }

                var firstArgument = attribute.ArgumentList?.Arguments.FirstOrDefault(static argument => argument.NameEquals is null);
                if (firstArgument is null)
                {
                    return null;
                }

                return Unquote(firstArgument.Expression.WithoutTrivia().ToString());
            }
        }

        return null;
    }

    private static string? GetAttributeNamedStringValue(
        SyntaxList<AttributeListSyntax> attributeLists,
        string attributeName,
        string argumentName)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                if (!string.Equals(NormalizeAttributeName(attribute.Name), attributeName, StringComparison.Ordinal))
                {
                    continue;
                }

                var namedArgument = attribute.ArgumentList?.Arguments.FirstOrDefault(argument =>
                    string.Equals(argument.NameEquals?.Name.Identifier.Text, argumentName, StringComparison.Ordinal));

                if (namedArgument is null)
                {
                    continue;
                }

                return Unquote(namedArgument.Expression.WithoutTrivia().ToString());
            }
        }

        return null;
    }

    private static string NormalizeAttributeName(NameSyntax nameSyntax)
    {
        var rawName = nameSyntax.WithoutTrivia().ToString();
        var shortName = rawName.Split('.').Last();
        return shortName.EndsWith("Attribute", StringComparison.Ordinal)
            ? shortName[..^"Attribute".Length]
            : shortName;
    }

    private static string NormalizeAlias(string alias)
    {
        return alias.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string Unquote(string text)
    {
        return text.Length >= 2 && text.StartsWith('"') && text.EndsWith('"')
            ? text[1..^1]
            : text;
    }
}

internal sealed class SymbolAliasLookup
{
    private readonly Dictionary<string, List<string>> comparisonKeysByAlias = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> lookupKeysByComparisonKey = new(StringComparer.Ordinal);

    public void AddAliases(IReadOnlyDictionary<string, string>? aliases)
    {
        if (aliases is null)
        {
            return;
        }

        foreach (var alias in aliases)
        {
            AddAlias(alias.Key, alias.Value);
            AddAlias(alias.Value, alias.Value);
        }
    }

    public IEnumerable<string> GetEquivalentComparisonKeys(string comparisonKey)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lookupKeys = new HashSet<string>(GetLookupKeys(comparisonKey), StringComparer.Ordinal);
        if (lookupKeysByComparisonKey.TryGetValue(comparisonKey, out var registeredLookupKeys))
        {
            lookupKeys.UnionWith(registeredLookupKeys);
        }

        foreach (var lookupKey in lookupKeys)
        {
            if (!comparisonKeysByAlias.TryGetValue(lookupKey, out var comparisonKeys))
            {
                continue;
            }

            foreach (var equivalentComparisonKey in comparisonKeys)
            {
                if (seen.Add(equivalentComparisonKey))
                {
                    yield return equivalentComparisonKey;
                }
            }
        }
    }

    public bool TryGetComparableEntry<T>(
        IReadOnlyDictionary<string, T> entries,
        string comparisonKey,
        out T entry)
    {
        if (entries.TryGetValue(comparisonKey, out entry!))
        {
            return true;
        }

        foreach (var equivalentKey in GetEquivalentComparisonKeys(comparisonKey))
        {
            if (entries.TryGetValue(equivalentKey, out entry!))
            {
                return true;
            }

            var simplifiedEquivalentKey = equivalentKey.Split('.').Last();
            if (entries.TryGetValue(simplifiedEquivalentKey, out entry!))
            {
                return true;
            }
        }

        var simplifiedKey = comparisonKey.Split('.').Last();
        if (entries.TryGetValue(simplifiedKey, out entry!))
        {
            return true;
        }

        foreach (var equivalentKey in GetEquivalentComparisonKeys(simplifiedKey))
        {
            if (entries.TryGetValue(equivalentKey, out entry!))
            {
                return true;
            }
        }

        return false;
    }

    public string NormalizeSimpleType(string typeName)
    {
        var matchingComparisonKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var lookupKey in GetLookupKeys(typeName))
        {
            if (comparisonKeysByAlias.TryGetValue(lookupKey, out var comparisonKeys))
            {
                foreach (var comparisonKey in comparisonKeys)
                {
                    matchingComparisonKeys.Add(comparisonKey);
                }
            }
        }

        if (matchingComparisonKeys.Count > 0)
        {
            return matchingComparisonKeys
                .OrderBy(static comparisonKey => GetComparisonKeyRank(comparisonKey))
                .ThenBy(static comparisonKey => comparisonKey.Length)
                .ThenBy(static comparisonKey => comparisonKey, StringComparer.Ordinal)
                .First();
        }

        return NormalizeLookupKey(typeName.Split('.').Last());
    }

    public static string NormalizeLookupKey(string alias)
    {
        return alias.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private void AddAlias(string alias, string comparisonKey)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(comparisonKey))
        {
            return;
        }

        foreach (var lookupKey in GetLookupKeys(alias))
        {
            if (!comparisonKeysByAlias.TryGetValue(lookupKey, out var comparisonKeys))
            {
                comparisonKeys = [];
                comparisonKeysByAlias[lookupKey] = comparisonKeys;
            }

            if (!comparisonKeys.Contains(comparisonKey, StringComparer.Ordinal))
            {
                comparisonKeys.Add(comparisonKey);
            }

            if (!lookupKeysByComparisonKey.TryGetValue(comparisonKey, out var lookupKeys))
            {
                lookupKeys = new HashSet<string>(StringComparer.Ordinal);
                lookupKeysByComparisonKey[comparisonKey] = lookupKeys;
            }

            lookupKeys.Add(lookupKey);
        }
    }

    private static IEnumerable<string> GetLookupKeys(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            yield break;
        }

        var lookupKeys = new HashSet<string>(StringComparer.Ordinal);
        AddLookupKeyVariants(lookupKeys, alias);
        var simpleName = alias.Split('.').Last();
        AddLookupKeyVariants(lookupKeys, simpleName);

        foreach (var lookupKey in lookupKeys)
        {
            yield return NormalizeLookupKey(lookupKey);
        }
    }

    private static int GetComparisonKeyRank(string comparisonKey)
    {
        var simpleName = comparisonKey.Split('.').Last();
        return HasObjectiveCPrefixShape(simpleName) ? 1 : 0;
    }

    private static bool HasObjectiveCPrefixShape(string alias)
    {
        return (alias.Length > 3 && alias.StartsWith("FIR", StringComparison.Ordinal) && char.IsUpper(alias[3])) ||
               (alias.Length > 3 && alias.StartsWith("ABT", StringComparison.Ordinal) && char.IsUpper(alias[3])) ||
               (alias.Length > 4 && alias.StartsWith("IFIR", StringComparison.Ordinal) && char.IsUpper(alias[4])) ||
               (alias.Length > 4 && alias.StartsWith("IABT", StringComparison.Ordinal) && char.IsUpper(alias[4]));
    }

    private static void AddLookupKeyVariants(HashSet<string> lookupKeys, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias) || !lookupKeys.Add(alias))
        {
            return;
        }

        if (alias.EndsWith("Block", StringComparison.Ordinal))
        {
            AddLookupKeyVariants(lookupKeys, $"{alias[..^"Block".Length]}Handler");
        }

        if (alias.EndsWith("Callback", StringComparison.Ordinal))
        {
            AddLookupKeyVariants(lookupKeys, $"{alias[..^"Callback".Length]}Handler");
        }

        if (alias.EndsWith("Completion", StringComparison.Ordinal))
        {
            AddLookupKeyVariants(lookupKeys, $"{alias}Handler");
        }

        if (TryStripObjectiveCPrefix(alias, out var withoutObjectiveCPrefix))
        {
            AddLookupKeyVariants(lookupKeys, withoutObjectiveCPrefix);
        }

        if (TryStripInterfaceObjectiveCPrefix(alias, out var withoutInterfaceObjectiveCPrefix))
        {
            AddLookupKeyVariants(lookupKeys, withoutInterfaceObjectiveCPrefix);
            AddLookupKeyVariants(lookupKeys, withoutInterfaceObjectiveCPrefix[1..]);
        }
    }

    private static bool TryStripObjectiveCPrefix(string alias, out string result)
    {
        foreach (var prefix in new[] { "FIR", "ABT" })
        {
            if (alias.Length > prefix.Length &&
                alias.StartsWith(prefix, StringComparison.Ordinal) &&
                char.IsUpper(alias[prefix.Length]))
            {
                result = alias[prefix.Length..];
                return true;
            }
        }

        result = string.Empty;
        return false;
    }

    private static bool TryStripInterfaceObjectiveCPrefix(string alias, out string result)
    {
        if (alias.Length > 4 &&
            alias.StartsWith("IFIR", StringComparison.Ordinal) &&
            char.IsUpper(alias[4]))
        {
            result = $"I{alias[4..]}";
            return true;
        }

        if (alias.Length > 4 &&
            alias.StartsWith("IABT", StringComparison.Ordinal) &&
            char.IsUpper(alias[4]))
        {
            result = $"I{alias[4..]}";
            return true;
        }

        result = string.Empty;
        return false;
    }
}

internal sealed class BindingComparer
{
    private static readonly HashSet<string> ExternalPlaceholderTypeKeys = new(StringComparer.Ordinal)
    {
        "UIApplication"
    };

    public TargetComparisonResult Compare(
        BindingSnapshot baseline,
        BindingSnapshot generated,
        IReadOnlyDictionary<string, string>? sharedAliases = null)
    {
        var failures = new List<AuditFinding>();
        var infos = new List<AuditFinding>();
        var aliases = BuildAliasLookup(baseline.TypeAliases, generated.TypeAliases, sharedAliases);
        var baselineManualLookup = new HashSet<string>(
            baseline.ManualItems
                .Where(static item => !string.IsNullOrWhiteSpace(item.MatchMemberKey))
                .Select(static item => CreateManualLookupKey(item.MatchTypeKey, item.MatchMemberKey!)),
            StringComparer.Ordinal);
        var generatedManualMemberLookup = new HashSet<string>(
            generated.ManualItems
                .Where(static item => !string.IsNullOrWhiteSpace(item.MatchMemberKey))
                .Select(static item => item.MatchMemberKey!),
            StringComparer.Ordinal);

        var smoothedDelegateKeys = new HashSet<string>(StringComparer.Ordinal);

        CompareBoundTypes(
            baseline.BoundTypes,
            generated.BoundTypes,
            baseline.Delegates,
            generated.Delegates,
            aliases,
            baselineManualLookup,
            generatedManualMemberLookup,
            failures,
            infos,
            smoothedDelegateKeys);
        CompareDelegates(baseline.Delegates, generated.Delegates, aliases, failures, smoothedDelegateKeys);
        CompareEnums(baseline.Enums, generated.Enums, aliases, failures);

        foreach (var manualItem in baseline.ManualItems)
        {
            infos.Add(new AuditFinding(
                Category: "manual-surface",
                Severity: "info",
                Message: $"Baseline-only manual surface: {manualItem.Signature}",
                TypeName: manualItem.TypeName,
                MemberName: manualItem.MemberName,
                Selector: null,
                BaselineFile: manualItem.SourceFile,
                GeneratedFile: null));
        }

        return new TargetComparisonResult(failures, infos);
    }

    private static SymbolAliasLookup BuildAliasLookup(
        IReadOnlyDictionary<string, string> baselineAliases,
        IReadOnlyDictionary<string, string> generatedAliases,
        IReadOnlyDictionary<string, string>? sharedAliases)
    {
        var aliases = new SymbolAliasLookup();

        aliases.AddAliases(sharedAliases);
        aliases.AddAliases(baselineAliases);
        aliases.AddAliases(generatedAliases);
        return aliases;
    }

    private static void CompareBoundTypes(
        IReadOnlyDictionary<string, BoundTypeSurface> baseline,
        IReadOnlyDictionary<string, BoundTypeSurface> generated,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        IReadOnlySet<string> baselineManualLookup,
        IReadOnlySet<string> generatedManualMemberLookup,
        List<AuditFinding> failures,
        List<AuditFinding> infos,
        ISet<string> smoothedDelegateKeys)
    {
        foreach (var baselineEntry in baseline.Values)
        {
            if (!TryGetComparableEntry(generated, baselineEntry.ComparisonKey, aliases, out var generatedType))
            {
                failures.Add(new AuditFinding(
                    Category: "stale-baseline-binding",
                    Severity: "failure",
                    Message: $"Type '{baselineEntry.DisplayName}' exists in the baseline but not in fresh output.",
                    TypeName: baselineEntry.DisplayName,
                    MemberName: null,
                    Selector: null,
                    BaselineFile: baselineEntry.SourceFile,
                    GeneratedFile: null,
                    ComparisonTypeKey: baselineEntry.ComparisonKey));
                continue;
            }

            CompareTypeMetadata(baselineEntry, generatedType, aliases, failures);
            CompareMembers(
                baselineEntry,
                generatedType,
                baselineDelegates,
                generatedDelegates,
                aliases,
                baselineManualLookup,
                generatedManualMemberLookup,
                failures,
                infos,
                smoothedDelegateKeys);
        }

        foreach (var generatedEntry in generated.Values)
        {
            if (ContainsEquivalentEntry(baseline, generatedEntry.ComparisonKey, aliases))
            {
                continue;
            }

            if (IsExternalPlaceholderType(generatedEntry))
            {
                continue;
            }

            failures.Add(new AuditFinding(
                Category: "missing-baseline-binding",
                Severity: "failure",
                Message: $"Type '{generatedEntry.DisplayName}' exists in fresh output but not in the baseline.",
                TypeName: generatedEntry.DisplayName,
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: generatedEntry.SourceFile,
                ComparisonTypeKey: generatedEntry.ComparisonKey));
        }
    }

    private static void CompareTypeMetadata(
        BoundTypeSurface baseline,
        BoundTypeSurface generated,
        SymbolAliasLookup aliases,
        List<AuditFinding> failures)
    {
        if (!string.Equals(baseline.ContainerKind, generated.ContainerKind, StringComparison.Ordinal) ||
            !string.Equals(NormalizeTypeReference(baseline.BaseType, aliases), NormalizeTypeReference(generated.BaseType, aliases), StringComparison.Ordinal) ||
            baseline.IsProtocol != generated.IsProtocol ||
            baseline.IsStatic != generated.IsStatic)
        {
            failures.Add(new AuditFinding(
                Category: "attribute-drift",
                Severity: "failure",
                Message: $"Type metadata drift for '{baseline.DisplayName}'. Baseline: base='{baseline.BaseType}', protocol={baseline.IsProtocol}, static={baseline.IsStatic}. Fresh: base='{generated.BaseType}', protocol={generated.IsProtocol}, static={generated.IsStatic}.",
                TypeName: baseline.DisplayName,
                MemberName: null,
                Selector: null,
                BaselineFile: baseline.SourceFile,
                GeneratedFile: generated.SourceFile,
                ComparisonTypeKey: baseline.ComparisonKey));
        }
    }

    private static void CompareMembers(
        BoundTypeSurface baseline,
        BoundTypeSurface generated,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        IReadOnlySet<string> baselineManualLookup,
        IReadOnlySet<string> generatedManualMemberLookup,
        List<AuditFinding> failures,
        List<AuditFinding> infos,
        ISet<string> smoothedDelegateKeys)
    {
        foreach (var baselineMemberEntry in baseline.Members.Values)
        {
            if (!TryGetComparableMember(generated.Members, baselineMemberEntry, out var generatedMember))
            {
                if (IsMatchedGeneratedManualBinding(baselineMemberEntry, generatedManualMemberLookup))
                {
                    continue;
                }

                failures.Add(new AuditFinding(
                    Category: "stale-baseline-binding",
                    Severity: "failure",
                    Message: $"Member '{baselineMemberEntry.Name}' on '{baseline.DisplayName}' exists in the baseline but not in fresh output.",
                    TypeName: baseline.DisplayName,
                    MemberName: baselineMemberEntry.Name,
                    Selector: baselineMemberEntry.BindingValue,
                    BaselineFile: baselineMemberEntry.SourceFile,
                    GeneratedFile: null,
                    ComparisonTypeKey: baseline.ComparisonKey,
                    ComparisonMemberKey: baselineMemberEntry.Key));
                continue;
            }

            CompareMemberMetadata(
                baseline.DisplayName,
                baseline.ComparisonKey,
                baselineMemberEntry,
                generatedMember,
                baselineDelegates,
                generatedDelegates,
                aliases,
                failures,
                infos,
                smoothedDelegateKeys);
        }

        foreach (var generatedMemberEntry in generated.Members.Values)
        {
            if (TryGetComparableMember(baseline.Members, generatedMemberEntry, out _))
            {
                continue;
            }

            if (baselineManualLookup.Contains(CreateManualLookupKey(baseline.ComparisonKey, generatedMemberEntry.Key)))
            {
                continue;
            }

            failures.Add(new AuditFinding(
                Category: "missing-baseline-binding",
                Severity: "failure",
                Message: $"Member '{generatedMemberEntry.Name}' on '{generated.DisplayName}' exists in fresh output but not in the baseline.",
                TypeName: generated.DisplayName,
                MemberName: generatedMemberEntry.Name,
                Selector: generatedMemberEntry.BindingValue,
                BaselineFile: null,
                GeneratedFile: generatedMemberEntry.SourceFile,
                ComparisonTypeKey: generated.ComparisonKey,
                ComparisonMemberKey: generatedMemberEntry.Key));
        }
    }

    private static bool IsExternalPlaceholderType(BoundTypeSurface type)
    {
        return type.Members.Count == 0 &&
               !type.IsProtocol &&
               ExternalPlaceholderTypeKeys.Contains(type.ComparisonKey);
    }

    private static bool IsMatchedGeneratedManualBinding(
        BindingMemberSurface baselineMember,
        IReadOnlySet<string> generatedManualMemberLookup)
    {
        return string.Equals(baselineMember.BindingAttribute, "Field", StringComparison.Ordinal) &&
               generatedManualMemberLookup.Contains(baselineMember.Key);
    }

    private static bool TryGetComparableMember(
        IReadOnlyDictionary<string, BindingMemberSurface> members,
        BindingMemberSurface requestedMember,
        out BindingMemberSurface member)
    {
        if (members.TryGetValue(requestedMember.Key, out member!))
        {
            return true;
        }

        foreach (var candidate in members.Values)
        {
            if (AreComparableMembers(requestedMember, candidate))
            {
                member = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool AreComparableMembers(BindingMemberSurface left, BindingMemberSurface right)
    {
        return HaveEquivalentMemberKinds(left, right) &&
               string.Equals(left.BindingAttribute, right.BindingAttribute, StringComparison.Ordinal) &&
               HaveEquivalentBindingSelectors(left, right);
    }

    private static bool HaveEquivalentMemberKinds(BindingMemberSurface left, BindingMemberSurface right)
    {
        return string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) ||
               IsReadOnlyPropertyGetterMethodPair(left, right);
    }

    private static bool HaveEquivalentAccessors(BindingMemberSurface left, BindingMemberSurface right)
    {
        if (IsReadOnlyPropertyGetterMethodPair(left, right))
        {
            return true;
        }

        return left.HasGetter == right.HasGetter &&
               left.HasSetter == right.HasSetter;
    }

    private static bool IsReadOnlyPropertyGetterMethodPair(BindingMemberSurface left, BindingMemberSurface right)
    {
        return IsReadOnlyPropertyGetter(left, right) ||
               IsReadOnlyPropertyGetter(right, left);
    }

    private static bool IsReadOnlyPropertyGetter(BindingMemberSurface property, BindingMemberSurface method)
    {
        return string.Equals(property.Kind, "property", StringComparison.Ordinal) &&
               string.Equals(method.Kind, "method", StringComparison.Ordinal) &&
               property.HasGetter &&
               !property.HasSetter &&
               property.Parameters.Count == 0 &&
               method.Parameters.Count == 0;
    }

    private static bool HaveEquivalentBindingSelectors(BindingMemberSurface left, BindingMemberSurface right)
    {
        if (string.Equals(left.BindingValue, right.BindingValue, StringComparison.Ordinal) &&
            string.Equals(left.GetterBind, right.GetterBind, StringComparison.Ordinal) &&
            string.Equals(left.SetterBind, right.SetterBind, StringComparison.Ordinal))
        {
            return true;
        }

        var leftSelectors = GetBindingSelectorAliases(left);
        var rightSelectors = GetBindingSelectorAliases(right);
        return leftSelectors.Overlaps(rightSelectors);
    }

    private static HashSet<string> GetBindingSelectorAliases(BindingMemberSurface member)
    {
        var selectors = new HashSet<string>(StringComparer.Ordinal);
        AddSelectorAlias(selectors, member.BindingValue);
        AddSelectorAlias(selectors, member.GetterBind);
        AddSelectorAlias(selectors, member.SetterBind);

        if (string.Equals(member.Kind, "property", StringComparison.Ordinal))
        {
            AddBooleanPropertySelectorAlias(selectors, member.Name);
            AddBooleanPropertySelectorAlias(selectors, member.BindingValue);
            AddBooleanPropertySelectorAlias(selectors, member.GetterBind);
        }

        return selectors;
    }

    private static void AddSelectorAlias(HashSet<string> selectors, string? selector)
    {
        if (!string.IsNullOrWhiteSpace(selector))
        {
            selectors.Add(SymbolAliasLookup.NormalizeLookupKey(selector));
        }
    }

    private static void AddBooleanPropertySelectorAlias(HashSet<string> selectors, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return;
        }

        var trimmed = selector.Trim();
        if (TryStripBooleanGetterPrefix(trimmed, "is", out var propertyName) ||
            TryStripBooleanGetterPrefix(trimmed, "Is", out propertyName) ||
            TryStripBooleanGetterPrefix(trimmed, "has", out propertyName) ||
            TryStripBooleanGetterPrefix(trimmed, "Has", out propertyName))
        {
            selectors.Add(SymbolAliasLookup.NormalizeLookupKey(ToLowerCamelCase(propertyName)));
        }
    }

    private static bool TryStripBooleanGetterPrefix(string selector, string prefix, out string propertyName)
    {
        if (selector.Length > prefix.Length &&
            selector.StartsWith(prefix, StringComparison.Ordinal) &&
            char.IsUpper(selector[prefix.Length]))
        {
            propertyName = selector[prefix.Length..];
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

    private static string ToLowerCamelCase(string value)
    {
        return string.IsNullOrEmpty(value)
            ? value
            : $"{char.ToLowerInvariant(value[0])}{value[1..]}";
    }

    private static void CompareMemberMetadata(
        string typeName,
        string typeKey,
        BindingMemberSurface baseline,
        BindingMemberSurface generated,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        List<AuditFinding> failures,
        List<AuditFinding> infos,
        ISet<string> smoothedDelegateKeys)
    {
        var baselineParameterTypes = baseline.Parameters.Select(parameter => NormalizeTypeReference(parameter.Type, aliases)).ToList();
        var generatedParameterTypes = generated.Parameters.Select(parameter => NormalizeTypeReference(parameter.Type, aliases)).ToList();
        var signatureHasDrift = false;
        var preferredShapeNotes = new List<string>();

        if (!HaveEquivalentMemberKinds(baseline, generated) ||
            !string.Equals(NormalizeTypeReference(baseline.ReturnType, aliases), NormalizeTypeReference(generated.ReturnType, aliases), StringComparison.Ordinal) ||
            baseline.IsStatic != generated.IsStatic ||
            !HaveEquivalentAccessors(baseline, generated) ||
            baseline.Parameters.Count != generated.Parameters.Count)
        {
            signatureHasDrift = true;
        }

        if (!signatureHasDrift)
        {
            for (var index = 0; index < baselineParameterTypes.Count; index++)
            {
                if (string.Equals(baselineParameterTypes[index], generatedParameterTypes[index], StringComparison.Ordinal))
                {
                    continue;
                }

                if (TryMatchActionSmoothing(
                        baseline.Parameters[index].Type,
                        generated.Parameters[index].Type,
                        baselineDelegates,
                        generatedDelegates,
                        aliases,
                        smoothedDelegateKeys,
                        out var preferredShapeNote))
                {
                    preferredShapeNotes.Add(preferredShapeNote);
                    continue;
                }

                signatureHasDrift = true;
                break;
            }
        }

        if (signatureHasDrift)
        {
            failures.Add(new AuditFinding(
                Category: "signature-drift",
                Severity: "failure",
                Message: $"Signature drift for '{typeName}.{baseline.Name}'. Baseline: {baseline.Signature}. Fresh: {generated.Signature}.",
                TypeName: typeName,
                MemberName: baseline.Name,
                Selector: baseline.BindingValue ?? generated.BindingValue,
                BaselineFile: baseline.SourceFile,
                GeneratedFile: generated.SourceFile,
                ComparisonTypeKey: typeKey,
                ComparisonMemberKey: baseline.Key));
        }
        else
        {
            foreach (var preferredShapeNote in preferredShapeNotes.Distinct(StringComparer.Ordinal))
            {
                infos.Add(new AuditFinding(
                    Category: "preferred-api-shape",
                    Severity: "info",
                    Message: $"Compatible callback shape difference for '{typeName}.{baseline.Name}': {preferredShapeNote}. Keep existing public surface for compatibility; prefer Action<> smoothing for new or additive APIs case-by-case.",
                    TypeName: typeName,
                    MemberName: baseline.Name,
                    Selector: baseline.BindingValue ?? generated.BindingValue,
                    BaselineFile: baseline.SourceFile,
                    GeneratedFile: generated.SourceFile,
                    ComparisonTypeKey: typeKey,
                    ComparisonMemberKey: baseline.Key));
            }
        }

        var hasParameterNullabilityDrift = baseline.Parameters.Count == generated.Parameters.Count &&
            baseline.Parameters.Where((parameter, index) => parameter.IsNullAllowed != generated.Parameters[index].IsNullAllowed).Any();

        if (!string.Equals(baseline.BindingAttribute, generated.BindingAttribute, StringComparison.Ordinal) ||
            !HaveEquivalentBindingSelectors(baseline, generated) ||
            baseline.IsReturnNullAllowed != generated.IsReturnNullAllowed ||
            hasParameterNullabilityDrift)
        {
            failures.Add(new AuditFinding(
                Category: "attribute-drift",
                Severity: "failure",
                Message: $"Binding attribute drift for '{typeName}.{baseline.Name}'. Baseline selector='{baseline.BindingValue}', return nullable={baseline.IsReturnNullAllowed}. Fresh selector='{generated.BindingValue}', return nullable={generated.IsReturnNullAllowed}.",
                TypeName: typeName,
                MemberName: baseline.Name,
                Selector: baseline.BindingValue ?? generated.BindingValue,
                BaselineFile: baseline.SourceFile,
                GeneratedFile: generated.SourceFile,
                ComparisonTypeKey: typeKey,
                ComparisonMemberKey: baseline.Key));
        }
    }

    private static void CompareDelegates(
        IReadOnlyDictionary<string, DelegateSurface> baseline,
        IReadOnlyDictionary<string, DelegateSurface> generated,
        SymbolAliasLookup aliases,
        List<AuditFinding> failures,
        IReadOnlySet<string> smoothedDelegateKeys)
    {
        foreach (var baselineEntry in baseline.Values)
        {
            if (!TryGetComparableEntry(generated, baselineEntry.ComparisonKey, aliases, out var generatedDelegate))
            {
                if (smoothedDelegateKeys.Contains(baselineEntry.ComparisonKey))
                {
                    continue;
                }

                failures.Add(new AuditFinding(
                    Category: "stale-baseline-binding",
                    Severity: "failure",
                    Message: $"Delegate '{baselineEntry.DisplayName}' exists in the baseline but not in fresh output.",
                    TypeName: baselineEntry.DisplayName,
                    MemberName: null,
                    Selector: null,
                    BaselineFile: baselineEntry.SourceFile,
                    GeneratedFile: null,
                    ComparisonTypeKey: baselineEntry.ComparisonKey));
                continue;
            }

            if (!string.Equals(NormalizeTypeReference(baselineEntry.ReturnType, aliases), NormalizeTypeReference(generatedDelegate.ReturnType, aliases), StringComparison.Ordinal) ||
                baselineEntry.IsReturnNullAllowed != generatedDelegate.IsReturnNullAllowed ||
                baselineEntry.Parameters.Count != generatedDelegate.Parameters.Count ||
                baselineEntry.Parameters.Where((parameter, index) =>
                    !string.Equals(NormalizeTypeReference(parameter.Type, aliases), NormalizeTypeReference(generatedDelegate.Parameters[index].Type, aliases), StringComparison.Ordinal) ||
                    parameter.IsNullAllowed != generatedDelegate.Parameters[index].IsNullAllowed).Any())
            {
                failures.Add(new AuditFinding(
                    Category: "signature-drift",
                    Severity: "failure",
                    Message: $"Delegate drift for '{baselineEntry.DisplayName}'. Baseline: {baselineEntry.Signature}. Fresh: {generatedDelegate.Signature}.",
                    TypeName: baselineEntry.DisplayName,
                    MemberName: null,
                    Selector: null,
                    BaselineFile: baselineEntry.SourceFile,
                    GeneratedFile: generatedDelegate.SourceFile,
                    ComparisonTypeKey: baselineEntry.ComparisonKey));
            }
        }

        foreach (var generatedEntry in generated.Values)
        {
            if (ContainsEquivalentEntry(baseline, generatedEntry.ComparisonKey, aliases))
            {
                continue;
            }

            if (smoothedDelegateKeys.Contains(generatedEntry.ComparisonKey))
            {
                continue;
            }

            failures.Add(new AuditFinding(
                Category: "missing-baseline-binding",
                Severity: "failure",
                Message: $"Delegate '{generatedEntry.DisplayName}' exists in fresh output but not in the baseline.",
                TypeName: generatedEntry.DisplayName,
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: generatedEntry.SourceFile,
                ComparisonTypeKey: generatedEntry.ComparisonKey));
        }
    }

    private static void CompareEnums(
        IReadOnlyDictionary<string, EnumSurface> baseline,
        IReadOnlyDictionary<string, EnumSurface> generated,
        SymbolAliasLookup aliases,
        List<AuditFinding> failures)
    {
        foreach (var baselineEntry in baseline.Values)
        {
            if (!TryGetComparableEntry(generated, baselineEntry.ComparisonKey, aliases, out var generatedEnum))
            {
                failures.Add(new AuditFinding(
                    Category: "stale-baseline-binding",
                    Severity: "failure",
                    Message: $"Enum '{baselineEntry.DisplayName}' exists in the baseline but not in fresh output.",
                    TypeName: baselineEntry.DisplayName,
                    MemberName: null,
                    Selector: null,
                    BaselineFile: baselineEntry.SourceFile,
                    GeneratedFile: null,
                    ComparisonTypeKey: baselineEntry.ComparisonKey));
                continue;
            }

            if (!string.Equals(NormalizeTypeReference(baselineEntry.UnderlyingType, aliases), NormalizeTypeReference(generatedEnum.UnderlyingType, aliases), StringComparison.Ordinal) ||
                baselineEntry.IsNative != generatedEnum.IsNative)
            {
                failures.Add(new AuditFinding(
                    Category: "attribute-drift",
                    Severity: "failure",
                    Message: $"Enum metadata drift for '{baselineEntry.DisplayName}'. Baseline underlying type='{baselineEntry.UnderlyingType}', native={baselineEntry.IsNative}. Fresh underlying type='{generatedEnum.UnderlyingType}', native={generatedEnum.IsNative}.",
                    TypeName: baselineEntry.DisplayName,
                    MemberName: null,
                    Selector: null,
                    BaselineFile: baselineEntry.SourceFile,
                    GeneratedFile: generatedEnum.SourceFile,
                    ComparisonTypeKey: baselineEntry.ComparisonKey));
            }

            foreach (var baselineMemberEntry in baselineEntry.Members)
            {
                if (!TryGetComparableEnumMember(generatedEnum, baselineMemberEntry.Key, aliases, out var generatedMember))
                {
                    failures.Add(new AuditFinding(
                        Category: "stale-baseline-binding",
                        Severity: "failure",
                        Message: $"Enum member '{baselineMemberEntry.Key}' on '{baselineEntry.DisplayName}' exists in the baseline but not in fresh output.",
                        TypeName: baselineEntry.DisplayName,
                        MemberName: baselineMemberEntry.Key,
                        Selector: baselineMemberEntry.Value.FieldValue,
                        BaselineFile: baselineMemberEntry.Value.SourceFile,
                        GeneratedFile: null,
                        ComparisonTypeKey: baselineEntry.ComparisonKey,
                        ComparisonMemberKey: baselineMemberEntry.Key));
                    continue;
                }

                if (!string.Equals(baselineMemberEntry.Value.FieldValue, generatedMember.FieldValue, StringComparison.Ordinal))
                {
                    failures.Add(new AuditFinding(
                        Category: "attribute-drift",
                        Severity: "failure",
                        Message: $"Enum member drift for '{baselineEntry.DisplayName}.{baselineMemberEntry.Key}'. Baseline field='{baselineMemberEntry.Value.FieldValue}'. Fresh field='{generatedMember.FieldValue}'.",
                        TypeName: baselineEntry.DisplayName,
                        MemberName: baselineMemberEntry.Key,
                        Selector: baselineMemberEntry.Value.FieldValue ?? generatedMember.FieldValue,
                        BaselineFile: baselineMemberEntry.Value.SourceFile,
                        GeneratedFile: generatedMember.SourceFile,
                        ComparisonTypeKey: baselineEntry.ComparisonKey,
                        ComparisonMemberKey: baselineMemberEntry.Key));
                }
            }

            foreach (var generatedMemberEntry in generatedEnum.Members)
            {
                if (ContainsEquivalentEnumMember(baselineEntry, generatedMemberEntry.Key, aliases))
                {
                    continue;
                }

                failures.Add(new AuditFinding(
                    Category: "missing-baseline-binding",
                    Severity: "failure",
                    Message: $"Enum member '{generatedMemberEntry.Key}' on '{generatedEnum.DisplayName}' exists in fresh output but not in the baseline.",
                    TypeName: generatedEnum.DisplayName,
                    MemberName: generatedMemberEntry.Key,
                    Selector: generatedMemberEntry.Value.FieldValue,
                    BaselineFile: null,
                    GeneratedFile: generatedMemberEntry.Value.SourceFile,
                    ComparisonTypeKey: generatedEnum.ComparisonKey,
                    ComparisonMemberKey: generatedMemberEntry.Key));
            }
        }

        foreach (var generatedEntry in generated.Values)
        {
            if (ContainsEquivalentEntry(baseline, generatedEntry.ComparisonKey, aliases))
            {
                continue;
            }

            failures.Add(new AuditFinding(
                Category: "missing-baseline-binding",
                Severity: "failure",
                Message: $"Enum '{generatedEntry.DisplayName}' exists in fresh output but not in the baseline.",
                TypeName: generatedEntry.DisplayName,
                MemberName: null,
                Selector: null,
                BaselineFile: null,
                GeneratedFile: generatedEntry.SourceFile,
                ComparisonTypeKey: generatedEntry.ComparisonKey));
        }
    }

    private static bool TryGetComparableEnumMember(
        EnumSurface enumSurface,
        string memberKey,
        SymbolAliasLookup aliases,
        out EnumMemberSurface member)
    {
        if (enumSurface.Members.TryGetValue(memberKey, out member!))
        {
            return true;
        }

        var normalizedMemberKey = NormalizeEnumMemberKey(memberKey, enumSurface, aliases);
        foreach (var candidate in enumSurface.Members)
        {
            if (string.Equals(NormalizeEnumMemberKey(candidate.Key, enumSurface, aliases), normalizedMemberKey, StringComparison.Ordinal))
            {
                member = candidate.Value;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEquivalentEnumMember(
        EnumSurface enumSurface,
        string memberKey,
        SymbolAliasLookup aliases)
    {
        return TryGetComparableEnumMember(enumSurface, memberKey, aliases, out _);
    }

    private static string NormalizeEnumMemberKey(string memberKey, EnumSurface enumSurface, SymbolAliasLookup aliases)
    {
        var normalizedMember = SymbolAliasLookup.NormalizeLookupKey(StripObjectiveCPrefix(memberKey));
        foreach (var enumName in GetEnumNameVariants(enumSurface, aliases).OrderByDescending(static name => name.Length))
        {
            var normalizedEnumName = SymbolAliasLookup.NormalizeLookupKey(StripObjectiveCPrefix(enumName));
            if (normalizedMember.Length > normalizedEnumName.Length &&
                normalizedMember.StartsWith(normalizedEnumName, StringComparison.Ordinal))
            {
                return normalizedMember[normalizedEnumName.Length..];
            }
        }

        return normalizedMember;
    }

    private static IEnumerable<string> GetEnumNameVariants(EnumSurface enumSurface, SymbolAliasLookup aliases)
    {
        yield return enumSurface.ComparisonKey;
        yield return enumSurface.Name;
        yield return enumSurface.DisplayName.Split('.').Last();

        foreach (var equivalentKey in aliases.GetEquivalentComparisonKeys(enumSurface.ComparisonKey))
        {
            yield return equivalentKey;
            yield return equivalentKey.Split('.').Last();
        }
    }

    private static string StripObjectiveCPrefix(string symbol)
    {
        foreach (var prefix in new[] { "FIR", "ABT" })
        {
            if (symbol.Length > prefix.Length &&
                symbol.StartsWith(prefix, StringComparison.Ordinal) &&
                char.IsUpper(symbol[prefix.Length]))
            {
                return symbol[prefix.Length..];
            }
        }

        return symbol;
    }

    private static string NormalizeTypeReference(string? typeName, SymbolAliasLookup aliases)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var compact = typeName.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (compact.EndsWith("[]", StringComparison.Ordinal))
        {
            return $"{NormalizeTypeReference(compact[..^2], aliases)}[]";
        }

        if (compact.EndsWith("?", StringComparison.Ordinal))
        {
            return $"{NormalizeTypeReference(compact[..^1], aliases)}?";
        }

        var genericStart = compact.IndexOf('<');
        if (genericStart >= 0 && compact.EndsWith(">", StringComparison.Ordinal))
        {
            var outer = compact[..genericStart];
            var arguments = compact[(genericStart + 1)..^1];
            var normalizedOuter = aliases.NormalizeSimpleType(outer);
            if (IsFoundationCollectionType(normalizedOuter))
            {
                return normalizedOuter;
            }

            return $"{normalizedOuter}<{string.Join(",", SplitGenericArguments(arguments).Select(argument => NormalizeTypeReference(argument, aliases)))}>";
        }

        return aliases.NormalizeSimpleType(compact);
    }

    private static bool IsFoundationCollectionType(string normalizedTypeName)
    {
        return string.Equals(normalizedTypeName, "nsarray", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsmutablearray", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsdictionary", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsmutabledictionary", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsset", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsmutableset", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsorderedset", StringComparison.Ordinal) ||
               string.Equals(normalizedTypeName, "nsmutableorderedset", StringComparison.Ordinal);
    }

    private static bool TryMatchActionSmoothing(
        string baselineType,
        string generatedType,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        ISet<string> smoothedDelegateKeys,
        out string note)
    {
        if (TryMatchActionSmoothingDirection(baselineType, generatedType, baselineDelegates, generatedDelegates, aliases, out var baselineNote, out var baselineDelegate))
        {
            smoothedDelegateKeys.Add(baselineDelegate.ComparisonKey);
            note = baselineNote;
            return true;
        }

        if (TryMatchActionSmoothingDirection(generatedType, baselineType, baselineDelegates, generatedDelegates, aliases, out var generatedNote, out var generatedDelegate))
        {
            smoothedDelegateKeys.Add(generatedDelegate.ComparisonKey);
            note = generatedNote;
            return true;
        }

        note = string.Empty;
        return false;
    }

    private static bool TryMatchActionSmoothingDirection(
        string delegateType,
        string actionType,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        out string note,
        out DelegateSurface delegateSurface)
    {
        if (!TryGetDelegateSurface(delegateType, baselineDelegates, generatedDelegates, aliases, out delegateSurface) ||
            !TryGetActionParameterTypes(actionType, out var actionParameterTypes))
        {
            note = string.Empty;
            return false;
        }

        if (!string.Equals(NormalizeTypeReference(delegateSurface.ReturnType, aliases), "void", StringComparison.Ordinal) ||
            delegateSurface.Parameters.Count != actionParameterTypes.Count)
        {
            note = string.Empty;
            return false;
        }

        for (var index = 0; index < delegateSurface.Parameters.Count; index++)
        {
            if (!string.Equals(
                    NormalizeTypeReference(delegateSurface.Parameters[index].Type, aliases),
                    NormalizeTypeReference(actionParameterTypes[index], aliases),
                    StringComparison.Ordinal))
            {
                note = string.Empty;
                return false;
            }
        }

        note = $"named delegate '{delegateSurface.Name}' is equivalent to '{actionType}'";
        return true;
    }

    private static bool TryGetDelegateSurface(
        string typeName,
        IReadOnlyDictionary<string, DelegateSurface> baselineDelegates,
        IReadOnlyDictionary<string, DelegateSurface> generatedDelegates,
        SymbolAliasLookup aliases,
        out DelegateSurface delegateSurface)
    {
        var compact = typeName.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (compact.EndsWith("?", StringComparison.Ordinal))
        {
            compact = compact[..^1];
        }

        return TryGetComparableEntry(baselineDelegates, compact, aliases, out delegateSurface) ||
               TryGetComparableEntry(generatedDelegates, compact, aliases, out delegateSurface);
    }

    private static bool TryGetActionParameterTypes(string typeName, out IReadOnlyList<string> parameterTypes)
    {
        var compact = typeName.Replace("global::", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        if (compact.EndsWith("?", StringComparison.Ordinal))
        {
            compact = compact[..^1];
        }

        if (string.Equals(compact, "Action", StringComparison.Ordinal) ||
            string.Equals(compact, "System.Action", StringComparison.Ordinal))
        {
            parameterTypes = [];
            return true;
        }

        var genericStart = compact.IndexOf('<');
        if (genericStart < 0 || !compact.EndsWith(">", StringComparison.Ordinal))
        {
            parameterTypes = [];
            return false;
        }

        var outer = compact[..genericStart].Split('.').Last();
        if (!string.Equals(outer, "Action", StringComparison.Ordinal))
        {
            parameterTypes = [];
            return false;
        }

        parameterTypes = SplitGenericArguments(compact[(genericStart + 1)..^1]).ToList();
        return true;
    }

    private static IEnumerable<string> SplitGenericArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            yield break;
        }

        var depth = 0;
        var builder = new StringBuilder();

        foreach (var character in arguments)
        {
            switch (character)
            {
                case '<':
                    depth++;
                    builder.Append(character);
                    break;
                case '>':
                    depth--;
                    builder.Append(character);
                    break;
                case ',' when depth == 0:
                    yield return builder.ToString();
                    builder.Clear();
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        if (builder.Length > 0)
        {
            yield return builder.ToString();
        }
    }

    private static string CreateManualLookupKey(string typeKey, string memberKey)
    {
        return $"{typeKey}|{memberKey}";
    }

    private static bool TryGetComparableEntry<T>(
        IReadOnlyDictionary<string, T> entries,
        string comparisonKey,
        SymbolAliasLookup aliases,
        out T entry)
    {
        return aliases.TryGetComparableEntry(entries, comparisonKey, out entry);
    }

    private static bool ContainsEquivalentEntry<T>(
        IReadOnlyDictionary<string, T> entries,
        string comparisonKey,
        SymbolAliasLookup aliases)
    {
        return TryGetComparableEntry(entries, comparisonKey, aliases, out _);
    }
}

internal sealed record TargetComparisonResult(
    IReadOnlyList<AuditFinding> Failures,
    IReadOnlyList<AuditFinding> Infos);
