// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Replaces a parameterless <c>new EventArgs()</c> with <c>EventArgs.Empty</c> (PSH1022). The type is
/// written back exactly as the author wrote it — <c>new System.EventArgs()</c> becomes
/// <c>System.EventArgs.Empty</c> — and a target-typed <c>new()</c>, which named nothing, becomes
/// <c>EventArgs.Empty</c>, which the analyzer has already confirmed resolves at that position.
/// </summary>
/// <remarks>
/// The replacement is an expression, so it fits wherever the allocation did: an argument to a
/// <c>Raise</c>/<c>Invoke</c> call, a field initializer, a local, a returned value.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1022PreferEventArgsEmptyCodeFixProvider))]
[Shared]
public sealed class Psh1022PreferEventArgsEmptyCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.PreferEventArgsEmpty.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Use EventArgs.Empty",
            nameof(Psh1022PreferEventArgsEmptyCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported allocation and builds its replacement.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
        => root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is BaseObjectCreationExpressionSyntax creation
            && Psh1022PreferEventArgsEmptyAnalyzer.IsParameterlessCreationShape(creation)
            ? new NodeReplacement(creation, Rewrite(creation))
            : null;

    /// <summary>Builds the <c>EventArgs.Empty</c> access, reusing the type name the author wrote.</summary>
    /// <param name="creation">The reported allocation.</param>
    /// <returns>The replacement expression.</returns>
    private static MemberAccessExpressionSyntax Rewrite(BaseObjectCreationExpressionSyntax creation)
    {
        var type = creation is ObjectCreationExpressionSyntax { Type: NameSyntax name }
            ? TypeNameExpression.From(name.WithoutTrivia())
            : SyntaxFactory.IdentifierName(Psh1022PreferEventArgsEmptyAnalyzer.EventArgsTypeName);

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            type,
            SyntaxFactory.IdentifierName(Psh1022PreferEventArgsEmptyAnalyzer.EmptyFieldName))
            .WithTriviaFrom(creation);
    }
}
