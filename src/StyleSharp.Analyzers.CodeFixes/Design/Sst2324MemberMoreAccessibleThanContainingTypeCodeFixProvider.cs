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
public sealed class Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider : CodeFixProvider
{
    /// <summary>Batches this fix's edits across a document.</summary>
    private static readonly BatchEditFixAllProvider FixAll = new(TryRewrite);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(DesignRules.MemberMoreAccessibleThanContainingType.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => FixAll;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context) =>
        ReplaceNodeCodeFix.RegisterAsync(context, "Narrow the member to its container's accessibility", nameof(Sst2324MemberMoreAccessibleThanContainingTypeCodeFixProvider), CanRewrite, TryRewrite);

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
        for (var start = 0; start < keywords.Length;)
        {
            var end = keywords.IndexOf(' ', start);
            if (end < 0)
            {
                end = keywords.Length;
            }

            if (KeywordKind(keywords.AsSpan(start, end - start)) is { } kind)
            {
                tokens.Add(SyntaxFactory.Token(default, kind, SyntaxFactory.TriviaList(SyntaxFactory.Space)));
            }

            start = end + 1;
        }
    }

    /// <summary>Gets the token kind of one accessibility keyword.</summary>
    /// <param name="keyword">The keyword text.</param>
    /// <returns>The token kind, or <see langword="null"/> when the text names no access modifier.</returns>
    private static SyntaxKind? KeywordKind(ReadOnlySpan<char> keyword) => keyword switch
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
        if (!TryResolve(root, diagnostic, out var target, out var declaration))
        {
            return null;
        }

        var narrowed = WithAccessibility(declaration, target);
        return narrowed == declaration ? null : new NodeReplacement(declaration, narrowed);
    }

    /// <summary>Resolves the reported member and the accessibility the analyzer named for it.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <param name="target">The accessibility to write, as its C# keywords.</param>
    /// <param name="declaration">The member carrying at least one modifier.</param>
    /// <returns><see langword="true"/> when both still resolve.</returns>
    private static bool TryResolve(
        SyntaxNode root,
        Diagnostic diagnostic,
        [NotNullWhen(true)] out string? target,
        [NotNullWhen(true)] out MemberDeclarationSyntax? declaration)
    {
        if (!diagnostic.Properties.TryGetValue(Sst2324MemberMoreAccessibleThanContainingTypeAnalyzer.TargetAccessibilityKey, out target)
            || target is not { Length: > 0 })
        {
            declaration = null;
            return false;
        }

        declaration = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as MemberDeclarationSyntax;
        return declaration is { Modifiers.Count: > 0 };
    }

    /// <summary>Checks the access modifiers without constructing a narrowed declaration.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to resolve.</param>
    /// <returns>Whether the reported member has access modifiers to replace.</returns>
    private static bool CanRewrite(SyntaxNode root, Diagnostic diagnostic)
    {
        if (!TryResolve(root, diagnostic, out var target, out var declaration))
        {
            return false;
        }

        var hasAccessModifier = false;
        var hasOtherModifier = false;
        foreach (var modifier in declaration.Modifiers)
        {
            if (IsAccessModifier(modifier))
            {
                hasAccessModifier = true;
            }
            else
            {
                hasOtherModifier = true;
            }
        }

        return hasAccessModifier && (hasOtherModifier || HasAccessibilityKeyword(target));
    }

    /// <summary>Checks whether the target contributes any access modifier tokens.</summary>
    /// <param name="target">The target accessibility's keyword text.</param>
    /// <returns>Whether at least one keyword is an access modifier.</returns>
    private static bool HasAccessibilityKeyword(string target)
    {
        for (var start = 0; start < target.Length;)
        {
            var end = target.IndexOf(' ', start);
            if (end < 0)
            {
                end = target.Length;
            }

            if (KeywordKind(target.AsSpan(start, end - start)) is not null)
            {
                return true;
            }

            start = end + 1;
        }

        return false;
    }
}
