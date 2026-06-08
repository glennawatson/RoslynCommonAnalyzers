// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires explicitly named tuple elements to follow the configured casing
/// convention (SST1316), defaulting to PascalCase. Configure with
/// <c>stylesharp.tuple_element_naming</c> in <c>.editorconfig</c>. Inferred
/// element names (taken from a variable) are not reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1316TupleElementNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.TupleElement);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.TupleType, SyntaxKind.TupleExpression);
    }

    /// <summary>Analyzes a tuple type or tuple expression's explicit element names.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var convention = NamingConventions.Read(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree),
            NamingConventions.TupleElementSpecificKey,
            NamingConventions.TupleElementGeneralKey,
            NamingConvention.PascalCase);

        switch (context.Node)
        {
            case TupleTypeSyntax tupleType:
            {
                foreach (var element in tupleType.Elements)
                {
                    CheckElement(context, element.Identifier, convention);
                }

                break;
            }

            case TupleExpressionSyntax tupleExpression:
            {
                foreach (var argument in tupleExpression.Arguments)
                {
                    if (argument.NameColon is { } nameColon)
                    {
                        CheckElement(context, nameColon.Name.Identifier, convention);
                    }
                }

                break;
            }
        }
    }

    /// <summary>Reports SST1316 when a tuple element name does not match the configured convention.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The element's identifier (default token when unnamed).</param>
    /// <param name="convention">The configured casing convention.</param>
    private static void CheckElement(SyntaxNodeAnalysisContext context, SyntaxToken identifier, NamingConvention convention)
    {
        var name = identifier.ValueText;
        if (name.Length == 0 || NamingHelper.IsAllUnderscores(name) || NamingConventions.Conforms(name, convention))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.TupleElement, identifier, NamingConventions.Suggest(name, convention));
    }
}
