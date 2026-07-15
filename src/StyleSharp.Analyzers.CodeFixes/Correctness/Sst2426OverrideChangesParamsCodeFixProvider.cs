// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Makes an override parameter's <c>params</c> modifier match the base (SST2426) by adding it when the base
/// has it, or removing it when the base does not.
/// </summary>
/// <remarks>
/// Because an override's <c>params</c> modifier is inert, toggling it is behaviour-preserving; the edit only
/// makes the declaration honest. The reported parameter already disagrees with the base, so flipping its own
/// modifier is what brings the two into line.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2426OverrideChangesParamsCodeFixProvider))]
[Shared]
public sealed class Sst2426OverrideChangesParamsCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.OverrideChangesParams.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Match the base's params modifier",
            nameof(Sst2426OverrideChangesParamsCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported parameter and toggles its <c>params</c> modifier.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The parameter replacement, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<ParameterSyntax>() is not { Type: { } type } parameter)
        {
            return null;
        }

        return new NodeReplacement(parameter, Toggle(parameter, type));
    }

    /// <summary>Adds or removes the <c>params</c> modifier, moving the shared trivia between it and the type.</summary>
    /// <param name="parameter">The parameter to rewrite.</param>
    /// <param name="type">The parameter's type.</param>
    /// <returns>The rewritten parameter.</returns>
    private static ParameterSyntax Toggle(ParameterSyntax parameter, TypeSyntax type)
    {
        for (var i = 0; i < parameter.Modifiers.Count; i++)
        {
            if (parameter.Modifiers[i].IsKind(SyntaxKind.ParamsKeyword))
            {
                var paramsToken = parameter.Modifiers[i];
                return parameter
                    .WithModifiers(parameter.Modifiers.RemoveAt(i))
                    .WithType(type.WithLeadingTrivia(paramsToken.LeadingTrivia));
            }
        }

        var added = SyntaxFactory.Token(type.GetLeadingTrivia(), SyntaxKind.ParamsKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));
        return parameter
            .WithType(type.WithLeadingTrivia())
            .WithModifiers(parameter.Modifiers.Add(added));
    }
}
