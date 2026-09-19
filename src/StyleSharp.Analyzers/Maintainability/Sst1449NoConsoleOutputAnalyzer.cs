// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags direct <c>Console.Write</c> and <c>Console.WriteLine</c> calls, including writes through
/// <c>Console.Out</c> and <c>Console.Error</c> (SST1449). Console writes
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

    /// <summary>Reports a write call on <c>System.Console</c> or one of its standard writers.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="consoleType">The compilation's console type, resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyMetadataType consoleType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !TryGetConsoleWrite(memberAccess, out var methodName, out var writerAccess))
        {
            return;
        }

        if (!TargetsConsoleWrite(context, invocation, writerAccess, consoleType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.NoConsoleOutput,
            invocation.SyntaxTree,
            invocation.Span,
            methodName == "Write" ? "Console.Write" : "Console.WriteLine"));
    }

    /// <summary>Returns the write method name and the standard-writer access, if the syntax is eligible.</summary>
    /// <param name="memberAccess">The invoked member access.</param>
    /// <param name="methodName">The eligible write method name.</param>
    /// <param name="writerAccess">The standard-writer property access, or <see langword="null"/> for direct Console calls.</param>
    /// <returns><see langword="true"/> when the syntax can target console output.</returns>
    private static bool TryGetConsoleWrite(
        MemberAccessExpressionSyntax memberAccess,
        out string methodName,
        out MemberAccessExpressionSyntax? writerAccess)
    {
        methodName = memberAccess.Name.Identifier.ValueText;
        writerAccess = null;
        if (methodName is not ("Write" or "WriteLine"))
        {
            return false;
        }

        var receiver = memberAccess.Expression;
        if (SyntaxNames.GetMemberName(receiver) == ConsoleTypeName)
        {
            return true;
        }

        if (receiver is not MemberAccessExpressionSyntax candidate
            || candidate.Name.Identifier.ValueText is not ("Out" or "Error")
            || SyntaxNames.GetMemberName(candidate.Expression) != ConsoleTypeName)
        {
            return false;
        }

        writerAccess = candidate;
        return true;
    }

    /// <summary>Confirms that the eligible invocation binds to Console or its returned writer type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="writerAccess">The standard-writer access, or <see langword="null"/> for direct Console calls.</param>
    /// <param name="consoleType">The compilation's lazily resolved Console type.</param>
    /// <returns><see langword="true"/> when the invocation is a framework console write.</returns>
    private static bool TargetsConsoleWrite(
        in SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax? writerAccess,
        LazyMetadataType consoleType)
    {
        if (consoleType.Get() is not { } resolved)
        {
            return false;
        }

        return writerAccess is null
            ? InvocationTargets.IsMethodOf(context.SemanticModel, invocation, resolved, context.CancellationToken)
            : context.SemanticModel.GetSymbolInfo(writerAccess, context.CancellationToken).Symbol is IPropertySymbol property
                && SymbolEqualityComparer.Default.Equals(property.ContainingType, resolved)
                && property.Name is "Out" or "Error"
                && property.Type is INamedTypeSymbol writerType
                && InvocationTargets.IsMethodOf(context.SemanticModel, invocation, writerType, context.CancellationToken);
    }
}
