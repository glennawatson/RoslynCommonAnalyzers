// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a string literal that is a database connection string authenticating with a user name but supplying an
/// empty or missing password (SES1203). The rule inspects each <see cref="LiteralExpressionSyntax"/> string literal
/// and reports it when <see cref="EmptyConnectionStringPasswordClassifier.IsEmptyPasswordConnectionString"/> confirms
/// the value carries a data-source key and a user key while its password is blank -- an empty <c>Password</c>/<c>Pwd</c>
/// value or no password key -- and it does not use integrated or trusted authentication. Detection is entirely
/// syntactic: string literals exist on every target framework, so the rule needs no semantic model, no framework
/// probing, and no configuration, and the classifier keeps the no-diagnostic path allocation-free. No code fix is
/// offered because there is no password to insert. The complementary hard-coded-password shape (a non-empty
/// connection-string password) is handled by the pattern-based secret rule, so the two never double-report.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1203EmptyConnectionStringPasswordAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.EmptyConnectionStringPassword);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports SES1203 when a string literal is a connection string with user-name authentication and a blank password.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;

        if (!EmptyConnectionStringPasswordClassifier.IsEmptyPasswordConnectionString(literal.Token.ValueText))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(SecurityRules.EmptyConnectionStringPassword, literal.GetLocation()));
    }
}
