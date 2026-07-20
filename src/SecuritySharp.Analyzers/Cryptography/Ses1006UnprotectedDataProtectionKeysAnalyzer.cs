// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
/// probed once per compilation; a project without ASP.NET Core Data Protection registers nothing and pays
/// nothing.
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

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.UnprotectedDataProtectionKeys);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var builderType = start.Compilation.GetTypeByMetadataName(DataProtectionBuilderMetadataName);
            if (builderType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, builderType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1006 for a <c>PersistKeysTo*</c> call whose scope holds no <c>ProtectKeysWith*</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="builderType">The gated <c>IDataProtectionBuilder</c> type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol builderType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member-access persistence call. The builder receiver is required, so
        // an unqualified identifier can never reach the extension method and is ignored.
        if (GetCalleeName(invocation.Expression) is not { } persistName
            || !NameMatches(persistName.Identifier.ValueText, PersistMethodNames))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsBuilderExtension(method, builderType)
            || GetChainScope(invocation) is not { } scope
            || ScopeProtectsKeys(scope, context.SemanticModel, builderType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnprotectedDataProtectionKeys,
            invocation.SyntaxTree,
            TextSpan.FromBounds(persistName.SpanStart, invocation.Span.End),
            persistName.Identifier.ValueText));
    }

    /// <summary>Returns the enclosing scope to scan: the nearest lambda body, else the nearest statement or single-expression clause.</summary>
    /// <param name="node">The reported <c>PersistKeysTo*</c> invocation.</param>
    /// <returns>The scope node to search for a <c>ProtectKeysWith*</c> call, or <see langword="null"/> when none is found.</returns>
    private static SyntaxNode? GetChainScope(SyntaxNode node)
    {
        SyntaxNode? fallbackScope = null;
        for (var ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            // A configuration lambda is the local unit that holds the whole builder setup: prefer it over any
            // intervening statement so a multi-statement block body is scanned in full.
            if (ancestor is AnonymousFunctionExpressionSyntax lambda)
            {
                return lambda.Body;
            }

            // Outside a lambda the fluent chain lives in one local unit: a statement, an expression-bodied
            // member ('=> chain'), or an initializer ('= chain'). The nearest such unit is the scope.
            fallbackScope ??= ancestor is StatementSyntax or ArrowExpressionClauseSyntax or EqualsValueClauseSyntax ? ancestor : null;

            // No lambda encloses the call once a declaration boundary is reached.
            if (ancestor is MemberDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                break;
            }
        }

        return fallbackScope;
    }

    /// <summary>Returns whether the scope contains a <c>ProtectKeysWith*</c> call on the gated builder.</summary>
    /// <param name="scope">The scope to search.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>IDataProtectionBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a matching <c>ProtectKeysWith*</c> call is present.</returns>
    private static bool ScopeProtectsKeys(SyntaxNode scope, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken)
    {
        // An expression-lambda body can itself be the outermost protection call of a reversed chain.
        // The descendant walk skips its own root, so the scope node is tested first.
        if (scope is InvocationExpressionSyntax rootInvocation && IsProtectKeysCall(rootInvocation, model, builderType, cancellationToken))
        {
            return true;
        }

        var scan = new ProtectKeysScan(model, builderType, false, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, ProtectKeysScan>(
            scope,
            ref scan,
            static (InvocationExpressionSyntax invocation, ref ProtectKeysScan state) =>
            {
                if (!IsProtectKeysCall(invocation, state.Model, state.Builder, state.CancellationToken))
                {
                    return true;
                }

                state.Found = true;
                return false;
            });

        return scan.Found;
    }

    /// <summary>Returns whether an invocation is a <c>ProtectKeysWith*</c> call bound to the gated builder.</summary>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="builderType">The gated <c>IDataProtectionBuilder</c> type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a <c>ProtectKeysWith*</c> call on the gated builder.</returns>
    private static bool IsProtectKeysCall(InvocationExpressionSyntax invocation, SemanticModel model, INamedTypeSymbol builderType, CancellationToken cancellationToken)
        => GetCalleeName(invocation.Expression) is { } calleeName
            && NameMatches(calleeName.Identifier.ValueText, ProtectMethodNames)
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
            && definition.Parameters.Length > 0
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, builderType);
    }

    /// <summary>Returns whether a simple name's text matches one of a curated method-name set.</summary>
    /// <param name="text">The invoked member's simple name text.</param>
    /// <param name="names">The curated set to match against.</param>
    /// <returns><see langword="true"/> when the name is in the set.</returns>
    private static bool NameMatches(string text, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(text, names[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the simple name a member invocation targets, or <see langword="null"/> when there is no receiver.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The invoked member's simple name, or <see langword="null"/> when it is not a member access.</returns>
    private static SimpleNameSyntax? GetCalleeName(ExpressionSyntax invoked)
        => invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null,
        };

    /// <summary>Threads the binding inputs and the found flag through the <c>ProtectKeysWith*</c> descendant scan.</summary>
    /// <param name="Model">The semantic model used to bind candidate invocations.</param>
    /// <param name="Builder">The gated <c>IDataProtectionBuilder</c> type.</param>
    /// <param name="Found">Whether a matching <c>ProtectKeysWith*</c> call has been found.</param>
    /// <param name="CancellationToken">A token that cancels the binding.</param>
    private record struct ProtectKeysScan(SemanticModel Model, INamedTypeSymbol Builder, bool Found, CancellationToken CancellationToken);
}
