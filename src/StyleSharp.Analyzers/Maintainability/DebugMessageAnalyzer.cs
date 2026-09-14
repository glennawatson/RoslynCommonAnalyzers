// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires <c>Debug.Assert</c> (SST1405) and <c>Debug.Fail</c> (SST1406) calls to
/// pass message text. Resolution of <c>System.Diagnostics.Debug</c> is done once per
/// compilation, only after a possible call without a message is found.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DebugMessageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The <c>Debug.Assert</c> method name.</summary>
    private const string AssertName = "Assert";

    /// <summary>The <c>Debug.Fail</c> method name.</summary>
    private const string FailName = "Fail";

    /// <summary>The argument index of the message in <c>Debug.Assert</c>.</summary>
    private const int AssertMessageIndex = 1;

    /// <summary>The argument index of the message in <c>Debug.Fail</c>.</summary>
    private const int FailMessageIndex = 0;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        MaintainabilityRules.AssertMessage,
        MaintainabilityRules.FailMessage);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, "System.Diagnostics.Debug"),
            Analyze,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports a Debug.Assert/Debug.Fail call that omits a message.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="debug">The <c>System.Diagnostics.Debug</c> symbol, resolved on first demand.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, LazyMetadataType debug)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        var messageIndex = name switch
        {
            AssertName => AssertMessageIndex,
            FailName => FailMessageIndex,
            _ => -1
        };

        if (messageIndex < 0 || HasMessage(invocation.ArgumentList.Arguments, messageIndex))
        {
            return;
        }

        if (debug.Get() is not { } resolved
            || !InvocationTargets.IsMethodOf(context.SemanticModel, invocation, resolved, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            name == AssertName ? MaintainabilityRules.AssertMessage : MaintainabilityRules.FailMessage,
            access.Name.GetLocation()));
    }

    /// <summary>Returns whether a non-empty message argument occupies the given position.</summary>
    /// <param name="arguments">The call arguments.</param>
    /// <param name="messageIndex">The index of the message parameter.</param>
    /// <returns><see langword="true"/> when a usable message is present.</returns>
    private static bool HasMessage(SeparatedSyntaxList<ArgumentSyntax> arguments, int messageIndex)
    {
        if (arguments.Count <= messageIndex)
        {
            return false;
        }

        var expression = arguments[messageIndex].Expression;
        return expression is not LiteralExpressionSyntax literal || !string.IsNullOrWhiteSpace(literal.Token.ValueText);
    }
}
