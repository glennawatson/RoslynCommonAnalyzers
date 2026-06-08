// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an anonymous method with an empty parameter list (SST1410) and an
/// attribute with an empty argument list (SST1411) — both redundant parentheses.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RedundantParenthesesAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.RemoveDelegateParentheses,
        MaintainabilityRules.RemoveAttributeParentheses);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeAnonymousMethod, SyntaxKind.AnonymousMethodExpression);
        context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
    }

    /// <summary>Reports an anonymous method that declares an empty parameter list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAnonymousMethod(SyntaxNodeAnalysisContext context)
    {
        var anonymous = (AnonymousMethodExpressionSyntax)context.Node;
        if (anonymous.ParameterList is not { Parameters.Count: 0 } parameterList)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.RemoveDelegateParentheses, parameterList.GetLocation()));
    }

    /// <summary>Reports an attribute that declares an empty argument list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.ArgumentList is not { Arguments.Count: 0 } argumentList)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.RemoveAttributeParentheses, argumentList.GetLocation()));
    }
}
