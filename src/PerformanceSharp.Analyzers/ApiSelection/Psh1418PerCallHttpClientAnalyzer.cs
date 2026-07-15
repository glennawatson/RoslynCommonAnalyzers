// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports an <c>HttpClient</c> that is constructed for a single call and then dropped (PSH1418): a
/// <c>using new HttpClient(...)</c> declaration or statement, and <c>new HttpClient(...).SendAsync(...)</c>
/// used directly as the receiver of the call it feeds. Each such instance carries its own connection pool,
/// so building one per request opens and abandons a socket every time and drains the ephemeral port range
/// under load.
/// </summary>
/// <remarks>
/// <para>
/// Only those two shapes are reported, because only they prove — with no data-flow analysis — that the
/// instance dies with the call. A construction assigned to a field, used to initialise a property, returned
/// from a method, or handed to any other member is left alone: it may already be the single shared instance
/// the fix asks for.
/// </para>
/// <para>
/// The message adapts to the compilation. Where the dependency-injection client factory type resolves, it is
/// named as the destination; where it does not — the usual case for a library that cannot assume a container —
/// the rule steers to a single <c>static readonly HttpClient</c>, which is the correct answer either way. The
/// assembly entry point is exempt: a process that is about to exit exhausts nothing.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>HttpClient</c> resolving, so a project that never
/// references it registers no syntax action at all. The clean path is a parent-shape check and a token
/// comparison, and nothing binds until both hold.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1418PerCallHttpClientAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the constructed type.</summary>
    internal const string HttpClientTypeName = "HttpClient";

    /// <summary>The metadata name of the constructed type.</summary>
    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    /// <summary>The metadata name of the dependency-injection client factory.</summary>
    private const string HttpClientFactoryMetadataName = "System.Net.Http.IHttpClientFactory";

    /// <summary>The suggestion appended when the client factory is available.</summary>
    private const string FactorySuggestion = "obtain clients from the injected 'IHttpClientFactory' instead";

    /// <summary>The suggestion appended when the client factory is not referenced.</summary>
    private const string StaticSuggestion = "share one 'static readonly HttpClient' instead";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.ReuseHttpClient);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(HttpClientMetadataName) is not { } httpClientType)
            {
                return;
            }

            var suggestion = start.Compilation.GetTypeByMetadataName(HttpClientFactoryMetadataName) is not null
                ? FactorySuggestion
                : StaticSuggestion;
            var entryPoint = start.Compilation.GetEntryPoint(start.CancellationToken);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, httpClientType, entryPoint, suggestion),
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression);
        });
    }

    /// <summary>Returns whether a construction's position proves it dies with the call, before any binding.</summary>
    /// <param name="creation">The construction to inspect.</param>
    /// <returns><see langword="true"/> when the construction is a per-call shape.</returns>
    internal static bool IsPerCallShape(BaseObjectCreationExpressionSyntax creation)
    {
        ExpressionSyntax node = creation;
        while (node.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            node = parenthesized;
        }

        if (node.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == node)
        {
            return true;
        }

        if (creation.Parent is UsingStatementSyntax usingStatement && usingStatement.Expression == creation)
        {
            return true;
        }

        if (creation.Parent is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } })
        {
            return false;
        }

        return declaration.Parent switch
        {
            LocalDeclarationStatementSyntax local => !local.UsingKeyword.IsKind(SyntaxKind.None),
            UsingStatementSyntax => true,
            _ => false,
        };
    }

    /// <summary>Reports PSH1418 for a per-call construction of the client type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="httpClientType">The compilation's client type.</param>
    /// <param name="entryPoint">The compilation's entry point, when it has one.</param>
    /// <param name="suggestion">The compilation-specific replacement advice.</param>
    private static void AnalyzeCreation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpClientType,
        IMethodSymbol? entryPoint,
        string suggestion)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsPerCallShape(creation) || !NamesHttpClient(creation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, httpClientType)
            || IsInEntryPoint(context.SemanticModel, creation, entryPoint, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.ReuseHttpClient,
            creation.SyntaxTree,
            creation.Span,
            suggestion));
    }

    /// <summary>Rejects a construction not written as the client type, without binding it.</summary>
    /// <param name="creation">The construction to inspect.</param>
    /// <returns><see langword="true"/> when the written type or its declaration names the client type.</returns>
    private static bool NamesHttpClient(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitCreation => GetSimpleName(explicitCreation.Type) == HttpClientTypeName,
        _ => creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
            && GetSimpleName(declaration.Type) == HttpClientTypeName,
    };

    /// <summary>Returns whether the construction sits inside the assembly entry point.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The construction being reported.</param>
    /// <param name="entryPoint">The compilation's entry point, when it has one.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the enclosing member is the entry point.</returns>
    private static bool IsInEntryPoint(SemanticModel model, BaseObjectCreationExpressionSyntax creation, IMethodSymbol? entryPoint, CancellationToken cancellationToken)
    {
        if (entryPoint is null)
        {
            return false;
        }

        for (var symbol = model.GetEnclosingSymbol(creation.SpanStart, cancellationToken); symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol, entryPoint))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns the rightmost identifier of a written type name.</summary>
    /// <param name="type">The written type syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple type.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
        _ => null,
    };
}
