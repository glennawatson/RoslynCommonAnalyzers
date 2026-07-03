// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Combines a <c>ContainsKey</c> guard and the indexer reads it protects into one
/// <c>TryGetValue</c> call (PSH1104): the guard becomes
/// <c>receiver.TryGetValue(key, out var value)</c> and every guarded read of
/// <c>receiver[key]</c> becomes the out variable. The variable is named <c>value</c>
/// unless that identifier already appears in the enclosing member, in which case the
/// deterministic fallback <c>dictValue</c> is used.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1104UseTryGetValueCodeFixProvider))]
[Shared]
public sealed class Psh1104UseTryGetValueCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The preferred out-variable name.</summary>
    private const string DefaultValueName = "value";

    /// <summary>The fallback out-variable name when <c>value</c> is already taken.</summary>
    private const string FallbackValueName = "dictValue";

    /// <summary>An <c>out</c> keyword followed by one space, reused across fixes.</summary>
    private static readonly SyntaxToken OutKeywordToken = SyntaxFactory.Token(default, SyntaxKind.OutKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));

    /// <summary>A comma followed by one space, reused across fixes.</summary>
    private static readonly SyntaxToken CommaWithSpaceToken = SyntaxFactory.Token(default, SyntaxKind.CommaToken, SyntaxFactory.TriviaList(SyntaxFactory.Space));

    /// <summary>A <c>var</c> type name followed by one space, reused across fixes.</summary>
    private static readonly IdentifierNameSyntax VarTypeName = SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(default, "var", SyntaxFactory.TriviaList(SyntaxFactory.Space)));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CollectionRules.UseTryGetValue.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Use TryGetValue",
                    cancellationToken => Task.FromResult(Apply(context.Document, root, invocation)),
                    equivalenceKey: nameof(Psh1104UseTryGetValueCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (editor.OriginalRoot.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation
            || !Psh1104UseTryGetValueAnalyzer.TryGetGuardShape(invocation, out var shape))
        {
            return;
        }

        var valueName = GetValueName(invocation);
        editor.ReplaceNode(invocation, CreateTryGetValueInvocation(invocation, valueName));
        var reads = CollectGuardedReads(shape);
        for (var i = 0; i < reads.Count; i++)
        {
            editor.ReplaceNode(reads[i], SyntaxFactory.IdentifierName(valueName).WithTriviaFrom(reads[i]));
        }
    }

    /// <summary>Rewrites the reported guard and its protected indexer reads to a TryGetValue form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="invocation">The reported ContainsKey invocation.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, InvocationExpressionSyntax invocation)
    {
        if (!Psh1104UseTryGetValueAnalyzer.TryGetGuardShape(invocation, out var shape))
        {
            return document;
        }

        var valueName = GetValueName(invocation);
        var reads = CollectGuardedReads(shape);
        var nodes = new SyntaxNode[reads.Count + 1];
        for (var i = 0; i < reads.Count; i++)
        {
            nodes[i] = reads[i];
        }

        nodes[reads.Count] = invocation;
        var updatedRoot = root.ReplaceNodes(nodes, (original, _) => CreateReplacementNode(original, invocation, valueName));
        return document.WithSyntaxRoot(updatedRoot);
    }

    /// <summary>Returns the replacement for one rewritten node.</summary>
    /// <param name="original">The original node being replaced.</param>
    /// <param name="invocation">The reported ContainsKey invocation.</param>
    /// <param name="valueName">The chosen out-variable name.</param>
    /// <returns>The TryGetValue guard for the invocation, or the out variable for a guarded read.</returns>
    private static SyntaxNode CreateReplacementNode(SyntaxNode original, InvocationExpressionSyntax invocation, string valueName)
        => original == invocation
            ? CreateTryGetValueInvocation(invocation, valueName)
            : SyntaxFactory.IdentifierName(valueName).WithTriviaFrom(original);

    /// <summary>Builds the <c>receiver.TryGetValue(key, out var name)</c> replacement for the guard.</summary>
    /// <param name="invocation">The reported ContainsKey invocation.</param>
    /// <param name="valueName">The chosen out-variable name.</param>
    /// <returns>The rewritten invocation.</returns>
    private static InvocationExpressionSyntax CreateTryGetValueInvocation(InvocationExpressionSyntax invocation, string valueName)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        var tryGetValueName = SyntaxFactory.IdentifierName(Psh1104UseTryGetValueAnalyzer.TryGetValueMethodName).WithTriviaFrom(memberAccess.Name);
        var outArgument = SyntaxFactory.Argument(
            nameColon: null,
            refKindKeyword: OutKeywordToken,
            expression: SyntaxFactory.DeclarationExpression(
                VarTypeName,
                SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(valueName))));

        var arguments = SyntaxFactory.SeparatedList(
            [invocation.ArgumentList.Arguments[0], outArgument],
            [CommaWithSpaceToken]);

        return invocation
            .WithExpression(memberAccess.WithName(tryGetValueName))
            .WithArgumentList(invocation.ArgumentList.WithArguments(arguments));
    }

    /// <summary>Collects every guarded indexer read matching the guard's receiver and key.</summary>
    /// <param name="shape">The validated guard shape.</param>
    /// <returns>The matching element accesses in document order.</returns>
    private static List<ElementAccessExpressionSyntax> CollectGuardedReads(in Psh1104UseTryGetValueAnalyzer.GuardShape shape)
    {
        var state = new ReadCollectorState(shape.Receiver, shape.Key, new List<ElementAccessExpressionSyntax>(4));
        CollectRegion(shape.FirstRegion, ref state);
        if (shape.SecondRegion is { } secondRegion)
        {
            CollectRegion(secondRegion, ref state);
        }

        return state.Reads;
    }

    /// <summary>Collects matching indexer reads in one guarded region (including its root node).</summary>
    /// <param name="region">The guarded region root.</param>
    /// <param name="state">The collector state.</param>
    private static void CollectRegion(SyntaxNode region, ref ReadCollectorState state)
    {
        if (region is ElementAccessExpressionSyntax elementAccess
            && Psh1104UseTryGetValueAnalyzer.IsMatchingElementAccess(elementAccess, state.Receiver, state.Key))
        {
            state.Reads.Add(elementAccess);
        }

        DescendantTraversalHelper.VisitDescendants<ElementAccessExpressionSyntax, ReadCollectorState>(region, ref state, VisitElementAccess);
    }

    /// <summary>Records one matching element access encountered during collection.</summary>
    /// <param name="elementAccess">The visited element access.</param>
    /// <param name="state">The collector state.</param>
    /// <returns><see langword="true"/> to continue collecting.</returns>
    private static bool VisitElementAccess(ElementAccessExpressionSyntax elementAccess, ref ReadCollectorState state)
    {
        if (!Psh1104UseTryGetValueAnalyzer.IsMatchingElementAccess(elementAccess, state.Receiver, state.Key))
        {
            return true;
        }

        state.Reads.Add(elementAccess);
        return true;
    }

    /// <summary>Chooses the deterministic out-variable name for one fix.</summary>
    /// <param name="invocation">The reported ContainsKey invocation.</param>
    /// <returns><c>value</c>, or <c>dictValue</c> when an identifier named <c>value</c> already appears in the enclosing member.</returns>
    private static string GetValueName(InvocationExpressionSyntax invocation)
    {
        var scope = GetEnclosingScope(invocation);
        var hasValueIdentifier = false;
        DescendantTraversalHelper.VisitDescendantTokens(scope, ref hasValueIdentifier, VisitIdentifierToken);
        return hasValueIdentifier ? FallbackValueName : DefaultValueName;
    }

    /// <summary>Returns the enclosing member declaration, or the tree root when there is none.</summary>
    /// <param name="node">The node whose scope to resolve.</param>
    /// <returns>The name-conflict scan scope.</returns>
    private static SyntaxNode GetEnclosingScope(SyntaxNode node)
    {
        var scope = node;
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            scope = current;
            if (current is MemberDeclarationSyntax)
            {
                break;
            }
        }

        return scope;
    }

    /// <summary>Stops the token scan once an identifier named <c>value</c> is found.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="hasValueIdentifier">Set when a conflicting identifier is found.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitIdentifierToken(in SyntaxToken token, ref bool hasValueIdentifier)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != DefaultValueName)
        {
            return true;
        }

        hasValueIdentifier = true;
        return false;
    }

    /// <summary>Captures the state required while collecting guarded indexer reads.</summary>
    /// <param name="Receiver">The guard's receiver expression.</param>
    /// <param name="Key">The guard's key expression.</param>
    /// <param name="Reads">The matching element accesses collected so far.</param>
    private readonly record struct ReadCollectorState(
        ExpressionSyntax Receiver,
        ExpressionSyntax Key,
        List<ElementAccessExpressionSyntax> Reads);
}
