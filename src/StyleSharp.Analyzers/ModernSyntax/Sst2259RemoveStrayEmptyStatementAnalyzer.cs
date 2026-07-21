// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a stray trailing semicolon on a type declaration that already ends in a brace body (SST2259):
/// <c>class Foo { };</c>. The grammar permits the semicolon on a type, but it states an empty statement that
/// does nothing. A trailing semicolon on a method body is a compiler error (CS1597) rather than a stray
/// legal token, so it is left to the compiler and not reported here.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2259RemoveStrayEmptyStatementAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.RemoveStrayEmptyStatement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.EnumDeclaration);
    }

    /// <summary>Returns whether a type declaration carries a stray trailing semicolon after its body.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns><see langword="true"/> when a brace body and a trailing semicolon are both present.</returns>
    internal static bool HasStraySemicolon(BaseTypeDeclarationSyntax type)
        => type.CloseBraceToken.IsKind(SyntaxKind.CloseBraceToken) && type.SemicolonToken.IsKind(SyntaxKind.SemicolonToken);

    /// <summary>Reports a stray semicolon on a type declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var type = (BaseTypeDeclarationSyntax)context.Node;
        if (!HasStraySemicolon(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.RemoveStrayEmptyStatement, type.SemicolonToken.GetLocation()));
    }
}
