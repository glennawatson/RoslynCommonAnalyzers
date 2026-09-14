// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a custom <c>HttpClientHandler.ServerCertificateCustomValidationCallback</c> that always accepts the
/// server certificate (SES1108). A callback assigned to that property is what validates the TLS server
/// certificate; one whose body unconditionally yields <see langword="true"/> — an expression lambda
/// <c>(message, cert, chain, errors) =&gt; true</c>, a block lambda or anonymous method whose only result is
/// <c>return true;</c>, or a method group to a source method of that shape — turns server authentication off and
/// opens the connection to man-in-the-middle attacks. The callback body is inspected only locally, so a real
/// validation callback is never reported, and the built-in
/// <c>DangerousAcceptAnyServerCertificateValidator</c> sentinel is left to the rule that owns it. The rule is
/// resolved on the first syntactic candidate by probing <c>System.Net.Http.HttpClientHandler</c> and confirming
/// the property exists; the result, including an unavailable property, is cached for the compilation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1108AlwaysTrueServerCertificateValidationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the custom server-certificate validation callback property.</summary>
    private const string CallbackPropertyName = "ServerCertificateCustomValidationCallback";

    /// <summary>The metadata name of the handler type that owns the validation callback.</summary>
    private const string HttpClientHandlerMetadataName = "System.Net.Http.HttpClientHandler";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.AlwaysTrueServerCertificateValidation);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveHandlerTypes),
            AnalyzeAssignment,
            SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports SES1108 for an always-true assignment to the server-certificate validation callback.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The callback owner type resolved on first candidate.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: only 'x.ServerCertificateCustomValidationCallback = ...' (or the same member in an
        // object initializer) can match, before anything binds.
        if (MemberReferenceName.Of(assignment.Left) != CallbackPropertyName)
        {
            return;
        }

        // A lambda's always-true shape is decided syntactically here, so a real validation callback is rejected
        // before the semantic model is touched. A method group needs binding to find its declaration.
        var value = assignment.Right;
        var shape = AlwaysTrueCallback.Classify(value);
        if (shape == AlwaysTrueCallbackShape.None)
        {
            return;
        }

        if (types.Get() is not { } handlerType)
        {
            return;
        }

        // Bind the target: report only when it truly resolves to the property on HttpClientHandler, so a
        // same-named member on an unrelated type is never flagged.
        if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol { Name: CallbackPropertyName } property
            || !SymbolEqualityComparer.Default.Equals(property.ContainingType, handlerType))
        {
            return;
        }

        // A method group is always-true only when its referenced source method is; the built-in
        // 'DangerousAcceptAnyServerCertificateValidator' is a property, not a method, so it is never matched here.
        if (shape == AlwaysTrueCallbackShape.MethodGroup && !AlwaysTrueCallback.IsAlwaysTrueMethodGroup(context.SemanticModel, value, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AlwaysTrueServerCertificateValidation,
            value.SyntaxTree,
            value.Span));
    }

    /// <summary>Resolves the handler type and confirms the callback API on demand.</summary>
    /// <param name="compilation">The compilation the value is resolved from.</param>
    /// <returns>The handler type, or null when the callback API is unavailable.</returns>
    private static INamedTypeSymbol? ResolveHandlerTypes(Compilation compilation) =>
        compilation.GetTypeByMetadataName(HttpClientHandlerMetadataName) is { } handlerType
            && !handlerType.GetMembers(CallbackPropertyName).IsEmpty ? handlerType : null;
}
