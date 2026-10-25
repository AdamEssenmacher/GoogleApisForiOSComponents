using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FirebaseReleaseFingerprint;

public static class BuildInputs
{
    static CompilationUnitSyntax Parse(string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(kind: SourceCodeKind.Script));
        var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("Cannot fingerprint invalid Cake syntax: " + string.Join("; ", errors.Select(e => e.ToString())));
        return tree.GetCompilationUnitRoot();
    }

    public static Dictionary<string, string> ArtifactSymbols(string components)
    {
        // Read identities only. Cake remains the authority for the dependency closure.
        return Parse(components).DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(v => v.Initializer?.Value is ObjectCreationExpressionSyntax c && c.Type.ToString() == "Artifact")
            .ToDictionary(v => v.Identifier.ValueText, v =>
                ((LiteralExpressionSyntax)((ObjectCreationExpressionSyntax)v.Initializer!.Value).ArgumentList!.Arguments[0].Expression).Token.ValueText);
    }

    public static string Normalize(IReadOnlyDictionary<string, string> sources, HashSet<string> excludedArtifacts)
    {
        var rewriter = new ScopeRewriter(excludedArtifacts);
        var roots = sources.ToDictionary(p => p.Key, p => (CompilationUnitSyntax)rewriter.Visit(Parse(p.Value))!);
        var methods = roots.Values.SelectMany(r => r.Members.OfType<MethodDeclarationSyntax>()).ToArray();
        var byName = methods.GroupBy(m => m.Identifier.ValueText).ToDictionary(g => g.Key, g => g.ToArray());
        // Remove only methods referenced from excluded branches, including their private helper chain.
        // Keep unrelated declarations conservatively; keep any helper also referenced by retained code.
        var candidates = Closure(rewriter.RemovedReferences, byName);
        var retainedReferences = roots.Values.SelectMany(r => r.Members)
            .Where(m => m is not MethodDeclarationSyntax method || !candidates.Contains(method.Identifier.ValueText))
            .SelectMany(References).ToHashSet();
        candidates.ExceptWith(Closure(retainedReferences, byName));
        var result = new SortedDictionary<string, object>();
        foreach (var (path, root) in roots)
        {
            var kept = root.RemoveNodes(root.Members.OfType<MethodDeclarationSyntax>()
                .Where(m => candidates.Contains(m.Identifier.ValueText)), SyntaxRemoveOptions.KeepNoTrivia)!;
            kept = kept.ReplaceTrivia(kept.DescendantTrivia(descendIntoTrivia: true)
                .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)), (_, _) => default);
            // Token boundaries and literal text matter; comments and whitespace trivia do not.
            // Directives and inactive preprocessor text stay conservative, rather than disappearing.
            result[path] = new {
                tokens = kept.DescendantTokens(descendIntoTrivia: true).Select(t => new { kind = t.RawKind, text = t.Text }),
                inactive = kept.DescendantTrivia(descendIntoTrivia: true)
                    .Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia)).Select(t => t.ToFullString())
            };
        }
        return JsonSerializer.Serialize(result);
    }

    static IEnumerable<string> References(SyntaxNode node) =>
        node.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Select(n => n.Identifier.ValueText);

    static HashSet<string> Closure(IEnumerable<string> names, Dictionary<string, MethodDeclarationSyntax[]> methods)
    {
        var seen = new HashSet<string>();
        var pending = new Queue<string>(names);
        while (pending.TryDequeue(out var name))
            if (methods.TryGetValue(name, out var declarations) && seen.Add(name))
                foreach (var reference in declarations.SelectMany(References))
                    pending.Enqueue(reference);
        return seen;
    }

    sealed class ScopeRewriter(HashSet<string> excluded) : CSharpSyntaxRewriter
    {
        public HashSet<string> RemovedReferences { get; } = [];

        public override SyntaxNode? VisitGlobalStatement(GlobalStatementSyntax node)
        {
            var statement = (StatementSyntax?)Visit(node.Statement);
            return statement is null ? null : node.WithStatement(statement);
        }

        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            if (node.Condition is InvocationExpressionSyntax {
                    Expression: MemberAccessExpressionSyntax {
                        Expression: IdentifierNameSyntax receiver, Name.Identifier.ValueText: "Contains" },
                    ArgumentList.Arguments.Count: 1 } call
                && receiver.Identifier.ValueText == "ARTIFACTS_TO_BUILD"
                && call.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax artifact
                && excluded.Contains(artifact.Identifier.ValueText))
            {
                RemovedReferences.UnionWith(References(node.Statement));
                if (node.Else is not null)
                    return Visit(node.Else.Statement);
                return node.Parent is BlockSyntax or GlobalStatementSyntax ? null : SyntaxFactory.EmptyStatement();
            }
            return base.VisitIfStatement(node);
        }

        public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            // These values are already exported per selected artifact by Cake metadata.
            if (node.Identifier.ValueText == "ExternalDownloads" && node.Initializer is not null)
                return node.WithInitializer(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
            return base.VisitVariableDeclarator(node);
        }
    }
}
