// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags <c>AuthenticationStateSerializationOptions.SerializeAllClaims</c> set to the constant <c>true</c>
/// (SES1709). When a Blazor Web App serializes the server authentication state for a WebAssembly client, the
/// default emits only the name and role claims; setting this flag serializes every claim on the principal into the
/// client-readable state, disclosing internal identifiers, tokens, and personal data to the browser. The rule
/// reports the flag assigned <c>true</c> -- written directly (<c>options.SerializeAllClaims = true</c>) or as an
/// object-initializer member -- matched by symbol and containing type via <see cref="BlazorFlagAssignment"/>. The
/// <c>Microsoft.AspNetCore.Components.WebAssembly.Server.AuthenticationStateSerializationOptions</c> type is probed
/// only after an assignment passes the name-and-literal screen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1709SerializeAllClaimsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the flag whose <c>true</c> value serializes every claim into the client-readable state.</summary>
    private const string SerializeAllClaimsPropertyName = "SerializeAllClaims";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.SerializeAllClaimsEnabled);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var frameworkType = new FrameworkType(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, frameworkType), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1709 for <c>SerializeAllClaims = true</c> on the gated serialization-options type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="frameworkType">The deferred type lookup shared by this compilation's callbacks.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, FrameworkType frameworkType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (!assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
            || assignment.Left is not (MemberAccessExpressionSyntax { Name.Identifier.ValueText: SerializeAllClaimsPropertyName }
                or IdentifierNameSyntax { Identifier.ValueText: SerializeAllClaimsPropertyName }))
        {
            return;
        }

        if (frameworkType.Get() is not { } serializationOptions
            || !BlazorFlagAssignment.AssignsFlag(
                assignment,
                SyntaxKind.TrueLiteralExpression,
                SerializeAllClaimsPropertyName,
                serializationOptions,
                context.SemanticModel,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.SerializeAllClaimsEnabled,
            assignment.SyntaxTree,
            assignment.Span));
    }

    /// <summary>Resolves the serialization options type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose references supply the type.</param>
    private sealed class FrameworkType(Compilation compilation)
    {
        /// <summary>The metadata name of the options type that carries <c>SerializeAllClaims</c>.</summary>
        private const string AuthenticationStateSerializationOptionsMetadataName =
            "Microsoft.AspNetCore.Components.WebAssembly.Server.AuthenticationStateSerializationOptions";

        /// <summary>The cached type, or an empty array when unavailable; null until first demand.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the serialization options type, caching absent types as well as successful lookups.</summary>
        /// <returns>The resolved type, or null when unavailable.</returns>
        public INamedTypeSymbol? Get()
        {
            var resolved = _resolved ??= compilation.GetTypeByMetadataName(AuthenticationStateSerializationOptionsMetadataName) is { } type ? [type] : [];
            return resolved.Length == 0 ? null : resolved[0];
        }
    }
}
