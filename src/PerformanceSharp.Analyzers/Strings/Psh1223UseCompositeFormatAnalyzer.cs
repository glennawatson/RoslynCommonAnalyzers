// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a <c>string.Format</c> call whose format string is a compile-time constant (PSH1223).
/// The placeholders are fixed the moment the code is compiled, yet <c>string.Format</c> re-scans the
/// string for them on every single call. A <c>CompositeFormat</c> holds the parsed result, so the
/// scanning happens once and the call site reads a field.
/// </summary>
/// <remarks>
/// <para>
/// <b>.NET 8 and later only.</b> <c>CompositeFormat</c> and the <c>string.Format</c> overloads that
/// take one do not exist before then, so the rule resolves both in the analyzed compilation and
/// registers nothing at all when either is missing. Nothing is inferred from a target-framework
/// string.
/// </para>
/// <para>
/// <b>The format string is validated before it is hoisted.</b> <c>CompositeFormat.Parse</c> throws on
/// a malformed format, and it would throw from a static field initializer — that is, at type
/// initialization, wrapped in a <c>TypeInitializationException</c>, possibly far from the call that
/// used to throw a plain <c>FormatException</c>. Moving an exception is not this rule's business, so a
/// format the rule cannot prove well-formed is left alone. The check is deliberately conservative: it
/// accepts the ordinary placeholder grammar and refuses anything it is not sure about, which costs at
/// most a missed report.
/// </para>
/// <para>
/// <b>The provider is preserved.</b> Every <c>CompositeFormat</c> overload takes an
/// <see cref="IFormatProvider"/>, while <c>string.Format(format, args)</c> does not — it passes
/// <see langword="null"/> internally, which means the current culture. The rewrite therefore names the
/// current culture explicitly rather than passing a bare <see langword="null"/>, which would be both
/// obscure and, with a <c>CompositeFormat</c> in the next slot, genuinely ambiguous against the old
/// <c>Format(string, object, object)</c> overload. A call that already supplies a provider keeps it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1223UseCompositeFormatAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The formatted member name the syntax gate requires.</summary>
    internal const string FormatMethodName = "Format";

    /// <summary>The metadata name of the parsed-format type the fix hoists.</summary>
    internal const string CompositeFormatMetadataName = "System.Text.CompositeFormat";

    /// <summary>The simple name of the parsed-format type the fix hoists.</summary>
    internal const string CompositeFormatTypeName = "CompositeFormat";

    /// <summary>The fully qualified stand-in bound in place of the field the fix has not created yet.</summary>
    private const string CompositeFormatPlaceholder = "default(global::System.Text.CompositeFormat)";

    /// <summary>The fully qualified current culture the rewrite names when the call had no provider.</summary>
    private const string QualifiedCurrentCulture = "global::System.Globalization.CultureInfo.CurrentCulture";

    /// <summary>The fewest arguments a reported call carries: a format and at least one value.</summary>
    private const int MinFormatArguments = 2;

    /// <summary>The two arguments the rewrite always puts first: the provider and the parsed format.</summary>
    private const int ProviderAndFormatCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseCompositeFormat);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(CompositeFormatMetadataName) is not { } compositeFormat
                || !HasCompositeFormatOverload(start.Compilation, compositeFormat))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeFormat, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a plain <c>Format</c> call of at least a format and a value.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsFormatShape(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < MinFormatArguments
            || invocation.Expression is not MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            || access.Name.Identifier.ValueText != FormatMethodName)
        {
            return false;
        }

        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].NameColon is not null || !arguments[i].RefOrOutKeyword.IsKind(SyntaxKind.None))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Binds a <c>Format</c> call and reports where its format string sits, when it is hoistable.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The <c>Format</c> invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The format argument's index, or <c>-1</c> when the call is not reportable.</returns>
    internal static int GetHoistableFormatIndex(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        if (BindStringFormat(model, invocation, cancellationToken) is not { } format)
        {
            return -1;
        }

        var formatIndex = IsFormatProvider(format.Parameters[0].Type) ? 1 : 0;
        if (format.Parameters.Length <= formatIndex
            || format.Parameters[formatIndex].Type.SpecialType != SpecialType.System_String
            || invocation.ArgumentList.Arguments.Count <= formatIndex + 1)
        {
            return -1;
        }

        var formatArgument = invocation.ArgumentList.Arguments[formatIndex].Expression;
        var constant = model.GetConstantValue(formatArgument, cancellationToken);
        return constant is { HasValue: true, Value: string text } && CompositeFormatText.IsWellFormed(text) ? formatIndex : -1;
    }

    /// <summary>Builds the <c>string.Format(provider, compositeFormat, values)</c> rewrite of a reported call.</summary>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <param name="formatIndex">The format argument's index.</param>
    /// <param name="provider">The format provider the rewrite passes.</param>
    /// <param name="compositeFormat">The parsed format the rewrite passes.</param>
    /// <returns>The rewritten call.</returns>
    internal static InvocationExpressionSyntax BuildCompositeFormatCall(
        InvocationExpressionSyntax invocation,
        int formatIndex,
        ExpressionSyntax provider,
        ExpressionSyntax compositeFormat)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var values = arguments.Count - formatIndex - 1;
        var rewritten = new ArgumentSyntax[values + ProviderAndFormatCount];
        rewritten[0] = SyntaxFactory.Argument(provider);
        rewritten[1] = SyntaxFactory.Argument(compositeFormat);
        for (var i = 0; i < values; i++)
        {
            rewritten[i + ProviderAndFormatCount] = arguments[formatIndex + 1 + i].WithoutTrivia();
        }

        return invocation.WithArgumentList(invocation.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(rewritten)));
    }

    /// <summary>Returns the provider expression the rewrite should pass, reusing the call's own when it has one.</summary>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <param name="formatIndex">The format argument's index.</param>
    /// <param name="cultureSpelling">The spelling of the current culture to use when the call has no provider.</param>
    /// <returns>The provider expression.</returns>
    internal static ExpressionSyntax GetProvider(InvocationExpressionSyntax invocation, int formatIndex, string cultureSpelling)
        => formatIndex == 1
            ? invocation.ArgumentList.Arguments[0].Expression.WithoutTrivia()
            : SyntaxFactory.ParseExpression(cultureSpelling);

    /// <summary>Confirms the rewrite binds to a <c>CompositeFormat</c> overload of <c>string.Format</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The reported <c>Format</c> invocation.</param>
    /// <param name="formatIndex">The format argument's index.</param>
    /// <param name="cultureSpelling">The spelling of the current culture the rewrite would use.</param>
    /// <returns><see langword="true"/> when the rewritten call resolves as intended.</returns>
    /// <remarks>
    /// The field the fix creates does not exist yet, so it cannot be bound. A <c>default</c> expression
    /// of the same type stands in for it: overload resolution sees a <c>CompositeFormat</c> in that
    /// slot, which is exactly what it will see once the field is there. This is what proves the
    /// rewritten call is not ambiguous against the old <c>Format(string, object, object)</c> overload.
    /// </remarks>
    internal static bool RewriteBindsToCompositeFormat(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        int formatIndex,
        string cultureSpelling)
    {
        var provider = GetProvider(invocation, formatIndex, cultureSpelling);
        var placeholder = SyntaxFactory.ParseExpression(CompositeFormatPlaceholder);
        var rewritten = BuildCompositeFormatCall(invocation, formatIndex, provider, placeholder);
        var symbol = model.GetSpeculativeSymbolInfo(invocation.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol;
        return symbol is IMethodSymbol { IsStatic: true, Name: FormatMethodName, ReturnType.SpecialType: SpecialType.System_String } resolved
            && resolved.ContainingType.SpecialType == SpecialType.System_String
            && resolved.Parameters.Length >= ProviderAndFormatCount
            && resolved.Parameters[1].Type.ToDisplayString() == CompositeFormatMetadataName;
    }

    /// <summary>Reports PSH1223 for a constant format string that is re-parsed on every call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeFormat(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsFormatShape(invocation))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        var formatIndex = GetHoistableFormatIndex(model, invocation, cancellationToken);
        if (formatIndex < 0
            || SpanRewriteGuard.IsInsideExpressionTree(invocation, model, cancellationToken)
            || !RewriteBindsToCompositeFormat(model, invocation, formatIndex, QualifiedCurrentCulture))
        {
            return;
        }

        var formatArgument = invocation.ArgumentList.Arguments[formatIndex].Expression;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseCompositeFormat,
            formatArgument.SyntaxTree,
            formatArgument.Span,
            formatArgument.ToString()));
    }

    /// <summary>Binds a call and keeps it only when it is a <c>string.Format</c> that takes a format string.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The <c>Format</c> invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound method, or <see langword="null"/> when it is not the framework's own format.</returns>
    private static IMethodSymbol? BindStringFormat(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol format
            || !format.IsStatic
            || format.Name != FormatMethodName
            || format.ReturnType.SpecialType != SpecialType.System_String
            || format.ContainingType.SpecialType != SpecialType.System_String
            || format.Parameters.Length == 0)
        {
            return null;
        }

        return format;
    }

    /// <summary>Returns whether <see cref="string"/> declares a <c>Format</c> overload taking a parsed format.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <param name="compositeFormat">The parsed-format type.</param>
    /// <returns><see langword="true"/> when the overloads exist.</returns>
    private static bool HasCompositeFormatOverload(Compilation compilation, INamedTypeSymbol compositeFormat)
    {
        foreach (var member in compilation.GetSpecialType(SpecialType.System_String).GetMembers(FormatMethodName))
        {
            if (member is IMethodSymbol { IsStatic: true, Parameters: [{ } first, { } second, ..] }
                && IsFormatProvider(first.Type)
                && SymbolEqualityComparer.Default.Equals(second.Type, compositeFormat))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <see cref="IFormatProvider"/>.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> for <c>System.IFormatProvider</c>.</returns>
    private static bool IsFormatProvider(ITypeSymbol type)
        => type is INamedTypeSymbol
        {
            Name: nameof(IFormatProvider),
            TypeKind: TypeKind.Interface,
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };
}
