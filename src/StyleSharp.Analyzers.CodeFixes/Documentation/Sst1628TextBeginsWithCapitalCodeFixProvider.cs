// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Capitalizes the first letter of a reported documentation summary (SST1628). Only a summary whose own
/// first content is text is rewritten, so a summary opening with an element keeps whatever that element
/// renders.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1628TextBeginsWithCapitalCodeFixProvider))]
[Shared]
public sealed class Sst1628TextBeginsWithCapitalCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DocumentationRules.TextBeginsWithCapital.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Begin the summary with a capital letter", nameof(Sst1628TextBeginsWithCapitalCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Gets the first content node that carries a visible character.</summary>
    /// <param name="summary">The summary element.</param>
    /// <returns>The leading text node, or <see langword="null"/> when the summary opens with something else.</returns>
    private static XmlTextSyntax? LeadingText(XmlElementSyntax summary)
    {
        for (var i = 0; i < summary.Content.Count; i++)
        {
            if (summary.Content[i] is not XmlTextSyntax text)
            {
                return null;
            }

            if (FirstVisibleToken(text) >= 0)
            {
                return text;
            }
        }

        return null;
    }

    /// <summary>Gets the index of the first token holding a non-whitespace character.</summary>
    /// <param name="text">The text node.</param>
    /// <returns>The token index, or -1 when every token is whitespace.</returns>
    private static int FirstVisibleToken(XmlTextSyntax text)
    {
        var tokens = text.TextTokens;
        for (var i = 0; i < tokens.Count; i++)
        {
            if (FirstVisibleCharacter(tokens[i].ValueText) >= 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gets the index of the first non-whitespace character.</summary>
    /// <param name="value">The token text.</param>
    /// <returns>The character index, or -1 when the text is blank.</returns>
    private static int FirstVisibleCharacter(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Resolves the reported summary and builds it with a capitalized first letter.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        // A documentation comment is trivia, so the lookup has to descend into it to reach the element.
        if (root.FindNode(diagnostic.Location.SourceSpan, findInsideTrivia: true, getInnermostNodeForTie: true) is not XmlElementSyntax summary
            || LeadingText(summary) is not { } text)
        {
            return null;
        }

        var tokens = text.TextTokens;
        var index = FirstVisibleToken(text);
        var token = tokens[index];
        var value = token.ValueText;
        var at = FirstVisibleCharacter(value);
        if (!char.IsLower(value[at]))
        {
            return null;
        }

        var capitalized = value.Substring(0, at) + char.ToUpperInvariant(value[at]) + value.Substring(at + 1);
        var replacement = SyntaxFactory.XmlTextLiteral(token.LeadingTrivia, capitalized, capitalized, token.TrailingTrivia);
        return new NodeReplacement(text, text.WithTextTokens(tokens.Replace(token, replacement)));
    }
}
