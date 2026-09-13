// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags <c>RequireHttpsMetadata = false</c> on the JWT-bearer or OpenID Connect authentication options
/// when it is not guarded by a development-environment check (SES1105). The rule reports the assignment
/// -- whether written as <c>options.RequireHttpsMetadata = false</c> or inside an object initializer
/// (<c>new JwtBearerOptions { RequireHttpsMetadata = false }</c>) -- when the assigned member's containing
/// type is <c>Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions</c> or
/// <c>Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions</c>. It stays silent when the
/// assignment is lexically enclosed by an <c>if</c> statement or conditional whose condition calls a method
/// named <c>IsDevelopment</c> (a purely local ancestor scan, no data-flow). The two option types are resolved
/// only after an unguarded assignment passes the syntactic property-name and literal checks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1105PlainHttpMetadataRetrievalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the option property whose <c>false</c> assignment is guarded.</summary>
    private const string RequireHttpsMetadataPropertyName = "RequireHttpsMetadata";

    /// <summary>The metadata names of the authentication option types whose property is guarded.</summary>
    private static readonly string[] OptionMetadataNames =
    [
        "Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions",
        "Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions"
    ];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.PlainHttpMetadataRetrieval);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(static nodeContext => AnalyzeAssignment(nodeContext), SyntaxKind.SimpleAssignmentExpression);
    }

    /// <summary>Reports SES1105 for an unguarded <c>RequireHttpsMetadata = false</c> on a gated option type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.RequireHttpsMetadata = false' or the initializer form
        // 'RequireHttpsMetadata = false'. Both bind the left member to the option property below.
        if (!assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)
            || GetRequireHttpsMetadataTarget(assignment.Left) is not { } memberExpression
            || DevelopmentGuard.Encloses(assignment))
        {
            return;
        }

        if (GetOptionTypes(context.Compilation) is not { } optionTypes
            || context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: RequireHttpsMetadataPropertyName } property
            || !IsGatedOptionType(property.ContainingType, optionTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.PlainHttpMetadataRetrieval,
            assignment.SyntaxTree,
            assignment.Span,
            property.ContainingType.Name));
    }

    /// <summary>Returns the assignment's left expression when it names <c>RequireHttpsMetadata</c>.</summary>
    /// <param name="left">The assignment's left-hand expression.</param>
    /// <returns>The left expression to bind, or <see langword="null"/> when it is not the guarded member.</returns>
    private static ExpressionSyntax? GetRequireHttpsMetadataTarget(ExpressionSyntax left) =>
        left switch
        {
            // 'options.RequireHttpsMetadata = false'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: RequireHttpsMetadataPropertyName } or IdentifierNameSyntax { Identifier.ValueText: RequireHttpsMetadataPropertyName } => left,

            _ => null,
        };

    /// <summary>Returns whether a property's containing type is one of the gated option types.</summary>
    /// <param name="containingType">The bound property's containing type.</param>
    /// <param name="optionTypes">The gated authentication option types resolved for the compilation.</param>
    /// <returns><see langword="true"/> when the container is a gated option type.</returns>
    private static bool IsGatedOptionType(INamedTypeSymbol containingType, INamedTypeSymbol?[] optionTypes)
    {
        for (var i = 0; i < optionTypes.Length; i++)
        {
            if (optionTypes[i] is { } optionType && SymbolEqualityComparer.Default.Equals(optionType, containingType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves the authentication option types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved option type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetOptionTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < OptionMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(OptionMetadataNames[i]) is not { } type)
            {
                continue;
            }

            types ??= new INamedTypeSymbol?[OptionMetadataNames.Length];
            types[i] = type;
        }

        return types;
    }
}
