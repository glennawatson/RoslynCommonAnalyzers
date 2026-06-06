// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires <c>Debug.Assert</c> (SST1405) and <c>Debug.Fail</c> (SST1406) calls to
/// pass message text. Resolution of <c>System.Diagnostics.Debug</c> is done once per
/// compilation, so the rule costs nothing when the type is absent.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.AssertMessage,
        MaintainabilityRules.FailMessage);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var debug = start.Compilation.GetTypeByMetadataName("System.Diagnostics.Debug");
            if (debug is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, debug), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports a Debug.Assert/Debug.Fail call that omits a message.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="debug">The resolved <c>System.Diagnostics.Debug</c> symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol debug)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var name = access.Name.Identifier.ValueText;
        var (rule, messageIndex) = name switch
        {
            AssertName => (MaintainabilityRules.AssertMessage, AssertMessageIndex),
            FailName => (MaintainabilityRules.FailMessage, FailMessageIndex),
            _ => (null!, -1),
        };

        if (rule is null || HasMessage(invocation.ArgumentList.Arguments, messageIndex))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, debug))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, access.Name.GetLocation()));
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
