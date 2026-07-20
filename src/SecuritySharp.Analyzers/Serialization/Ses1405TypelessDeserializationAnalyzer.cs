// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags MessagePack's typeless (open type binding) deserialization surface (SES1405). Typeless
/// serialization embeds the full .NET type name of every value in the payload and reconstructs that
/// exact type on read, the same open type binding that makes <c>BinaryFormatter</c> dangerous, so an
/// attacker who controls the bytes chooses which type is instantiated (CWE-502). The rule reports two
/// local shapes: a <c>MessagePackSerializer.Typeless.Deserialize</c>/<c>DeserializeAsync</c> call
/// (the always-typeless facade), and a <c>.Instance</c> reference to a typeless resolver
/// (<c>MessagePack.Resolvers.TypelessObjectResolver</c> or
/// <c>MessagePack.Resolvers.TypelessContractlessStandardResolver</c>) that opens the type set. A
/// resolver first stored in a field or passed through a variable is out of scope because confirming it
/// would require data-flow tracking. The rule is resolved once per compilation by probing the typeless
/// facade and the two resolver types; a project that does not reference MessagePack resolves none of
/// them and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1405TypelessDeserializationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The typeless deserialization method name on the facade.</summary>
    private const string DeserializeMethodName = "Deserialize";

    /// <summary>The asynchronous typeless deserialization method name on the facade.</summary>
    private const string DeserializeAsyncMethodName = "DeserializeAsync";

    /// <summary>The singleton member through which a resolver is referenced.</summary>
    private const string InstanceMemberName = "Instance";

    /// <summary>The simple name of the nested facade that hosts the typeless deserialization methods.</summary>
    private const string TypelessFacadeSimpleName = "Typeless";

    /// <summary>The simple name of the resolver that reconstructs arbitrary types by their embedded name.</summary>
    private const string TypelessObjectResolverSimpleName = "TypelessObjectResolver";

    /// <summary>The simple name of the contractless resolver that also opens the type set.</summary>
    private const string TypelessContractlessResolverSimpleName = "TypelessContractlessStandardResolver";

    /// <summary>The display name reported for a typeless facade call.</summary>
    private const string TypelessFacadeDisplayName = "MessagePackSerializer.Typeless";

    /// <summary>The metadata name of the nested typeless facade.</summary>
    private const string TypelessFacadeMetadataName = "MessagePack.MessagePackSerializer+Typeless";

    /// <summary>The metadata name of the resolver that reconstructs arbitrary types by their embedded name.</summary>
    private const string TypelessObjectResolverMetadataName = "MessagePack.Resolvers.TypelessObjectResolver";

    /// <summary>The metadata name of the contractless resolver that also opens the type set.</summary>
    private const string TypelessContractlessResolverMetadataName = "MessagePack.Resolvers.TypelessContractlessStandardResolver";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.TypelessDeserialization);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var facade = start.Compilation.GetTypeByMetadataName(TypelessFacadeMetadataName);
            var objectResolver = start.Compilation.GetTypeByMetadataName(TypelessObjectResolverMetadataName);
            var contractlessResolver = start.Compilation.GetTypeByMetadataName(TypelessContractlessResolverMetadataName);

            // A project that does not reference MessagePack resolves none of these; nothing registers and it pays nothing.
            if (facade is null && objectResolver is null && contractlessResolver is null)
            {
                return;
            }

            if (facade is not null)
            {
                start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, facade), SyntaxKind.InvocationExpression);
            }

            if (objectResolver is not null || contractlessResolver is not null)
            {
                start.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeResolverReference(nodeContext, objectResolver, contractlessResolver),
                    SyntaxKind.SimpleMemberAccessExpression);
            }
        });
    }

    /// <summary>Reports SES1405 for a <c>MessagePackSerializer.Typeless.Deserialize</c>/<c>DeserializeAsync</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="facade">The resolved typeless facade type.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol facade)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: an '<x>.Typeless.Deserialize(...)'/'DeserializeAsync(...)' call, rejected before binding.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: DeserializeMethodName or DeserializeAsyncMethodName } memberAccess
            || GetRightmostSimpleName(memberAccess.Expression) != TypelessFacadeSimpleName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, facade))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.TypelessDeserialization,
            invocation.SyntaxTree,
            invocation.Span,
            TypelessFacadeDisplayName));
    }

    /// <summary>Reports SES1405 for a <c>.Instance</c> reference to a typeless resolver that opens the type set.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="objectResolver">The resolved <c>TypelessObjectResolver</c>, or <see langword="null"/> when absent.</param>
    /// <param name="contractlessResolver">The resolved <c>TypelessContractlessStandardResolver</c>, or <see langword="null"/> when absent.</param>
    private static void AnalyzeResolverReference(SyntaxNodeAnalysisContext context, INamedTypeSymbol? objectResolver, INamedTypeSymbol? contractlessResolver)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Syntactic prefilter: a '<TypelessResolver>.Instance' reference, rejected on name text before binding.
        if (memberAccess.Name.Identifier.ValueText != InstanceMemberName)
        {
            return;
        }

        var qualifier = GetRightmostSimpleName(memberAccess.Expression);
        if (qualifier is not (TypelessObjectResolverSimpleName or TypelessContractlessResolverSimpleName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken).Symbol is not INamedTypeSymbol resolverType
            || GetResolverDisplayName(resolverType, objectResolver, contractlessResolver) is not { } displayName)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.TypelessDeserialization,
            memberAccess.SyntaxTree,
            memberAccess.Span,
            displayName));
    }

    /// <summary>Returns the display name for a resolver reference when it binds to a gated typeless resolver.</summary>
    /// <param name="resolverType">The bound qualifier type.</param>
    /// <param name="objectResolver">The resolved <c>TypelessObjectResolver</c>, or <see langword="null"/> when absent.</param>
    /// <param name="contractlessResolver">The resolved <c>TypelessContractlessStandardResolver</c>, or <see langword="null"/> when absent.</param>
    /// <returns>The resolver's simple name, or <see langword="null"/> when the qualifier is not a gated resolver.</returns>
    private static string? GetResolverDisplayName(INamedTypeSymbol resolverType, INamedTypeSymbol? objectResolver, INamedTypeSymbol? contractlessResolver)
        => SymbolEqualityComparer.Default.Equals(resolverType, objectResolver) || SymbolEqualityComparer.Default.Equals(resolverType, contractlessResolver)
            ? resolverType.Name
            : null;

    /// <summary>Returns the rightmost simple identifier of a qualifier expression, or <see langword="null"/>.</summary>
    /// <param name="expression">The qualifier expression to the left of a member access.</param>
    /// <returns>The rightmost identifier text, or <see langword="null"/> when the qualifier is not a name.</returns>
    private static string? GetRightmostSimpleName(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => null,
        };
}
