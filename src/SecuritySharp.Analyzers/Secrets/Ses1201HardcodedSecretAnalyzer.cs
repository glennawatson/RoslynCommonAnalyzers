// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a string literal whose content is a recognisable hard-coded credential (SES1201). The rule inspects
/// each <see cref="LiteralExpressionSyntax"/> string literal and reports it when
/// <see cref="HardcodedSecretClassifier.Classify"/> matches one of a curated, high-precision credential shapes
/// (OpenAI/AWS/GitHub/Slack/Google keys, a PEM private-key block, an Azure key body, or a connection-string
/// password) while staying silent on ordinary text and obvious placeholders. Detection is entirely syntactic:
/// string literals exist on every target framework, so the rule needs no semantic model, no framework probing,
/// and no configuration, and the classifier keeps the no-diagnostic path allocation-free.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1201HardcodedSecretAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.HardcodedSecret);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports SES1201 when a string literal's content matches a recognised credential shape.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        if (HardcodedSecretClassifier.Classify(literal.Token.ValueText) is not { } kind)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.HardcodedSecret, literal.GetLocation(), kind));
    }
}
