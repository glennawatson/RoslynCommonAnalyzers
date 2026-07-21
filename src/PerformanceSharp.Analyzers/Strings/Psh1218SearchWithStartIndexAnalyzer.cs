// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a substring allocated only to be searched (PSH1218) — <c>text.Substring(i).IndexOf(v)</c>
/// and its <c>LastIndexOf</c>, <c>Contains</c>, <c>StartsWith</c>, and <c>EndsWith</c> siblings. The
/// tail of the string is copied before a single character is compared; slicing with <c>AsSpan(i)</c>
/// searches the same characters in place and allocates nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The index basis.</b> The obvious rewrite — <c>text.IndexOf(v, i)</c> — is not the one this rule
/// makes, and deliberately so. The string overload's result is relative to the <i>original</i> string
/// while the substring's is relative to the <i>slice</i>, so the two differ by <c>i</c>, and a naive
/// <c>- i</c> correction turns the not-found <c>-1</c> into <c>-1 - i</c>. <c>text.AsSpan(i)</c> has
/// no such problem: a span search reports its hit relative to the span, exactly as the substring did,
/// and returns <c>-1</c> on a miss just the same. The rewrite is therefore value-preserving whether
/// the result is tested as a boolean or used as an index, and the fix is offered in both cases.
/// <c>string.LastIndexOf(v, i)</c> is never suggested either — it searches <i>backward</i> from
/// <c>i</c>, which is not what the substring form does at all, while a span's <c>LastIndexOf</c>
/// searches the whole slice and matches.
/// </para>
/// <para>
/// <b>The comparison basis.</b> A span search is ordinal; several string searches are not. Only calls
/// whose semantics carry across untouched are reported: the <c>char</c> overloads and
/// <c>Contains(string)</c>, which are already ordinal, and any overload that names its own
/// <see cref="StringComparison"/>, which the span overload takes as well. A culture-sensitive
/// <c>IndexOf(string)</c> is left alone rather than silently turned ordinal — PSH1207 is the rule
/// that asks for a <see cref="StringComparison"/> there.
/// </para>
/// <para>
/// The rewritten call is then bound speculatively, so the rule only fires when the
/// <c>MemoryExtensions</c> overload really exists on the target framework, resolves at the call site,
/// and returns the same type. That keeps the rule correct across .NET 8 through .NET 11, where the
/// span search surface differs — <c>StartsWith(span, char)</c>, for instance, only arrives in .NET 10.
/// A substring used for anything besides the one search never matches the shape, so it is never
/// reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1218SearchWithStartIndexAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The sliced member name the syntax gate requires.</summary>
    internal const string SubstringMethodName = "Substring";

    /// <summary>The replacement member name.</summary>
    internal const string AsSpanMethodName = "AsSpan";

    /// <summary>The message display of the reported slice.</summary>
    private const string SubstringDisplay = "Substring";

    /// <summary>The search whose <see cref="string"/> overload taking a string is already ordinal.</summary>
    private const string ContainsMethodName = "Contains";

    /// <summary>The metadata name of the extensions type providing the span slice and the span searches.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <summary>The simple name of the extensions type providing the span slice and the span searches.</summary>
    private const string MemoryExtensionsTypeName = "MemoryExtensions";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.SearchWithStartIndex);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensions
                || extensions.GetMembers(AsSpanMethodName).IsEmpty)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeSearch, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Returns whether an invocation is a plain <c>x.Substring(i)</c>, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    /// <remarks>
    /// A named argument is rejected because <c>Substring</c> calls its parameter <c>startIndex</c>
    /// while <c>AsSpan</c> calls it <c>start</c>, so the rename would not compile.
    /// </remarks>
    internal static bool IsSubstringSliceShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 1
            && invocation.ArgumentList.Arguments[0] is { NameColon: null, RefOrOutKeyword.RawKind: (int)SyntaxKind.None }
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == SubstringMethodName;

    /// <summary>Returns whether a member name is one of the searches the rule rewrites.</summary>
    /// <param name="name">The invoked member name.</param>
    /// <returns><see langword="true"/> for a reported search.</returns>
    internal static bool IsSearchName(string name)
        => name is "IndexOf" or "LastIndexOf" or ContainsMethodName or "StartsWith" or "EndsWith";

    /// <summary>Reports PSH1218 for a substring that exists only to be searched.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeSearch(SyntaxNodeAnalysisContext context)
    {
        var outer = (InvocationExpressionSyntax)context.Node;
        if (!TryGetSearchShape(outer, out var slice, out var searchName))
        {
            return;
        }

        var model = context.SemanticModel;
        if (!BindsToStringSubstring(model, slice!, context.CancellationToken)
            || model.GetSymbolInfo(outer, context.CancellationToken).Symbol is not IMethodSymbol search
            || search.IsStatic
            || search.ContainingType.SpecialType != SpecialType.System_String
            || !PreservesComparison(search)
            || !RewriteBindsToSpanSearch(model, outer, slice!, search, context.CancellationToken)
            || IsInsideExpressionTree(outer, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.SearchWithStartIndex,
            slice!.SyntaxTree,
            slice.Span,
            SubstringDisplay,
            searchName!));
    }

    /// <summary>Splits a <c>x.Substring(i).Search(...)</c> call into its slice and its search name, syntactically.</summary>
    /// <param name="outer">The candidate search invocation.</param>
    /// <param name="slice">The inner <c>Substring</c> invocation when the shape matches.</param>
    /// <param name="searchName">The invoked search name when the shape matches.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    private static bool TryGetSearchShape(
        InvocationExpressionSyntax outer,
        out InvocationExpressionSyntax? slice,
        out string? searchName)
    {
        if (outer.ArgumentList.Arguments.Count > 0
            && outer.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Expression is InvocationExpressionSyntax candidate
            && IsSearchName(access.Name.Identifier.ValueText)
            && IsSubstringSliceShape(candidate))
        {
            slice = candidate;
            searchName = access.Name.Identifier.ValueText;
            return true;
        }

        slice = null;
        searchName = null;
        return false;
    }

    /// <summary>Returns whether the inner call is the one-argument <see cref="string.Substring(int)"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="slice">The inner invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the slice takes a start index off a string.</returns>
    private static bool BindsToStringSubstring(SemanticModel model, InvocationExpressionSyntax slice, CancellationToken cancellationToken)
        => model.GetSymbolInfo(slice, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Parameters: [{ Type.SpecialType: SpecialType.System_Int32 }],
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Returns whether a string search means the same thing once it is done on a span.</summary>
    /// <param name="search">The bound search method.</param>
    /// <returns><see langword="true"/> when the call's comparison semantics survive the rewrite.</returns>
    /// <remarks>
    /// The span searches are ordinal. A <c>char</c> search and <c>Contains(string)</c> already are,
    /// and an overload that names a <see cref="StringComparison"/> hands the same value to the span
    /// overload, so all three carry across exactly. Everything else — a bare
    /// <c>IndexOf(string)</c>, <c>StartsWith(string)</c>, an <c>ignoreCase</c>-plus-culture overload
    /// — is culture-sensitive, and turning it ordinal behind the author's back is a behavior change,
    /// not an optimization.
    /// </remarks>
    private static bool PreservesComparison(IMethodSymbol search)
    {
        var parameters = search.Parameters;
        if (parameters.Length == 1)
        {
            var only = parameters[0].Type.SpecialType;
            return only == SpecialType.System_Char
                || (only == SpecialType.System_String && search.Name == ContainsMethodName);
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            if (IsStringComparison(parameters[i].Type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <see cref="StringComparison"/>.</summary>
    /// <param name="type">The parameter type to inspect.</param>
    /// <returns><see langword="true"/> for <c>System.StringComparison</c>.</returns>
    private static bool IsStringComparison(ITypeSymbol type)
        => type is INamedTypeSymbol
        {
            Name: nameof(StringComparison),
            TypeKind: TypeKind.Enum,
            ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
        };

    /// <summary>Confirms the <c>AsSpan</c> rewrite binds to a span search with the same result type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="outer">The search invocation.</param>
    /// <param name="slice">The inner <c>Substring</c> invocation.</param>
    /// <param name="search">The bound string search.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the fix compiles and answers the same question.</returns>
    /// <remarks>
    /// This is what makes the rule safe on every target framework at once. The span search surface
    /// grew over time — <c>StartsWith(span, char)</c> and <c>EndsWith(span, char)</c> only exist from
    /// .NET 10 — and binding the rewritten call is the only honest way to know whether the overload
    /// the fix needs is actually there, whether <c>AsSpan</c> is even in scope, and whether the result
    /// is still the <see cref="int"/> or <see cref="bool"/> the caller consumes.
    /// </remarks>
    private static bool RewriteBindsToSpanSearch(
        SemanticModel model,
        InvocationExpressionSyntax outer,
        InvocationExpressionSyntax slice,
        IMethodSymbol search,
        CancellationToken cancellationToken)
    {
        // Speculative binding is the most expensive step in the rule and the semantic model has no
        // cancellable overload of it, so the token is honoured on the way in instead.
        cancellationToken.ThrowIfCancellationRequested();

        // A call reached through a conditional access cannot be speculatively rebound: detaching the outer call
        // to test the span rewrite orphans its member or element binding and Roslyn's binder then dereferences
        // null. The rewrite stays unverified, so the search call keeps its start-index form.
        if (ConditionalAccessSpeculation.ReachedThroughConditionalAccess(outer.Expression))
        {
            return false;
        }

        var sliceName = ((MemberAccessExpressionSyntax)slice.Expression).Name;
        var rewritten = outer.ReplaceNode(sliceName, SyntaxFactory.IdentifierName(AsSpanMethodName));
        if (model.GetSpeculativeSymbolInfo(outer.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol
            is not IMethodSymbol resolved)
        {
            return false;
        }

        return resolved.Name == search.Name
            && SymbolEqualityComparer.Default.Equals(resolved.ReturnType, search.ReturnType)
            && resolved.ContainingType is
            {
                Name: MemoryExtensionsTypeName,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            };
    }

    /// <summary>Returns whether a call sits inside an expression tree.</summary>
    /// <param name="node">The search invocation.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a span may not be introduced here.</returns>
    /// <remarks>
    /// An expression tree cannot hold a <c>ref struct</c>, so an <c>AsSpan</c> slice inside one stops
    /// compiling (CS8640) even though the same slice is fine everywhere else. A LINQ query expression
    /// is skipped too: its lambdas are hidden, and an <c>IQueryable</c> source turns them into
    /// expression trees.
    /// </remarks>
    private static bool IsInsideExpressionTree(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is QueryExpressionSyntax)
            {
                return true;
            }

            if (current is LambdaExpressionSyntax
                && model.GetTypeInfo(current, cancellationToken).ConvertedType is INamedTypeSymbol
                {
                    Name: "Expression",
                    ContainingNamespace:
                    {
                        Name: "Expressions",
                        ContainingNamespace: { Name: "Linq", ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true } },
                    },
                })
            {
                return true;
            }

            if (current is MemberDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
