// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a member with more code lines than the configured maximum (SST1523), which defaults to 60 and is
/// configured with <c>stylesharp.SST1523.max_member_lines</c>.
/// </summary>
/// <remarks>
/// <para>
/// Methods, constructors, operators, conversion operators, accessors and local functions are measured, each
/// from its first token to its last: the signature is part of what a reader has to hold, and so are the
/// braces. A declaration with no body — an abstract or partial method, an interface member, an auto-property
/// accessor — has nothing to split and is never measured. Blank lines and comments do not count, so
/// explaining a member never pushes it over.
/// </para>
/// <para>
/// A long member containing a long local function reports both. That is deliberate: the local function is too
/// long on its own terms, and the member that hosts it is still exactly as long on screen as it looks.
/// </para>
/// <para>
/// The raw line span of the declaration is an upper bound on its code lines, so a member inside the limit is
/// rejected on two line lookups and never walks a token.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1523MethodTooLongAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LayoutRules.MethodTooLong);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.LocalFunctionStatement,
            SyntaxKind.GetAccessorDeclaration,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration);
    }

    /// <summary>Measures one member and reports it when its code lines exceed the maximum.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;
        if (!HasBody(node))
        {
            return;
        }

        var maximum = SizeLimitOptions.ReadMaxMemberLines(context.Options.AnalyzerConfigOptionsProvider.GetOptions(node.SyntaxTree));
        var text = node.SyntaxTree.GetText(context.CancellationToken);
        if (CodeLineCounter.SpannedLines(text, node.Span) <= maximum)
        {
            return;
        }

        var codeLines = CodeLineCounter.Count(text, node);
        if (codeLines <= maximum)
        {
            return;
        }

        var identifier = GetIdentifier(node);
        context.ReportDiagnostic(Diagnostic.Create(
            LayoutRules.MethodTooLong,
            identifier.GetLocation(),
            DescribeMember(node),
            codeLines,
            maximum));
    }

    /// <summary>Returns whether a declaration carries a body, and so has something to measure.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns><see langword="true"/> when the declaration has a block or expression body.</returns>
    private static bool HasBody(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
        LocalFunctionStatementSyntax local => local.Body is not null || local.ExpressionBody is not null,
        AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
        _ => false,
    };

    /// <summary>Gets the token the diagnostic is reported on.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The name token, the <c>operator</c> keyword, or the accessor keyword.</returns>
    private static SyntaxToken GetIdentifier(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        ConstructorDeclarationSyntax constructor => constructor.Identifier,
        OperatorDeclarationSyntax @operator => @operator.OperatorKeyword,
        ConversionOperatorDeclarationSyntax conversion => conversion.OperatorKeyword,
        LocalFunctionStatementSyntax local => local.Identifier,
        AccessorDeclarationSyntax accessor => accessor.Keyword,
        _ => default,
    };

    /// <summary>Names the member in the diagnostic message.</summary>
    /// <param name="node">The declaration.</param>
    /// <returns>The member's name as a reader would say it.</returns>
    /// <remarks>Only reached once a member is already over the limit, so the formatting never costs a clean file.</remarks>
    private static string DescribeMember(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        OperatorDeclarationSyntax @operator => "operator " + @operator.OperatorToken.ValueText,
        ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
        LocalFunctionStatementSyntax local => local.Identifier.ValueText,
        AccessorDeclarationSyntax accessor => DescribeAccessor(accessor),
        _ => string.Empty,
    };

    /// <summary>Names an accessor by its keyword and the member that owns it.</summary>
    /// <param name="accessor">The accessor declaration.</param>
    /// <returns>The accessor's name, qualified by its property or event when one is in scope.</returns>
    private static string DescribeAccessor(AccessorDeclarationSyntax accessor)
    {
        var keyword = accessor.Keyword.ValueText;
        if (accessor.Parent?.Parent is not BasePropertyDeclarationSyntax owner)
        {
            return keyword;
        }

        return owner switch
        {
            PropertyDeclarationSyntax property => property.Identifier.ValueText + "." + keyword,
            EventDeclarationSyntax @event => @event.Identifier.ValueText + "." + keyword,
            IndexerDeclarationSyntax => "this[]." + keyword,
            _ => keyword,
        };
    }
}
