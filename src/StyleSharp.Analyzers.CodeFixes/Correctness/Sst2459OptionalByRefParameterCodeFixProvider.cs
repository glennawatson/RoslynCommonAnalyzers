// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the <c>[Optional]</c> attribute from a <c>ref</c> or <c>out</c> parameter (SST2459).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2459OptionalByRefParameterCodeFixProvider))]
[Shared]
public sealed class Sst2459OptionalByRefParameterCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.OptionalByRefParameter.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(
            context,
            "Remove the [Optional] attribute",
            nameof(Sst2459OptionalByRefParameterCodeFixProvider),
            TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported attribute and removes it from its parameter.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The node to replace, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan).FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute
            || attribute.Parent is not AttributeListSyntax list)
        {
            return null;
        }

        // The list holds more than [Optional]: drop the one attribute and keep the rest of the list intact.
        if (list.Attributes.Count > 1)
        {
            return list.RemoveNode(attribute, SyntaxRemoveOptions.KeepNoTrivia) is { } trimmedList
                ? new NodeReplacement(list, trimmedList)
                : null;
        }

        // [Optional] stood alone: remove the whole list, keeping any leading trivia so the parameter's
        // own indentation survives.
        return list.Parent is ParameterSyntax parameter
            && parameter.RemoveNode(list, SyntaxRemoveOptions.KeepLeadingTrivia) is { } trimmedParameter
            ? new NodeReplacement(parameter, trimmedParameter)
            : null;
    }
}
