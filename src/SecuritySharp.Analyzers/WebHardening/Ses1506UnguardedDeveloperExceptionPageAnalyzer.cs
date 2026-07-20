// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags an <c>UseDeveloperExceptionPage</c> call that is not lexically enclosed by a
/// development-environment guard (SES1506). The extension method (on
/// <c>Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions</c>, whose first parameter is
/// <c>IApplicationBuilder</c>) installs middleware that writes full exception detail and stack traces
/// back to the client; that belongs in Development only, so the rule reports the invocation when no
/// enclosing <c>if</c> statement or conditional expression whose condition calls a method named
/// <c>IsDevelopment</c> (for example <c>app.Environment.IsDevelopment()</c>) guards it. The guard scan
/// is a purely local ancestor walk with no data-flow. The extensions type is probed once per
/// compilation; a project without ASP.NET Core hosting registers nothing and pays nothing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1506UnguardedDeveloperExceptionPageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the middleware-registration method that is guarded.</summary>
    private const string UseDeveloperExceptionPageMethodName = "UseDeveloperExceptionPage";

    /// <summary>The name of the development-environment guard method that suppresses the diagnostic.</summary>
    private const string DevelopmentGuardMethodName = "IsDevelopment";

    /// <summary>The metadata name of the type that declares the guarded extension method.</summary>
    private const string DeveloperExceptionPageExtensionsMetadataName =
        "Microsoft.AspNetCore.Builder.DeveloperExceptionPageExtensions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(SecurityRules.UnguardedDeveloperExceptionPage);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(DeveloperExceptionPageExtensionsMetadataName) is not { } extensionsType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, extensionsType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1506 for an unguarded <c>UseDeveloperExceptionPage</c> call on the gated extensions type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="extensionsType">The gated extensions type resolved for the compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol extensionsType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a call to a member named 'UseDeveloperExceptionPage'.
        if (GetInvokedName(invocation.Expression) is not UseDeveloperExceptionPageMethodName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: UseDeveloperExceptionPageMethodName } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, extensionsType)
            || IsInsideDevelopmentGuard(invocation))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnguardedDeveloperExceptionPage,
            invocation.SyntaxTree,
            invocation.Span));
    }

    /// <summary>Returns whether an enclosing <c>if</c> or conditional guards the call with an <c>IsDevelopment</c> check.</summary>
    /// <param name="invocation">The reported invocation.</param>
    /// <returns><see langword="true"/> when a development-environment guard lexically encloses the call.</returns>
    private static bool IsInsideDevelopmentGuard(SyntaxNode invocation)
    {
        for (var ancestor = invocation.Parent; ancestor is not null; ancestor = ancestor.Parent)
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
}
