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
/// named <c>IsDevelopment</c> (a purely local ancestor scan, no data-flow). The two option types are probed
/// once per compilation; a project without ASP.NET Core authentication registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1105PlainHttpMetadataRetrievalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the option property whose <c>false</c> assignment is guarded.</summary>
    private const string RequireHttpsMetadataPropertyName = "RequireHttpsMetadata";

    /// <summary>The name of the development-environment guard method that suppresses the diagnostic.</summary>
    private const string DevelopmentGuardMethodName = "IsDevelopment";

    /// <summary>The metadata names of the authentication option types whose property is guarded.</summary>
    private static readonly string[] OptionMetadataNames =
    [
        "Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions",
        "Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.PlainHttpMetadataRetrieval);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var optionTypes = GetOptionTypes(start.Compilation);
            if (optionTypes is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, optionTypes), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1105 for an unguarded <c>RequireHttpsMetadata = false</c> on a gated option type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionTypes">The gated authentication option types resolved for the compilation.</param>
    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context, INamedTypeSymbol?[] optionTypes)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // Syntactic prefilter: '<expr>.RequireHttpsMetadata = false' or the initializer form
        // 'RequireHttpsMetadata = false'. Both bind the left member to the option property below.
        if (!assignment.Right.IsKind(SyntaxKind.FalseLiteralExpression)
            || GetRequireHttpsMetadataTarget(assignment.Left) is not { } memberExpression)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberExpression, context.CancellationToken).Symbol is not IPropertySymbol { Name: RequireHttpsMetadataPropertyName } property
            || !IsGatedOptionType(property.ContainingType, optionTypes)
            || IsInsideDevelopmentGuard(assignment))
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
    private static ExpressionSyntax? GetRequireHttpsMetadataTarget(ExpressionSyntax left)
        => left switch
        {
            // 'options.RequireHttpsMetadata = false'.
            MemberAccessExpressionSyntax { Name.Identifier.ValueText: RequireHttpsMetadataPropertyName } => left,

            // 'new JwtBearerOptions { RequireHttpsMetadata = false }' (object-initializer member).
            IdentifierNameSyntax { Identifier.ValueText: RequireHttpsMetadataPropertyName } => left,

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

    /// <summary>Returns whether an enclosing <c>if</c> or conditional guards the assignment with an <c>IsDevelopment</c> check.</summary>
    /// <param name="assignment">The reported assignment.</param>
    /// <returns><see langword="true"/> when a development-environment guard lexically encloses the assignment.</returns>
    private static bool IsInsideDevelopmentGuard(SyntaxNode assignment)
    {
        for (var ancestor = assignment.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            var condition = ancestor switch
            {
                IfStatementSyntax ifStatement => ifStatement.Condition,
                ConditionalExpressionSyntax conditional => conditional.Condition,
                _ => null,
            };

            if (condition is not null && ContainsDevelopmentGuardCall(condition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a condition subtree calls a method named <c>IsDevelopment</c>.</summary>
    /// <param name="condition">The guard condition to scan.</param>
    /// <returns><see langword="true"/> when the condition contains an <c>IsDevelopment</c> invocation.</returns>
    private static bool ContainsDevelopmentGuardCall(ExpressionSyntax condition)
    {
        if (IsDevelopmentGuardInvocation(condition))
        {
            return true;
        }

        var found = false;
        DescendantTraversalHelper.VisitDescendants<InvocationExpressionSyntax, bool>(
            condition,
            ref found,
            static (InvocationExpressionSyntax invocation, ref bool state) =>
            {
                if (!IsDevelopmentGuardInvocation(invocation))
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a node is an invocation of a method named <c>IsDevelopment</c>.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns><see langword="true"/> for an <c>IsDevelopment</c> invocation.</returns>
    private static bool IsDevelopmentGuardInvocation(SyntaxNode node)
        => node is InvocationExpressionSyntax invocation && GetInvokedName(invocation.Expression) is DevelopmentGuardMethodName;

    /// <summary>Returns the simple method name an invocation targets, ignoring the receiver.</summary>
    /// <param name="invoked">The invocation's callee expression.</param>
    /// <returns>The simple method name, or <see langword="null"/> when it cannot be read syntactically.</returns>
    private static string? GetInvokedName(ExpressionSyntax invoked)
        => invoked switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Resolves the authentication option types present in the compilation.</summary>
    /// <param name="compilation">The compilation to probe.</param>
    /// <returns>An array whose slots hold each resolved option type, or <see langword="null"/> when none resolve.</returns>
    private static INamedTypeSymbol?[]? GetOptionTypes(Compilation compilation)
    {
        INamedTypeSymbol?[]? types = null;
        for (var i = 0; i < OptionMetadataNames.Length; i++)
        {
            if (compilation.GetTypeByMetadataName(OptionMetadataNames[i]) is { } type)
            {
                types ??= new INamedTypeSymbol?[OptionMetadataNames.Length];
                types[i] = type;
            }
        }

        return types;
    }
}
