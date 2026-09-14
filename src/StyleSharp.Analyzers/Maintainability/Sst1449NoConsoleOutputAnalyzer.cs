// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags direct <c>Console.Write</c> and <c>Console.WriteLine</c> calls (SST1449). Console writes
/// bypass log levels, sinks, and redirection, and turn into noise or silently lost output when the
/// code runs without an attached console; diagnostics belong behind the application's logging
/// abstraction. The check is syntax-gated on the member name and a receiver ending in
/// <c>Console</c> before a single semantic bind confirms <c>System.Console</c>, so ordinary
/// invocations never bind.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1449NoConsoleOutputAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The console type name used in the syntax gate.</summary>
    private const string ConsoleTypeName = "Console";

    /// <summary>The metadata name of the console type.</summary>
    private const string ConsoleMetadataName = "System.Console";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(MaintainabilityRules.NoConsoleOutput);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, ConsoleMetadataName),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a write call whose receiver binds to <c>System.Console</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="consoleType">The compilation's console type, resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataType consoleType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (methodName is not ("Write" or "WriteLine"))
        {
            return;
        }

        if (SyntaxNames.GetMemberName(memberAccess.Expression) != ConsoleTypeName)
        {
            return;
        }

        if (consoleType.Get() is not { } resolved
            || !InvocationTargets.IsMethodOf(context.SemanticModel, invocation, resolved, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.NoConsoleOutput,
            invocation.SyntaxTree,
            invocation.Span,
            $"Console.{methodName}"));
    }
}
