// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Fixes a culture-sensitive custom date/time format (SST2445) two ways. "Quote the separators" wraps each
/// unquoted <c>/</c> and <c>:</c> in single quotes so the format keeps a fixed shape under any culture, and
/// applies to both the method-call and interpolated-string shapes. "Use the invariant culture" replaces the
/// current-culture provider argument with the invariant culture, and applies to the method-call shape only.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst2445CultureSensitiveDateFormatCodeFixProvider))]
[Shared]
public sealed class Sst2445CultureSensitiveDateFormatCodeFixProvider : CodeFixProvider
{
    /// <summary>The equivalence key for the quote-separators fix.</summary>
    private const string QuoteEquivalenceKey = "SST2445.Quote";

    /// <summary>The equivalence key for the invariant-culture fix.</summary>
    private const string InvariantEquivalenceKey = "SST2445.Invariant";

    /// <summary>The replacement culture member.</summary>
    private const string InvariantCultureName = "InvariantCulture";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(CorrectnessRules.CultureSensitiveDateFormat.Id);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            RegisterQuoteFix(context, root, diagnostic);
            RegisterInvariantFix(context, root, diagnostic);
        }
    }

    /// <summary>Registers the fix that quotes the format's separators.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    private static void RegisterQuoteFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        var shape = GetShape(diagnostic);
        var document = context.Document;
        if (shape == Sst2445CultureSensitiveDateFormatAnalyzer.InvocationShape
            && root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is LiteralExpressionSyntax { Token.Value: string value } literal)
        {
            context.RegisterCodeFix(
                CodeAction.Create("Quote the format separators", _ => Task.FromResult(QuoteLiteral(document, root, literal, value)), QuoteEquivalenceKey),
                diagnostic);
            return;
        }

        if (shape == Sst2445CultureSensitiveDateFormatAnalyzer.InterpolationShape)
        {
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent is InterpolationFormatClauseSyntax)
            {
                context.RegisterCodeFix(
                    CodeAction.Create("Quote the format separators", _ => Task.FromResult(QuoteFormatToken(document, root, token)), QuoteEquivalenceKey),
                    diagnostic);
            }
        }
    }

    /// <summary>Registers the fix that switches the provider to the invariant culture.</summary>
    /// <param name="context">The code fix context.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    private static void RegisterInvariantFix(CodeFixContext context, SyntaxNode root, Diagnostic diagnostic)
    {
        if (GetShape(diagnostic) != Sst2445CultureSensitiveDateFormatAnalyzer.InvocationShape
            || !diagnostic.Properties.TryGetValue(Sst2445CultureSensitiveDateFormatAnalyzer.ProviderSpanKey, out var spanText)
            || spanText is null
            || !TryParseSpan(spanText, out var providerSpan)
            || root.FindNode(providerSpan, getInnermostNodeForTie: true) is not ExpressionSyntax provider
            || BuildInvariant(provider) is not { } invariant)
        {
            return;
        }

        var document = context.Document;
        context.RegisterCodeFix(
            CodeAction.Create("Use the invariant culture", _ => Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(provider, invariant))), InvariantEquivalenceKey),
            diagnostic);
    }

    /// <summary>Replaces a string literal with its separator-quoted form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="literal">The format literal.</param>
    /// <param name="value">The literal's value.</param>
    /// <returns>The updated document.</returns>
    private static Document QuoteLiteral(Document document, SyntaxNode root, LiteralExpressionSyntax literal, string value)
    {
        var quoted = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(DateFormatText.QuoteSeparators(value)))
            .WithTriviaFrom(literal);
        return document.WithSyntaxRoot(root.ReplaceNode(literal, quoted));
    }

    /// <summary>Replaces an interpolation format token with its separator-quoted form.</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="root">The syntax root.</param>
    /// <param name="token">The format token.</param>
    /// <returns>The updated document.</returns>
    private static Document QuoteFormatToken(Document document, SyntaxNode root, SyntaxToken token)
    {
        var quoted = DateFormatText.QuoteSeparators(token.ValueText);
        var replacement = SyntaxFactory.Token(token.LeadingTrivia, token.Kind(), quoted, quoted, token.TrailingTrivia);
        return document.WithSyntaxRoot(root.ReplaceToken(token, replacement));
    }

    /// <summary>Builds the invariant-culture provider from a current-culture provider expression.</summary>
    /// <param name="provider">The current-culture provider expression.</param>
    /// <returns>The invariant-culture expression, or <see langword="null"/> for an unexpected shape.</returns>
    private static ExpressionSyntax? BuildInvariant(ExpressionSyntax provider)
    {
        var invariant = SyntaxFactory.IdentifierName(InvariantCultureName);
        return provider switch
        {
            MemberAccessExpressionSyntax access => access.WithName(invariant),
            IdentifierNameSyntax identifier => invariant.WithTriviaFrom(identifier),
            _ => null,
        };
    }

    /// <summary>Reads the shape property from a diagnostic.</summary>
    /// <param name="diagnostic">The diagnostic to read.</param>
    /// <returns>The shape value, or an empty string when absent.</returns>
    private static string GetShape(Diagnostic diagnostic)
        => diagnostic.Properties.TryGetValue(Sst2445CultureSensitiveDateFormatAnalyzer.ShapeKey, out var shape) && shape is not null
            ? shape
            : string.Empty;

    /// <summary>Parses a <c>start:length</c> span from a diagnostic property.</summary>
    /// <param name="text">The formatted span.</param>
    /// <param name="span">The parsed span.</param>
    /// <returns><see langword="true"/> when the span parses.</returns>
    private static bool TryParseSpan(string text, out TextSpan span)
    {
        span = default;
        var separator = text.IndexOf(':');
        if (separator < 0
            || !int.TryParse(text.Substring(0, separator), out var start)
            || !int.TryParse(text.Substring(separator + 1), out var length))
        {
            return false;
        }

        span = new TextSpan(start, length);
        return true;
    }
}
