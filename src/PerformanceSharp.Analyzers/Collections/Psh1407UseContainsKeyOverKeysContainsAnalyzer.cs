// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>dictionary.Keys.Contains(key)</c> (PSH1407): asking the keys view answers the
/// dictionary's own membership question the slow way — the view may be allocated and enumerated
/// (or LINQ's <c>Contains</c> walked) where <c>ContainsKey</c> is a single hash probe. A chain is
/// reported when <c>Keys</c> binds to an instance property and the receiver's static type (its
/// declared chain, or the interface set for interface and type-parameter receivers) exposes an
/// accessible <c>bool ContainsKey(TKey)</c> — so <c>Dictionary&lt;,&gt;</c>,
/// <c>IDictionary&lt;,&gt;</c>, <c>IReadOnlyDictionary&lt;,&gt;</c>, and their implementations
/// qualify, while a custom type with a <c>Keys</c> list but no <c>ContainsKey</c> does not.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1407UseContainsKeyOverKeysContainsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The dictionary's direct membership method the fix calls.</summary>
    internal const string ContainsKeyMethodName = "ContainsKey";

    /// <summary>The keys-view membership method this rule replaces.</summary>
    private const string ContainsMethodName = "Contains";

    /// <summary>The keys-view property name.</summary>
    private const string KeysPropertyName = "Keys";

    /// <summary>The metadata name of the generic dictionary interface used as the compilation gate.</summary>
    private const string IDictionaryMetadataName = "System.Collections.Generic.IDictionary`2";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseContainsKeyOverKeysContains);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(IDictionaryMetadataName) is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation has the single-argument <c>x.Keys.Contains(key)</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="keysAccess">The <c>x.Keys</c> member access when the shape matches.</param>
    /// <returns><see langword="true"/> when the syntax-only chain shape matches.</returns>
    internal static bool IsKeysContainsShape(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out MemberAccessExpressionSyntax? keysAccess)
    {
        if (invocation.ArgumentList.Arguments.Count == 1
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: ContainsMethodName } containsAccess
            && containsAccess.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: KeysPropertyName } keys)
        {
            keysAccess = keys;
            return true;
        }

        keysAccess = null;
        return false;
    }

    /// <summary>Reports PSH1407 for a Keys.Contains chain on a receiver that exposes ContainsKey.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsKeysContainsShape(invocation, out var keysAccess))
        {
            return;
        }

        // Both the collection's own Contains and the LINQ extension count; only require that the
        // call actually binds so broken code is not reported.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(keysAccess, context.CancellationToken).Symbol is not IPropertySymbol { IsStatic: false } keysProperty)
        {
            return;
        }

        var dictionaryType = context.SemanticModel.GetTypeInfo(keysAccess.Expression, context.CancellationToken).Type ?? keysProperty.ContainingType;
        if (!HasAccessibleContainsKey(dictionaryType))
        {
            return;
        }

        var containsAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseContainsKeyOverKeysContains,
            invocation.SyntaxTree,
            containsAccess.Name.Span));
    }

    /// <summary>Returns whether a receiver's static type exposes an accessible <c>bool ContainsKey(TKey)</c>.</summary>
    /// <param name="type">The receiver's static type.</param>
    /// <returns><see langword="true"/> when the rewritten <c>ContainsKey</c> call would compile at the call site.</returns>
    private static bool HasAccessibleContainsKey(ITypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (HasDirectContainsKey(current))
            {
                return true;
            }
        }

        if (type.TypeKind is not (TypeKind.Interface or TypeKind.TypeParameter))
        {
            return false;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (HasDirectContainsKey(interfaces[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type directly declares a public instance <c>bool ContainsKey</c> with one parameter.</summary>
    /// <param name="type">The type whose declared members are searched.</param>
    /// <returns><see langword="true"/> when a matching method is declared on <paramref name="type"/> itself.</returns>
    private static bool HasDirectContainsKey(ITypeSymbol type)
    {
        var members = type.GetMembers(ContainsKeyMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1, ReturnType.SpecialType: SpecialType.System_Boolean })
            {
                return true;
            }
        }

        return false;
    }
}
