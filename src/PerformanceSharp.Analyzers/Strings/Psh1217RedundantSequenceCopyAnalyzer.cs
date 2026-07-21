// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports <c>string.ToCharArray()</c> and <c>ReadOnlySpan&lt;T&gt;.ToArray()</c> whose array is
/// handed straight to something the original would have satisfied (PSH1217) — a <c>foreach</c>, a
/// <c>Length</c> read, an indexer, or a parameter that also has a sequence-taking overload. In each
/// of those the allocation and the copy buy nothing.
/// </summary>
/// <remarks>
/// <para>
/// The consumers are a whitelist, not a blacklist. A copy that is stored, returned, assigned,
/// mutated, or handed to an API that genuinely wants an array simply never matches a reported shape,
/// so the mutable-buffer case <c>ToCharArray</c> exists for is left alone by construction rather than
/// by a list of exclusions that could be incomplete. The element-write shapes (<c>arr[0] = 'x'</c>,
/// <c>arr[0]++</c>, <c>ref arr[0]</c>) are rejected explicitly because a string and a
/// <c>ReadOnlySpan&lt;T&gt;</c> are both read-only and the rewrite would not compile.
/// </para>
/// <para>
/// Two deliberate narrowings. A <c>foreach</c> is only reported for a string receiver: a span is a
/// <c>ref struct</c>, and enumerating one directly inside an <c>async</c> method or an iterator keeps
/// it alive across an <c>await</c> or a <c>yield</c>, which does not compile. And the argument case is
/// confirmed by speculatively binding the rewritten call, because passing a <c>string</c> where a
/// <c>char[]</c> used to go can land on an overload that was never meant to receive it — a
/// <c>Use(object)</c> sibling swallows the string happily, and which conversion wins depends on the
/// language version the host compiler is running. Only a rewrite that provably lands on a
/// sequence-taking overload with the same shape and the same return type is reported.
/// </para>
/// <para>
/// PSH1209 owns the copy-mutate-rebuild pattern (<c>new string(s.ToCharArray())</c> after writing to
/// the array); this rule stays out of its way because a mutated array is never a reported shape.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1217RedundantSequenceCopyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The copying member name on <see cref="string"/>.</summary>
    internal const string ToCharArrayMethodName = "ToCharArray";

    /// <summary>The copying member name on <c>ReadOnlySpan&lt;T&gt;</c>.</summary>
    internal const string ToArrayMethodName = "ToArray";

    /// <summary>The member name of the length read the copy is not needed for.</summary>
    private const string LengthPropertyName = "Length";

    /// <summary>The simple name of the span type whose <c>ToArray</c> is reported.</summary>
    private const string ReadOnlySpanTypeName = "ReadOnlySpan";

    /// <summary>The message display of the string copy.</summary>
    private const string ToCharArrayDisplay = "ToCharArray()";

    /// <summary>The message display of the span copy.</summary>
    private const string ToArrayDisplay = "ToArray()";

    /// <summary>The message display of a <c>foreach</c> consumer.</summary>
    private const string ForEachDisplay = "foreach";

    /// <summary>The message display of an indexer consumer.</summary>
    private const string IndexerDisplay = "the indexer";

    /// <summary>The message display of the sequence a string copy came from.</summary>
    private const string StringDisplay = "string";

    /// <summary>The message display of the sequence a span copy came from.</summary>
    private const string SpanDisplay = "span";

    /// <summary>The sequence the copied array was made from.</summary>
    private enum SequenceSource
    {
        /// <summary>Not a reported copy.</summary>
        None,

        /// <summary>A <c>string.ToCharArray()</c> copy.</summary>
        String,

        /// <summary>A <c>ReadOnlySpan&lt;T&gt;.ToArray()</c> copy.</summary>
        Span,
    }

    /// <summary>What the copied array is handed to.</summary>
    private enum ConsumerKind
    {
        /// <summary>Not a consumer the original would satisfy.</summary>
        None,

        /// <summary>The array is enumerated by a <c>foreach</c>.</summary>
        ForEach,

        /// <summary>The array's <c>Length</c> is read.</summary>
        Length,

        /// <summary>A single element of the array is read.</summary>
        Indexer,

        /// <summary>The array is passed as an argument.</summary>
        Argument,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.RedundantSequenceCopy);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether an invocation is a bare <c>x.ToCharArray()</c> or <c>x.ToArray()</c>, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsSequenceCopyShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText is ToCharArrayMethodName or ToArrayMethodName;

    /// <summary>Reports PSH1217 for a copy the consumer never needed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsSequenceCopyShape(invocation))
        {
            return;
        }

        var kind = GetConsumerKind(invocation);
        if (kind == ConsumerKind.None)
        {
            return;
        }

        var source = GetBoundSequenceSource(context, invocation, out var elementType);
        if (source == SequenceSource.None)
        {
            return;
        }

        if (GetConsumerDisplay(context, invocation, kind, source, elementType) is not { } consumer)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.RedundantSequenceCopy,
            invocation.GetLocation(),
            source == SequenceSource.String ? ToCharArrayDisplay : ToArrayDisplay,
            consumer,
            source == SequenceSource.String ? StringDisplay : SpanDisplay));
    }

    /// <summary>Classifies what the copy's result is handed to, syntactically.</summary>
    /// <param name="invocation">The copying invocation.</param>
    /// <returns>The consumer kind, or <see cref="ConsumerKind.None"/>.</returns>
    /// <remarks>
    /// Anything that is not one of these four shapes — a local, a field, a <c>return</c>, an
    /// assignment, a cast, a second call — keeps the array, so the copy is not redundant and the
    /// invocation is dropped here without binding anything.
    /// </remarks>
    private static ConsumerKind GetConsumerKind(InvocationExpressionSyntax invocation) => invocation.Parent switch
    {
        ForEachStatementSyntax forEach when forEach.Expression == invocation => ConsumerKind.ForEach,
        MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            when access.Expression == invocation && access.Name.Identifier.ValueText == LengthPropertyName => ConsumerKind.Length,
        ElementAccessExpressionSyntax elementAccess
            when elementAccess.Expression == invocation
                && elementAccess.ArgumentList.Arguments.Count == 1
                && !IsWriteTarget(elementAccess) => ConsumerKind.Indexer,
        ArgumentSyntax { NameColon: null, Parent.Parent: InvocationExpressionSyntax } argument
            when argument.RefOrOutKeyword.RawKind == (int)SyntaxKind.None => ConsumerKind.Argument,
        _ => ConsumerKind.None,
    };

    /// <summary>Returns whether an element access writes to, or takes a reference to, its element.</summary>
    /// <param name="elementAccess">The element access on the copied array.</param>
    /// <returns><see langword="true"/> when the element is not merely read.</returns>
    /// <remarks>
    /// A <c>char[]</c> element is a writable storage location; a <c>string</c> or
    /// <c>ReadOnlySpan&lt;T&gt;</c> element is not. Every shape that needs the location rather than
    /// the value would stop compiling after the rewrite, so none of them is reported.
    /// </remarks>
    private static bool IsWriteTarget(ElementAccessExpressionSyntax elementAccess) => elementAccess.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == elementAccess,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PreIncrementExpression or (int)SyntaxKind.PreDecrementExpression } => true,
        PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.PostIncrementExpression or (int)SyntaxKind.PostDecrementExpression } => true,
        RefExpressionSyntax => true,
        ArgumentSyntax argument => argument.RefOrOutKeyword.RawKind != (int)SyntaxKind.None,
        _ => false,
    };

    /// <summary>Binds the copying invocation and classifies the sequence its array was made from.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The copying invocation.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns>The sequence source, or <see cref="SequenceSource.None"/> when this is not a copy the rule reports.</returns>
    private static SequenceSource GetBoundSequenceSource(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        out ITypeSymbol? elementType)
    {
        elementType = null;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol copy
            || !IsInstanceCopyCall(copy))
        {
            return SequenceSource.None;
        }

        return GetSequenceSource(copy, out elementType);
    }

    /// <summary>Returns whether a bound member really is the framework's parameterless copy.</summary>
    /// <param name="copy">The bound copying method.</param>
    /// <returns><see langword="true"/> when the call is the copy the rule knows how to drop.</returns>
    /// <remarks>
    /// A static look-alike, an argument-taking one, and a user-defined extension method named
    /// <c>ToArray</c> all share the reported name, and none of them is the copy the receiver already
    /// satisfies.
    /// </remarks>
    private static bool IsInstanceCopyCall(IMethodSymbol copy)
        => !copy.IsStatic
            && copy.Parameters.Length == 0
            && !copy.IsExtensionMethod
            && copy.ReducedFrom is null;

    /// <summary>Classifies the receiver the copy was taken from.</summary>
    /// <param name="copy">The bound copying method.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns>The sequence source, or <see cref="SequenceSource.None"/>.</returns>
    private static SequenceSource GetSequenceSource(IMethodSymbol copy, out ITypeSymbol? elementType)
    {
        elementType = null;
        var containingType = copy.ContainingType;
        if (copy.Name == ToCharArrayMethodName)
        {
            return containingType.SpecialType == SpecialType.System_String ? SequenceSource.String : SequenceSource.None;
        }

        if (containingType is not
            {
                Name: ReadOnlySpanTypeName,
                IsGenericType: true,
                TypeArguments.Length: 1,
                ContainingNamespace: { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true },
            })
        {
            return SequenceSource.None;
        }

        elementType = containingType.TypeArguments[0];
        return SequenceSource.Span;
    }

    /// <summary>Confirms the consumer really does accept the original, and returns its message display.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The copying invocation.</param>
    /// <param name="kind">The consumer kind matched syntactically.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns>The consumer's display, or <see langword="null"/> when the copy must stay.</returns>
    private static string? GetConsumerDisplay(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        ConsumerKind kind,
        SequenceSource source,
        ITypeSymbol? elementType)
        => kind switch
        {
            // A ref struct enumerated directly cannot survive an await or a yield in the loop body,
            // so only the string receiver — a plain reference type — is reported here.
            ConsumerKind.ForEach => source == SequenceSource.String ? ForEachDisplay : null,
            ConsumerKind.Length => LengthPropertyName,
            ConsumerKind.Indexer => GetIndexerDisplay(context, invocation, source, elementType),
            ConsumerKind.Argument => GetArgumentConsumerName(context, invocation, source, elementType),
            _ => null,
        };

    /// <summary>Returns the indexer display when the element access reads exactly one element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The copying invocation.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns>The display, or <see langword="null"/> when the access is not a single-element read.</returns>
    /// <remarks>
    /// Checking the access's own type is what rules out a range: <c>chars[1..]</c> is a
    /// <c>char[]</c>, while <c>text[1..]</c> is a <c>string</c> and <c>span[1..]</c> is a
    /// <c>ReadOnlySpan&lt;char&gt;</c>. Only an access that already produces the element type
    /// produces the same value after the copy is dropped.
    /// </remarks>
    private static string? GetIndexerDisplay(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SequenceSource source,
        ITypeSymbol? elementType)
    {
        var elementAccess = (ElementAccessExpressionSyntax)invocation.Parent!;
        var accessType = context.SemanticModel.GetTypeInfo(elementAccess, context.CancellationToken).Type;
        if (accessType is null)
        {
            return null;
        }

        var reads = source == SequenceSource.String
            ? accessType.SpecialType == SpecialType.System_Char
            : SymbolEqualityComparer.Default.Equals(accessType, elementType);

        return reads ? IndexerDisplay : null;
    }

    /// <summary>Returns the consuming method's name when dropping the copy provably keeps the call valid.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="invocation">The copying invocation.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns>The consumer's name, or <see langword="null"/> when the copy must stay.</returns>
    /// <remarks>
    /// The rewritten call is bound speculatively rather than matched against a sibling overload,
    /// because overload resolution over a <c>string</c> argument is not obvious: a <c>Use(object)</c>
    /// overload is applicable to it, and whether a span-taking sibling outranks that depends on the
    /// conversions the host compiler's language version defines. Binding the real thing is the only way
    /// to know which overload the fix would land on.
    /// </remarks>
    private static string? GetArgumentConsumerName(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        SequenceSource source,
        ITypeSymbol? elementType)
    {
        var argument = (ArgumentSyntax)invocation.Parent!;
        var argumentList = (ArgumentListSyntax)argument.Parent!;
        var outer = (InvocationExpressionSyntax)argumentList.Parent!;
        var model = context.SemanticModel;
        if (model.GetSymbolInfo(outer, context.CancellationToken).Symbol is not IMethodSymbol consumer
            || consumer.IsExtensionMethod
            || consumer.ReducedFrom is not null)
        {
            return null;
        }

        var index = argumentList.Arguments.IndexOf(argument);
        if (index < 0 || index >= consumer.Parameters.Length || !TakesMatchingArray(consumer.Parameters[index], source, elementType))
        {
            return null;
        }

        // A call reached through a conditional access cannot be speculatively rebound: detaching the outer call
        // to test the copy-elision rewrite orphans its member or element binding and Roslyn's binder then
        // dereferences null. The rewrite stays unverified, so the sequence copy is left in place.
        if (ConditionalAccessSpeculation.ReachedThroughConditionalAccess(outer.Expression))
        {
            return null;
        }

        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        var rewritten = outer.ReplaceNode(invocation, receiver);
        if (model.GetSpeculativeSymbolInfo(outer.SpanStart, rewritten, SpeculativeBindingOption.BindAsExpression).Symbol is not IMethodSymbol resolved
            || !IsSameCallWithSequenceSlot(resolved, consumer, index, source, elementType))
        {
            return null;
        }

        return consumer.Name;
    }

    /// <summary>Returns whether a parameter takes exactly the array the copy produces.</summary>
    /// <param name="parameter">The consuming parameter.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns><see langword="true"/> when the parameter is a plain one-dimensional array of the element type.</returns>
    private static bool TakesMatchingArray(IParameterSymbol parameter, SequenceSource source, ITypeSymbol? elementType)
    {
        if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.Type is not IArrayTypeSymbol { Rank: 1 } array)
        {
            return false;
        }

        return source == SequenceSource.String
            ? array.ElementType.SpecialType == SpecialType.System_Char
            : SymbolEqualityComparer.Default.Equals(array.ElementType, elementType);
    }

    /// <summary>Returns whether the speculatively bound call is the same call with a sequence-taking slot.</summary>
    /// <param name="resolved">The method the rewritten call binds to.</param>
    /// <param name="original">The method the current call binds to.</param>
    /// <param name="index">The parameter position the copy is passed at.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns><see langword="true"/> when the rewrite keeps the call's shape, its return type, and its meaning.</returns>
    private static bool IsSameCallWithSequenceSlot(
        IMethodSymbol resolved,
        IMethodSymbol original,
        int index,
        SequenceSource source,
        ITypeSymbol? elementType)
    {
        if (!SymbolEqualityComparer.Default.Equals(resolved.ContainingType, original.ContainingType)
            || resolved.Parameters.Length != original.Parameters.Length
            || resolved.IsStatic != original.IsStatic
            || !SymbolEqualityComparer.Default.Equals(resolved.ReturnType, original.ReturnType))
        {
            return false;
        }

        for (var i = 0; i < resolved.Parameters.Length; i++)
        {
            if (i != index && !SymbolEqualityComparer.Default.Equals(resolved.Parameters[i].Type, original.Parameters[i].Type))
            {
                return false;
            }
        }

        return AcceptsSequence(resolved.Parameters[index].Type, source, elementType);
    }

    /// <summary>Returns whether a parameter slot takes the original sequence itself.</summary>
    /// <param name="slotType">The rewritten call's parameter type at the copy's position.</param>
    /// <param name="source">The sequence the copy was made from.</param>
    /// <param name="elementType">The span's element type, for a span copy.</param>
    /// <returns><see langword="true"/> for a <see cref="string"/> slot or a matching <c>ReadOnlySpan&lt;T&gt;</c> slot.</returns>
    private static bool AcceptsSequence(ITypeSymbol slotType, SequenceSource source, ITypeSymbol? elementType)
    {
        if (source == SequenceSource.String && slotType.SpecialType == SpecialType.System_String)
        {
            return true;
        }

        if (slotType is not INamedTypeSymbol { Name: ReadOnlySpanTypeName, IsGenericType: true, TypeArguments.Length: 1 } span
            || span.ContainingNamespace is not { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true })
        {
            return false;
        }

        return source == SequenceSource.String
            ? span.TypeArguments[0].SpecialType == SpecialType.System_Char
            : SymbolEqualityComparer.Default.Equals(span.TypeArguments[0], elementType);
    }
}
