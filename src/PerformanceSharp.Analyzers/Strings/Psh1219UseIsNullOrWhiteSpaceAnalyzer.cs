// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a string trimmed only to ask whether it is blank (PSH1219): <c>text.Trim().Length == 0</c>,
/// <c>text.Trim() == ""</c> and <c>text.Trim() == string.Empty</c> (with their <c>!=</c> forms), and
/// <c>string.IsNullOrEmpty(text.Trim())</c>. <c>Trim</c> allocates a whole new string and copies the
/// non-blank characters into it — of which, on the path that matters, there are none — and the copy is
/// then thrown away unread. <c>string.IsNullOrWhiteSpace</c> scans the original in place, stops at the
/// first character that is not white space, and allocates nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rewrite is more forgiving about null, not less.</b> <c>text.Trim()</c> throws a
/// <see cref="NullReferenceException"/> on a null string; <c>string.IsNullOrWhiteSpace(null)</c> is
/// <see langword="true"/>. So the fix removes a latent crash and, on that one input, answers where the
/// original threw. That is a behavior change the author should know about, and the rule's page says so —
/// but it is not one that can turn a working program into a broken one, which is why the fix is offered.
/// </para>
/// <para>
/// <b>Only an argument-free <c>Trim()</c> qualifies.</b> <c>Trim(char[])</c> and <c>Trim(char)</c> strip
/// specific characters, which is a different question entirely and has nothing to do with white space.
/// A trimmed value used for anything besides the emptiness test is never matched either: the shape
/// requires the <c>Trim</c> call to be the receiver of the <c>Length</c> read, an operand of the
/// comparison, or the sole argument of <c>IsNullOrEmpty</c> — so <c>var t = text.Trim();</c> followed by
/// a test of <c>t</c> keeps its copy, because something else may still read it.
/// </para>
/// <para>
/// Tests inside lambdas converted to <c>System.Linq.Expressions.Expression&lt;TDelegate&gt;</c> are
/// skipped: a query provider translates the tree it is handed, and swapping one call for another changes
/// the SQL it emits rather than the work this process does.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1219UseIsNullOrWhiteSpaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The trimming member the syntax gate requires.</summary>
    internal const string TrimMethodName = "Trim";

    /// <summary>The replacement member name.</summary>
    internal const string IsNullOrWhiteSpaceMethodName = "IsNullOrWhiteSpace";

    /// <summary>The emptiness helper the rule also matches as a test.</summary>
    internal const string IsNullOrEmptyMethodName = "IsNullOrEmpty";

    /// <summary>The length member the syntax gate reads.</summary>
    private const string LengthPropertyName = "Length";

    /// <summary>The empty-string field the rule accepts alongside the <c>""</c> literal.</summary>
    private const string EmptyFieldName = "Empty";

    /// <summary>The three ways a trimmed string gets asked whether it is empty.</summary>
    internal enum BlankTestKind
    {
        /// <summary><c>text.Trim().Length == 0</c>.</summary>
        Length,

        /// <summary><c>text.Trim() == ""</c> or <c>text.Trim() == string.Empty</c>.</summary>
        EmptyString,

        /// <summary><c>string.IsNullOrEmpty(text.Trim())</c>.</summary>
        IsNullOrEmpty
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseIsNullOrWhiteSpace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (!HasIsNullOrWhiteSpace(start.Compilation))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeTrim, SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Resolves a reported blank test back to the string it trims, and whether the answer is inverted.</summary>
    /// <param name="reported">The node the diagnostic was reported on.</param>
    /// <param name="receiver">The expression the <c>Trim</c> call was made on.</param>
    /// <param name="negated">Whether the test asks for "not blank" and so needs a <c>!</c>.</param>
    /// <returns><see langword="true"/> when the node is still one of the three tests.</returns>
    /// <remarks>
    /// Written top-down for the code fix, which sees only the reported node and the syntax tree. The
    /// analyzer reaches the same three shapes from the other direction, walking up from the <c>Trim</c>
    /// call it is registered on.
    /// </remarks>
    internal static bool TryGetBlankTest(SyntaxNode reported, out ExpressionSyntax? receiver, out bool negated)
    {
        receiver = null;
        if (reported is BinaryExpressionSyntax binary)
        {
            negated = binary.IsKind(SyntaxKind.NotEqualsExpression);
            return TryGetTestedReceiver(binary.Left, out receiver) || TryGetTestedReceiver(binary.Right, out receiver);
        }

        negated = false;
        return reported is InvocationExpressionSyntax { ArgumentList.Arguments: [{ } argument] }
            && TryGetTrimReceiver(argument.Expression, out receiver);
    }

    /// <summary>Returns whether an invocation is a plain argument-free <c>x.Trim()</c>, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsTrimShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == TrimMethodName;

    /// <summary>Reports PSH1219 for a string trimmed only to ask whether it is blank.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeTrim(SyntaxNodeAnalysisContext context)
    {
        var trim = (InvocationExpressionSyntax)context.Node;
        if (!IsTrimShape(trim) || !TryGetTest(trim, out var reported, out var other, out var kind))
        {
            return;
        }

        var model = context.SemanticModel;
        if (!BindsToStringTrim(model, trim, context.CancellationToken)
            || !IsBlankTest(model, reported!, other, kind, context.CancellationToken)
            || IsInsideExpressionTree(reported!, model, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseIsNullOrWhiteSpace,
            reported!.SyntaxTree,
            reported.Span,
            TrimMethodName));
    }

    /// <summary>Finds the test a <c>Trim</c> call feeds, syntactically.</summary>
    /// <param name="trim">The <c>Trim</c> invocation.</param>
    /// <param name="reported">The whole test expression, which is what the fix replaces.</param>
    /// <param name="other">The operand the trimmed value is compared against, for the comparison shapes.</param>
    /// <param name="kind">The shape that matched.</param>
    /// <returns><see langword="true"/> when the trimmed value is consumed by nothing but an emptiness test.</returns>
    private static bool TryGetTest(
        InvocationExpressionSyntax trim,
        out ExpressionSyntax? reported,
        out ExpressionSyntax? other,
        out BlankTestKind kind)
    {
        switch (trim.Parent)
        {
            case MemberAccessExpressionSyntax { Name.Identifier.ValueText: LengthPropertyName } length
                when length.Parent is BinaryExpressionSyntax lengthTest && IsEqualityTest(lengthTest):
            {
                reported = lengthTest;
                other = GetOtherOperand(lengthTest, length);
                kind = BlankTestKind.Length;
                return true;
            }

            case BinaryExpressionSyntax comparison when IsEqualityTest(comparison):
            {
                reported = comparison;
                other = GetOtherOperand(comparison, trim);
                kind = BlankTestKind.EmptyString;
                return true;
            }

            case ArgumentSyntax { NameColon: null, Parent: ArgumentListSyntax { Arguments.Count: 1, Parent: InvocationExpressionSyntax outer } }:
            {
                reported = outer;
                other = null;
                kind = BlankTestKind.IsNullOrEmpty;
                return true;
            }

            default:
            {
                reported = null;
                other = null;
                kind = default;
                return false;
            }
        }
    }

    /// <summary>Returns whether a binary expression is an <c>==</c> or <c>!=</c> test.</summary>
    /// <param name="binary">The candidate expression.</param>
    /// <returns><see langword="true"/> for the two equality kinds.</returns>
    private static bool IsEqualityTest(BinaryExpressionSyntax binary)
        => binary.RawKind is (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression;

    /// <summary>Gets the operand of a comparison that is not the one already matched.</summary>
    /// <param name="binary">The comparison.</param>
    /// <param name="matched">The operand the trimmed value produced.</param>
    /// <returns>The other operand.</returns>
    private static ExpressionSyntax GetOtherOperand(BinaryExpressionSyntax binary, SyntaxNode matched)
        => ReferenceEquals(binary.Left, matched) ? binary.Right : binary.Left;

    /// <summary>Confirms that the matched shape really asks whether the trimmed string is empty.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="reported">The whole test expression.</param>
    /// <param name="other">The compared-against operand, for the comparison shapes.</param>
    /// <param name="kind">The shape that matched.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the test is an emptiness test.</returns>
    private static bool IsBlankTest(
        SemanticModel model,
        ExpressionSyntax reported,
        ExpressionSyntax? other,
        BlankTestKind kind,
        CancellationToken cancellationToken)
        => kind switch
        {
            BlankTestKind.Length => model.GetConstantValue(other!, cancellationToken).Value is 0,
            BlankTestKind.EmptyString => IsEmptyString(model, other!, cancellationToken),
            _ => BindsToIsNullOrEmpty(model, reported, cancellationToken),
        };

    /// <summary>Returns whether an operand is the empty string, as a literal or as <see cref="string.Empty"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="operand">The compared-against operand.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for <c>""</c> and for the <c>string.Empty</c> field.</returns>
    private static bool IsEmptyString(SemanticModel model, ExpressionSyntax operand, CancellationToken cancellationToken)
    {
        if (operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } literal)
        {
            return literal.Token.ValueText.Length == 0;
        }

        return operand is MemberAccessExpressionSyntax { Name.Identifier.ValueText: EmptyFieldName }
            && model.GetSymbolInfo(operand, cancellationToken).Symbol is IFieldSymbol
            {
                IsStatic: true,
                ContainingType.SpecialType: SpecialType.System_String,
            };
    }

    /// <summary>Returns whether the outer call is <see cref="string.IsNullOrEmpty(string)"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="reported">The outer invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the trimmed value is handed straight to the emptiness helper.</returns>
    private static bool BindsToIsNullOrEmpty(SemanticModel model, ExpressionSyntax reported, CancellationToken cancellationToken)
        => model.GetSymbolInfo(reported, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: true,
            Name: IsNullOrEmptyMethodName,
            Parameters.Length: 1,
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Returns whether the invocation is the argument-free <see cref="string.Trim()"/>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="trim">The <c>Trim</c> invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when it trims white space off a string.</returns>
    /// <remarks>
    /// The parameter count settles the <c>Trim(char[])</c> and <c>Trim(char)</c> exclusions on its own —
    /// they take arguments, so they never bind here — and it also rules out an extension method named
    /// <c>Trim</c> on some other type, which would carry the receiver as its first parameter.
    /// </remarks>
    private static bool BindsToStringTrim(SemanticModel model, InvocationExpressionSyntax trim, CancellationToken cancellationToken)
        => model.GetSymbolInfo(trim, cancellationToken).Symbol is IMethodSymbol
        {
            IsStatic: false,
            Name: TrimMethodName,
            Parameters.Length: 0,
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Returns the receiver of a tested value, whether it was tested by length or compared directly.</summary>
    /// <param name="operand">One operand of the comparison.</param>
    /// <param name="receiver">The expression the <c>Trim</c> call was made on.</param>
    /// <returns><see langword="true"/> when the operand is a trimmed value, or the length of one.</returns>
    private static bool TryGetTestedReceiver(ExpressionSyntax operand, out ExpressionSyntax? receiver)
    {
        if (operand is MemberAccessExpressionSyntax { Name.Identifier.ValueText: LengthPropertyName } length)
        {
            return TryGetTrimReceiver(length.Expression, out receiver);
        }

        return TryGetTrimReceiver(operand, out receiver);
    }

    /// <summary>Returns the receiver of an argument-free <c>Trim()</c> call.</summary>
    /// <param name="expression">The candidate expression.</param>
    /// <param name="receiver">The expression the <c>Trim</c> call was made on.</param>
    /// <returns><see langword="true"/> when the expression is <c>x.Trim()</c>.</returns>
    private static bool TryGetTrimReceiver(ExpressionSyntax expression, out ExpressionSyntax? receiver)
    {
        if (expression is InvocationExpressionSyntax invocation && IsTrimShape(invocation))
        {
            receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
            return true;
        }

        receiver = null;
        return false;
    }

    /// <summary>Returns whether the compilation has the replacement API at all.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns><see langword="true"/> when <c>string.IsNullOrWhiteSpace</c> exists.</returns>
    private static bool HasIsNullOrWhiteSpace(Compilation compilation)
    {
        var members = compilation.GetSpecialType(SpecialType.System_String).GetMembers(IsNullOrWhiteSpaceMethodName);
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { IsStatic: true, Parameters.Length: 1 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a test sits inside a lambda converted to an expression tree.</summary>
    /// <param name="node">The reported test.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a query provider, not this process, runs the test.</returns>
    private static bool IsInsideExpressionTree(SyntaxNode node, SemanticModel model, CancellationToken cancellationToken)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is QueryExpressionSyntax)
            {
                return true;
            }

            if (current is AnonymousFunctionExpressionSyntax
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
