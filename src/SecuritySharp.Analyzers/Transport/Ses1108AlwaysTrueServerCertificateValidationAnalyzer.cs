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
/// resolved once per compilation by probing <c>System.Net.Http.HttpClientHandler</c> and confirming the property
/// exists; on a target framework without it (netstandard2.0, .NET Framework) nothing is registered, so a project
/// that cannot reference the property pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1108AlwaysTrueServerCertificateValidationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the handler type that owns the validation callback.</summary>
    private const string HttpClientHandlerMetadataName = "System.Net.Http.HttpClientHandler";

    /// <summary>The name of the custom server-certificate validation callback property.</summary>
    private const string CallbackPropertyName = "ServerCertificateCustomValidationCallback";

    /// <summary>The shapes a callback value can take that let it be reported.</summary>
    private enum CallbackShape
    {
        /// <summary>Not a reportable callback shape.</summary>
        None,

        /// <summary>A lambda or anonymous method already known to always return true.</summary>
        AlwaysTrueLambda,

        /// <summary>A method group whose referenced method still needs to be inspected.</summary>
        MethodGroup,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.AlwaysTrueServerCertificateValidation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var handlerType = start.Compilation.GetTypeByMetadataName(HttpClientHandlerMetadataName);
            if (handlerType is null || handlerType.GetMembers(CallbackPropertyName).Length == 0)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, handlerType), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1108 for an always-true assignment to the server-certificate validation callback.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="handlerType">The gated <c>HttpClientHandler</c> type resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol handlerType)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: only 'x.ServerCertificateCustomValidationCallback = ...' (or the same member in an
        // object initializer) can match, before anything binds.
        if (GetAssignedMemberName(assignment.Left) != CallbackPropertyName)
        {
            return;
        }

        // A lambda's always-true shape is decided syntactically here, so a real validation callback is rejected
        // before the semantic model is touched. A method group needs binding to find its declaration.
        var value = assignment.Right;
        var shape = ClassifyCallback(value);
        if (shape == CallbackShape.None)
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
        if (shape == CallbackShape.MethodGroup && !AlwaysTrueCallback.IsAlwaysTrueMethodGroup(context.SemanticModel, value, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.AlwaysTrueServerCertificateValidation,
            value.SyntaxTree,
            value.Span));
    }

    /// <summary>Returns the simple name an assignment target names, without binding it.</summary>
    /// <param name="left">The left-hand side of the assignment.</param>
    /// <returns>The member's simple name for a member access or an object-initializer target, or <see langword="null"/>.</returns>
    private static string? GetAssignedMemberName(ExpressionSyntax left) => left switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Classifies an assigned callback value into the shape that lets it be reported.</summary>
    /// <param name="value">The right-hand side of the callback assignment.</param>
    /// <returns>The callback shape: an always-true lambda, a bindable method group, or neither.</returns>
    private static CallbackShape ClassifyCallback(ExpressionSyntax value)
    {
        if (value is AnonymousFunctionExpressionSyntax function)
        {
            return AlwaysTrueCallback.IsAlwaysTrueLambda(function) ? CallbackShape.AlwaysTrueLambda : CallbackShape.None;
        }

        return value is IdentifierNameSyntax or MemberAccessExpressionSyntax ? CallbackShape.MethodGroup : CallbackShape.None;
    }
}
