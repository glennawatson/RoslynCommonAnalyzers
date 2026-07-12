// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a comparison that tests a count or a length against a value it can never take (SST1479).
/// <c>count &gt;= 0</c> is always true, <c>count &lt; 0</c> is always false, and every comparison against a
/// negative literal is decided before it runs — so the test does nothing and hides the check that was meant.
/// </summary>
/// <remarks>
/// <para>
/// The left operand must be a member the framework guarantees to be non-negative: <c>Array.Length</c> (and
/// <c>LongLength</c>), <c>string.Length</c>, <c>Span&lt;T&gt;.Length</c>, <c>ReadOnlySpan&lt;T&gt;.Length</c>,
/// the <c>Count</c> that satisfies <c>ICollection&lt;T&gt;</c> or <c>IReadOnlyCollection&lt;T&gt;</c> — so
/// <c>List&lt;T&gt;</c>, <c>Dictionary&lt;,&gt;</c> and every other BCL collection follow — and
/// <c>Enumerable.Count()</c>. A <c>Count</c> property that satisfies neither interface is left alone: nothing
/// says a user-defined count cannot be negative. The operands may be written in either order, so
/// <c>0 &lt;= count</c> reads the same as <c>count &gt;= 0</c>.
/// </para>
/// <para>
/// Ordered so the clean path never binds. The bound is classified from the literal's token text without
/// allocating, the comparison is folded on the operator alone, and only a comparison that already folds to a
/// constant is matched against the count-member names. <c>count &gt; 0</c> dies at the fold;
/// <c>IndexOf(x) &gt;= 0</c> and <c>value &gt;= 0</c> die at the name gate. Nothing else reaches the semantic
/// model.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1479MeaninglessCountComparisonAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the count a collection interface declares and the LINQ operator returns.</summary>
    private const string CountName = "Count";

    /// <summary>The name of the LINQ operator that counts into a <see cref="long"/>.</summary>
    private const string LongCountName = "LongCount";

    /// <summary>The name of the length an array, a string and a span declare.</summary>
    private const string LengthName = "Length";

    /// <summary>The name of the length an array declares as a <see cref="long"/>.</summary>
    private const string LongLengthName = "LongLength";

    /// <summary>The kind of bound an operand denotes.</summary>
    private enum CountBound
    {
        /// <summary>Not a bound a count can never fail.</summary>
        None,

        /// <summary>The literal zero, which a count meets or exceeds.</summary>
        Zero,

        /// <summary>A negative literal, which a count always exceeds.</summary>
        Negative,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.MeaninglessCountComparison);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Re-derives the constant a reported comparison folds to, for the code fix.</summary>
    /// <param name="binary">The reported comparison.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns><see langword="true"/> when the comparison still folds to a constant.</returns>
    /// <remarks>
    /// The fold depends only on the operator and the sign of the literal, both of which are syntax, so the
    /// fix can confirm the reported shape without a semantic model.
    /// </remarks>
    internal static bool TryGetConstantResult(BinaryExpressionSyntax binary, out bool result)
        => TryFoldComparison(binary, out result, out _);

    /// <summary>Sets up the per-compilation state, then analyzes every comparison.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// The span and LINQ symbols stay unresolved until a comparison actually reaches the bind. Resolving them
    /// eagerly costs a metadata-name lookup over every referenced assembly, which a compilation with no such
    /// comparison — the common case — would pay for nothing.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        var countTypes = new Lazy<CountMemberTypes>(
            () => CountMemberTypes.Create(compilation),
            LazyThreadSafetyMode.ExecutionAndPublication);
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, countTypes),
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    /// <summary>Reports one comparison whose answer the framework already decided.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="countTypes">The well-known count-member types.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, Lazy<CountMemberTypes> countTypes)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (!TryFoldComparison(binary, out var result, out var counted)
            || !IsCountMemberSyntax(counted)
            || !IsNonNegativeCountMember(counted, context, countTypes))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.MeaninglessCountComparison,
            binary.GetLocation(),
            result ? "true" : "false"));
    }

    /// <summary>Folds a comparison against a zero or negative literal, without touching the semantic model.</summary>
    /// <param name="binary">The comparison expression.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <param name="counted">The operand that has to be a non-negative count for the fold to hold.</param>
    /// <returns><see langword="true"/> when the comparison is decided for every non-negative left operand.</returns>
    private static bool TryFoldComparison(BinaryExpressionSyntax binary, out bool result, out ExpressionSyntax counted)
    {
        result = false;
        counted = binary;

        var left = ClassifyBound(binary.Left);
        var right = ClassifyBound(binary.Right);
        var leftIsBound = left != CountBound.None;
        var rightIsBound = right != CountBound.None;
        if (leftIsBound == rightIsBound)
        {
            // Neither operand is a bound, or both are (`0 >= 0`): nothing here is a count comparison.
            return false;
        }

        // `0 <= count` states the same thing as `count >= 0`, so a literal on the left flips the operator.
        counted = leftIsBound ? binary.Right : binary.Left;
        var bound = leftIsBound ? left : right;
        var kind = leftIsBound ? Flip(binary.Kind()) : binary.Kind();

        return bound == CountBound.Zero
            ? TryFoldAgainstZero(kind, out result)
            : TryFoldAgainstNegative(kind, out result);
    }

    /// <summary>Folds a comparison against zero, where only the two impossible tests are decided.</summary>
    /// <param name="kind">The comparison, oriented as <c>count OP 0</c>.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns><see langword="true"/> when the comparison is decided.</returns>
    /// <remarks><c>count &gt; 0</c>, <c>count == 0</c> and their friends all still ask a real question.</remarks>
    private static bool TryFoldAgainstZero(SyntaxKind kind, out bool result)
    {
        switch (kind)
        {
            case SyntaxKind.GreaterThanOrEqualExpression:
            {
                result = true;
                return true;
            }

            case SyntaxKind.LessThanExpression:
            {
                result = false;
                return true;
            }

            default:
            {
                result = false;
                return false;
            }
        }
    }

    /// <summary>Folds a comparison against a negative literal, where every test is decided.</summary>
    /// <param name="kind">The comparison, oriented as <c>count OP -n</c>.</param>
    /// <param name="result">The constant the comparison always evaluates to.</param>
    /// <returns><see langword="true"/>, because a non-negative value settles every comparison against a negative one.</returns>
    private static bool TryFoldAgainstNegative(SyntaxKind kind, out bool result)
    {
        result = kind is SyntaxKind.NotEqualsExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression;
        return true;
    }

    /// <summary>Mirrors a comparison so the counted operand can always be read as the left one.</summary>
    /// <param name="kind">The comparison as written.</param>
    /// <returns>The comparison with its operands swapped.</returns>
    private static SyntaxKind Flip(SyntaxKind kind) => kind switch
    {
        SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
        SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
        SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
        SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
        _ => kind,
    };

    /// <summary>Classifies an operand as the zero or negative bound a count can never fail.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns>The bound the operand denotes, or <see cref="CountBound.None"/>.</returns>
    /// <remarks>A positive literal is not a bound: <c>count &gt;= 3</c> asks a question worth asking.</remarks>
    private static CountBound ClassifyBound(ExpressionSyntax expression)
    {
        if (expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } negated)
        {
            // `-0` is still zero, so only a negated non-zero literal is the negative bound.
            if (!TryReadMagnitude(negated.Operand, out var negatedIsZero))
            {
                return CountBound.None;
            }

            return negatedIsZero ? CountBound.Zero : CountBound.Negative;
        }

        return TryReadMagnitude(expression, out var isZero) && isZero ? CountBound.Zero : CountBound.None;
    }

    /// <summary>Reads whether a numeric literal is zero, from its digits alone.</summary>
    /// <param name="expression">The candidate literal.</param>
    /// <param name="isZero">Whether the literal's magnitude is zero.</param>
    /// <returns><see langword="true"/> when the literal's magnitude is plainly readable.</returns>
    /// <remarks>
    /// Only the token's text is read, so no value is boxed on a path every comparison in the compilation
    /// takes. A bit pattern and an exponent are rejected rather than decoded: neither is ever written as the
    /// bound of a count, and guessing at one risks reading <c>-0e1</c> as a negative number. Digit separators
    /// and the type suffixes (<c>u</c>, <c>L</c>, <c>f</c>, <c>d</c>, <c>m</c>) contain no digits, so they
    /// pass through unread.
    /// </remarks>
    private static bool TryReadMagnitude(ExpressionSyntax expression, out bool isZero)
    {
        isZero = false;
        if (expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal)
        {
            return false;
        }

        var text = literal.Token.Text;
        if (text.Length == 0 || IsBitPatternText(text))
        {
            return false;
        }

        return TryReadDigits(text, out isZero);
    }

    /// <summary>Returns whether a numeric literal is written as a hexadecimal or binary bit pattern.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> for a <c>0x</c> or <c>0b</c> prefix.</returns>
    private static bool IsBitPatternText(string text)
        => text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B';

    /// <summary>Reads whether every digit of a plain numeric literal is a zero.</summary>
    /// <param name="text">The literal's source text, already known not to be a bit pattern.</param>
    /// <param name="isZero">Whether the literal's magnitude is zero.</param>
    /// <returns><see langword="true"/> when the magnitude is plainly readable, and <see langword="false"/> for an exponent.</returns>
    private static bool TryReadDigits(string text, out bool isZero)
    {
        isZero = false;

        var zero = true;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is 'e' or 'E')
            {
                return false;
            }

            if (character is >= '1' and <= '9')
            {
                zero = false;
            }
        }

        isZero = zero;
        return true;
    }

    /// <summary>Returns whether an operand is named like a count, before anything is bound.</summary>
    /// <param name="expression">The counted operand.</param>
    /// <returns><see langword="true"/> when the name is worth a bind.</returns>
    /// <remarks>
    /// This is what keeps <c>value &gt;= 0</c> and <c>text.IndexOf('x') &gt;= 0</c> — both of which fold to a
    /// constant only if their operand is non-negative, which neither is — off the semantic model entirely.
    /// </remarks>
    private static bool IsCountMemberSyntax(ExpressionSyntax expression) => expression switch
    {
        InvocationExpressionSyntax invocation => GetSimpleName(invocation.Expression) is CountName or LongCountName,
        _ => GetSimpleName(expression) is CountName or LengthName or LongLengthName,
    };

    /// <summary>Gets the rightmost identifier of a member access or a bare name.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns>The simple name, or an empty string when the shape names nothing.</returns>
    /// <remarks>
    /// A conditional access (<c>list?.Count</c>) is deliberately not a name here: it produces a nullable
    /// value, and a null one fails every comparison, so the fold does not hold.
    /// </remarks>
    private static string GetSimpleName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Returns whether the counted operand reads a member the framework keeps non-negative.</summary>
    /// <param name="counted">The counted operand.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="countTypes">The well-known count-member types.</param>
    /// <returns><see langword="true"/> when the value can never be negative.</returns>
    private static bool IsNonNegativeCountMember(
        ExpressionSyntax counted,
        SyntaxNodeAnalysisContext context,
        Lazy<CountMemberTypes> countTypes)
        => context.SemanticModel.GetSymbolInfo(counted, context.CancellationToken).Symbol switch
        {
            IPropertySymbol property => IsNonNegativeProperty(property, countTypes),
            IMethodSymbol method => IsEnumerableCount(method, countTypes),
            _ => false,
        };

    /// <summary>Returns whether a property is one of the framework's non-negative cardinality members.</summary>
    /// <param name="property">The bound property.</param>
    /// <param name="countTypes">The well-known count-member types.</param>
    /// <returns><see langword="true"/> for an array, string or span length, or a collection count.</returns>
    private static bool IsNonNegativeProperty(IPropertySymbol property, Lazy<CountMemberTypes> countTypes)
    {
        if (property.ContainingType is not { } containing)
        {
            return false;
        }

        return property.Name switch
        {
            LengthName or LongLengthName => containing.SpecialType is SpecialType.System_Array or SpecialType.System_String
                || countTypes.Value.IsSpan(containing),
            CountName => SatisfiesCollectionCount(property, containing),
            _ => false,
        };
    }

    /// <summary>Returns whether a <c>Count</c> property is the one a BCL collection interface demands.</summary>
    /// <param name="property">The bound property.</param>
    /// <param name="containing">The type that declares the property.</param>
    /// <returns><see langword="true"/> when the property satisfies <c>ICollection&lt;T&gt;</c> or <c>IReadOnlyCollection&lt;T&gt;</c>.</returns>
    /// <remarks>
    /// Asking the interface rather than the type is what makes the rule cover <c>List&lt;T&gt;</c>,
    /// <c>Dictionary&lt;,&gt;</c>, <c>HashSet&lt;T&gt;</c> and every collection a user writes, while leaving a
    /// <c>Count</c> that means something else — a running total, a delta — alone.
    /// </remarks>
    private static bool SatisfiesCollectionCount(IPropertySymbol property, INamedTypeSymbol containing)
    {
        // The comparison may read the interface directly: `ICollection<int> items; items.Count >= 0`.
        if (IsCountInterface(containing))
        {
            return true;
        }

        var interfaces = containing.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidate = interfaces[i];
            if (!IsCountInterface(candidate))
            {
                continue;
            }

            var members = candidate.GetMembers(CountName);
            for (var j = 0; j < members.Length; j++)
            {
                var implementation = containing.FindImplementationForInterfaceMember(members[j]);
                if (SymbolEqualityComparer.Default.Equals(implementation, property))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is one of the BCL collection interfaces that declare a count.</summary>
    /// <param name="type">The candidate interface.</param>
    /// <returns><see langword="true"/> for <c>ICollection&lt;T&gt;</c> and <c>IReadOnlyCollection&lt;T&gt;</c>.</returns>
    private static bool IsCountInterface(INamedTypeSymbol type)
        => type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_ICollection_T
            or SpecialType.System_Collections_Generic_IReadOnlyCollection_T;

    /// <summary>Returns whether a method is one of the LINQ counting operators.</summary>
    /// <param name="method">The bound method.</param>
    /// <param name="countTypes">The well-known count-member types.</param>
    /// <returns><see langword="true"/> for <c>Enumerable.Count</c> and <c>Enumerable.LongCount</c>.</returns>
    /// <remarks>The reduced form is unwrapped so the extension call and the static call resolve to the same symbol.</remarks>
    private static bool IsEnumerableCount(IMethodSymbol method, Lazy<CountMemberTypes> countTypes)
    {
        var declared = method.ReducedFrom ?? method;
        return declared.Name is CountName or LongCountName
            && declared.ContainingType is { } containing
            && countTypes.Value.IsEnumerable(containing);
    }
}
