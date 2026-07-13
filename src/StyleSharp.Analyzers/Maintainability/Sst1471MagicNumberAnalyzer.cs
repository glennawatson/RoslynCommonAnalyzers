// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports numeric literals that carry meaning without a name (SST1471). The allowed values default to
/// <c>-1</c>, <c>0</c> and <c>1</c> and are configured with
/// <c>stylesharp.SST1471.magic_number_allowed_values</c>.
/// </summary>
/// <remarks>
/// Ordered so the overwhelmingly common case costs almost nothing: a bit-pattern token is rejected on its
/// text, the value is read from the token's digits without boxing, and the allow-list is a linear scan of
/// three decimals. Only a literal that survives all three pays for the ancestor walk, and only one that
/// survives that pays for a symbol bind.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1471MagicNumberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The radix used when reading a literal's digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The most digits that can be accumulated into a <see cref="long"/> without overflow.</summary>
    private const int MaximumDigits = 18;

    /// <summary>The smallest <see cref="double"/> that converts to <see cref="decimal"/>.</summary>
    private const double DecimalMinimum = (double)decimal.MinValue;

    /// <summary>The largest <see cref="double"/> that converts to <see cref="decimal"/>.</summary>
    private const double DecimalMaximum = (double)decimal.MaxValue;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.MagicNumber);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Sets up the per-compilation state, then analyzes every numeric literal.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// The positional constructor types stay unresolved until a literal actually reaches that exemption.
    /// Resolving them eagerly costs five metadata-name lookups over every referenced assembly, which a
    /// compilation with no such literal — the common case — would pay for nothing.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        var positionalTypes = new Lazy<PositionalConstructorTypes>(
            () => PositionalConstructorTypes.Create(compilation),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var allowedByTree = new ConcurrentDictionary<SyntaxTree, decimal[]>();
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, allowedByTree, positionalTypes),
            SyntaxKind.NumericLiteralExpression);
    }

    /// <summary>Reports one numeric literal when nothing in its value or position explains it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="allowedByTree">The per-tree allow-list cache.</param>
    /// <param name="positionalTypes">The well-known positional constructor types.</param>
    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, decimal[]> allowedByTree,
        Lazy<PositionalConstructorTypes> positionalTypes)
    {
        var literal = (LiteralExpressionSyntax)context.Node;
        if (IsBitPattern(literal.Token.Text))
        {
            return;
        }

        var node = Unwrap(literal, out var negated);
        if (!TryGetValue(literal.Token, negated, out var value)
            || MagicNumberOptions.Contains(GetAllowedValues(context, allowedByTree), value)
            || IsPositionExempt(node)
            || IsAtNamedDeclarationSite(node)
            || IsPositionalConstructorArgument(node, context, positionalTypes))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.MagicNumber, node.GetLocation(), node.ToString()));
    }

    /// <summary>Reads the allow-list for the literal's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="allowedByTree">The per-tree allow-list cache.</param>
    /// <returns>The allowed values.</returns>
    private static decimal[] GetAllowedValues(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, decimal[]> allowedByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (allowedByTree.TryGetValue(tree, out var allowed))
        {
            return allowed;
        }

        allowed = MagicNumberOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        allowedByTree.TryAdd(tree, allowed);
        return allowed;
    }

    /// <summary>Returns whether a literal is written as a hexadecimal or binary bit pattern.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <returns><see langword="true"/> for a bit pattern, which states its own meaning.</returns>
    private static bool IsBitPattern(string text)
        => text.Length > 1 && text[0] == '0' && text[1] is 'x' or 'X' or 'b' or 'B';

    /// <summary>Walks out through the wrappers that do not change a literal's meaning.</summary>
    /// <param name="literal">The numeric literal.</param>
    /// <param name="negated">Whether an odd number of unary minus operators apply.</param>
    /// <returns>The outermost node that still denotes the same constant.</returns>
    private static ExpressionSyntax Unwrap(LiteralExpressionSyntax literal, out bool negated)
    {
        negated = false;
        ExpressionSyntax node = literal;
        while (true)
        {
            switch (node.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                {
                    node = parenthesized;
                    continue;
                }

                case CastExpressionSyntax cast:
                {
                    node = cast;
                    continue;
                }

                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } minus:
                {
                    negated = !negated;
                    node = minus;
                    continue;
                }

                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryPlusExpression } plus:
                {
                    node = plus;
                    continue;
                }

                default:
                    return node;
            }
        }
    }

    /// <summary>Reads a literal's numeric value, preferring an allocation-free scan of its digits.</summary>
    /// <param name="token">The literal token.</param>
    /// <param name="negated">Whether a unary minus applies.</param>
    /// <param name="value">The literal's value.</param>
    /// <returns><see langword="true"/> when a value could be determined.</returns>
    private static bool TryGetValue(SyntaxToken token, bool negated, out decimal value)
    {
        if (TryGetDigits(token.Text, out var digits))
        {
            value = negated ? -digits : digits;
            return true;
        }

        // A suffixed, separated or floating literal falls back to the parsed value, which boxes.
        return TryConvert(token.Value, negated, out value);
    }

    /// <summary>Accumulates a run of plain decimal digits without allocating.</summary>
    /// <param name="text">The literal's source text.</param>
    /// <param name="value">The accumulated value.</param>
    /// <returns><see langword="true"/> when the text is only digits and cannot overflow.</returns>
    private static bool TryGetDigits(string text, out long value)
    {
        value = 0;
        if (text.Length is 0 or > MaximumDigits)
        {
            return false;
        }

        long result = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var character = text[i];
            if (character is < '0' or > '9')
            {
                return false;
            }

            result = (result * DecimalRadix) + (character - '0');
        }

        value = result;
        return true;
    }

    /// <summary>Converts a parsed literal value to a decimal.</summary>
    /// <param name="parsed">The token's parsed value.</param>
    /// <param name="negated">Whether a unary minus applies.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> when the value is representable as a decimal.</returns>
    private static bool TryConvert(object? parsed, bool negated, out decimal value) => parsed switch
    {
        double number => TryConvertReal(number, negated, out value),
        float number => TryConvertReal(number, negated, out value),
        _ => TryConvertIntegral(parsed, negated, out value),
    };

    /// <summary>Converts an integral or decimal literal value.</summary>
    /// <param name="parsed">The token's parsed value.</param>
    /// <param name="negated">Whether a unary minus applies.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> when the value is an integral or decimal number.</returns>
    private static bool TryConvertIntegral(object? parsed, bool negated, out decimal value)
    {
        decimal? converted = parsed switch
        {
            int number => number,
            long number => number,
            uint number => number,
            ulong number => number,
            decimal number => number,
            _ => null,
        };

        if (converted is not { } result)
        {
            value = 0m;
            return false;
        }

        value = negated ? -result : result;
        return true;
    }

    /// <summary>Converts a real literal that lies inside the decimal range.</summary>
    /// <param name="number">The real value.</param>
    /// <param name="negated">Whether a unary minus applies.</param>
    /// <param name="value">The converted value.</param>
    /// <returns><see langword="true"/> when the value is finite and representable.</returns>
    private static bool TryConvertReal(double number, bool negated, out decimal value)
    {
        value = 0m;
        if (double.IsNaN(number) || double.IsInfinity(number) || number < DecimalMinimum || number > DecimalMaximum)
        {
            return false;
        }

        value = negated ? -(decimal)number : (decimal)number;
        return true;
    }

    /// <summary>Returns whether the literal's immediate position already carries its meaning.</summary>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> when the position explains the value.</returns>
    private static bool IsPositionExempt(ExpressionSyntax node) => node.Parent switch
    {
        ArgumentSyntax argument => IsLabelledOrIndexArgument(argument),

        // `new byte[16]`, `stackalloc char[32]` — the literal is a buffer length.
        ArrayRankSpecifierSyntax => true,

        EqualsValueClauseSyntax initializer => IsNamedStorageInitializer(initializer, node),
        ExpressionElementSyntax or InitializerExpressionSyntax => IsNamedStorageCollectionElement(node),
        BinaryExpressionSyntax binary => IsShiftDistance(binary, node) || IsCardinalityComparison(binary, node),
        AssignmentExpressionSyntax assignment => IsShiftAssignmentDistance(assignment, node),
        _ => false,
    };

    /// <summary>Returns whether an argument's position already carries the literal's meaning.</summary>
    /// <param name="argument">The argument the literal sits in.</param>
    /// <returns><see langword="true"/> for a named argument and for an index.</returns>
    /// <remarks>
    /// <c>capacity: 4</c> is already labelled by the caller. <c>sources[3]</c> is a slot — the same
    /// positional shape as an array rank, which is exempt for the same reason: naming it can only produce a
    /// constant that restates the number it holds.
    /// </remarks>
    private static bool IsLabelledOrIndexArgument(ArgumentSyntax argument)
        => argument.NameColon is not null || argument.Parent is BracketedArgumentListSyntax;

    /// <summary>Returns whether the literal is an element of a collection that is itself a named declaration's whole value.</summary>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> when the declaration's identifier names the collection the literal sits in.</returns>
    /// <remarks>
    /// <c>var timeout = 500;</c> is exempt because the name explains the number, and
    /// <c>int[] offsets = [0, 4, 8];</c> is the same statement written three times over — the name explains
    /// the whole list. Reporting only some of the elements was the odd part: <c>[1, 2, 3]</c> pointed at the
    /// 2 and the 3 while leaving the 1 alone, because 1 is allowlisted, so the reader was asked to name two
    /// thirds of one literal.
    /// </remarks>
    private static bool IsNamedStorageCollectionElement(ExpressionSyntax node)
    {
        SyntaxNode? collection = node.Parent switch
        {
            ExpressionElementSyntax { Parent: CollectionExpressionSyntax expression } => expression,
            InitializerExpressionSyntax initializer when initializer.IsKind(SyntaxKind.ArrayInitializerExpression) => initializer,
            _ => null,
        };

        if (collection is null)
        {
            return false;
        }

        // An array initializer hangs off the creation expression that owns it.
        if (collection.Parent is ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
        {
            collection = collection.Parent;
        }

        return collection.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax or PropertyDeclarationSyntax } clause
            && ReferenceEquals(clause.Value, collection);
    }

    /// <summary>Returns whether the literal is the whole initializer of a named field, local or property.</summary>
    /// <param name="initializer">The initializer clause.</param>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> when the declaration's identifier names the value.</returns>
    /// <remarks>
    /// Only a bare literal qualifies. <c>var timeout = 500;</c> names the number; <c>var task = Delay(500);</c>
    /// names the task, and leaves 500 unexplained.
    /// </remarks>
    private static bool IsNamedStorageInitializer(EqualsValueClauseSyntax initializer, ExpressionSyntax node)
        => ReferenceEquals(initializer.Value, node)
            && initializer.Parent is VariableDeclaratorSyntax or PropertyDeclarationSyntax;

    /// <summary>Returns whether the literal is the distance of a shift operator.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> for a shift distance, which is a bit position.</returns>
    private static bool IsShiftDistance(BinaryExpressionSyntax binary, ExpressionSyntax node)
        => ReferenceEquals(binary.Right, node)
            && binary.Kind() is SyntaxKind.LeftShiftExpression
                or SyntaxKind.RightShiftExpression
                or SyntaxKind.UnsignedRightShiftExpression;

    /// <summary>Returns whether the literal is the distance of a compound shift assignment.</summary>
    /// <param name="assignment">The assignment expression.</param>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> for a shift distance.</returns>
    private static bool IsShiftAssignmentDistance(AssignmentExpressionSyntax assignment, ExpressionSyntax node)
        => ReferenceEquals(assignment.Right, node)
            && assignment.Kind() is SyntaxKind.LeftShiftAssignmentExpression
                or SyntaxKind.RightShiftAssignmentExpression
                or SyntaxKind.UnsignedRightShiftAssignmentExpression;

    /// <summary>Returns whether the literal is compared against a count or a length.</summary>
    /// <param name="binary">The binary expression.</param>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> for a cardinality guard such as <c>args.Count &lt; 2</c>.</returns>
    private static bool IsCardinalityComparison(BinaryExpressionSyntax binary, ExpressionSyntax node)
    {
        if (binary.Kind() is not (SyntaxKind.EqualsExpression
            or SyntaxKind.NotEqualsExpression
            or SyntaxKind.LessThanExpression
            or SyntaxKind.LessThanOrEqualExpression
            or SyntaxKind.GreaterThanExpression
            or SyntaxKind.GreaterThanOrEqualExpression))
        {
            return false;
        }

        var other = ReferenceEquals(binary.Left, node) ? binary.Right : binary.Left;
        return IsCountOrLength(other);
    }

    /// <summary>Returns whether an expression reads a count, length or rank.</summary>
    /// <param name="expression">The expression opposite the literal.</param>
    /// <returns><see langword="true"/> when the expression denotes a cardinality.</returns>
    private static bool IsCountOrLength(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is InvocationExpressionSyntax invocation)
        {
            current = invocation.Expression;
        }

        return current switch
        {
            MemberAccessExpressionSyntax member => IsCardinalityName(member.Name.Identifier.ValueText),
            IdentifierNameSyntax identifier => IsCardinalityName(identifier.Identifier.ValueText),
            _ => false,
        };
    }

    /// <summary>Returns whether a member name denotes a cardinality.</summary>
    /// <param name="name">The member name.</param>
    /// <returns><see langword="true"/> for a count, length or rank.</returns>
    private static bool IsCardinalityName(string name)
        => name is "Count" or "Length" or "LongLength" or "Rank";

    /// <summary>Returns whether the literal sits at a declaration that gives it a name.</summary>
    /// <param name="node">The unwrapped literal.</param>
    /// <returns><see langword="true"/> when an enclosing declaration names the value.</returns>
    /// <remarks>
    /// A constant-defining site exempts its whole initializer, because everything in it is part of the
    /// named value: <c>static readonly TimeSpan Retry = TimeSpan.FromSeconds(30);</c> is the fix this rule
    /// asks for, not a violation. The walk stops at a lambda or local function so a literal buried in one
    /// does not inherit the enclosing field's name. A declaration that does not name its whole initializer
    /// must let the walk continue, or a mixing prime in a local inside <c>GetHashCode</c> never reaches the
    /// method that excuses it.
    /// </remarks>
    private static bool IsAtNamedDeclarationSite(ExpressionSyntax node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                case BaseTypeDeclarationSyntax:
                    return false;
                case EnumMemberDeclarationSyntax:
                case AttributeArgumentSyntax:
                case ParameterSyntax:
                    return true;
                case FieldDeclarationSyntax field
                    when ModifierListHelper.ContainsEither(field.Modifiers, SyntaxKind.ConstKeyword, SyntaxKind.ReadOnlyKeyword):
                    return true;
                case LocalDeclarationStatementSyntax { IsConst: true }:
                    return true;
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText == "GetHashCode";
            }
        }

        return false;
    }

    /// <summary>Returns whether the literal is an argument to a constructor whose parameters are positional by convention.</summary>
    /// <param name="node">The unwrapped literal.</param>
    /// <param name="context">The syntax node context.</param>
    /// <param name="positionalTypes">The well-known positional constructor types.</param>
    /// <returns><see langword="true"/> for an argument of <c>DateTime</c>, <c>TimeSpan</c>, <c>Version</c> and friends.</returns>
    /// <remarks>The syntactic shape and the bind are both checked before the well-known types are resolved.</remarks>
    private static bool IsPositionalConstructorArgument(
        ExpressionSyntax node,
        SyntaxNodeAnalysisContext context,
        Lazy<PositionalConstructorTypes> positionalTypes)
        => node.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: BaseObjectCreationExpressionSyntax creation } }
            && context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken).Symbol is IMethodSymbol constructor
            && positionalTypes.Value.Contains(constructor.ContainingType);
}
