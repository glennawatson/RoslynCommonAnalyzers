// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports the range indexer applied to an array (PSH1019) — <c>data[1..5]</c> — when the slice is
/// consumed as a read-only span or memory. The indexer allocates a new array and copies the elements
/// into it; <c>AsSpan(1..5)</c> and <c>AsMemory(1..5)</c> return a view over the original and copy
/// nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only where the copy is not the point.</b> A slice consumed as an array is left alone: the caller
/// asked for an array and the rewrite would not even compile. The rule looks at the <i>converted</i>
/// type of the indexer expression, so it fires exactly where the compiler was already going to turn
/// the fresh array into a span or a memory — a parameter, a local, a return — and nowhere else.
/// </para>
/// <para>
/// <b>Only read-only targets.</b> A <c>Span&lt;T&gt;</c> or <c>Memory&lt;T&gt;</c> target is
/// deliberately <i>not</i> reported even though the allocation is just as real, because the copy and
/// the view differ in more than speed there: a write through the view lands on the original array,
/// while a write through the copy does not. Turning one into the other is a behavior change, not an
/// optimization. A <c>ReadOnlySpan&lt;T&gt;</c> or <c>ReadOnlyMemory&lt;T&gt;</c> target cannot write
/// at all, so the view and the copy answer every question the same way.
/// </para>
/// <para>
/// The rule is switched off at compilation start when <c>MemoryExtensions</c> has no
/// <see cref="Range"/>-taking slice, and the rewritten call is bound speculatively before anything is
/// reported, so a target framework that cannot express the fix never sees the diagnostic.
/// </para>
/// <para>
/// Unlike its sibling span rules, this one carries no expression-tree guard, and deliberately: the
/// compiler already refuses a range indexer and a range expression inside a tree, so a slice this rule
/// could report never appears in one and a guard would be code that can never run.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1019UseAsSpanOverRangeIndexerAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming the slice method the fix should emit.</summary>
    internal const string SliceMethodKey = "SliceMethod";

    /// <summary>The span slice method name.</summary>
    internal const string AsSpanMethodName = "AsSpan";

    /// <summary>The memory slice method name.</summary>
    internal const string AsMemoryMethodName = "AsMemory";

    /// <summary>The simple name of the extensions type providing the range slices.</summary>
    internal const string MemoryExtensionsTypeName = "MemoryExtensions";

    /// <summary>The metadata name of the extensions type providing the range slices.</summary>
    private const string MemoryExtensionsMetadataName = "System.MemoryExtensions";

    /// <summary>The read-only span type name a reported slice must be converted to.</summary>
    private const string ReadOnlySpanTypeName = "ReadOnlySpan";

    /// <summary>The read-only memory type name a reported slice must be converted to.</summary>
    private const string ReadOnlyMemoryTypeName = "ReadOnlyMemory";

    /// <summary>The cached properties for a slice the fix should rewrite to <c>AsSpan</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> AsSpanProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SliceMethodKey, AsSpanMethodName);

    /// <summary>The cached properties for a slice the fix should rewrite to <c>AsMemory</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> AsMemoryProperties =
        ImmutableDictionary<string, string?>.Empty.Add(SliceMethodKey, AsMemoryMethodName);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AllocationRules.UseAsSpanOverRangeIndexer);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (!HasRangeSlice(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeElementAccess, SyntaxKind.ElementAccessExpression);
        });
    }

    /// <summary>Returns whether an element access is a plain <c>x[a..b]</c>, before any binding.</summary>
    /// <param name="access">The element access to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsRangeIndexerShape(ElementAccessExpressionSyntax access)
        => access.ArgumentList.Arguments.Count == 1
            && access.ArgumentList.Arguments[0] is { NameColon: null, RefOrOutKeyword.RawKind: (int)SyntaxKind.None, Expression: RangeExpressionSyntax };

    /// <summary>Builds the <c>receiver.AsSpan(range)</c> rewrite for a reported range indexer.</summary>
    /// <param name="access">The reported element access.</param>
    /// <param name="sliceMethod">The slice method name to emit.</param>
    /// <returns>The rewritten invocation.</returns>
    internal static InvocationExpressionSyntax BuildSlice(ElementAccessExpressionSyntax access, string sliceMethod)
        => SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                access.Expression.WithoutTrivia(),
                SyntaxFactory.IdentifierName(sliceMethod)),
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(access.ArgumentList.Arguments[0].Expression.WithoutTrivia()))));

    /// <summary>Returns whether <c>MemoryExtensions</c> exposes a <see cref="Range"/>-taking slice.</summary>
    /// <param name="compilation">The analyzed compilation.</param>
    /// <returns><see langword="true"/> when at least one range slice exists.</returns>
    /// <remarks>
    /// The range slices arrived with <see cref="Range"/> itself, so a target framework old enough to
    /// lack them cannot even parse <c>data[1..5]</c> — but probing the member list rather than
    /// assuming that keeps the rule honest if the two ever come apart.
    /// </remarks>
    private static bool HasRangeSlice(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(MemoryExtensionsMetadataName) is not { } extensions
            || compilation.GetTypeByMetadataName("System.Range") is not { } range)
        {
            return false;
        }

        return HasRangeOverload(extensions, AsSpanMethodName, range) || HasRangeOverload(extensions, AsMemoryMethodName, range);
    }

    /// <summary>Returns whether a named slice takes an array and a <see cref="Range"/>.</summary>
    /// <param name="extensions">The extensions type.</param>
    /// <param name="methodName">The slice method name.</param>
    /// <param name="range">The <see cref="Range"/> symbol.</param>
    /// <returns><see langword="true"/> when the range overload exists.</returns>
    private static bool HasRangeOverload(INamedTypeSymbol extensions, string methodName, INamedTypeSymbol range)
    {
        foreach (var member in extensions.GetMembers(methodName))
        {
            if (member is IMethodSymbol { IsStatic: true, Parameters: [{ Type: IArrayTypeSymbol }, { } second] }
                && SymbolEqualityComparer.Default.Equals(second.Type, range))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports PSH1019 for an array range indexer whose result is consumed as a read-only view.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeElementAccess(SyntaxNodeAnalysisContext context)
    {
        var access = (ElementAccessExpressionSyntax)context.Node;
        if (!IsRangeIndexerShape(access))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (model.GetTypeInfo(access.Expression, cancellationToken).Type is not IArrayTypeSymbol { Rank: 1 })
        {
            return;
        }

        // No expression-tree guard is needed, and one would be dead code: the compiler already refuses
        // a range indexer and a range expression inside a tree (CS8790, CS8792), so a reportable slice
        // can never appear in one.
        var sliced = model.GetTypeInfo(access, cancellationToken);
        if (sliced.Type is not IArrayTypeSymbol
            || GetSliceMethod(sliced.ConvertedType) is not { } sliceMethod
            || !RewriteBindsToSlice(model, access, sliceMethod, sliced.ConvertedType!, cancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            AllocationRules.UseAsSpanOverRangeIndexer,
            access.SyntaxTree,
            access.Span,
            sliceMethod == AsSpanMethodName ? AsSpanProperties : AsMemoryProperties,
            access.ToString()));
    }

    /// <summary>Maps the slice's converted type to the extension that produces the same view without copying.</summary>
    /// <param name="convertedType">The type the slice is converted to at the use site.</param>
    /// <returns>The slice method name, or <see langword="null"/> when the copy is not replaceable.</returns>
    /// <remarks>
    /// Only the read-only views map. A mutable <c>Span&lt;T&gt;</c> or <c>Memory&lt;T&gt;</c> target
    /// would let the consumer write through to the original array, which the copy it replaces never
    /// allowed — that is a change in behavior and no rule of ours makes it silently.
    /// </remarks>
    private static string? GetSliceMethod(ITypeSymbol? convertedType)
    {
        if (convertedType is not INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named
            || named.ContainingNamespace is not { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
        {
            return null;
        }

        return named.Name switch
        {
            ReadOnlySpanTypeName => AsSpanMethodName,
            ReadOnlyMemoryTypeName => AsMemoryMethodName,
            _ => null,
        };
    }

    /// <summary>Confirms the slice rewrite binds to the extension and still converts to what the use site wants.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="access">The reported element access.</param>
    /// <param name="sliceMethod">The slice method name to emit.</param>
    /// <param name="convertedType">The type the slice is converted to at the use site.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the fix compiles and produces the same view.</returns>
    /// <remarks>
    /// Binding the rewrite is the only honest way to know that <c>AsSpan</c> is in scope, that the
    /// range overload really exists on this target, and that its result still converts to the type the
    /// use site asked for. The final conversion check is what stops a rewrite that binds but does not
    /// fit.
    /// </remarks>
    private static bool RewriteBindsToSlice(
        SemanticModel model,
        ElementAccessExpressionSyntax access,
        string sliceMethod,
        ITypeSymbol convertedType,
        CancellationToken cancellationToken)
    {
        // Speculative binding is the most expensive step in the rule and has no cancellable overload,
        // so the token is honoured on the way in instead.
        cancellationToken.ThrowIfCancellationRequested();

        if (model.GetSpeculativeSymbolInfo(access.SpanStart, BuildSlice(access, sliceMethod), SpeculativeBindingOption.BindAsExpression).Symbol
            is not IMethodSymbol { ReturnType: { } returnType } resolved
            || resolved.Name != sliceMethod
            || resolved.ContainingType is not
            {
                Name: MemoryExtensionsTypeName,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            })
        {
            return false;
        }

        return model.Compilation.ClassifyConversion(returnType, convertedType) is { Exists: true, IsImplicit: true };
    }
}
