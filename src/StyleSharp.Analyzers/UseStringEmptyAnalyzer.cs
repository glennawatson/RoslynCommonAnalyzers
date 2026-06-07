// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an empty string literal (<c>""</c>) that could be written as <c>string.Empty</c>
/// (SST1122). The rule skips contexts that require a compile-time constant — attribute arguments,
/// <c>const</c> declarations, default parameter values, case labels, constant patterns, and enum
/// members — because <c>string.Empty</c> is a static field, not a constant, and cannot be used there.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseStringEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseStringEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports an empty string literal outside a constant-required context.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (!IsEmptyStringLiteral(literal.Token) || IsConstantContext(literal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseStringEmpty, literal.GetLocation()));
    }

    /// <summary>Returns whether the literal sits in a context that requires a compile-time constant.</summary>
    /// <param name="literal">The empty string literal.</param>
    /// <returns><see langword="true"/> when 'string.Empty' would be illegal in the literal's position.</returns>
    private static bool IsConstantContext(SyntaxNode literal)
    {
        for (var node = literal.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case AttributeSyntax:
                case ParameterSyntax:
                case CaseSwitchLabelSyntax:
                case ConstantPatternSyntax:
                case EnumMemberDeclarationSyntax:
                    return true;
                case FieldDeclarationSyntax field:
                    return ModifierListHelper.Contains(field.Modifiers, SyntaxKind.ConstKeyword);
                case LocalDeclarationStatementSyntax local:
                    return ModifierListHelper.Contains(local.Modifiers, SyntaxKind.ConstKeyword);
                case StatementSyntax:
                case MemberDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Returns whether a token spells an empty string literal.</summary>
    /// <param name="token">The literal token.</param>
    /// <returns><see langword="true"/> for <c>""</c> and <c>@""</c>.</returns>
    private static bool IsEmptyStringLiteral(SyntaxToken token)
        => token.Text is ['"', '"'] or ['@', '"', '"'];
}
