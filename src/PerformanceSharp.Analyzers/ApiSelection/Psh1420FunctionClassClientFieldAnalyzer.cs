// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a heavyweight, shareable client kept in an instance field or instance auto-property of an
/// isolated-worker function class (PSH1420): a class that declares at least one method carrying
/// <c>Microsoft.Azure.Functions.Worker.FunctionAttribute</c>. The worker constructs that class once per
/// invocation, so a per-instance <c>HttpClient</c> — whose connection pool abandons a socket per call —
/// or one of the cloud service clients this rule shares with the per-call construction family is
/// effectively rebuilt and discarded on every request, exhausting sockets and connections under load.
/// The fix is a <c>static</c> or singleton instance, or an injected <c>IHttpClientFactory</c> for
/// <c>HttpClient</c>.
/// </summary>
/// <remarks>
/// <para>
/// This rule owns the field shape that the per-call construction rule deliberately leaves alone. That rule
/// reports a client whose position proves it dies with the call — a <c>using</c> over the construction, or a
/// construction used directly as a call receiver — and treats a field assignment as potentially long-lived.
/// In a function class the field is not long-lived: the instance itself is per-invocation, so the field is
/// reported here on the member declaration rather than the construction, and the two rules never fire on the
/// same span. The known-client set and its lazy per-compilation resolver are shared with that rule.
/// </para>
/// <para>
/// Both <c>FunctionAttribute</c> and <c>HttpClient</c> are resolved on first demand per compilation.
/// Per class the clean path is pure syntax: a single member scan that bails unless the class
/// has both a method whose attribute is spelled <c>Function</c> and an instance field or auto-property. Only
/// then is the attribute bound to confirm it is the worker attribute, and each candidate member bound to
/// confirm its type against a client resolved lazily and cached for the compilation.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1420FunctionClassClientFieldAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name a <c>[Function]</c> attribute is written as.</summary>
    private const string FunctionAttributeShortName = "Function";

    /// <summary>The unabbreviated simple name of the function attribute.</summary>
    private const string FunctionAttributeTypeName = "FunctionAttribute";

    /// <summary>The suggestion appended for the service clients, which are all safe to share across threads.</summary>
    private const string SharedClientSuggestion = "cache one shared instance for the lifetime of the process, or inject a registered singleton, instead";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.ShareClientAcrossInvocations);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new FunctionTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeType(nodeContext, markers),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
        });
    }

    /// <summary>Reports each per-invocation client member of a confirmed function class.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The function and client types resolved on first demand.</param>
    private static void AnalyzeType(in SyntaxNodeAnalysisContext context, FunctionTypes markers)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        if (!HasFunctionMethodAndInstanceMember(type)
            || markers.Get() is not [var types]
            || !DeclaresFunction(context.SemanticModel, type, types.FunctionAttribute, context.CancellationToken))
        {
            return;
        }

        foreach (var member in type.Members)
        {
            if (member is FieldDeclarationSyntax field && !IsStaticOrConst(field.Modifiers))
            {
                ReportClientField(context, field, types.HttpClient, types.ClientTypes, types.HttpClientSuggestion);
            }
            else if (member is PropertyDeclarationSyntax property && IsInstanceAutoProperty(property))
            {
                ReportClientProperty(context, property, types.HttpClient, types.ClientTypes, types.HttpClientSuggestion);
            }
        }
    }

    /// <summary>Returns whether a class syntactically has both a function-named method and an instance data member.</summary>
    /// <param name="type">The type declaration to inspect.</param>
    /// <returns><see langword="true"/> when both are present, so binding is worthwhile.</returns>
    private static bool HasFunctionMethodAndInstanceMember(TypeDeclarationSyntax type)
    {
        var hasFunctionMethod = false;
        var hasInstanceMember = false;
        foreach (var member in type.Members)
        {
            if (!hasFunctionMethod && member is MethodDeclarationSyntax method && HasFunctionAttributeName(method))
            {
                hasFunctionMethod = true;
            }
            else if (!hasInstanceMember && IsInstanceFieldOrAutoProperty(member))
            {
                hasInstanceMember = true;
            }

            if (hasFunctionMethod && hasInstanceMember)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member is an instance field or an instance auto-property.</summary>
    /// <param name="member">The member to inspect.</param>
    /// <returns><see langword="true"/> when the member has a per-instance backing store.</returns>
    private static bool IsInstanceFieldOrAutoProperty(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax field => !IsStaticOrConst(field.Modifiers),
        PropertyDeclarationSyntax property => IsInstanceAutoProperty(property),
        _ => false,
    };

    /// <summary>Returns whether a method syntactically carries an attribute spelled <c>Function</c>.</summary>
    /// <param name="method">The method to inspect.</param>
    /// <returns><see langword="true"/> when an attribute name matches, before any binding.</returns>
    private static bool HasFunctionAttributeName(MethodDeclarationSyntax method)
    {
        foreach (var list in method.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                if (IsFunctionAttributeName(attribute.Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an attribute's written simple name is the function attribute.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns><see langword="true"/> when the name is <c>Function</c> or <c>FunctionAttribute</c>.</returns>
    private static bool IsFunctionAttributeName(NameSyntax name) =>
        GetSimpleName(name) is FunctionAttributeShortName or FunctionAttributeTypeName;

    /// <summary>Confirms the class declares a method whose function-named attribute binds to the worker attribute.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The type declaration being analyzed.</param>
    /// <param name="functionAttributeType">The resolved worker function attribute.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the class is a function class.</returns>
    private static bool DeclaresFunction(SemanticModel model, TypeDeclarationSyntax type, INamedTypeSymbol functionAttributeType, CancellationToken cancellationToken)
    {
        foreach (var member in type.Members)
        {
            if (member is not MethodDeclarationSyntax method)
            {
                continue;
            }

            foreach (var list in method.AttributeLists)
            {
                foreach (var attribute in list.Attributes)
                {
                    if (IsFunctionAttributeName(attribute.Name)
                        && model.GetSymbolInfo(attribute, cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
                        && SymbolEqualityComparer.Default.Equals(constructor.ContainingType, functionAttributeType))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Reports each declarator of a field whose bound type is a known client.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="field">The candidate field declaration.</param>
    /// <param name="httpClientType">The resolved HTTP client type.</param>
    /// <param name="clientTypes">The compilation's lazily resolved client types.</param>
    /// <param name="httpClientSuggestion">The compilation-specific replacement advice for the HTTP client.</param>
    private static void ReportClientField(
        in SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax field,
        INamedTypeSymbol httpClientType,
        Psh1418PerCallHttpClientAnalyzer.ClientTypeCache clientTypes,
        string httpClientSuggestion)
    {
        foreach (var variable in field.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is IFieldSymbol { IsStatic: false, IsConst: false } symbol
                && clientTypes.ResolveBySimpleName(symbol.Type.Name) is { } clientType
                && SymbolEqualityComparer.Default.Equals(symbol.Type, clientType))
            {
                ReportClient(context, variable.Identifier, clientType, httpClientType, httpClientSuggestion);
            }
        }
    }

    /// <summary>Reports an auto-property whose bound type is a known client.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="property">The candidate auto-property declaration.</param>
    /// <param name="httpClientType">The resolved HTTP client type.</param>
    /// <param name="clientTypes">The compilation's lazily resolved client types.</param>
    /// <param name="httpClientSuggestion">The compilation-specific replacement advice for the HTTP client.</param>
    private static void ReportClientProperty(
        in SyntaxNodeAnalysisContext context,
        PropertyDeclarationSyntax property,
        INamedTypeSymbol httpClientType,
        Psh1418PerCallHttpClientAnalyzer.ClientTypeCache clientTypes,
        string httpClientSuggestion)
    {
        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not IPropertySymbol { IsStatic: false } symbol
            || clientTypes.ResolveBySimpleName(symbol.Type.Name) is not { } clientType
            || !SymbolEqualityComparer.Default.Equals(symbol.Type, clientType))
        {
            return;
        }

        ReportClient(context, property.Identifier, clientType, httpClientType, httpClientSuggestion);
    }

    /// <summary>Reports PSH1420 on a member identifier, steering the suggestion by client type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The offending member's identifier token.</param>
    /// <param name="clientType">The resolved client type held by the member.</param>
    /// <param name="httpClientType">The resolved HTTP client type.</param>
    /// <param name="httpClientSuggestion">The compilation-specific replacement advice for the HTTP client.</param>
    private static void ReportClient(
        in SyntaxNodeAnalysisContext context,
        SyntaxToken identifier,
        INamedTypeSymbol clientType,
        INamedTypeSymbol httpClientType,
        string httpClientSuggestion)
    {
        var suggestion = SymbolEqualityComparer.Default.Equals(clientType, httpClientType)
            ? httpClientSuggestion
            : SharedClientSuggestion;

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.ShareClientAcrossInvocations,
            context.Node.SyntaxTree,
            identifier.Span,
            clientType.Name,
            suggestion));
    }

    /// <summary>Returns whether a property is an instance auto-property, and therefore has a backing field.</summary>
    /// <param name="property">The property declaration to inspect.</param>
    /// <returns><see langword="true"/> when the property is a non-static auto-property.</returns>
    private static bool IsInstanceAutoProperty(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is not null
            || property.AccessorList is null
            || property.Modifiers.Any(SyntaxKind.StaticKeyword)
            || property.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || property.Modifiers.Any(SyntaxKind.ExternKeyword))
        {
            return false;
        }

        foreach (var accessor in property.AccessorList.Accessors)
        {
            if (accessor.Body is not null || accessor.ExpressionBody is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a field's modifiers make it static or const, and therefore already shared.</summary>
    /// <param name="modifiers">The field modifiers.</param>
    /// <returns><see langword="true"/> when the field is static or const.</returns>
    private static bool IsStaticOrConst(in SyntaxTokenList modifiers) =>
        modifiers.Any(SyntaxKind.StaticKeyword) || modifiers.Any(SyntaxKind.ConstKeyword);

    /// <summary>Returns the rightmost identifier of a written name, without binding it.</summary>
    /// <param name="name">The written name syntax.</param>
    /// <returns>The simple name, or <see langword="null"/> when the syntax names no simple identifier.</returns>
    private static string? GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
        AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
        _ => null,
    };

    /// <summary>The function types and advice published together for one compilation.</summary>
    /// <param name="FunctionAttribute">The worker function attribute.</param>
    /// <param name="HttpClient">The HTTP client type.</param>
    /// <param name="ClientTypes">The lazily resolved shareable client types.</param>
    /// <param name="HttpClientSuggestion">The compilation-specific HTTP client replacement advice.</param>
    private readonly record struct ResolvedTypes(
        INamedTypeSymbol FunctionAttribute,
        INamedTypeSymbol HttpClient,
        Psh1418PerCallHttpClientAnalyzer.ClientTypeCache ClientTypes,
        string HttpClientSuggestion);

    /// <summary>Resolves function and client metadata only after the class syntax filter passes.</summary>
    /// <param name="compilation">The compilation whose function and client types are resolved.</param>
    private sealed class FunctionTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the isolated-worker function attribute.</summary>
        private const string FunctionAttributeMetadataName = "Microsoft.Azure.Functions.Worker.FunctionAttribute";

        /// <summary>The metadata name of the HTTP client type the rule is gated on.</summary>
        private const string HttpClientMetadataName = "System.Net.Http.HttpClient";

        /// <summary>The metadata name of the dependency-injection client factory.</summary>
        private const string HttpClientFactoryMetadataName = "System.Net.Http.IHttpClientFactory";

        /// <summary>The suggestion appended for <c>HttpClient</c> when the client factory is available.</summary>
        private const string FactorySuggestion = "inject an 'IHttpClientFactory' and create clients from it, or hold one shared 'static' client, instead";

        /// <summary>The suggestion appended for <c>HttpClient</c> when the client factory is not referenced.</summary>
        private const string StaticSuggestion = "hold one shared 'static readonly HttpClient' for the lifetime of the process instead";

        /// <summary>The cached types and suggestion, or an empty result when required types are absent.</summary>
        private ResolvedTypes[]? _resolved;

        /// <summary>Gets the function types on first demand, caching an unavailable result too.</summary>
        /// <returns>The resolved types, or an empty array when the rule cannot apply.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ResolvedTypes[] Get() => _resolved ??= Resolve(compilation);

        /// <summary>Resolves the function marker, HTTP client, and replacement advice.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The resolved types, or an empty array when required types are absent.</returns>
        private static ResolvedTypes[] Resolve(Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(FunctionAttributeMetadataName) is not { } functionAttribute
                || compilation.GetTypeByMetadataName(HttpClientMetadataName) is not { } httpClient)
            {
                return [];
            }

            var suggestion = compilation.GetTypeByMetadataName(HttpClientFactoryMetadataName) is not null
                ? FactorySuggestion
                : StaticSuggestion;
            return [new ResolvedTypes(functionAttribute, httpClient, new Psh1418PerCallHttpClientAnalyzer.ClientTypeCache(compilation, httpClient), suggestion)];
        }
    }
}
