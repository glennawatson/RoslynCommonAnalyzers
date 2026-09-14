// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Text;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a Data Protection configuration chain that persists the key ring to an explicit repository but
/// never encrypts it at rest (SES1006). The rule triggers on a <c>PersistKeysTo*</c> call --
/// <c>PersistKeysToFileSystem</c>, <c>PersistKeysToDbContext</c>, <c>PersistKeysToAzureBlobStorage</c>,
/// <c>PersistKeysToStackExchangeRedis</c>, or <c>PersistKeysToRegistry</c> -- bound to an extension method
/// on <c>Microsoft.AspNetCore.DataProtection.IDataProtectionBuilder</c>, then scans the enclosing local
/// scope (the configuration lambda body, or, for a bare fluent chain, the single enclosing statement) for a
/// <c>ProtectKeysWith*</c> call -- <c>ProtectKeysWithCertificate</c>, <c>ProtectKeysWithDpapi</c>,
/// <c>ProtectKeysWithDpapiNG</c>, or <c>ProtectKeysWithAzureKeyVault</c> -- on the same builder. When a
/// persistence call is present and no protection call is, the persistence call is reported: selecting an
/// explicit repository disables the default at-rest key encryption, so the keys are written to the store in
/// plaintext. Both persistence and protection methods are bound by symbol (an extension whose <c>this</c>
/// parameter is the gated builder), so a same-named method on an unrelated type is never matched. The scan
/// is a purely local ancestor/descendant walk: no data flow, and a persistence and protection call split
/// across separate non-chained statements are deliberately left alone. <c>IDataProtectionBuilder</c> is
/// probed once per compilation, only after a persistence call with an enclosing scope is found.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1006UnprotectedDataProtectionKeysAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the Data Protection builder that gates the rule.</summary>
    private const string DataProtectionBuilderMetadataName = "Microsoft.AspNetCore.DataProtection.IDataProtectionBuilder";

    /// <summary>The persistence method names that select an explicit key repository and disable default at-rest encryption.</summary>
    private static readonly string[] PersistMethodNames =
    [
        "PersistKeysToFileSystem",
        "PersistKeysToDbContext",
        "PersistKeysToAzureBlobStorage",
        "PersistKeysToStackExchangeRedis",
        "PersistKeysToRegistry"
    ];

    /// <summary>The protection method names that re-enable at-rest key encryption for a persisted key ring.</summary>
    private static readonly string[] ProtectMethodNames =
    [
        "ProtectKeysWithCertificate",
        "ProtectKeysWithDpapi",
        "ProtectKeysWithDpapiNG",
        "ProtectKeysWithAzureKeyVault"
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.UnprotectedDataProtectionKeys);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyCompilationValue<INamedTypeSymbol?>(compilation, ResolveBuilderType, runOnce: true),
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports SES1006 for a <c>PersistKeysTo*</c> call whose scope holds no <c>ProtectKeysWith*</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderType">The <c>IDataProtectionBuilder</c> type resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, LazyCompilationValue<INamedTypeSymbol?> builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member-access persistence call. The builder receiver is required, so
        // an unqualified identifier can never reach the extension method and is ignored.
        if (FluentConfigurationScope.GetInvokedName(invocation.Expression) is not { } persistName
            || !StringArrays.ContainsOrdinal(PersistMethodNames, persistName.Identifier.ValueText)
            || FluentConfigurationScope.GetScope(invocation) is not { } scope)
        {
            return;
        }

        if (builderType.Get() is not { } resolvedBuilderType
            || FluentConfigurationScope.ContainsBuilderCall(scope, context.SemanticModel, resolvedBuilderType, IsProtectKeysCall, context.CancellationToken)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsBuilderExtension(method, resolvedBuilderType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnprotectedDataProtectionKeys,
            invocation.SyntaxTree,
            TextSpan.FromBounds(persistName.SpanStart, invocation.Span.End),
            persistName.Identifier.ValueText));
    }

    /// <summary>Returns whether an invocation is a <c>ProtectKeysWith*</c> call bound to the gated builder.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>IDataProtectionBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a <c>ProtectKeysWith*</c> call on the gated builder.</returns>
    private static bool IsProtectKeysCall(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken) =>
        FluentConfigurationScope.GetInvokedName(invocation.Expression) is { } calleeName
            && StringArrays.ContainsOrdinal(ProtectMethodNames, calleeName.Identifier.ValueText)
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol method
            && IsBuilderExtension(method, builderType);

    /// <summary>Returns whether a bound method is an extension method whose <c>this</c> parameter is the gated builder.</summary>
    /// <param name="method">The bound invocation symbol.</param>
    /// <param name="builderType">The gated <c>IDataProtectionBuilder</c> type.</param>
    /// <returns><see langword="true"/> when the method extends the gated builder.</returns>
    private static bool IsBuilderExtension(IMethodSymbol method, INamedTypeSymbol builderType)
    {
        // The reduced form of an instance-style extension call drops the 'this' parameter, so bind against
        // the original definition to inspect the receiver type; a static call keeps the definition as-is.
        var definition = method.ReducedFrom ?? method;
        return definition.IsExtensionMethod
            && !definition.Parameters.IsEmpty
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, builderType);
    }

    /// <summary>Resolves the Data Protection builder type for a compilation.</summary>
    /// <param name="compilation">The compilation whose builder type is resolved.</param>
    /// <returns>The builder type, or <see langword="null"/> when it is absent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static INamedTypeSymbol? ResolveBuilderType(Compilation compilation) =>
        compilation.GetTypeByMetadataName(DataProtectionBuilderMetadataName);
}
