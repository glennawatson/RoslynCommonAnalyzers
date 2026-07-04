// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>Substring</c> results passed to methods whose overload set accepts
/// <c>ReadOnlySpan&lt;char&gt;</c> in the same position (PSH1212). <c>AsSpan</c> takes the
/// same start and length arguments and produces the slice with no allocation. Reported only
/// when the span overload exists — identical apart from the one parameter — and when
/// <c>AsSpan</c> resolves at the call site, so the rename fix always compiles.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1212AsSpanOverSubstringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The sliced member name the syntax gate requires.</summary>
    internal const string SubstringMethodName = "Substring";

    /// <summary>The replacement member name.</summary>
    internal const string AsSpanMethodName = "AsSpan";

    /// <summary>The metadata name of the extensions type providing AsSpan.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <summary>The most arguments a Substring call carries (start and length).</summary>
    private const int MaxSliceArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseAsSpanOverSubstring);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensions
                || extensions.GetMembers(AsSpanMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a plain <c>x.Substring(...)</c> in an argument position, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsSubstringArgumentShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count is >= 1 and <= MaxSliceArgumentCount
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == SubstringMethodName
            && invocation.Parent is ArgumentSyntax { NameColon: null };

    /// <summary>Reports PSH1212 for a Substring argument the consumer can take as a span.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsSubstringArgumentShape(invocation)
            || invocation.Parent!.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax outer } argumentList
            || TryBindConsumer(context, invocation, outer) is not { } method)
        {
            return;
        }

        var index = argumentList.Arguments.IndexOf((ArgumentSyntax)invocation.Parent!);
        if (index >= method.Parameters.Length
            || method.Parameters[index].Type.SpecialType != SpecialType.System_String
            || !HasSpanOverload(method, index)
            || !ResolvesAsSpan(context.SemanticModel, invocation.SpanStart))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseAsSpanOverSubstring,
            invocation.SyntaxTree,
            invocation.Span,
            method.Name));
    }

    /// <summary>Binds the consuming invocation when the sliced receiver is a string.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The Substring invocation.</param>
    /// <param name="outer">The consuming invocation.</param>
    /// <returns>The consumer's method symbol, or <see langword="null"/>.</returns>
    private static IMethodSymbol? TryBindConsumer(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, InvocationExpressionSyntax outer)
    {
        var model = context.SemanticModel;
        var access = (MemberAccessExpressionSyntax)invocation.Expression;
        if (model.GetTypeInfo(access.Expression, context.CancellationToken).Type?.SpecialType != SpecialType.System_String
            || model.GetSymbolInfo(outer, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.IsExtensionMethod
            || method.ReducedFrom is not null)
        {
            return null;
        }

        return method;
    }

    /// <summary>Scans the method group for a sibling overload taking a char span at the slot.</summary>
    /// <param name="method">The bound string-taking method.</param>
    /// <param name="index">The parameter position of the Substring result.</param>
    /// <returns><see langword="true"/> when the span overload exists.</returns>
    private static bool HasSpanOverload(IMethodSymbol method, int index)
    {
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && !SymbolEqualityComparer.Default.Equals(sibling, method)
                && !sibling.IsGenericMethod
                && sibling.IsStatic == method.IsStatic
                && sibling.Parameters.Length == method.Parameters.Length
                && AcceptsCharSpanAt(sibling, method, index))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a sibling matches the method except for a char-span slot.</summary>
    /// <param name="sibling">The candidate overload.</param>
    /// <param name="method">The bound string-taking method.</param>
    /// <param name="index">The parameter position of the Substring result.</param>
    /// <returns><see langword="true"/> when the slot is <c>ReadOnlySpan&lt;char&gt;</c> and other parameters match.</returns>
    private static bool AcceptsCharSpanAt(IMethodSymbol sibling, IMethodSymbol method, int index)
    {
        for (var i = 0; i < sibling.Parameters.Length; i++)
        {
            if (i == index)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(sibling.Parameters[i].Type, method.Parameters[i].Type))
            {
                return false;
            }
        }

        return sibling.Parameters[index].Type is INamedTypeSymbol
        {
            Name: "ReadOnlySpan",
            IsGenericType: true,
            TypeArguments: [{ SpecialType: SpecialType.System_Char }],
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };
    }

    /// <summary>Returns whether the AsSpan extension resolves by simple type name at a position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The lookup position.</param>
    /// <returns><see langword="true"/> when the extension form binds after the rename.</returns>
    private static bool ResolvesAsSpan(SemanticModel model, int position)
    {
        foreach (var candidate in model.LookupNamespacesAndTypes(position, name: "MemoryExtensions"))
        {
            if (candidate is INamedTypeSymbol { ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }
}
