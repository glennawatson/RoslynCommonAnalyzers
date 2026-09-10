// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Narrows a reported member's access modifiers to the accessibility its container can actually deliver
/// (SST2324). The analyzer names that accessibility in the diagnostic's properties, so the fix writes it
/// where the old modifiers stood and leaves every other modifier in place.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider))]
[Shared]
public sealed class Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider : CodeFixProvider, IBatchFixableCodeFix
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.MemberMoreAccessibleThanContainingType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => BatchEditFixAllProvider.Instance;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
        => ReplaceNodeCodeFix.RegisterAsync(context, "Narrow the member to its container's accessibility", nameof(Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider), TryRewrite);

    /// <inheritdoc/>
    void IBatchFixableCodeFix.RegisterBatchEdits(DocumentEditor editor, Diagnostic diagnostic)
        => ReplaceNodeCodeFix.ApplyBatchEdit(editor, diagnostic, TryRewrite);

    /// <summary>Rewrites a member's access modifiers to the named accessibility.</summary>
    /// <param name="declaration">The member to narrow.</param>
    /// <param name="keywords">The accessibility to write, as its C# keywords.</param>
    /// <returns>The narrowed member.</returns>
    /// <remarks>
    /// The member's leading trivia sits on the first modifier, so it is re-seated on whichever token ends up
    /// first. Modifiers that carry no accessibility keep their own trivia and their order.
    /// </remarks>
    internal static MemberDeclarationSyntax WithAccessibility(MemberDeclarationSyntax declaration, string keywords)
    {
        var modifiers = declaration.Modifiers;
        var leading = modifiers[0].LeadingTrivia;
        var rebuilt = new List<SyntaxToken>(modifiers.Count + 1);
        var written = false;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (!IsAccessModifier(modifier))
            {
                rebuilt.Add(modifier.WithLeadingTrivia());
                continue;
            }

            if (written)
            {
                continue;
            }

            written = true;
            AppendKeywords(rebuilt, keywords);
        }

        if (!written || rebuilt.Count == 0)
        {
            return declaration;
        }

        rebuilt[0] = rebuilt[0].WithLeadingTrivia(leading);
        return declaration.WithModifiers(SyntaxFactory.TokenList(rebuilt));
    }

    /// <summary>Returns whether a modifier is one of the access modifiers.</summary>
    /// <param name="modifier">The modifier token.</param>
    /// <returns><see langword="true"/> for <c>public</c>, <c>private</c>, <c>protected</c>, and <c>internal</c>.</returns>
    private static bool IsAccessModifier(SyntaxToken modifier) => modifier.Kind() is SyntaxKind.PublicKeyword
        or SyntaxKind.PrivateKeyword
        or SyntaxKind.ProtectedKeyword
        or SyntaxKind.InternalKeyword;

    /// <summary>Appends the keyword tokens of an accessibility, each trailed by a space.</summary>
    /// <param name="tokens">The token list being built.</param>
    /// <param name="keywords">The accessibility as its C# keywords, separated by spaces.</param>
    private static void AppendKeywords(List<SyntaxToken> tokens, string keywords)
    {
        var parts = keywords.Split(' ');
        for (var i = 0; i < parts.Length; i++)
        {
            if (KeywordKind(parts[i]) is { } kind)
            {
                tokens.Add(SyntaxFactory.Token(default, kind, SyntaxFactory.TriviaList(SyntaxFactory.Space)));
            }
        }
    }

    /// <summary>Gets the token kind of one accessibility keyword.</summary>
    /// <param name="keyword">The keyword text.</param>
    /// <returns>The token kind, or <see langword="null"/> when the text names no access modifier.</returns>
    private static SyntaxKind? KeywordKind(string keyword) => keyword switch
    {
        "public" => SyntaxKind.PublicKeyword,
        "private" => SyntaxKind.PrivateKeyword,
        "protected" => SyntaxKind.ProtectedKeyword,
        "internal" => SyntaxKind.InternalKeyword,
        _ => null,
    };

    /// <summary>Resolves the reported member and builds it with the narrowed modifiers.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>The nodes to swap, or <see langword="null"/> when the shape no longer matches.</returns>
    private static NodeReplacement? TryRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer.TargetAccessibilityKey, out var target)
            || string.IsNullOrEmpty(target)
            || root.FindToken(diagnostic.Location.SourceSpan.Start).Parent is not MemberDeclarationSyntax declaration
            || declaration.Modifiers.Count == 0)
        {
            return null;
        }

        var narrowed = WithAccessibility(declaration, target!);
        return narrowed == declaration ? null : new NodeReplacement(declaration, narrowed);
    }
}
