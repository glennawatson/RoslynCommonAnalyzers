// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a heavyweight, shareable client that is constructed for a single call and then dropped
/// (PSH1418): a <c>using</c> (or <c>await using</c>) declaration or statement over the construction,
/// and <c>new Client(...).Method(...)</c> used directly as the receiver of the call it feeds. Beyond
/// <c>HttpClient</c>, whose per-instance connection pool abandons a socket per call, the same
/// treatment covers the cloud service clients that are documented as thread-safe and intended to be
/// cached for the lifetime of the process — the storage, messaging, document-database, and secret
/// clients — because each construction builds a transport pipeline, authentication state, and
/// metadata caches that die with the call.
/// </summary>
/// <remarks>
/// <para>
/// Only those two shapes are reported, because only they prove — with no data-flow analysis — that the
/// instance dies with the call. A construction assigned to a field, used to initialise a property, returned
/// from a method, or handed to any other member is left alone: it may already be the single shared instance
/// the fix asks for.
/// </para>
/// <para>
/// The message adapts to the compilation. For <c>HttpClient</c>, where the dependency-injection client
/// factory type resolves it is named as the destination; where it does not — the usual case for a library
/// that cannot assume a container — the rule steers to a single <c>static readonly HttpClient</c>. The
/// service clients all get the same steer: cache one shared instance for the lifetime of the process. The
/// assembly entry point is exempt: a process that is about to exit exhausts nothing.
/// </para>
/// <para>
/// The whole rule is gated at compilation start on <c>HttpClient</c> resolving — every supported client's
/// SDK layers on the HTTP stack, so a compilation where <c>HttpClient</c> is absent cannot construct any of
/// them and registers no syntax action at all. The clean path is a parent-shape check and a token
/// comparison; each service-client type is resolved lazily, the first time its simple name appears in a
/// per-call shape, and the result (present or absent) is cached for the compilation, so a project without a
/// client's package pays a single failed lookup for it at most.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1418PerCallHttpClientAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the HTTP client type.</summary>
    internal const string HttpClientTypeName = "HttpClient";

    /// <summary>The index of the HTTP client in <see cref="ClientMetadataNames"/>.</summary>
    private const int HttpClientIndex = 0;

    /// <summary>The metadata name of the HTTP client type.</summary>
    private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

    /// <summary>The metadata name of the dependency-injection client factory.</summary>
    private const string HttpClientFactoryMetadataName = "System.Net.Http.IHttpClientFactory";

    /// <summary>The suggestion appended when the client factory is available.</summary>
    private const string FactorySuggestion = "obtain clients from the injected 'IHttpClientFactory' instead";

    /// <summary>The suggestion appended when the client factory is not referenced.</summary>
    private const string StaticSuggestion = "share one 'static readonly HttpClient' instead";

    /// <summary>The suggestion appended for the service clients, which are all safe to share across threads.</summary>
    private const string SharedClientSuggestion = "cache one shared instance for the lifetime of the process instead";

    /// <summary>
    /// The metadata names of the reported client types, indexed like <see cref="ClientSimpleNames"/>.
    /// Every entry must be documented as thread-safe, intended for process-lifetime reuse, and expensive
    /// to construct, so the shared-instance steer is always correct.
    /// </summary>
    private static readonly string[] ClientMetadataNames =
    [
        HttpClientMetadataName,
        "Azure.Storage.Blobs.BlobServiceClient",
        "Azure.Storage.Queues.QueueServiceClient",
        "Azure.Storage.Files.Shares.ShareServiceClient",
        "Azure.Storage.Files.DataLake.DataLakeServiceClient",
        "Azure.Data.Tables.TableServiceClient",
        "Azure.Messaging.ServiceBus.ServiceBusClient",
        "Azure.Messaging.EventHubs.Producer.EventHubProducerClient",
        "Azure.Messaging.EventGrid.EventGridPublisherClient",
        "Microsoft.Azure.Cosmos.CosmosClient",
        "Azure.Security.KeyVault.Secrets.SecretClient",
        "Azure.Security.KeyVault.Keys.KeyClient",
        "Azure.Security.KeyVault.Certificates.CertificateClient",
    ];

    /// <summary>The simple names of the reported client types.</summary>
    private static readonly string[] ClientSimpleNames =
    [
        HttpClientTypeName,
        "BlobServiceClient",
        "QueueServiceClient",
        "ShareServiceClient",
        "DataLakeServiceClient",
        "TableServiceClient",
        "ServiceBusClient",
        "EventHubProducerClient",
        "EventGridPublisherClient",
        "CosmosClient",
        "SecretClient",
        "KeyClient",
        "CertificateClient",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.ReuseSharedClient);

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

            var httpClientSuggestion = start.Compilation.GetTypeByMetadataName(HttpClientFactoryMetadataName) is not null
                ? FactorySuggestion
                : StaticSuggestion;
            var entryPoint = start.Compilation.GetEntryPoint(start.CancellationToken);
            var clientTypes = new ClientTypeCache(start.Compilation, httpClientType);

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeCreation(nodeContext, clientTypes, entryPoint, httpClientSuggestion),
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

    /// <summary>Reports PSH1418 for a per-call construction of a known client type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="clientTypes">The compilation's lazily resolved client types.</param>
    /// <param name="entryPoint">The compilation's entry point, when it has one.</param>
    /// <param name="httpClientSuggestion">The compilation-specific replacement advice for the HTTP client.</param>
    private static void AnalyzeCreation(
        SyntaxNodeAnalysisContext context,
        ClientTypeCache clientTypes,
        IMethodSymbol? entryPoint,
        string httpClientSuggestion)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (!IsPerCallShape(creation))
        {
            return;
        }

        var writtenName = GetWrittenTypeName(creation);
        var index = GetKnownClientIndex(writtenName);
        if (index < 0 || clientTypes.Resolve(index) is not { } clientType)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, clientType)
            || IsInEntryPoint(context.SemanticModel, creation, entryPoint, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.ReuseSharedClient,
            creation.SyntaxTree,
            creation.Span,
            writtenName!,
            index == HttpClientIndex ? httpClientSuggestion : SharedClientSuggestion));
    }

    /// <summary>Maps a written simple name to its index in the client tables, without binding.</summary>
    /// <param name="simpleName">The written simple name, when the construction spells one.</param>
    /// <returns>The index into <see cref="ClientMetadataNames"/>, or -1 when the name is not a known client.</returns>
    private static int GetKnownClientIndex(string? simpleName)
    {
        if (simpleName is not null)
        {
            for (var i = 0; i < ClientSimpleNames.Length; i++)
            {
                if (ClientSimpleNames[i] == simpleName)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>Returns the simple name a construction is written as, without binding it.</summary>
    /// <param name="creation">The construction to inspect.</param>
    /// <returns>The written type's simple name, or <see langword="null"/> when nothing spells one.</returns>
    private static string? GetWrittenTypeName(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitCreation => GetSimpleName(explicitCreation.Type),
        _ => creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax declaration } }
            ? GetSimpleName(declaration.Type)
            : null,
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

    /// <summary>Lazily resolves and caches the known client types for one compilation.</summary>
    /// <remarks>
    /// Each slot moves from unprobed (<see langword="null"/>) to either the resolved symbol or the
    /// absent sentinel, so a compilation probes each metadata name once and a project without a
    /// client's package pays one failed lookup for it in total. The unsynchronized writes race
    /// benignly: concurrent probes of the same slot compute the same value.
    /// </remarks>
    internal sealed class ClientTypeCache
    {
        /// <summary>The sentinel cached when a metadata name does not resolve.</summary>
        private static readonly object Absent = new();

        /// <summary>The compilation the client types resolve in.</summary>
        private readonly Compilation _compilation;

        /// <summary>The per-index slots: unprobed, <see cref="Absent"/>, or the resolved symbol.</summary>
        private readonly object?[] _clientTypes;

        /// <summary>Initializes a new instance of the <see cref="ClientTypeCache"/> class.</summary>
        /// <param name="compilation">The compilation the client types resolve in.</param>
        /// <param name="httpClientType">The already-resolved HTTP client type.</param>
        internal ClientTypeCache(Compilation compilation, INamedTypeSymbol httpClientType)
        {
            _compilation = compilation;
            _clientTypes = new object?[ClientMetadataNames.Length];
            _clientTypes[HttpClientIndex] = httpClientType;
        }

        /// <summary>Resolves the client type at an index, probing its metadata name at most once.</summary>
        /// <param name="index">The index into <see cref="ClientMetadataNames"/>.</param>
        /// <returns>The resolved type, or <see langword="null"/> when the compilation does not reference it.</returns>
        internal INamedTypeSymbol? Resolve(int index)
        {
            var cached = Volatile.Read(ref _clientTypes[index]);
            if (cached is null)
            {
                cached = (object?)_compilation.GetTypeByMetadataName(ClientMetadataNames[index]) ?? Absent;
                Volatile.Write(ref _clientTypes[index], cached);
            }

            return cached as INamedTypeSymbol;
        }

        /// <summary>Resolves the client type whose simple name matches, probing its metadata name at most once.</summary>
        /// <param name="simpleName">The written simple name to resolve, when the syntax spells one.</param>
        /// <returns>The resolved client type, or <see langword="null"/> when the name is not a known client or its package is absent.</returns>
        internal INamedTypeSymbol? ResolveBySimpleName(string? simpleName)
        {
            var index = GetKnownClientIndex(simpleName);
            return index < 0 ? null : Resolve(index);
        }
    }
}
