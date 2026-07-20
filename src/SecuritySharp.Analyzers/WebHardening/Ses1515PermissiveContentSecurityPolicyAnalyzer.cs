// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a string literal that sets a <c>Content-Security-Policy</c> to a value that neuters it (SES1515). A CSP
/// exists mainly to block injected inline scripts; a policy whose <c>default-src</c>/<c>script-src</c>/
/// <c>style-src</c>/<c>object-src</c>/<c>base-uri</c> also carries <c>'unsafe-inline'</c>, <c>'unsafe-eval'</c>, or a
/// bare <c>*</c> source re-permits exactly what CSP is meant to stop, so the header is present but no longer
/// defends against cross-site scripting. Detection is local and framework-agnostic: the rule looks only at string
/// literals, and the clean path is skipped unless the literal contains one of the directive tokens above. A literal
/// that both contains a directive token and a permissive source is reported when it is the value set on a
/// <c>Content-Security-Policy</c> header -- the value argument of a two-argument header call
/// (<c>Headers.Add("Content-Security-Policy", value)</c>, <c>.Append("Content-Security-Policy", value)</c>) whose
/// name argument is that header (case-insensitively), or the right side of a
/// <c>Headers["Content-Security-Policy"] = value</c> indexer assignment -- or when the literal itself begins with a
/// directive token (a self-evident policy value). No semantic model is consulted, so a project using any web stack
/// pays only the directive-token scan on the clean path.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1515PermissiveContentSecurityPolicyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The header name whose value the rule guards, compared case-insensitively.</summary>
    private const string ContentSecurityPolicyHeaderName = "Content-Security-Policy";

    /// <summary>The <c>'unsafe-inline'</c> keyword source, matched case-insensitively including its quotes.</summary>
    private const string UnsafeInlineSource = "'unsafe-inline'";

    /// <summary>The <c>'unsafe-eval'</c> keyword source, matched case-insensitively including its quotes.</summary>
    private const string UnsafeEvalSource = "'unsafe-eval'";

    /// <summary>The message argument used when the policy carries <c>'unsafe-inline'</c>.</summary>
    private const string UnsafeInlineDisplay = "the 'unsafe-inline' source";

    /// <summary>The message argument used when the policy carries <c>'unsafe-eval'</c>.</summary>
    private const string UnsafeEvalDisplay = "the 'unsafe-eval' source";

    /// <summary>The message argument used when the policy carries a bare wildcard source.</summary>
    private const string WildcardDisplay = "a wildcard '*' source";

    /// <summary>The directive tokens that identify a string as a Content-Security-Policy value.</summary>
    private static readonly string[] DirectiveTokens =
        ["default-src", "script-src", "style-src", "object-src", "base-uri"];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.PermissiveContentSecurityPolicy);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports SES1515 for a permissive Content-Security-Policy value.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.ValueText;

        // Clean-path gate: only a literal that carries a CSP directive token is worth parsing further.
        if (!ContainsDirectiveToken(text))
        {
            return;
        }

        var permissiveSource = GetPermissiveSourceDisplay(text);
        if (permissiveSource is null)
        {
            return;
        }

        // Report only a self-evident policy value, or one wired into a Content-Security-Policy header set.
        if (!BeginsWithDirectiveToken(text) && !IsContentSecurityPolicyHeaderValue(literal))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.PermissiveContentSecurityPolicy,
            literal.SyntaxTree,
            literal.Span,
            permissiveSource));
    }

    /// <summary>Returns whether the text contains any CSP directive token anywhere.</summary>
    /// <param name="text">The literal's decoded text.</param>
    /// <returns><see langword="true"/> when a directive token is present.</returns>
    private static bool ContainsDirectiveToken(string text)
    {
        for (var i = 0; i < DirectiveTokens.Length; i++)
        {
            if (text.IndexOf(DirectiveTokens[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the text begins with a CSP directive token followed by a source boundary.</summary>
    /// <param name="text">The literal's decoded text.</param>
    /// <returns><see langword="true"/> when the value is a self-evident policy.</returns>
    private static bool BeginsWithDirectiveToken(string text)
    {
        for (var i = 0; i < DirectiveTokens.Length; i++)
        {
            var token = DirectiveTokens[i];
            if (token.Length <= text.Length
                && string.CompareOrdinal(text, 0, token, 0, token.Length) == 0
                && IsSourceBoundary(text, token.Length))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the display phrase for the first permissive source in the text, if any.</summary>
    /// <param name="text">The literal's decoded text.</param>
    /// <returns>The message argument, or <see langword="null"/> when no permissive source is present.</returns>
    private static string? GetPermissiveSourceDisplay(string text)
    {
        if (text.IndexOf(UnsafeInlineSource, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return UnsafeInlineDisplay;
        }

        if (text.IndexOf(UnsafeEvalSource, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return UnsafeEvalDisplay;
        }

        return ContainsBareWildcardSource(text) ? WildcardDisplay : null;
    }

    /// <summary>Returns whether the text contains a bare <c>*</c> source (excluding host wildcards like <c>*.example.com</c>).</summary>
    /// <param name="text">The literal's decoded text.</param>
    /// <returns><see langword="true"/> when a standalone <c>*</c> source is present.</returns>
    private static bool ContainsBareWildcardSource(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '*' && IsSourceBoundary(text, i - 1) && IsSourceBoundary(text, i + 1))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the character at the index separates two CSP sources (or is out of range).</summary>
    /// <param name="text">The literal's decoded text.</param>
    /// <param name="index">The index to test; a negative or past-end index counts as a boundary.</param>
    /// <returns><see langword="true"/> when the position bounds a source.</returns>
    private static bool IsSourceBoundary(string text, int index)
        => (uint)index >= (uint)text.Length || char.IsWhiteSpace(text[index]) || text[index] == ';';

    /// <summary>Returns whether the literal is the value set on a Content-Security-Policy header.</summary>
    /// <param name="literal">The candidate CSP value literal.</param>
    /// <returns><see langword="true"/> when the literal is a CSP header value.</returns>
    private static bool IsContentSecurityPolicyHeaderValue(LiteralExpressionSyntax literal)
        => literal.Parent switch
        {
            // 'headers["Content-Security-Policy"] = value'.
            AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax elementAccess }
                => IsContentSecurityPolicyName(GetIndexerHeaderName(elementAccess)),

            // 'headers.Add("Content-Security-Policy", value)' / '.Append("Content-Security-Policy", value)'.
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax } argumentList } argument
                => IsHeaderSetInvocation(argumentList, argument),

            _ => false,
        };

    /// <summary>Returns whether an invocation is a two-argument header set whose name is the CSP header and whose value is the literal.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <param name="valueArgument">The argument holding the candidate CSP value literal.</param>
    /// <returns><see langword="true"/> when the call sets the Content-Security-Policy header.</returns>
    private static bool IsHeaderSetInvocation(ArgumentListSyntax argumentList, ArgumentSyntax valueArgument)
    {
        var arguments = argumentList.Arguments;
        return arguments.Count == 2
            && arguments[1] == valueArgument
            && IsContentSecurityPolicyName(GetStringLiteralText(arguments[0].Expression));
    }

    /// <summary>Returns the single indexer argument's string value, if the access has exactly one string-literal key.</summary>
    /// <param name="elementAccess">The indexer access on the left of the assignment.</param>
    /// <returns>The key text, or <see langword="null"/> when it is not a single string-literal key.</returns>
    private static string? GetIndexerHeaderName(ElementAccessExpressionSyntax elementAccess)
    {
        var arguments = elementAccess.ArgumentList.Arguments;
        return arguments.Count == 1 ? GetStringLiteralText(arguments[0].Expression) : null;
    }

    /// <summary>Returns the decoded text of a string-literal expression, or <see langword="null"/> when it is not one.</summary>
    /// <param name="expression">The expression to read.</param>
    /// <returns>The string value, or <see langword="null"/>.</returns>
    private static string? GetStringLiteralText(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

    /// <summary>Returns whether a header name equals <c>Content-Security-Policy</c> case-insensitively.</summary>
    /// <param name="name">The header name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the name is the Content-Security-Policy header.</returns>
    private static bool IsContentSecurityPolicyName(string? name)
        => string.Equals(name, ContentSecurityPolicyHeaderName, StringComparison.OrdinalIgnoreCase);
}
