// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an exact <c>==</c> / <c>!=</c> comparison of binary floating-point values, and any comparison
/// against <c>NaN</c> (SST1473). A comparison against a literal zero is allowed by default and is opted
/// back in with <c>stylesharp.SST1473.allow_zero_comparison = false</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two things are wrong with an exact floating-point comparison, and the rule reports both. Arithmetic in
/// base 2 rounds, so <c>0.1 + 0.2 == 0.3</c> is false; and <c>NaN</c> compares false against every value
/// under every operator, so <c>x == double.NaN</c> is false even when <c>x</c> is NaN, and <c>x &lt; NaN</c>
/// is false as well. Only <c>!=</c> answers true against NaN, and only because it negates the false.
/// </para>
/// <para>
/// <c>decimal</c> is never reported: it is exact for the values it can hold, so an equality on it means
/// what it says. A comparison against a literal zero is left alone by default because it asks a different
/// question — is this negative, was this ever assigned — rather than whether two computed values landed on
/// the same bits.
/// </para>
/// <para>
/// A self-comparison <c>x == x</c> is the deliberate NaN idiom, and it is reported here rather than by
/// SST1474: it is a floating-point comparison, and the fix — <c>!double.IsNaN(x)</c> — is a floating-point
/// fix. SST1474 defers the shape back to this rule so it is stated once.
/// </para>
/// <para>
/// Ordered so the clean path never binds. The four relational kinds leave immediately unless an operand is
/// literally spelled <c>NaN</c>, and the two equality kinds first reject an operand no floating-point value
/// could be — a <see langword="null"/>, a <see langword="bool"/>, a string or a char literal — and then a
/// zero literal, all on syntax. Only what survives is bound, and the per-tree settings are read only once a
/// zero literal has actually been seen. The structural self-comparison test runs last of all, on the report
/// path, where its cost buys the code fix.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1473FloatingPointEqualityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming the rewrite the code fix should apply.</summary>
    internal const string FixKindKey = "SST1473.FixKind";

    /// <summary>The diagnostic property naming the type whose <c>IsNaN</c> the code fix should call.</summary>
    internal const string TypeKeywordKey = "SST1473.TypeKeyword";

    /// <summary>The <see cref="FixKindKey"/> value that rewrites the comparison to <c>T.IsNaN(x)</c>.</summary>
    internal const string IsNaNFixKind = "IsNaN";

    /// <summary>The <see cref="FixKindKey"/> value that rewrites the comparison to <c>!T.IsNaN(x)</c>.</summary>
    internal const string NotIsNaNFixKind = "NotIsNaN";

    /// <summary>The name of the <c>NaN</c> field on <see cref="float"/> and <see cref="double"/>.</summary>
    private const string NaNFieldName = "NaN";

    /// <summary>The properties of a diagnostic the code fix cannot rewrite.</summary>
    private static readonly ImmutableDictionary<string, string?> NoFixProperties = ImmutableDictionary<string, string?>.Empty;

    /// <summary>The properties that rewrite a <see cref="double"/> comparison to <c>double.IsNaN(x)</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> DoubleIsNaNProperties = CreateProperties(FloatingPointTypes.DoubleKeyword, IsNaNFixKind);

    /// <summary>The properties that rewrite a <see cref="double"/> comparison to <c>!double.IsNaN(x)</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> DoubleNotIsNaNProperties = CreateProperties(FloatingPointTypes.DoubleKeyword, NotIsNaNFixKind);

    /// <summary>The properties that rewrite a <see cref="float"/> comparison to <c>float.IsNaN(x)</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> SingleIsNaNProperties = CreateProperties(FloatingPointTypes.SingleKeyword, IsNaNFixKind);

    /// <summary>The properties that rewrite a <see cref="float"/> comparison to <c>!float.IsNaN(x)</c>.</summary>
    private static readonly ImmutableDictionary<string, string?> SingleNotIsNaNProperties = CreateProperties(FloatingPointTypes.SingleKeyword, NotIsNaNFixKind);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.FloatingPointEquality);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Returns whether an expression is spelled as a reference to a <c>NaN</c> field.</summary>
    /// <param name="expression">The operand to inspect.</param>
    /// <returns><see langword="true"/> for <c>double.NaN</c>, <c>Single.NaN</c>, or a bare <c>NaN</c> imported with <c>using static</c>.</returns>
    /// <remarks>
    /// Purely syntactic, and shared with the code fix so both agree on which operand is the NaN and which is
    /// the value being tested. Whether the name really binds to <see cref="float"/> or <see cref="double"/>
    /// is a separate, semantic question the analyzer answers before it reports.
    /// </remarks>
    internal static bool IsNaNShaped(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == NaNFieldName,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == NaNFieldName,
        _ => false,
    };

    /// <summary>Registers the per-compilation state, then analyzes every equality and relational comparison.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, FloatingPointComparisonOptions>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, optionsByTree),
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression);
    }

    /// <summary>Reports one comparison that cannot answer the question it appears to ask.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, FloatingPointComparisonOptions> optionsByTree)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        var isEquality = binary.RawKind is (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression;

        // A NaN operand makes every operator wrong, so this runs for the relational kinds too. It costs two
        // name comparisons when no operand is spelled 'NaN', which is the whole cost of a clean '<' or '>'.
        if (TryReportNaNComparison(context, binary, isEquality))
        {
            return;
        }

        if (!isEquality
            || HasNonFloatingLiteralOperand(binary)
            || IsAllowedZeroComparison(context, binary, optionsByTree)
            || !TryGetComparisonKeyword(context, binary, out var keyword, out var isNullable))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.FloatingPointEquality,
            binary.SyntaxTree,
            binary.Span,
            GetSelfComparisonProperties(binary, keyword, isNullable),
            keyword,
            binary.OperatorToken.Text));
    }

    /// <summary>Reports a comparison against <c>float.NaN</c> or <c>double.NaN</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="binary">The comparison.</param>
    /// <param name="isEquality">Whether the operator is <c>==</c> or <c>!=</c>.</param>
    /// <returns><see langword="true"/> when the comparison was reported, so the general path is skipped.</returns>
    /// <remarks>
    /// The settings are never consulted here: a NaN comparison is a bug whatever the zero setting says. A
    /// name that merely reads <c>NaN</c> but binds to something else — a constant on the caller's own type —
    /// falls through to the general path, where an equality on a floating-point type is still reported and
    /// anything else is not.
    /// </remarks>
    private static bool TryReportNaNComparison(SyntaxNodeAnalysisContext context, BinaryExpressionSyntax binary, bool isEquality)
    {
        var leftIsNaN = IsNaNComparand(context, binary.Left);
        var rightIsNaN = IsNaNComparand(context, binary.Right);
        if (!leftIsNaN && !rightIsNaN)
        {
            return false;
        }

        if (!TryGetComparisonKeyword(context, binary, out var keyword, out var isNullable))
        {
            return false;
        }

        // A relational operator states no intent to rewrite, 'NaN == NaN' has no value left to test, and a
        // nullable operand has no 'IsNaN' overload to call — so only a one-sided, non-nullable equality
        // carries a rewrite.
        var fixable = isEquality && leftIsNaN != rightIsNaN && !isNullable;

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.FloatingPointEquality,
            binary.SyntaxTree,
            binary.Span,
            GetNaNProperties(binary, keyword, fixable),
            keyword,
            binary.OperatorToken.Text));
        return true;
    }

    /// <summary>Builds the properties for a comparison against NaN.</summary>
    /// <param name="binary">The reported comparison.</param>
    /// <param name="keyword">The floating-point keyword.</param>
    /// <param name="fixable">Whether the comparison has a rewrite at all.</param>
    /// <returns>The rewrite properties, or none.</returns>
    /// <remarks>
    /// <c>x == NaN</c> is always false, so the test it was written to make is <c>IsNaN(x)</c>, and <c>!=</c>
    /// inverts it. That mapping is the opposite of the self-comparison one — see
    /// <see cref="GetSelfComparisonProperties"/>.
    /// </remarks>
    private static ImmutableDictionary<string, string?> GetNaNProperties(BinaryExpressionSyntax binary, string keyword, bool fixable)
    {
        if (!fixable)
        {
            return NoFixProperties;
        }

        return GetProperties(keyword, binary.IsKind(SyntaxKind.NotEqualsExpression) ? NotIsNaNFixKind : IsNaNFixKind);
    }

    /// <summary>Returns whether an operand is the <c>NaN</c> field of <see cref="float"/> or <see cref="double"/>.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The operand.</param>
    /// <returns><see langword="true"/> when the name really is the framework's NaN.</returns>
    /// <remarks>The syntactic shape is checked first, so an operand not spelled <c>NaN</c> never reaches the semantic model.</remarks>
    private static bool IsNaNComparand(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        => IsNaNShaped(expression)
            && context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is IFieldSymbol { Name: NaNFieldName } field
            && field.ContainingType.SpecialType is SpecialType.System_Single or SpecialType.System_Double;

    /// <summary>Reads the floating-point type the comparison is performed in.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="binary">The comparison.</param>
    /// <param name="keyword">The <c>float</c> or <c>double</c> keyword.</param>
    /// <param name="isNullable">Whether the comparison is a lifted one over nullable operands.</param>
    /// <returns><see langword="true"/> when the operands are compared as a binary floating-point type.</returns>
    /// <remarks>
    /// The converted type is what matters, not the natural one: in <c>1 == d</c> the literal is an
    /// <see cref="int"/> that the compiler widens to <see cref="double"/>, and the comparison is the one
    /// that rounds. Both operands convert to the same type, so the left one answers the question — the right
    /// is consulted only when the left failed to bind at all.
    /// </remarks>
    private static bool TryGetComparisonKeyword(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax binary,
        out string keyword,
        out bool isNullable)
    {
        var leftType = context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).ConvertedType;
        if (leftType is not null && leftType.TypeKind != TypeKind.Error)
        {
            return FloatingPointTypes.TryGetKeyword(leftType, out keyword, out isNullable);
        }

        var rightType = context.SemanticModel.GetTypeInfo(binary.Right, context.CancellationToken).ConvertedType;
        return FloatingPointTypes.TryGetKeyword(rightType, out keyword, out isNullable);
    }

    /// <summary>Builds the properties for a self-comparison, which is the one shape the code fix can rewrite here.</summary>
    /// <param name="binary">The reported comparison.</param>
    /// <param name="keyword">The floating-point keyword.</param>
    /// <param name="isNullable">Whether the operands are nullable.</param>
    /// <returns>The rewrite properties, or none when the comparison is a plain tolerance problem.</returns>
    /// <remarks>
    /// The boolean sense inverts, and it is the easy thing to get wrong. <c>x == x</c> is true for every
    /// value except NaN, so it already means <c>!IsNaN(x)</c>; <c>x != x</c> is true only for NaN, so it
    /// means <c>IsNaN(x)</c>. That is the opposite of the <c>x == NaN</c> case, where <c>==</c> maps to
    /// <c>IsNaN</c>. Operands that do something — <c>Next() == Next()</c> — are still reported as an exact
    /// comparison, but they are not a self-comparison and get no rewrite.
    /// </remarks>
    private static ImmutableDictionary<string, string?> GetSelfComparisonProperties(BinaryExpressionSyntax binary, string keyword, bool isNullable)
    {
        if (isNullable
            || !SideEffectFreeExpression.IsSideEffectFree(binary.Left)
            || !SyntaxFactory.AreEquivalent(binary.Left, binary.Right, topLevel: false))
        {
            return NoFixProperties;
        }

        return GetProperties(keyword, binary.IsKind(SyntaxKind.NotEqualsExpression) ? IsNaNFixKind : NotIsNaNFixKind);
    }

    /// <summary>Returns whether an operand is a literal no floating-point value could ever be.</summary>
    /// <param name="binary">The comparison.</param>
    /// <returns><see langword="true"/> when a <see langword="null"/>, <see langword="bool"/>, string or char literal rules the comparison out.</returns>
    /// <remarks>This is the syntactic prepass that keeps <c>name == "x"</c> and <c>value == null</c> off the semantic model.</remarks>
    private static bool HasNonFloatingLiteralOperand(BinaryExpressionSyntax binary)
        => IsNonFloatingLiteral(binary.Left) || IsNonFloatingLiteral(binary.Right);

    /// <summary>Returns whether an expression is a literal of a kind no floating-point value has.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns><see langword="true"/> for a <see langword="null"/>, <see langword="bool"/>, string or char literal.</returns>
    private static bool IsNonFloatingLiteral(ExpressionSyntax expression)
        => expression.RawKind is (int)SyntaxKind.NullLiteralExpression
            or (int)SyntaxKind.TrueLiteralExpression
            or (int)SyntaxKind.FalseLiteralExpression
            or (int)SyntaxKind.StringLiteralExpression
            or (int)SyntaxKind.CharacterLiteralExpression;

    /// <summary>Returns whether the comparison is a zero comparison the settings leave alone.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="binary">The comparison.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns><see langword="true"/> when a zero literal is compared and zero comparisons are allowed.</returns>
    /// <remarks>The settings are read only after a zero literal has been found, so a file with none never touches them.</remarks>
    private static bool IsAllowedZeroComparison(
        SyntaxNodeAnalysisContext context,
        BinaryExpressionSyntax binary,
        ConcurrentDictionary<SyntaxTree, FloatingPointComparisonOptions> optionsByTree)
        => (IsZeroLiteral(binary.Left) || IsZeroLiteral(binary.Right))
            && GetOptions(context, optionsByTree).AllowZeroComparison;

    /// <summary>Reads the settings for the comparison's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static FloatingPointComparisonOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, FloatingPointComparisonOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = FloatingPointComparisonOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Returns whether an operand is a literal zero.</summary>
    /// <param name="expression">The operand.</param>
    /// <returns><see langword="true"/> for <c>0</c>, <c>0.0</c>, <c>0f</c>, <c>0x0</c> and <c>default</c>.</returns>
    private static bool IsZeroLiteral(ExpressionSyntax expression)
    {
        if (expression.RawKind == (int)SyntaxKind.DefaultLiteralExpression)
        {
            return true;
        }

        return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal
            && IsZeroText(literal.Token.Text);
    }

    /// <summary>Returns whether a numeric literal's source text denotes zero, without parsing or boxing it.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> when every digit in the literal is a zero.</returns>
    private static bool IsZeroText(string text)
        => text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B'
            ? !HasNonZeroBitDigit(text)
            : !HasNonZeroDecimalDigit(text);

    /// <summary>Returns whether a hexadecimal or binary literal carries a digit other than zero.</summary>
    /// <param name="text">The literal's source text, including its <c>0x</c> or <c>0b</c> prefix.</param>
    /// <returns><see langword="true"/> when the bit pattern is not all zeroes.</returns>
    /// <remarks>The <c>u</c> and <c>l</c> suffixes a bit pattern may carry are not hexadecimal digits, so they need no special case.</remarks>
    private static bool HasNonZeroBitDigit(string text)
    {
        for (var i = 2; i < text.Length; i++)
        {
            var character = text[i];
            if (character is (>= '1' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a decimal or real literal carries a digit other than zero.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> when the literal's value is not zero.</returns>
    /// <remarks>
    /// Only the mantissa is read: no exponent can make a zero mantissa non-zero, so the scan stops at
    /// <c>e</c>. The <c>f</c>, <c>d</c> and <c>m</c> suffixes and the <c>_</c> separator are not digits, so
    /// they fall through untouched.
    /// </remarks>
    private static bool HasNonZeroDecimalDigit(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is 'e' or 'E')
            {
                return false;
            }

            if (character is >= '1' and <= '9')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Picks the cached property set for one type and one rewrite.</summary>
    /// <param name="keyword">The floating-point keyword.</param>
    /// <param name="fixKind">The rewrite the code fix should apply.</param>
    /// <returns>The cached properties.</returns>
    private static ImmutableDictionary<string, string?> GetProperties(string keyword, string fixKind)
    {
        if (keyword == FloatingPointTypes.DoubleKeyword)
        {
            return fixKind == IsNaNFixKind ? DoubleIsNaNProperties : DoubleNotIsNaNProperties;
        }

        return fixKind == IsNaNFixKind ? SingleIsNaNProperties : SingleNotIsNaNProperties;
    }

    /// <summary>Builds one of the four property sets a diagnostic can carry.</summary>
    /// <param name="keyword">The floating-point keyword.</param>
    /// <param name="fixKind">The rewrite the code fix should apply.</param>
    /// <returns>The property set.</returns>
    private static ImmutableDictionary<string, string?> CreateProperties(string keyword, string fixKind)
        => ImmutableDictionary<string, string?>.Empty
            .Add(TypeKeywordKey, keyword)
            .Add(FixKindKey, fixKind);
}
