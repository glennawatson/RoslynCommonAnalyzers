// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags <c>CircuitOptions.DetailedErrors</c> set to the constant <c>true</c> (SES1708). A server-side Blazor
/// circuit normally returns only a generic error identifier to the browser; enabling detailed errors streams the
/// full exception message and stack trace to every connected client, which belongs only in Development. The rule
/// reports the flag assigned <c>true</c> -- written directly (<c>options.DetailedErrors = true</c>) or as an
/// object-initializer member -- matched by symbol and containing type via <see cref="BlazorFlagAssignment"/>. The
/// <c>Microsoft.AspNetCore.Components.Server.CircuitOptions</c> type is probed once per compilation and gates the
/// rule, so a project without server-side Blazor registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1708CircuitDetailedErrorsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the flag whose <c>true</c> value ships server exception detail to the client.</summary>
    private const string DetailedErrorsPropertyName = "DetailedErrors";

    /// <summary>The metadata name of the options type that carries <c>DetailedErrors</c>.</summary>
    private const string CircuitOptionsMetadataName = "Microsoft.AspNetCore.Components.Server.CircuitOptions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.CircuitDetailedErrorsEnabled);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(CircuitOptionsMetadataName) is not { } circuitOptions)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, circuitOptions), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1708 for <c>DetailedErrors = true</c> on the gated circuit-options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="circuitOptions">The gated <c>CircuitOptions</c> type resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol circuitOptions)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (!BlazorFlagAssignment.AssignsFlag(
                assignment,
                SyntaxKind.TrueLiteralExpression,
                DetailedErrorsPropertyName,
                circuitOptions,
                context.SemanticModel,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.CircuitDetailedErrorsEnabled,
            assignment.SyntaxTree,
            assignment.Span));
    }
}
