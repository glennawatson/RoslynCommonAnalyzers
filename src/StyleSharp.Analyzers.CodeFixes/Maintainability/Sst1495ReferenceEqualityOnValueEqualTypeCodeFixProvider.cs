// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Rewrites a reference comparison of a value-equal type as a call to the static
/// <c>object.Equals(object, object)</c> (SST1495): <c>a == b</c> becomes <c>object.Equals(a, b)</c> and
/// <c>a != b</c> becomes <c>!object.Equals(a, b)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The static two-argument form is used rather than the instance <c>a.Equals(b)</c> because it is null-safe:
/// it returns <see langword="true"/> for two nulls and <see langword="false"/> when only one side is null,
/// where the instance call would throw on a null receiver. The operator being replaced never threw, so a fix
/// that can is not a fix.
/// </para>
/// <para>
/// It is written out as <c>object.Equals</c>, not as a bare <c>Equals</c>, so that it cannot bind to
/// anything else. An unqualified two-argument <c>Equals</c> in a type that happens to declare its own
/// two-argument <c>Equals</c> would silently call that one instead — which is precisely the class of quiet
/// mis-binding this rule exists to remove.
/// </para>
/// <para>
/// The rewritten call is then bound speculatively at the call site and checked to have landed on
/// <c>System.Object.Equals(object, object)</c> before the fix is offered at all. Writing the name is not the
/// same as proving where it goes: a local named <c>object</c> is impossible, but an extension method, a
/// shadowing type, or a framework without the overload would each be enough to make the replacement mean
/// something other than what it says — and a fix that changes meaning is not a fix.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider))]
[Shared]
public sealed class Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <summary>The parameter count of the static <c>object.Equals(object, object)</c> the rewrite must reach.</summary>
    private const int StaticEqualsParameterCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArrays.Of(MaintainabilityRules.ReferenceEqualityOnValueEqualType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

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
            if (!TryGetComparison(root, model, diagnostic, context.CancellationToken, out var comparison))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Compare with object.Equals",
                    _ => Task.FromResult(Apply(context.Document, root, comparison!)),
                    equivalenceKey: nameof(Sst1495ReferenceEqualityOnValueEqualTypeCodeFixProvider)),
                diagnostic);
        }
    }

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
    {
        if (!TryGetComparison(editor.OriginalRoot, editor.SemanticModel, diagnostic, CancellationToken.None, out var comparison))
        {
            return;
        }

        editor.ReplaceNode(comparison!, BuildEqualsCall(comparison!));
    }

    /// <summary>Replaces the reported comparison with an <c>object.Equals</c> call.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <returns>The updated document.</returns>
    internal static Document Apply(Document document, SyntaxNode root, BinaryExpressionSyntax comparison)
        => document.WithSyntaxRoot(root.ReplaceNode(comparison, BuildEqualsCall(comparison)));

    /// <summary>Resolves the diagnostic to a comparison whose rewrite provably calls the framework's Equals.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="comparison">The reported comparison when the rewrite is safe.</param>
    /// <returns><see langword="true"/> when the reported shape still matches and the rewrite binds as intended.</returns>
    private static bool TryGetComparison(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out BinaryExpressionSyntax? comparison)
    {
        comparison = root.FindNode(diagnostic.Location.SourceSpan) as BinaryExpressionSyntax;
        if (comparison is not null
            && (comparison.IsKind(SyntaxKind.EqualsExpression) || comparison.IsKind(SyntaxKind.NotEqualsExpression))
            && BindsToObjectEquals(model, comparison, cancellationToken))
        {
            return true;
        }

        comparison = null;
        return false;
    }

    /// <summary>Returns whether the rewritten call resolves to <c>System.Object.Equals(object, object)</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="comparison">The reported comparison.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the replacement means what it is meant to mean.</returns>
    private static bool BindsToObjectEquals(SemanticModel model, BinaryExpressionSyntax comparison, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var speculative = model.GetSpeculativeSymbolInfo(
            comparison.SpanStart,
            BuildInvocation(comparison),
            SpeculativeBindingOption.BindAsExpression);

        return speculative.Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: nameof(Equals),
            Parameters.Length: StaticEqualsParameterCount,
            ContainingType.SpecialType: SpecialType.System_Object,
        };
    }

    /// <summary>Builds the <c>object.Equals(a, b)</c> call that replaces the comparison.</summary>
    /// <param name="comparison">The reported comparison.</param>
    /// <returns>The replacement expression, negated for a <c>!=</c> comparison.</returns>
    private static ExpressionSyntax BuildEqualsCall(BinaryExpressionSyntax comparison)
    {
        ExpressionSyntax call = BuildInvocation(comparison);
        if (comparison.IsKind(SyntaxKind.NotEqualsExpression))
        {
            call = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, call);
        }

        return call.WithTriviaFrom(comparison);
    }

    /// <summary>Builds the bare <c>object.Equals(a, b)</c> invocation, without the negation or the trivia.</summary>
    /// <param name="comparison">The reported comparison.</param>
    /// <returns>The invocation, which is also what gets bound speculatively.</returns>
    private static InvocationExpressionSyntax BuildInvocation(BinaryExpressionSyntax comparison)
    {
        var arguments = SyntaxFactory.SeparatedList<ArgumentSyntax>(
            [SyntaxFactory.Argument(Bare(comparison.Left)), SyntaxFactory.Argument(Bare(comparison.Right))],
            [SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(SyntaxFactory.Space)]);

        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
                SyntaxFactory.IdentifierName(nameof(Equals))),
            SyntaxFactory.ArgumentList(arguments));
    }

    /// <summary>Strips the trivia an operand carried around the operator it no longer sits beside.</summary>
    /// <param name="operand">The comparison operand.</param>
    /// <returns>The operand with no surrounding trivia.</returns>
    private static ExpressionSyntax Bare(ExpressionSyntax operand) => operand.WithoutLeadingTrivia().WithoutTrailingTrivia();
}
