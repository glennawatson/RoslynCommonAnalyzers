// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a non-constant value rendered as raw HTML (SES1701). Blazor HTML-encodes the values it renders,
/// and that encoding is what stops attacker-supplied text from becoming markup. Three sinks opt out of it:
/// constructing a <c>Microsoft.AspNetCore.Components.MarkupString</c> (via <c>new MarkupString(x)</c> or the
/// explicit <c>(MarkupString)x</c> conversion) and calling
/// <c>Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder.AddMarkupContent(seq, x)</c>. The rule
/// reports the value passed to any of these sinks when it is not a compile-time constant, since a
/// non-constant value can carry untrusted input and inject script (cross-site scripting); a constant is
/// developer-authored markup and is not reported. A value wrapped in a call to a method whose name is listed
/// in <c>securitysharp.SES1701.sanitizers</c> (or the project-wide <c>securitysharp.sanitizers</c>) is
/// treated as already-encoded and stays silent. The sink is confirmed by binding, so a same-named type or
/// method on an unrelated type is ignored. The whole rule is gated on <c>MarkupString</c> resolving, so a
/// non-Blazor project registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1701RawHtmlFromNonConstantAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the raw-markup wrapper the rule gates on.</summary>
    private const string MarkupStringMetadataName = "Microsoft.AspNetCore.Components.MarkupString";

    /// <summary>The metadata name of the render-tree builder that owns the raw-markup append helper.</summary>
    private const string RenderTreeBuilderMetadataName = "Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder";

    /// <summary>The simple type name of the raw-markup wrapper, used for the syntactic prefilter.</summary>
    private const string MarkupStringSimpleName = "MarkupString";

    /// <summary>The name of the render-tree helper that appends raw markup.</summary>
    private const string AddMarkupContentMethodName = "AddMarkupContent";

    /// <summary>The zero-based position of the markup argument on <c>AddMarkupContent(sequence, markupContent)</c>.</summary>
    private const int AddMarkupContentMarkupPosition = 1;

    /// <summary>The rule-specific sanitizer allow-list key.</summary>
    private const string SanitizersRuleKey = "securitysharp.SES1701.sanitizers";

    /// <summary>The project-wide sanitizer allow-list key.</summary>
    private const string SanitizersGeneralKey = "securitysharp.sanitizers";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.RawHtmlFromNonConstant);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markupString = start.Compilation.GetTypeByMetadataName(MarkupStringMetadataName);
            if (markupString is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeObjectCreation(nodeContext, markupString), SyntaxKind.ObjectCreationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeCast(nodeContext, markupString), SyntaxKind.CastExpression);

            var renderTreeBuilder = start.Compilation.GetTypeByMetadataName(RenderTreeBuilderMetadataName);
            if (renderTreeBuilder is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, renderTreeBuilder), SyntaxKind.InvocationExpression);
            }
        });
    }

    /// <summary>Reports SES1701 for <c>new MarkupString(x)</c> whose value argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markupString">The gated <c>MarkupString</c> type resolved for the compilation.</param>
    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context, INamedTypeSymbol markupString)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Syntactic prefilter: 'new MarkupString(<single argument>)'.
        if (creation.ArgumentList is not { Arguments.Count: 1 } argumentList
            || !string.Equals(GetRightmostIdentifier(creation.Type), MarkupStringSimpleName, StringComparison.Ordinal))
        {
            return;
        }

        var value = argumentList.Arguments[0].Expression;
        if (IsSafeValue(context, value)
            || !SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type, markupString))
        {
            return;
        }

        Report(context, value, MarkupStringSimpleName);
    }

    /// <summary>Reports SES1701 for <c>(MarkupString)x</c> whose cast operand is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markupString">The gated <c>MarkupString</c> type resolved for the compilation.</param>
    private static void AnalyzeCast(SyntaxNodeAnalysisContext context, INamedTypeSymbol markupString)
    {
        var cast = (CastExpressionSyntax)context.Node;

        // Syntactic prefilter: '(MarkupString)<operand>'.
        if (!string.Equals(GetRightmostIdentifier(cast.Type), MarkupStringSimpleName, StringComparison.Ordinal))
        {
            return;
        }

        if (IsSafeValue(context, cast.Expression)
            || !SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(cast.Type, context.CancellationToken).Type, markupString))
        {
            return;
        }

        Report(context, cast.Expression, MarkupStringSimpleName);
    }

    /// <summary>Reports SES1701 for <c>AddMarkupContent(seq, x)</c> whose markup argument is non-constant.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="renderTreeBuilder">The gated <c>RenderTreeBuilder</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol renderTreeBuilder)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.AddMarkupContent(sequence, markup)' call.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: AddMarkupContentMethodName }
            || invocation.ArgumentList.Arguments.Count <= AddMarkupContentMarkupPosition)
        {
            return;
        }

        var value = invocation.ArgumentList.Arguments[AddMarkupContentMarkupPosition].Expression;
        if (IsSafeValue(context, value))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: AddMarkupContentMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, renderTreeBuilder))
        {
            return;
        }

        Report(context, value, AddMarkupContentMethodName);
    }

    /// <summary>Reports SES1701 on a raw-HTML value expression.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="value">The value expression written to the raw-HTML sink.</param>
    /// <param name="sink">The sink name used in the diagnostic message.</param>
    private static void Report(SyntaxNodeAnalysisContext context, ExpressionSyntax value, string sink)
        => context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.RawHtmlFromNonConstant,
            value.SyntaxTree,
            value.Span,
            sink));

    /// <summary>Returns whether a value is out of scope: a compile-time constant, or an allow-listed sanitizer call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="value">The value expression written to a raw-HTML sink.</param>
    /// <returns><see langword="true"/> when the value is constant or sanitizer-wrapped and must not be reported.</returns>
    private static bool IsSafeValue(SyntaxNodeAnalysisContext context, ExpressionSyntax value)
        => context.SemanticModel.GetConstantValue(value, context.CancellationToken).HasValue
            || IsSanitizerCall(context, value);

    /// <summary>Returns whether a value is a call to a method named in the sanitizer allow-list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="value">The value expression written to a raw-HTML sink.</param>
    /// <returns><see langword="true"/> when the value is an invocation whose simple name is allow-listed.</returns>
    private static bool IsSanitizerCall(SyntaxNodeAnalysisContext context, ExpressionSyntax value)
    {
        if (value is not InvocationExpressionSyntax invocation)
        {
            return false;
        }

        var name = GetInvokedName(invocation.Expression);
        var sanitizers = AnalyzerOptionReader.ReadCommaSeparatedList(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(value.SyntaxTree),
            SanitizersRuleKey,
            SanitizersGeneralKey);

        for (var i = 0; i < sanitizers.Length; i++)
        {
            if (string.Equals(sanitizers[i], name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the invoked member's simple name for an <c>Identifier(...)</c> or <c>x.Identifier(...)</c> call.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple name, or <see langword="null"/> when the callee is not a plain member reference.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Returns the rightmost identifier of a plain or (possibly <c>global::</c>-) qualified type name.</summary>
    /// <param name="type">The type syntax to inspect.</param>
    /// <returns>The simple name, or <see langword="null"/> when the type is not a plain named type.</returns>
    private static string? GetRightmostIdentifier(TypeSyntax type)
        => type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            _ => null,
        };
}
