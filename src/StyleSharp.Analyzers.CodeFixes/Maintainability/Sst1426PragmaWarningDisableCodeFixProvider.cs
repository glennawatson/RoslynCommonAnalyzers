// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Replaces a <c>#pragma warning disable</c> directive (SST1426) with a scoped <c>[SuppressMessage]</c>
/// attribute on the nearest enclosing declaration, removing the matching <c>restore</c> directive. The
/// fix is offered only when every code on the directive is a suppressible analyzer id; a directive that
/// also carries a compiler (<c>CS</c>) code is left alone, because only a pragma can disable those.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Sst1426PragmaWarningDisableCodeFixProvider))]
[Shared]
public sealed class Sst1426PragmaWarningDisableCodeFixProvider : CodeFixProvider
{
    /// <summary>The fully-qualified SuppressMessage type, emitted so the fix needs no <c>using</c>.</summary>
    private const string SuppressMessageType = "System.Diagnostics.CodeAnalysis.SuppressMessage";

    /// <summary>The placeholder justification, matching the IDE's built-in suppression fix.</summary>
    private const string PendingJustification = "<Pending>";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArrays.Of(MaintainabilityRules.PreferSuppressMessageOverPragma.Id);

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
            var trivia = root.FindTrivia(diagnostic.Location.SourceSpan.Start);
            if (trivia.GetStructure() is not PragmaWarningDirectiveTriviaSyntax disable
                || ContainsCompilerCode(disable)
                || trivia.Token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is null)
            {
                continue;
            }

            var span = diagnostic.Location.SourceSpan;
            context.RegisterCodeFix(
                CodeAction.Create(
                    "Replace '#pragma warning disable' with [SuppressMessage]",
                    cancellationToken => ReplaceAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(Sst1426PragmaWarningDisableCodeFixProvider)),
                diagnostic);
        }
    }

    /// <summary>Moves the codes of an analyzer-only disable directive onto a member-level [SuppressMessage].</summary>
    /// <param name="document">The document being fixed.</param>
    /// <param name="disableSpan">The source span of the disable directive.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated document.</returns>
    private static async Task<Document> ReplaceAsync(Document document, TextSpan disableSpan, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null
            || root.FindTrivia(disableSpan.Start).GetStructure() is not PragmaWarningDirectiveTriviaSyntax disable
            || root.FindTrivia(disableSpan.Start).Token.Parent?.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member)
        {
            return document;
        }

        var movedCodes = new List<string>(disable.ErrorCodes.Count);
        for (var i = 0; i < disable.ErrorCodes.Count; i++)
        {
            movedCodes.Add(PragmaWarningHelper.CodeText(disable.ErrorCodes[i]));
        }

        var restore = FindMatchingRestore(disable, movedCodes);
        var restoreStart = restore?.SpanStart;

        // Annotate the member so it can be re-found after the directive trivia is removed. Annotations do
        // not change text, so the directive spans stay valid on the annotated tree.
        var memberAnnotation = new SyntaxAnnotation();
        root = root.ReplaceNode(member, member.WithAdditionalAnnotations(memberAnnotation));

        var removals = new HashSet<SyntaxTrivia>();
        CollectDirectiveLine(root.FindTrivia(disableSpan.Start), removals);
        if (restoreStart is { } start)
        {
            CollectDirectiveLine(root.FindTrivia(start), removals);
        }

        root = root.ReplaceTrivia(removals, (_, _) => default);

        using var annotated = root.GetAnnotatedNodes(memberAnnotation).GetEnumerator();
        if (!annotated.MoveNext() || annotated.Current is not MemberDeclarationSyntax target)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(target, AddSuppressions(target, movedCodes)));
    }

    /// <summary>Returns whether a directive lists any compiler (<c>CS</c> or numeric) code.</summary>
    /// <param name="directive">The pragma directive.</param>
    /// <returns><see langword="true"/> when at least one code is a compiler warning.</returns>
    private static bool ContainsCompilerCode(PragmaWarningDirectiveTriviaSyntax directive)
    {
        var codes = directive.ErrorCodes;
        for (var i = 0; i < codes.Count; i++)
        {
            if (PragmaWarningHelper.IsCompilerWarningCode(codes[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds the nearest following <c>restore</c> directive that lists every moved code.</summary>
    /// <param name="disable">The disable directive.</param>
    /// <param name="moved">The codes being moved.</param>
    /// <returns>The matching restore directive, or <see langword="null"/> when none is found.</returns>
    private static PragmaWarningDirectiveTriviaSyntax? FindMatchingRestore(PragmaWarningDirectiveTriviaSyntax disable, List<string> moved)
    {
        for (var directive = disable.GetNextDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            if (directive is PragmaWarningDirectiveTriviaSyntax pragma
                && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.RestoreKeyword)
                && RestoreCoversAll(pragma, moved))
            {
                return pragma;
            }
        }

        return null;
    }

    /// <summary>Returns whether a restore directive lists every moved code.</summary>
    /// <param name="restore">The restore directive.</param>
    /// <param name="moved">The codes being moved.</param>
    /// <returns><see langword="true"/> when every moved code is present.</returns>
    private static bool RestoreCoversAll(PragmaWarningDirectiveTriviaSyntax restore, List<string> moved)
    {
        var codes = restore.ErrorCodes;
        for (var i = 0; i < moved.Count; i++)
        {
            var found = false;
            for (var j = 0; j < codes.Count; j++)
            {
                if (string.Equals(PragmaWarningHelper.CodeText(codes[j]), moved[i], StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Collects a directive's trivia plus its line's leading indentation for removal.</summary>
    /// <param name="directiveTrivia">The directive's trivia (its text already includes the trailing newline).</param>
    /// <param name="removals">The set of trivia to remove.</param>
    private static void CollectDirectiveLine(SyntaxTrivia directiveTrivia, HashSet<SyntaxTrivia> removals)
    {
        removals.Add(directiveTrivia);

        var leading = directiveTrivia.Token.LeadingTrivia;
        var index = leading.IndexOf(directiveTrivia);
        if (index <= 0 || !leading[index - 1].IsKind(SyntaxKind.WhitespaceTrivia))
        {
            return;
        }

        removals.Add(leading[index - 1]);
    }

    /// <summary>Prepends a [SuppressMessage] attribute list for each moved code to the member.</summary>
    /// <param name="member">The member to annotate.</param>
    /// <param name="movedCodes">The codes to suppress.</param>
    /// <returns>The member with the new attribute lists.</returns>
    private static MemberDeclarationSyntax AddSuppressions(MemberDeclarationSyntax member, List<string> movedCodes)
    {
        var leading = member.GetLeadingTrivia();
        var indent = IndentTrivia(leading);
        var endOfLine = SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine("\n"));

        var attributeLists = new AttributeListSyntax[movedCodes.Count];
        for (var i = 0; i < movedCodes.Count; i++)
        {
            var attribute = BuildAttribute(SuppressionCategoryResolver.Resolve(movedCodes[i]), movedCodes[i]);
            attributeLists[i] = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithLeadingTrivia(i == 0 ? leading : indent)
                .WithTrailingTrivia(endOfLine);
        }

        var relocated = member.WithLeadingTrivia(indent);
        return relocated.WithAttributeLists(relocated.AttributeLists.InsertRange(0, attributeLists));
    }

    /// <summary>Builds a <c>[SuppressMessage(category, code, Justification = "&lt;Pending&gt;")]</c> attribute.</summary>
    /// <param name="category">The rule category.</param>
    /// <param name="code">The rule id to suppress.</param>
    /// <returns>The attribute syntax.</returns>
    private static AttributeSyntax BuildAttribute(string category, string code)
    {
        var arguments = new[]
        {
            SyntaxFactory.AttributeArgument(StringLiteral(category)),
            SyntaxFactory.AttributeArgument(StringLiteral(code)).WithLeadingTrivia(SyntaxFactory.Space),
            SyntaxFactory.AttributeArgument(StringLiteral(PendingJustification))
                .WithNameEquals(SyntaxFactory.NameEquals("Justification"))
                .WithLeadingTrivia(SyntaxFactory.Space),
        };

        var separators = new[]
        {
            SyntaxFactory.Token(SyntaxKind.CommaToken),
            SyntaxFactory.Token(SyntaxKind.CommaToken),
        };

        return SyntaxFactory.Attribute(
            SyntaxFactory.ParseName(SuppressMessageType),
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SeparatedList(arguments, separators)));
    }

    /// <summary>Creates a string literal expression.</summary>
    /// <param name="value">The literal value.</param>
    /// <returns>The literal expression.</returns>
    private static LiteralExpressionSyntax StringLiteral(string value)
        => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value));

    /// <summary>Returns the indentation trivia (the whitespace immediately before the member) of its leading trivia.</summary>
    /// <param name="leading">The member's leading trivia.</param>
    /// <returns>The indentation trivia list, or an empty list when the member starts at column zero.</returns>
    private static SyntaxTriviaList IndentTrivia(SyntaxTriviaList leading)
        => leading.Count > 0 && leading[leading.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(leading[leading.Count - 1])
            : SyntaxTriviaList.Empty;
}
