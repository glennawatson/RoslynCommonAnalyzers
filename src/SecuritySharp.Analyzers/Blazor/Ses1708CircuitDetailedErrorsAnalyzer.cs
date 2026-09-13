// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags <c>CircuitOptions.DetailedErrors</c> set to the constant <c>true</c> (SES1708). A server-side Blazor
/// circuit normally returns only a generic error identifier to the browser; enabling detailed errors streams the
/// full exception message and stack trace to every connected client, which belongs only in Development. The rule
/// reports the flag assigned <c>true</c> -- written directly (<c>options.DetailedErrors = true</c>) or as an
/// object-initializer member -- matched by symbol and containing type via <see cref="BlazorFlagAssignment"/>. The
/// <c>Microsoft.AspNetCore.Components.Server.CircuitOptions</c> type is probed on the first syntactic candidate
/// and cached per compilation, so unrelated assignments require no metadata lookup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1708CircuitDetailedErrorsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the flag whose <c>true</c> value ships server exception detail to the client.</summary>
    private const string DetailedErrorsPropertyName = "DetailedErrors";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.CircuitDetailedErrorsEnabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new CircuitOptionsTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, types), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1708 for <c>DetailedErrors = true</c> on the gated circuit-options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation's lazily resolved circuit-options type.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, CircuitOptionsTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        if (!assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            || assignment.Left is not (MemberAccessExpressionSyntax { Name.Identifier.ValueText: DetailedErrorsPropertyName }
                or IdentifierNameSyntax { Identifier.ValueText: DetailedErrorsPropertyName })
            || types.Get() is not { } circuitOptions)
        {
            return;
        }

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

    /// <summary>Resolves the circuit-options type only after a matching flag assignment is found.</summary>
    /// <param name="compilation">The compilation whose references are searched.</param>
    private sealed class CircuitOptionsTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the options type that carries <c>DetailedErrors</c>.</summary>
        private const string CircuitOptionsMetadataName = "Microsoft.AspNetCore.Components.Server.CircuitOptions";

        /// <summary>Caches the resolved type, including a missing result, in an atomically assigned array.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the circuit-options type on first demand.</summary>
        /// <returns>The resolved type, or null when it is unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(CircuitOptionsMetadataName)])[0];
    }
}
