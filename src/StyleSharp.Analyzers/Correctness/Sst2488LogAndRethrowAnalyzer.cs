// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>catch</c> whose body is a logging call (or several) followed by a bare <c>throw;</c>
/// (SST2488). Such a handler both logs the exception here and re-raises it unchanged, so the same
/// failure is recorded again wherever it is finally handled.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is purely syntactic: a catch is rejected unless its last statement is a bare
/// <c>throw;</c> and every statement before it is an invocation whose member name is a logging name.
/// The semantic model is touched only for that rare shape, to confirm each preceding call is a real
/// logging call — one made on a logger-typed receiver, or a <c>Log…</c> call handed the caught
/// exception. A statement that is neither is treated as genuine handling, and the catch is left alone.
/// </para>
/// <para>
/// A bare <c>throw;</c> alone (no logging) and a <c>throw ex;</c> that resets the stack are separate
/// concerns and are not reported here.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2488LogAndRethrowAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The fewest statements a logging rethrow needs: one log and the rethrow.</summary>
    private const int MinimumStatements = 2;

    /// <summary>The member names a logging call uses across the common logging abstractions.</summary>
    private static readonly HashSet<string> LoggingNames = new(StringComparer.Ordinal)
    {
        "Log", "LogError", "LogWarning", "LogInformation", "LogInfo",
        "LogDebug", "LogTrace", "LogCritical", "LogException", "LogFatal", "LogWarn",
        "Error", "Warn", "Warning", "Fatal", "Info", "Information",
        "Debug", "Trace", "Verbose", "Critical", "Exception",
    };

    /// <summary>The unambiguous <c>Log…</c> names, which read as logging on any receiver.</summary>
    private static readonly HashSet<string> StrongLoggingNames = new(StringComparer.Ordinal)
    {
        "Log", "LogError", "LogWarning", "LogInformation", "LogInfo",
        "LogDebug", "LogTrace", "LogCritical", "LogException", "LogFatal", "LogWarn",
    };

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.LogAndRethrow);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    /// <summary>Analyzes one catch clause.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        var statements = catchClause.Block.Statements;
        var count = statements.Count;
        if (count < MinimumStatements || statements[count - 1] is not ThrowStatementSyntax { Expression: null })
        {
            return;
        }

        var lastLog = count - 1;
        for (var i = 0; i < lastLog; i++)
        {
            if (GetLoggingNamedInvocation(statements[i]) is null)
            {
                return;
            }
        }

        var caught = GetCaughtLocal(context, catchClause);
        for (var i = 0; i < lastLog; i++)
        {
            if (!IsConfirmedLoggingCall(context, GetLoggingNamedInvocation(statements[i])!, caught))
            {
                return;
            }
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.LogAndRethrow, catchClause.CatchKeyword.GetLocation()));
    }

    /// <summary>Returns the invocation a statement wraps, when it is a call whose member name is a logging name.</summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The invocation, or <see langword="null"/> when the statement is not a logging-named call.</returns>
    private static InvocationExpressionSyntax? GetLoggingNamedInvocation(StatementSyntax statement)
    {
        if (statement is not ExpressionStatementSyntax expressionStatement)
        {
            return null;
        }

        var expression = expressionStatement.Expression is ConditionalAccessExpressionSyntax conditional
            ? conditional.WhenNotNull
            : expressionStatement.Expression;

        return expression is InvocationExpressionSyntax invocation
            && InvokedName.Of(invocation.Expression) is { } name
            && IsLoggingName(name)
            ? invocation
            : null;
    }

    /// <summary>Confirms a logging-named call is a real logging call, not a coincidental name match.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The logging-named invocation.</param>
    /// <param name="caught">The caught exception local, or <see langword="null"/> when the catch names none.</param>
    /// <returns><see langword="true"/> when the call logs on a logger-typed receiver or is handed the caught exception.</returns>
    private static bool IsConfirmedLoggingCall(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, ISymbol? caught)
    {
        if (GetReceiver(invocation) is { } receiver
            && IsLoggerType(context.SemanticModel.GetTypeInfo(receiver, context.CancellationToken).Type))
        {
            return true;
        }

        return caught is not null
            && InvokedName.Of(invocation.Expression) is { } name
            && IsStrongLoggingName(name)
            && ArgumentsReferenceCaught(context, invocation, caught);
    }

    /// <summary>Returns the value a call is made on, following a null-conditional access.</summary>
    /// <param name="invocation">The invocation.</param>
    /// <returns>The receiver expression, or <see langword="null"/> when the call has no simple receiver.</returns>
    private static ExpressionSyntax? GetReceiver(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            MemberBindingExpressionSyntax when invocation.Parent is ConditionalAccessExpressionSyntax conditional => conditional.Expression,
            _ => null,
        };

    /// <summary>Returns whether an argument of a call references the caught exception local.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="invocation">The invocation.</param>
    /// <param name="caught">The caught exception local.</param>
    /// <returns><see langword="true"/> when the caught local is passed, whole or as a member of it.</returns>
    private static bool ArgumentsReferenceCaught(in SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, ISymbol caught)
    {
        var arguments = invocation.ArgumentList.Arguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (IdentifierReferences.References(arguments[i].Expression, caught, context.SemanticModel, context.CancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads the caught exception local a catch declares, when it names one.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="catchClause">The catch clause.</param>
    /// <returns>The caught local symbol, or <see langword="null"/>.</returns>
    private static ISymbol? GetCaughtLocal(in SyntaxNodeAnalysisContext context, CatchClauseSyntax catchClause) =>
        catchClause.Declaration is { Identifier.ValueText.Length: > 0 } declaration
            ? context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken)
            : null;

    /// <summary>Returns whether a member name is one a logging call uses.</summary>
    /// <param name="name">The invoked member's name.</param>
    /// <returns><see langword="true"/> for a logging method name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLoggingName(string name) => LoggingNames.Contains(name);

    /// <summary>Returns whether a member name is an unambiguous <c>Log…</c> logging name.</summary>
    /// <param name="name">The invoked member's name.</param>
    /// <returns><see langword="true"/> for a name only a logger uses.</returns>
    /// <remarks>
    /// The bare-word names (<c>Error</c>, <c>Debug</c>, and the like) are logging methods only when they sit
    /// on a logger-typed receiver. On any other receiver they are too common to treat as logging on the
    /// strength of the caught exception being passed, so the caught-exception route is limited to these.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsStrongLoggingName(string name) => StrongLoggingNames.Contains(name);

    /// <summary>Returns whether a type is a logger — itself, a base type, or an interface named as one.</summary>
    /// <param name="type">The receiver's type, if resolved.</param>
    /// <returns><see langword="true"/> when the type presents a logger name.</returns>
    private static bool IsLoggerType(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsLoggerName(current.Name))
            {
                return true;
            }

            var interfaces = current.AllInterfaces;
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (IsLoggerName(interfaces[i].Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type name reads as a logger's.</summary>
    /// <param name="name">The type's simple name.</param>
    /// <returns><see langword="true"/> for a name a logging abstraction uses.</returns>
    private static bool IsLoggerName(string name) =>
        name == "ILog" || name.EndsWith("Logger", System.StringComparison.Ordinal);
}
