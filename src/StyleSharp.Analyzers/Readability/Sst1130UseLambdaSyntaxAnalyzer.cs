// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an anonymous method (<c>delegate (…) { … }</c>) that should be written as a lambda
/// expression (SST1130). Lambdas are shorter, allow expression bodies, and are the idiomatic way
/// to write an inline callback in modern C#.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1130UseLambdaSyntaxAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UseLambdaSyntax);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AnonymousMethodExpression);
    }

    /// <summary>Reports an anonymous method expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var anonymous = (AnonymousMethodExpressionSyntax)context.Node;
        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UseLambdaSyntax, anonymous.DelegateKeyword.GetLocation()));
    }
}
