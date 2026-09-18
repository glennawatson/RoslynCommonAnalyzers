// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Reports permissive effective CSP sources and describes the resource capability they allow (SES1515).
/// Reference text consumed by recognized comparisons and assertions is excluded.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1515PermissiveContentSecurityPolicyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The enforced policy header.</summary>
    private const string ContentSecurityPolicyHeaderName = "Content-Security-Policy";

    /// <summary>The policy header used for reporting without enforcement.</summary>
    private const string ReportOnlyHeaderName = "Content-Security-Policy-Report-Only";

    /// <summary>The name and value arguments in a header-setting call.</summary>
    private const int HeaderArgumentCount = 2;

    /// <summary>The descriptors this analyzer reports, shared across callbacks.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.PermissiveContentSecurityPolicy);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
    }

    /// <summary>Reports one effective permission in a policy literal.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        var text = literal.Token.ValueText;
        if (text.IndexOf('*') < 0 && text.IndexOf("'unsafe-", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        var header = GetHeaderName(literal);
        if (!ContentSecurityPolicyParser.BeginsWithDirective(text) && !IsPolicyHeader(header))
        {
            return;
        }

        if (ContentSecurityPolicyParser.FindViolation(text) is not { } violation
            || ContentSecurityPolicyComparison.IsReferenceText(context, literal))
        {
            return;
        }

        var reportOnly = string.Equals(header, ReportOnlyHeaderName, StringComparison.OrdinalIgnoreCase);
        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.PermissiveContentSecurityPolicy,
            literal.SyntaxTree,
            literal.Span,
            violation.Directive,
            ContentSecurityPolicyParser.DescribePermission(violation.Permission, reportOnly)));
    }

    /// <summary>Resolves a literal's header name from an indexer assignment or two-argument header call.</summary>
    /// <param name="literal">The candidate value literal.</param>
    /// <returns>The literal header name, or null when the context does not identify one.</returns>
    private static string? GetHeaderName(LiteralExpressionSyntax literal)
    {
        ExpressionSyntax value = literal;
        while (value.Parent is ExpressionSyntax parent
            && (parent is ParenthesizedExpressionSyntax || parent.IsKind(SyntaxKind.AddExpression)))
        {
            value = parent;
        }

        return value.Parent switch
        {
            AssignmentExpressionSyntax { Left: ElementAccessExpressionSyntax { ArgumentList.Arguments: { Count: 1 } arguments } } assignment
                when assignment.Right == value => GetStringLiteralText(arguments[0].Expression),
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax, Arguments: { Count: HeaderArgumentCount } arguments } } argument
                when arguments[1] == argument => GetStringLiteralText(arguments[0].Expression),
            _ => null,
        };
    }

    /// <summary>Reads a header-name literal without binding arbitrary expressions.</summary>
    /// <param name="expression">The written header-name expression.</param>
    /// <returns>The decoded header name, or null.</returns>
    private static string? GetStringLiteralText(ExpressionSyntax expression) =>
        expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal.Token.ValueText
            : null;

    /// <summary>Recognizes enforced and report-only CSP headers.</summary>
    /// <param name="name">The literal header name.</param>
    /// <returns>Whether this header carries a CSP.</returns>
    private static bool IsPolicyHeader(string? name) =>
        string.Equals(name, ContentSecurityPolicyHeaderName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ReportOnlyHeaderName, StringComparison.OrdinalIgnoreCase);
}
