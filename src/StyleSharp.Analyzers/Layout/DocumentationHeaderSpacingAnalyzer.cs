// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports the spacing around an element's documentation header: a header that is not
/// preceded by a blank line (SST1514) and a header that is followed by a blank line
/// before its element (SST1506). A header that opens a block (directly after an opening
/// brace) is exempt from the preceding-blank-line requirement.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DocumentationHeaderSpacingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The member kinds whose documentation header spacing is checked.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.ConstructorDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        LayoutRules.DocHeaderPrecededByBlankLine,
        LayoutRules.DocHeaderNotFollowedByBlankLine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Checks the blank-line spacing before and after a member's documentation header.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!LayoutHelpers.TryGetDocHeader(member, out var header))
        {
            return;
        }

        var text = context.Node.SyntaxTree.GetText(context.CancellationToken);
        var headerFirstLine = LayoutHelpers.LineOf(text, header.SpanStart);
        var headerLastLine = LayoutHelpers.LineOf(text, header.Span.End - 1);
        var memberLine = LayoutHelpers.StartLine(text, member.GetFirstToken());

        if (memberLine > headerLastLine + 1 && HasBlankLineBetween(text, headerLastLine, memberLine))
        {
            context.ReportDiagnostic(Diagnostic.Create(LayoutRules.DocHeaderNotFollowedByBlankLine, member.GetFirstToken().GetLocation()));
        }

        if (!PrecededByBlankLineRequired(text, member, headerFirstLine))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(LayoutRules.DocHeaderPrecededByBlankLine, member.GetFirstToken().GetLocation()));
    }

    /// <summary>
    /// Returns whether at least one genuinely blank line sits between the header and its element.
    /// The line span between them can be occupied entirely by a preprocessor directive (e.g. an
    /// <c>#if</c> guarding the declaration), which is not a blank line and must not be reported.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="headerLastLine">The last line of the documentation header.</param>
    /// <param name="memberLine">The line on which the element begins.</param>
    /// <returns><see langword="true"/> when a whitespace-only line separates the header and element.</returns>
    private static bool HasBlankLineBetween(SourceText text, int headerLastLine, int memberLine)
    {
        for (var line = headerLastLine + 1; line < memberLine; line++)
        {
            if (LayoutHelpers.IsBlankLine(text, line))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the header lacks a required preceding blank line.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="member">The documented member.</param>
    /// <param name="headerFirstLine">The first line of the documentation header.</param>
    /// <returns><see langword="true"/> when a blank line should precede the header but does not.</returns>
    private static bool PrecededByBlankLineRequired(SourceText text, MemberDeclarationSyntax member, int headerFirstLine)
    {
        var previous = member.GetFirstToken().GetPreviousToken();
        return !previous.IsKind(SyntaxKind.None)
               && !previous.IsKind(SyntaxKind.OpenBraceToken)
               && headerFirstLine <= LayoutHelpers.EndLine(text, previous) + 1;
    }
}
