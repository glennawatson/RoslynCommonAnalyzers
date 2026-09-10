// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>Process.ExitCode</c> read that sits beside a <c>WaitForExit</c> call, where the exit
/// code cannot separate a deliberate failure from termination by a signal (SST2499).
/// </summary>
/// <remarks>
/// The whole rule is gated on <c>WaitForExitStatus</c> resolving in the analyzed compilation, so a
/// project targeting a framework without it is never told to call an API it does not have.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2499ProcessExitStatusAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the process type.</summary>
    private const string ProcessMetadataName = "System.Diagnostics.Process";

    /// <summary>The property whose value is ambiguous under signal termination.</summary>
    private const string ExitCodeName = "ExitCode";

    /// <summary>The wait call that marks the shape this rule is about.</summary>
    private const string WaitForExitName = "WaitForExit";

    /// <summary>The replacement that reports how the process actually ended.</summary>
    private const string WaitForExitStatusName = "WaitForExitStatus";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(CorrectnessRules.ProcessExitStatusIgnoresSignal);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var processType = start.Compilation.GetTypeByMetadataName(ProcessMetadataName);
            if (processType is null || processType.GetMembers(WaitForExitStatusName).Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, processType),
                SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Reports one ambiguous exit-code read.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="processType">The resolved process type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol processType)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (access.Name.Identifier.ValueText != ExitCodeName)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (receiverType is null || !SymbolEqualityComparer.Default.Equals(receiverType, processType))
        {
            return;
        }

        if (access.FirstAncestorOrSelf<MemberDeclarationSyntax>() is not { } member || !WaitsForExit(member))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ProcessExitStatusIgnoresSignal,
            access.GetLocation()));
    }

    /// <summary>Gets whether a member contains a call to <c>WaitForExit</c>.</summary>
    /// <param name="member">The enclosing member declaration.</param>
    /// <returns><see langword="true"/> when the member waits on a process.</returns>
    private static bool WaitsForExit(MemberDeclarationSyntax member)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, bool>(member, ref found, Visit);
        return found;
    }

    /// <summary>Notes a <c>WaitForExit</c> invocation and stops the walk once one is seen.</summary>
    /// <param name="invocation">The invocation being visited.</param>
    /// <param name="found">Whether a wait call has been seen.</param>
    /// <returns><see langword="false"/> once a wait call is found.</returns>
    private static bool Visit(InvocationExpressionSyntax invocation, ref bool found)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        if (name is not (WaitForExitName or WaitForExitName + "Async"))
        {
            return true;
        }

        found = true;
        return false;
    }
}
