// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local assigned from an <c>as</c> conversion and then null-checked, which can use an <c>is</c>
/// declaration pattern (SST2274).
/// </summary>
/// <remarks>
/// <para>Two shapes are recognised, both folding the declaration and the separate null check into one pattern:</para>
/// <list type="bullet">
/// <item><description>the guarded-use shape <c>var s = o as T; if (s != null) { ...uses of s... }</c> becomes <c>if (o is T s) { ...uses of s... }</c>;</description></item>
/// <item><description>the early-exit shape <c>var s = o as T; if (s == null) return;</c> becomes <c>if (o is not T s) return;</c>, leaving later uses of <c>s</c> untouched.</description></item>
/// </list>
/// <para>
/// The clean path is a pure syntax check — a single-declarator local whose initializer is an <c>as</c> conversion
/// and whose immediately following statement is an <c>if</c> that null-checks that local — so an ordinary local
/// declaration is rejected before any symbol is bound. Only a matched candidate pays for the semantic work that
/// proves the rewrite safe: the target is a reference type, the local's type matches the pattern variable's, the
/// local is never reassigned, uses sit only where the pattern variable is definitely assigned, and the <c>as</c>
/// operand is side-effect-free so it can be re-read at the test.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2274AsAssignmentToIsPatternAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.ConvertAsAssignmentToIsPattern);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>
    /// Matches the pure-syntax shape shared by the analyzer and the code fix: a single-declarator local whose
    /// initializer is an <c>as</c> conversion, immediately followed by an <c>else</c>-less <c>if</c> that
    /// null-checks that local. The early-exit polarity additionally requires the guarded body to always exit.
    /// </summary>
    /// <param name="local">The candidate local declaration statement.</param>
    /// <param name="candidate">The matched candidate parts.</param>
    /// <returns><see langword="true"/> when the syntax shape matches.</returns>
    internal static bool TryGetSyntacticCandidate(LocalDeclarationStatementSyntax local, out AsAssignmentPatternCandidate candidate)
    {
        candidate = default;
        if (!TryGetAsDeclaration(local, out var block, out var declarator, out var asExpression, out var type))
        {
            return false;
        }

        var statements = block.Statements;
        var index = statements.IndexOf(local);
        if (index < 0
            || index + 1 >= statements.Count
            || statements[index + 1] is not IfStatementSyntax { Else: null } ifStatement
            || !TryMatchNullCheck(ifStatement.Condition, declarator.Identifier.ValueText, out var isNegative))
        {
            return false;
        }

        // The early-exit rewrite ('is not T s') leaves 's' unassigned on the guard path, so the guard must exit
        // before it could ever fall through to a use of an unassigned pattern variable.
        if (isNegative && !DefinitelyExits(ifStatement.Statement))
        {
            return false;
        }

        candidate = new AsAssignmentPatternCandidate(block, local, declarator, asExpression, type, ifStatement, isNegative);
        return true;
    }

    /// <summary>Builds the <c>o is T s</c> or <c>o is not T s</c> pattern condition the fix substitutes.</summary>
    /// <param name="operand">The <c>as</c> operand re-read by the pattern.</param>
    /// <param name="type">The <c>as</c> target type used as the pattern type.</param>
    /// <param name="name">The pattern variable name (the folded local's name).</param>
    /// <param name="isNegative">Whether to emit the negated early-exit pattern.</param>
    /// <returns>The is-pattern expression.</returns>
    internal static IsPatternExpressionSyntax BuildPattern(ExpressionSyntax operand, TypeSyntax type, string name, bool isNegative)
    {
        var declaration = SyntaxFactory.DeclarationPattern(
            type.WithoutTrivia(),
            SyntaxFactory.SingleVariableDesignation(SyntaxFactory.Identifier(name)));
        PatternSyntax pattern = isNegative
            ? SyntaxFactory.UnaryPattern(SyntaxFactory.Token(SyntaxKind.NotKeyword).WithTrailingTrivia(SyntaxFactory.Space), declaration)
            : declaration;
        return SyntaxFactory.IsPatternExpression(
            operand.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.Token(SyntaxKind.IsKeyword).WithTrailingTrivia(SyntaxFactory.Space),
            pattern);
    }

    /// <summary>Reports an <c>as</c>-assignment-plus-null-check that a declaration pattern can express directly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (!TryGetSyntacticCandidate(local, out var candidate)
            || !SupportsRequiredSyntax(context.Node.SyntaxTree, candidate.IsNegative))
        {
            return;
        }

        var operand = PatternMatchingAnalyzer.Unwrap(candidate.AsExpression.Left);
        if (!SideEffectFreeExpression.IsSideEffectFree(operand))
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        // 'o is T s' only stands in for 'o as T' when T is a reference type; a value type (including Nullable<T>)
        // would change the pattern variable's type or fail to compile.
        var targetType = model.GetTypeInfo(candidate.Type, token).Type;
        if (targetType is not { IsReferenceType: true, TypeKind: not (TypeKind.Error or TypeKind.Dynamic) })
        {
            return;
        }

        // The local's declared type must match the pattern variable's type exactly (ignoring nullability), so no
        // later use of the local can bind to a different overload once its type narrows to T.
        if (model.GetDeclaredSymbol(candidate.Declarator, token) is not ILocalSymbol localSymbol
            || !SymbolEqualityComparer.Default.Equals(localSymbol.Type, targetType))
        {
            return;
        }

        if (!ReferencesAreCompatible(model, candidate, localSymbol, token)
            || !RewrittenPatternBinds(model, candidate, operand))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.ConvertAsAssignmentToIsPattern,
            candidate.Declarator.Identifier.GetLocation(),
            localSymbol.Name));
    }

    /// <summary>Matches a single-declarator local whose initializer is an <c>as</c> conversion to a named type.</summary>
    /// <param name="local">The local declaration statement.</param>
    /// <param name="block">The block that owns the declaration.</param>
    /// <param name="declarator">The single declarator.</param>
    /// <param name="asExpression">The <c>as</c> conversion initializer.</param>
    /// <param name="type">The <c>as</c> target type.</param>
    /// <returns><see langword="true"/> when the declaration has the required shape.</returns>
    private static bool TryGetAsDeclaration(
        LocalDeclarationStatementSyntax local,
        out BlockSyntax block,
        out VariableDeclaratorSyntax declarator,
        out BinaryExpressionSyntax asExpression,
        out TypeSyntax type)
    {
        block = null!;
        declarator = null!;
        asExpression = null!;
        type = null!;

        // A 'using' declaration (including 'await using', which also carries the 'using' keyword) disposes at
        // scope end, so folding the local away would drop that disposal; a multi-declarator declaration cannot be
        // folded either. A 'const' local can never have an 'as' initializer, so the initializer check below covers it.
        if (local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
            || local.Declaration.Variables.Count != 1
            || local.Parent is not BlockSyntax parentBlock)
        {
            return false;
        }

        var candidateDeclarator = local.Declaration.Variables[0];
        if (candidateDeclarator.Initializer is not { } initializer
            || PatternMatchingAnalyzer.Unwrap(initializer.Value) is not BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } candidateAs
            || candidateAs.Right is not TypeSyntax candidateType)
        {
            return false;
        }

        block = parentBlock;
        declarator = candidateDeclarator;
        asExpression = candidateAs;
        type = candidateType;
        return true;
    }

    /// <summary>Returns whether the tree's language version supports the pattern the fix would emit.</summary>
    /// <param name="tree">The syntax tree.</param>
    /// <param name="isNegative">Whether the negated <c>is not</c> pattern (C# 9) is required.</param>
    /// <returns><see langword="true"/> when the required pattern syntax is available.</returns>
    private static bool SupportsRequiredSyntax(SyntaxTree tree, bool isNegative)
    {
        // The declaration pattern needs C# 7; the negated 'is not' pattern needs C# 9.
        var required = isNegative ? LanguageVersion.CSharp9 : LanguageVersion.CSharp7;
        return tree.Options is CSharpParseOptions parseOptions && parseOptions.LanguageVersion >= required;
    }

    /// <summary>Reads a null check on a named local, distinguishing the non-null and null polarities.</summary>
    /// <param name="condition">The <c>if</c> condition.</param>
    /// <param name="name">The local's name.</param>
    /// <param name="isNegative">Whether the check is the null (early-exit) polarity.</param>
    /// <returns><see langword="true"/> when the condition is a null check on the named local.</returns>
    private static bool TryMatchNullCheck(ExpressionSyntax condition, string name, out bool isNegative)
    {
        isNegative = false;
        return condition switch
        {
            BinaryExpressionSyntax binary => TryMatchBinaryNullCheck(binary, name, out isNegative),
            IsPatternExpressionSyntax isPattern => TryMatchPatternNullCheck(isPattern, name, out isNegative),
            _ => false,
        };
    }

    /// <summary>Reads a binary null check: <c>s == null</c>, <c>s != null</c>, or the <c>s is object</c> type-test.</summary>
    /// <param name="binary">The binary condition.</param>
    /// <param name="name">The local's name.</param>
    /// <param name="isNegative">Whether the check is the null polarity.</param>
    /// <returns><see langword="true"/> when the binary expression is a null check on the named local.</returns>
    private static bool TryMatchBinaryNullCheck(BinaryExpressionSyntax binary, string name, out bool isNegative)
    {
        isNegative = binary.IsKind(SyntaxKind.EqualsExpression);
        if (binary.IsKind(SyntaxKind.EqualsExpression) || binary.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return IsNullComparedToName(binary, name);
        }

        // 'o is object' is a type-test that is true exactly when the reference is non-null.
        return binary.IsKind(SyntaxKind.IsExpression)
            && IsNameExpression(binary.Left, name)
            && binary.Right is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword };
    }

    /// <summary>Reads a pattern null check: <c>s is null</c> or <c>s is not null</c>.</summary>
    /// <param name="isPattern">The is-pattern condition.</param>
    /// <param name="name">The local's name.</param>
    /// <param name="isNegative">Whether the check is the null polarity.</param>
    /// <returns><see langword="true"/> when the is-pattern is a null check on the named local.</returns>
    private static bool TryMatchPatternNullCheck(IsPatternExpressionSyntax isPattern, string name, out bool isNegative)
    {
        isNegative = false;
        if (!IsNameExpression(isPattern.Expression, name))
        {
            return false;
        }

        if (isPattern.Pattern is ConstantPatternSyntax constant && IsNullLiteral(constant.Expression))
        {
            isNegative = true;
            return true;
        }

        return isPattern.Pattern is UnaryPatternSyntax { RawKind: (int)SyntaxKind.NotPattern, Pattern: ConstantPatternSyntax notConstant }
            && IsNullLiteral(notConstant.Expression);
    }

    /// <summary>Returns whether a comparison pairs the <c>null</c> literal with the named local.</summary>
    /// <param name="comparison">The equality comparison.</param>
    /// <param name="name">The local's name.</param>
    /// <returns><see langword="true"/> when one side is <c>null</c> and the other is the named local.</returns>
    private static bool IsNullComparedToName(BinaryExpressionSyntax comparison, string name)
        => (IsNullLiteral(comparison.Right) && IsNameExpression(comparison.Left, name))
            || (IsNullLiteral(comparison.Left) && IsNameExpression(comparison.Right, name));

    /// <summary>Returns whether an expression is exactly the named identifier.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <param name="name">The identifier name.</param>
    /// <returns><see langword="true"/> for an identifier with that name.</returns>
    private static bool IsNameExpression(ExpressionSyntax expression, string name)
        => expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name;

    /// <summary>Returns whether an expression is the <c>null</c> literal.</summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns><see langword="true"/> for a <c>null</c> literal.</returns>
    private static bool IsNullLiteral(ExpressionSyntax expression) => expression.IsKind(SyntaxKind.NullLiteralExpression);

    /// <summary>Returns whether a statement always leaves the enclosing block without falling through.</summary>
    /// <param name="statement">The guard body.</param>
    /// <returns><see langword="true"/> when the body ends in a <c>return</c>, <c>throw</c>, <c>continue</c>, or <c>break</c>.</returns>
    private static bool DefinitelyExits(StatementSyntax statement) => statement switch
    {
        BlockSyntax block => block.Statements.Count > 0 && DefinitelyExits(block.Statements[block.Statements.Count - 1]),
        ReturnStatementSyntax or ThrowStatementSyntax or ContinueStatementSyntax or BreakStatementSyntax => true,
        _ => false,
    };

    /// <summary>Returns whether a reference writes to, or takes an alias of, the local rather than reading it.</summary>
    /// <param name="reference">The reference to inspect.</param>
    /// <returns><see langword="true"/> when the reference is an assignment target or a by-reference alias.</returns>
    /// <remarks>
    /// The folded local is always a reference type, so <c>++</c>/<c>--</c> can never target it; only a plain or
    /// compound assignment, a <c>ref</c>/<c>out</c>/<c>in</c> argument, or a <c>ref</c> alias can rebind it.
    /// </remarks>
    private static bool IsWriteOrAlias(IdentifierNameSyntax reference) => reference.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == reference,
        ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } => true,
        RefExpressionSyntax => true,
        _ => false,
    };

    /// <summary>Confirms every reference to the local sits where the folded pattern variable stays valid.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="candidate">The matched candidate.</param>
    /// <param name="local">The local symbol.</param>
    /// <param name="token">A token that cancels analysis.</param>
    /// <returns>
    /// <see langword="true"/> when the local is never reassigned and, for the guarded-use shape, is read only
    /// inside the guarded then-branch, or, for the early-exit shape, is never read inside the guard body.
    /// </returns>
    private static bool ReferencesAreCompatible(SemanticModel model, AsAssignmentPatternCandidate candidate, ILocalSymbol local, CancellationToken token)
    {
        var conditionSpan = candidate.IfStatement.Condition.Span;
        var bodySpan = candidate.IfStatement.Statement.Span;
        foreach (var node in candidate.Block.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax reference
                || reference.Identifier.Text != local.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(reference, token).Symbol, local))
            {
                continue;
            }

            if (!ReferenceIsAllowed(candidate.IsNegative, reference, conditionSpan, bodySpan))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether a single reference to the local keeps the fold behaviour-preserving.</summary>
    /// <param name="isNegative">Whether the candidate is the early-exit polarity.</param>
    /// <param name="reference">The reference to classify.</param>
    /// <param name="conditionSpan">The span of the <c>if</c> condition.</param>
    /// <param name="bodySpan">The span of the guarded body.</param>
    /// <returns><see langword="true"/> when the reference is a read the pattern variable can serve.</returns>
    private static bool ReferenceIsAllowed(bool isNegative, IdentifierNameSyntax reference, TextSpan conditionSpan, TextSpan bodySpan)
    {
        // Any reassignment or aliasing of the local means the pattern variable would not preserve its value.
        if (IsWriteOrAlias(reference))
        {
            return false;
        }

        var inBody = bodySpan.Contains(reference.Span);
        if (isNegative)
        {
            // Under 'is not T s' the local is unassigned inside the guard body, so it may not be read there.
            return !inBody;
        }

        // Under 'is T s' the pattern variable is only definitely assigned inside the then-branch (or the check).
        return inBody || conditionSpan.Contains(reference.Span);
    }

    /// <summary>Speculatively binds the rewritten type test to confirm it still yields a boolean at the check site.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="candidate">The matched candidate.</param>
    /// <param name="operand">The <c>as</c> operand re-read by the pattern.</param>
    /// <returns><see langword="true"/> when <c>operand is T</c> binds as a boolean expression.</returns>
    private static bool RewrittenPatternBinds(SemanticModel model, AsAssignmentPatternCandidate candidate, ExpressionSyntax operand)
    {
        var typeTest = PatternMatchingAnalyzer.BuildIsTypeTest(operand, candidate.Type);
        var speculative = model.GetSpeculativeTypeInfo(candidate.IfStatement.Condition.SpanStart, typeTest, SpeculativeBindingOption.BindAsExpression);
        return speculative.Type is { SpecialType: SpecialType.System_Boolean };
    }

    /// <summary>Matched parts for an <c>as</c> assignment followed by a null check on the assigned local.</summary>
    /// <param name="Block">The block containing the declaration and the <c>if</c>.</param>
    /// <param name="Declaration">The local declaration statement to remove.</param>
    /// <param name="Declarator">The single declarator whose name becomes the pattern variable.</param>
    /// <param name="AsExpression">The <c>as</c> conversion supplying the operand.</param>
    /// <param name="Type">The <c>as</c> target type used as the pattern type.</param>
    /// <param name="IfStatement">The immediately following <c>if</c> whose condition null-checks the local.</param>
    /// <param name="IsNegative">Whether the null check is the null (early-exit) polarity.</param>
    internal readonly record struct AsAssignmentPatternCandidate(
        BlockSyntax Block,
        LocalDeclarationStatementSyntax Declaration,
        VariableDeclaratorSyntax Declarator,
        BinaryExpressionSyntax AsExpression,
        TypeSyntax Type,
        IfStatementSyntax IfStatement,
        bool IsNegative);
}
