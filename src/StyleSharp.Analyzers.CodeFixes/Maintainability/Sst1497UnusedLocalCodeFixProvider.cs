// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes a local nothing reads (SST1497) without removing what computing it did.
/// </summary>
/// <remarks>
/// <para>
/// Deleting the declaration is only half the job. The value may have come from a call that does something —
/// <c>var handle = Acquire();</c> — and the local may be written again further down, in statements that
/// would stop compiling once the variable is gone. So the fix rewrites every one of them together, and each
/// assigned expression is treated the same way: an expression that provably has no side effects goes away
/// with the variable; a call, a <c>new</c>, an <c>await</c> or an assignment survives as a statement of its
/// own; anything else that still has to run is kept as a discard, <c>_ = expression;</c>.
/// </para>
/// <para>
/// An <c>out var</c> becomes <c>out _</c> — the callee still assigns it, the caller just stops naming what
/// it will not read.
/// </para>
/// <para>
/// Where no such rewrite is safe the fix stays out of the way rather than guessing: a variable declared
/// alongside others whose initializer must be preserved has nowhere to leave it, and a <c>stackalloc</c>
/// cannot be discarded. The diagnostic remains, because the local really is unused; only the automatic edit
/// is withheld.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1497UnusedLocalCodeFixProvider))]
[Shared]
public sealed class Sst1497UnusedLocalCodeFixProvider : CodeFixProvider
{
    /// <summary>The discard, which names nothing and reads nothing.</summary>
    private const string DiscardName = "_";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.UnusedLocal.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (TryBuildEdits(root, model, diagnostic, context.CancellationToken) is not { } edits)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove the unused local",
                    _ => Task.FromResult(ApplyEdits(context.Document, root, edits)),
                    equivalenceKey: nameof(Sst1497UnusedLocalCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Removes one reported local, keeping whatever its value came from.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The reported local.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document, or the original when no safe rewrite exists.</returns>
    internal static Document Apply(
        Document document,
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
        => TryBuildEdits(root, model, diagnostic, cancellationToken) is { } edits
            ? ApplyEdits(document, root, edits)
            : document;

    /// <summary>Applies every edit the fix computed for one unused local.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="edits">The declaration edit and the dead-write edits.</param>
    /// <returns>The updated document.</returns>
    /// <remarks>The edits are spread across the enclosing block, so the nodes are tracked before the first one moves.</remarks>
    private static Document ApplyEdits(Document document, SyntaxNode root, List<LocalEdit> edits)
    {
        var originals = new SyntaxNode[edits.Count];
        for (var i = 0; i < edits.Count; i++)
        {
            originals[i] = edits[i].Original;
        }

        var updated = root.TrackNodes(originals);
        for (var i = 0; i < edits.Count; i++)
        {
            if (updated.GetCurrentNode(edits[i].Original) is not { } current)
            {
                continue;
            }

            updated = edits[i].Replacement is { } replacement
                ? updated.ReplaceNode(current, replacement)
                : updated.RemoveNode(current, SyntaxRemoveOptions.KeepUnbalancedDirectives) ?? updated;
        }

        return document.WithSyntaxRoot(updated);
    }

    /// <summary>Plans every edit needed to remove one unused local, or nothing when no safe plan exists.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The reported local.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The edits, or <see langword="null"/> when the local cannot be removed mechanically.</returns>
    private static List<LocalEdit>? TryBuildEdits(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var declaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent;
        if (declaration is null || Sst1497UnusedLocalAnalyzer.GetScope(declaration) is not { } scope)
        {
            return null;
        }

        if (BuildDeclarationEdit(declaration, model, cancellationToken) is not { } declarationEdit
            || model.GetDeclaredSymbol(declaration, cancellationToken) is not ILocalSymbol local)
        {
            return null;
        }

        var edits = new List<LocalEdit>(2) { declarationEdit };
        return TryAddDeadWriteEdits(scope, local, model, edits, cancellationToken) ? edits : null;
    }

    /// <summary>Plans the edit that removes the declaration itself.</summary>
    /// <param name="declaration">The declarator or out-variable designation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The edit, or <see langword="null"/> when the declaration cannot be rewritten safely.</returns>
    private static LocalEdit? BuildDeclarationEdit(SyntaxNode declaration, SemanticModel model, CancellationToken cancellationToken)
    {
        if (declaration is SingleVariableDesignationSyntax { Parent: DeclarationExpressionSyntax outVariable })
        {
            return new LocalEdit(outVariable, SyntaxFactory.IdentifierName(DiscardName).WithTriviaFrom(outVariable));
        }

        if (declaration is not VariableDeclaratorSyntax variable
            || variable.Parent is not VariableDeclarationSyntax variableDeclaration
            || variableDeclaration.Parent is not LocalDeclarationStatementSyntax statement)
        {
            return null;
        }

        var initializer = variable.Initializer?.Value;
        if (variableDeclaration.Variables.Count > 1)
        {
            // The other variables still need the declaration, so an initializer that has to survive has
            // nowhere to go and the fix steps aside.
            return initializer is null || IsRemovable(initializer) ? new LocalEdit(variable, replacement: null) : null;
        }

        return BuildKeptExpressionEdit(statement, initializer, model, cancellationToken);
    }

    /// <summary>Plans the edit for a statement whose only purpose was to assign the unused local.</summary>
    /// <param name="statement">The declaration statement or the dead-write statement.</param>
    /// <param name="expression">The assigned expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The edit, or <see langword="null"/> when the expression can neither be dropped nor kept.</returns>
    private static LocalEdit? BuildKeptExpressionEdit(
        StatementSyntax statement,
        ExpressionSyntax? expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (expression is null || IsRemovable(expression))
        {
            // A top-level statement is wrapped in a global statement, and a global statement with nothing
            // inside it is not a node — so the wrapper is what has to go.
            var target = statement.Parent is GlobalStatementSyntax global ? (SyntaxNode)global : statement;
            return new LocalEdit(target, replacement: null);
        }

        var kept = expression.WithoutLeadingTrivia().WithoutTrailingTrivia();
        if (IsStatementExpression(expression))
        {
            return new LocalEdit(statement, SyntaxFactory.ExpressionStatement(kept).WithTriviaFrom(statement));
        }

        if (!IsDiscardable(expression, model, cancellationToken))
        {
            return null;
        }

        var discard = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName(DiscardName).WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.EqualsToken).WithTrailingTrivia(SyntaxFactory.Space),
            kept);
        return new LocalEdit(statement, SyntaxFactory.ExpressionStatement(discard).WithTriviaFrom(statement));
    }

    /// <summary>Plans an edit for every <c>local = value;</c> statement left behind by the removal.</summary>
    /// <param name="scope">The syntax that bounds the local.</param>
    /// <param name="local">The unused local.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="edits">The edit list to add to.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="false"/> when one of the dead writes cannot be rewritten safely.</returns>
    /// <remarks>
    /// The analyzer already proved that nothing reads the local, so every reference left in the scope is a
    /// write of this shape. Each one is still bound before it is touched: a name is not proof, and rewriting
    /// an assignment to some other variable would be worse than not fixing anything.
    /// </remarks>
    private static bool TryAddDeadWriteEdits(
        SyntaxNode scope,
        ILocalSymbol local,
        SemanticModel model,
        List<LocalEdit> edits,
        CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier
                || identifier.Identifier.ValueText != local.Name
                || !Sst1497UnusedLocalAnalyzer.IsDeadWriteTarget(identifier, out var statement))
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, local))
            {
                continue;
            }

            var assignment = (AssignmentExpressionSyntax)identifier.Parent!;
            if (BuildKeptExpressionEdit(statement!, assignment.Right, model, cancellationToken) is not { } edit)
            {
                return false;
            }

            edits.Add(edit);
        }

        return true;
    }

    /// <summary>Returns whether an expression can simply be deleted along with the local.</summary>
    /// <param name="expression">The assigned expression.</param>
    /// <returns><see langword="true"/> when evaluating it cannot change what the program does.</returns>
    /// <remarks>
    /// A lambda or anonymous method is removable even though it is not a plain read: writing one down only
    /// creates a delegate, and the body never runs unless something invokes it — and nothing can, because
    /// nothing reads the local.
    /// </remarks>
    private static bool IsRemovable(ExpressionSyntax expression)
        => expression is AnonymousFunctionExpressionSyntax || SideEffectFreeExpression.IsSideEffectFree(expression);

    /// <summary>Returns whether an expression is one the language allows as a statement on its own.</summary>
    /// <param name="expression">The assigned expression.</param>
    /// <returns><see langword="true"/> for a call, a creation, an await, an assignment, and an increment.</returns>
    private static bool IsStatementExpression(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or AwaitExpressionSyntax or AssignmentExpressionSyntax => true,
        PrefixUnaryExpressionSyntax prefix => IsIncrementOrDecrement(prefix.Kind()),
        PostfixUnaryExpressionSyntax postfix => IsIncrementOrDecrement(postfix.Kind()),
        _ => false,
    };

    /// <summary>Returns whether a unary operator kind increments or decrements its operand.</summary>
    /// <param name="kind">The unary expression's kind.</param>
    /// <returns><see langword="true"/> for <c>++</c> and <c>--</c> in either position.</returns>
    private static bool IsIncrementOrDecrement(SyntaxKind kind) => kind is SyntaxKind.PreIncrementExpression
        or SyntaxKind.PreDecrementExpression
        or SyntaxKind.PostIncrementExpression
        or SyntaxKind.PostDecrementExpression;

    /// <summary>Returns whether an expression can be assigned to a discard.</summary>
    /// <param name="expression">The assigned expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression has a type of its own for the discard to take.</returns>
    /// <remarks>
    /// The discard itself is a C# 7 feature, so a project pinned to an older language version cannot have it
    /// written into its source — the fix falls back to whatever it can do without one, and steps aside when
    /// that is nothing. A discard also has no declared type, so it can only take an expression that already
    /// has one: a <c>stackalloc</c> takes its type from the target it is assigned to and has none of its own.
    /// </remarks>
    private static bool IsDiscardable(ExpressionSyntax expression, SemanticModel model, CancellationToken cancellationToken)
    {
        if (expression is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax
            || !SupportsDiscard(expression))
        {
            return false;
        }

        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        return type is { TypeKind: not TypeKind.Error } && type.SpecialType != SpecialType.System_Void;
    }

    /// <summary>Returns whether the tree's language version has discards.</summary>
    /// <param name="node">A node of the tree being fixed.</param>
    /// <returns><see langword="true"/> for C# 7 and later.</returns>
    private static bool SupportsDiscard(SyntaxNode node)
        => node.SyntaxTree.Options is CSharpParseOptions { LanguageVersion: >= LanguageVersion.CSharp7 };

    /// <summary>One node the fix removes or replaces.</summary>
    internal readonly record struct LocalEdit
    {
        /// <summary>Initializes a new instance of the <see cref="LocalEdit"/> struct.</summary>
        /// <param name="original">The node to rewrite.</param>
        /// <param name="replacement">The replacement, or <see langword="null"/> to remove the node.</param>
        public LocalEdit(SyntaxNode original, SyntaxNode? replacement)
        {
            Original = original;
            Replacement = replacement;
        }

        /// <summary>Gets the node to rewrite.</summary>
        public SyntaxNode Original { get; }

        /// <summary>Gets the replacement, or <see langword="null"/> when the node is removed outright.</summary>
        public SyntaxNode? Replacement { get; }
    }
}
