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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoConsoleOutput);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ConsoleMetadataName) is not { } consoleType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, consoleType),
                SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a write call whose receiver binds to <c>System.Console</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="consoleType">The compilation's console type symbol.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol consoleType)
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

        if (!ReceiverEndsWithConsole(memberAccess.Expression))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, consoleType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.NoConsoleOutput,
            invocation.SyntaxTree,
            invocation.Span,
            "Console." + methodName));
    }

    /// <summary>Returns whether the receiver's rightmost identifier is <c>Console</c>.</summary>
    /// <param name="receiver">The member-access receiver.</param>
    /// <returns><see langword="true"/> when the receiver can name the console type.</returns>
    private static bool ReceiverEndsWithConsole(ExpressionSyntax receiver)
        => receiver switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == ConsoleTypeName,
            MemberAccessExpressionSyntax qualified => qualified.Name.Identifier.ValueText == ConsoleTypeName,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText == ConsoleTypeName,
            _ => false,
        };
}
