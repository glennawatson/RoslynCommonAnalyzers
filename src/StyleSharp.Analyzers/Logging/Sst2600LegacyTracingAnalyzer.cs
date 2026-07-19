// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>System.Diagnostics.Trace.Write</c>, <c>WriteLine</c>, <c>WriteIf</c>, or <c>WriteLineIf</c> call
/// used for application logging (SST2600), and only when structured logging is available in the compilation so the
/// suggestion is actionable. Legacy tracing emits a flat string with no level, no category, and no structured
/// fields; a structured logger keeps all three, so the same message survives as data a sink can filter, route, and
/// query.
/// </summary>
/// <remarks>
/// <para>
/// The whole rule is gated at compilation start on <c>Microsoft.Extensions.Logging.ILogger</c> resolving. A project
/// that references no structured-logging abstraction has nothing to migrate to, so it is never nagged and the rule
/// costs it nothing. The tracing type is resolved once at the same point; when it is absent no <c>Trace.*</c> call
/// can bind and the rule registers nothing.
/// </para>
/// <para>
/// The scope is <c>Trace.*</c> only, deliberately, and does <b>not</b> include <c>Debug.Write*</c>. <c>Debug</c>'s
/// output methods carry <c>[Conditional("DEBUG")]</c>, so the compiler omits the entire call from a release build:
/// it never runs in production and is a debug-time aid rather than an application log sink, a far weaker signal that
/// structured logging was the intent. <c>Trace.*</c>, by contrast, is not conditional and runs in release, so a
/// <c>Trace.Write*</c> call genuinely is application output going through legacy tracing, which is what this rule
/// reports. Flagging <c>Debug.*</c> as well would nag legitimate debug-only tracing, so it is left silent.
/// </para>
/// <para>
/// The clean path is syntax only: the rule sees every invocation and rejects all but calls whose simple name is one
/// of the four tracing methods (either <c>Trace.Write*</c> or an unqualified name reached through
/// <c>using static</c>). Nothing binds until that holds; only then is the call bound to confirm its containing type
/// really is <c>System.Diagnostics.Trace</c>, so a same-named method of the project's own is not reported. There is
/// no code fix: converting to <c>ILogger.Log*</c> needs a logger instance and a message template the author must
/// choose.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2600LegacyTracingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the structured-logging abstraction the suggestion depends on.</summary>
    private const string LoggerTypeMetadataName = "Microsoft.Extensions.Logging.ILogger";

    /// <summary>The metadata name of the legacy tracing type a reported call must bind to.</summary>
    private const string TraceTypeMetadataName = "System.Diagnostics.Trace";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(LoggingRules.LegacyTracing);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Gates the rule on structured logging being available, then analyzes each invocation.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(LoggerTypeMetadataName) is null)
        {
            return;
        }

        if (context.Compilation.GetTypeByMetadataName(TraceTypeMetadataName) is not { } traceType)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, traceType), SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one legacy tracing call used for application logging.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="traceType">The resolved <c>System.Diagnostics.Trace</c> type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol traceType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (GetTracingMethodName(invocation.Expression) is not { } methodName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, traceType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            LoggingRules.LegacyTracing,
            invocation.GetLocation(),
            methodName));
    }

    /// <summary>Returns the invoked tracing method's simple name when it is one of the four output methods.</summary>
    /// <param name="expression">The invocation's expression.</param>
    /// <returns>The method name for <c>Write</c>/<c>WriteLine</c>/<c>WriteIf</c>/<c>WriteLineIf</c>, otherwise <see langword="null"/>.</returns>
    private static string? GetTracingMethodName(ExpressionSyntax expression)
    {
        var name = expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        return IsTracingMethodName(name) ? name : null;
    }

    /// <summary>Returns whether a name is one of the legacy tracing output methods.</summary>
    /// <param name="name">The invoked simple name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for <c>Write</c>, <c>WriteLine</c>, <c>WriteIf</c>, or <c>WriteLineIf</c>.</returns>
    private static bool IsTracingMethodName(string? name)
        => name is "Write" or "WriteLine" or "WriteIf" or "WriteLineIf";
}
