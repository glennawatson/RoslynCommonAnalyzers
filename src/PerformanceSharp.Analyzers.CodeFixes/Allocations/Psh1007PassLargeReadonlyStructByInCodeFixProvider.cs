// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Adds the <c>in</c> modifier to a reported by-value parameter (PSH1007). The modifier becomes the
/// parameter's first token, so the type's leading trivia moves onto it and the parameter keeps its place
/// in a wrapped parameter list.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Psh1007PassLargeReadonlyStructByInCodeFixProvider))]
[Shared]
public sealed class Psh1007PassLargeReadonlyStructByInCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(FindParameter, static (current, _) => AddInModifier((ParameterSyntax)current));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(AllocationRules.PassLargeReadonlyStructByIn.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        TargetCodeFix.RegisterAsync(context, "Pass the parameter by 'in' reference", nameof(Psh1007PassLargeReadonlyStructByInCodeFixProvider), FindParameter, AddInModifier);

    /// <summary>Builds the parameter with an <c>in</c> modifier ahead of its type.</summary>
    /// <param name="parameter">The by-value parameter to rewrite.</param>
    /// <returns>The parameter passed by readonly reference.</returns>
    internal static ParameterSyntax AddInModifier(ParameterSyntax parameter)
    {
        var type = parameter.Type!;
        var modifier = SyntaxFactory.Token(type.GetLeadingTrivia(), SyntaxKind.InKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        return parameter.Update(
            parameter.AttributeLists,
            SyntaxFactory.TokenList(modifier),
            type.WithLeadingTrivia(),
            parameter.Identifier,
            parameter.Default);
    }

    /// <summary>Resolves the reported by-value parameter that has a written type and no modifier.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The parameter, or <see langword="null"/> when the shape no longer matches.</returns>
    private static ParameterSyntax? FindParameter(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) is ParameterSyntax { Type: not null, Modifiers.Count: 0 } parameter
            ? parameter
            : null;
}
