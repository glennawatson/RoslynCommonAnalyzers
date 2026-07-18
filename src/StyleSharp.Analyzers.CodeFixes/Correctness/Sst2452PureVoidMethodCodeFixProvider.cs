// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Removes the pure-contract attribute from a method with no observable result (SST2452). The
/// attribute is advisory metadata, so deleting it never changes what the method does. When the
/// attribute shares a list with others only the attribute is deleted; when it stands alone its
/// whole list goes, and a leading documentation comment stays with the method.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2452PureVoidMethodCodeFixProvider))]
[Shared]
public sealed class Sst2452PureVoidMethodCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.PureMethodWithoutResult.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Remove the [Pure] attribute", nameof(Sst2452PureVoidMethodCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Resolves the reported attribute and builds the edit that removes it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (root.FindNode(diagnostic.Location.SourceSpan)?.FirstAncestorOrSelf<AttributeSyntax>() is not { } attribute
            || !Sst2452PureVoidMethodAnalyzer.IsPureAttributeName(attribute.Name)
            || attribute.Parent is not AttributeListSyntax list)
        {
            return null;
        }

        if (list.Attributes.Count > 1)
        {
            return new NodeReplacement(list, list.WithAttributes(list.Attributes.Remove(attribute)));
        }

        if (list.Parent is not MethodDeclarationSyntax method)
        {
            return null;
        }

        var index = method.AttributeLists.IndexOf(list);
        var replacement = method.WithAttributeLists(method.AttributeLists.RemoveAt(index));
        if (index == 0)
        {
            // The first list's leading trivia is the method's own — indentation and any
            // documentation comment — so it moves onto the new first token instead of vanishing.
            replacement = replacement.WithLeadingTrivia(method.GetLeadingTrivia());
        }

        return new NodeReplacement(method, replacement);
    }
}
