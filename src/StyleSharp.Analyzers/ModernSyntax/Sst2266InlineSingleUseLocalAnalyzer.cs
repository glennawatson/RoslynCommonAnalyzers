// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local that is assigned once and read exactly once, whose initializer can be inlined into that one
/// use (SST2266). The rule is opt-in and off by default because a well-named intermediate local is often kept
/// deliberately.
/// </summary>
/// <remarks>
/// The rewrite is only offered when it is provably behaviour-preserving: the initializer is a side-effect-free
/// expression, the single read immediately follows the declaration, nothing side-effecting is evaluated before
/// the read within that statement, and the local is neither captured by a nested function, aliased by
/// <c>ref</c>/<c>out</c>/<c>in</c>, nor written after its declaration.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2266InlineSingleUseLocalAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.InlineSingleUseLocal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Returns whether an expression is side-effect-free and safe to duplicate at the use site.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <returns><see langword="true"/> when the expression reads state without invoking, allocating, or mutating.</returns>
    internal static bool IsPureInlinable(ExpressionSyntax expression)
        => IsAtomicPure(expression) || IsCompositePure(expression);

    /// <summary>Returns whether an inlined initializer needs parentheses to preserve its meaning.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <returns><see langword="true"/> for an operator expression whose precedence could change when spliced in.</returns>
    internal static bool NeedsParentheses(ExpressionSyntax expression)
        => expression is BinaryExpressionSyntax or ConditionalExpressionSyntax or CastExpressionSyntax or PrefixUnaryExpressionSyntax;

    /// <summary>Finds the single reference to a local within a block, or reports there is not exactly one.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="block">The block holding the local's scope.</param>
    /// <param name="local">The local symbol.</param>
    /// <returns>The single reference, or <see langword="null"/> when there is not exactly one.</returns>
    internal static IdentifierNameSyntax? FindSingleReference(SemanticModel model, BlockSyntax block, ILocalSymbol local)
    {
        IdentifierNameSyntax? reference = null;
        var count = 0;
        foreach (var descendant in block.DescendantNodes())
        {
            if (descendant is not IdentifierNameSyntax identifier || identifier.Identifier.Text != local.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier).Symbol, local))
            {
                continue;
            }

            reference = identifier;
            count++;
        }

        return count == 1 ? reference : null;
    }

    /// <summary>Returns whether a reference writes to, or takes an alias of, the local rather than reading it.</summary>
    /// <param name="reference">The reference to inspect.</param>
    /// <returns><see langword="true"/> when the reference is an assignment target, an increment, or a by-reference argument.</returns>
    internal static bool IsWriteOrAlias(IdentifierNameSyntax reference) => reference.Parent switch
    {
        AssignmentExpressionSyntax assignment => assignment.Left == reference,
        ArgumentSyntax { RefKindKeyword.RawKind: not (int)SyntaxKind.None } => true,
        PostfixUnaryExpressionSyntax => true,
        PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
        RefExpressionSyntax => true,
        _ => false,
    };

    /// <summary>Returns whether a reference sits inside a nested function relative to a boundary block.</summary>
    /// <param name="reference">The reference to inspect.</param>
    /// <param name="boundary">The block that bounds the local's scope.</param>
    /// <returns><see langword="true"/> when a lambda or local function encloses the reference within the boundary.</returns>
    internal static bool IsCaptured(SyntaxNode reference, BlockSyntax boundary)
    {
        for (var node = reference.Parent; node is not null && node != boundary; node = node.Parent)
        {
            if (node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a side-effecting expression is evaluated before a reference within a statement.</summary>
    /// <param name="statement">The statement holding the reference.</param>
    /// <param name="reference">The reference the initializer would be inlined into.</param>
    /// <returns><see langword="true"/> when moving the initializer here could reorder an observable side effect.</returns>
    internal static bool HasSideEffectBeforeReference(StatementSyntax statement, SyntaxNode reference)
    {
        var referenceStart = reference.Span.Start;
        foreach (var node in statement.DescendantNodes())
        {
            if (node.Span.End <= referenceStart && IsSideEffecting(node))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an expression is a leaf that reads state without any operation.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for a literal, identifier, <c>this</c>, <c>default(T)</c>, <c>typeof</c>, or <c>sizeof</c>.</returns>
    private static bool IsAtomicPure(ExpressionSyntax expression)
        => expression is LiteralExpressionSyntax or IdentifierNameSyntax or ThisExpressionSyntax
            or DefaultExpressionSyntax or TypeOfExpressionSyntax or SizeOfExpressionSyntax;

    /// <summary>Returns whether a compound expression is pure given that its operands are pure.</summary>
    /// <param name="expression">The expression to classify.</param>
    /// <returns><see langword="true"/> for a member access, cast, or operator expression over pure operands.</returns>
    private static bool IsCompositePure(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax member => member.IsKind(SyntaxKind.SimpleMemberAccessExpression) && IsPureInlinable(member.Expression),
        ParenthesizedExpressionSyntax parenthesized => IsPureInlinable(parenthesized.Expression),
        CastExpressionSyntax cast => IsPureInlinable(cast.Expression),
        PrefixUnaryExpressionSyntax unary => IsPureUnary(unary),
        BinaryExpressionSyntax binary => IsPureInlinable(binary.Left) && IsPureInlinable(binary.Right),
        ConditionalExpressionSyntax conditional => IsPureConditional(conditional),
        _ => false,
    };

    /// <summary>Returns whether a prefix-unary expression is a non-mutating operator over a pure operand.</summary>
    /// <param name="unary">The prefix-unary expression.</param>
    /// <returns><see langword="true"/> for a value operator such as <c>-</c>, <c>!</c>, or <c>~</c> over a pure operand.</returns>
    private static bool IsPureUnary(PrefixUnaryExpressionSyntax unary)
        => !unary.IsKind(SyntaxKind.PreIncrementExpression)
            && !unary.IsKind(SyntaxKind.PreDecrementExpression)
            && !unary.IsKind(SyntaxKind.AddressOfExpression)
            && !unary.IsKind(SyntaxKind.PointerIndirectionExpression)
            && IsPureInlinable(unary.Operand);

    /// <summary>Returns whether a conditional expression is pure in all three of its parts.</summary>
    /// <param name="conditional">The conditional expression.</param>
    /// <returns><see langword="true"/> when the condition and both branches are pure.</returns>
    private static bool IsPureConditional(ConditionalExpressionSyntax conditional)
        => IsPureInlinable(conditional.Condition) && IsPureInlinable(conditional.WhenTrue) && IsPureInlinable(conditional.WhenFalse);

    /// <summary>Returns whether a node performs an observable side effect.</summary>
    /// <param name="node">The node to classify.</param>
    /// <returns><see langword="true"/> for a call, allocation, assignment, await, or increment.</returns>
    private static bool IsSideEffecting(SyntaxNode node) => node
        is InvocationExpressionSyntax
        or ObjectCreationExpressionSyntax
        or ImplicitObjectCreationExpressionSyntax
        or AssignmentExpressionSyntax
        or AwaitExpressionSyntax
        or PostfixUnaryExpressionSyntax
        or ArrayCreationExpressionSyntax;

    /// <summary>Returns the enclosing block and declarator when a local's declaration can start a single-use inline.</summary>
    /// <param name="local">The local declaration statement.</param>
    /// <returns>The block and declarator, or <see langword="null"/> when the shape does not match.</returns>
    private static (BlockSyntax Block, VariableDeclaratorSyntax Declarator)? GetInlinableShape(LocalDeclarationStatementSyntax local)
    {
        if (local.Modifiers.Any(SyntaxKind.ConstKeyword)
            || local.Parent is not BlockSyntax block
            || local.Declaration is not { Variables.Count: 1 } declaration
            || declaration.Type is RefTypeSyntax
            || declaration.Variables[0].Initializer is not { } equalsValue
            || !IsPureInlinable(equalsValue.Value))
        {
            return null;
        }

        return (block, declaration.Variables[0]);
    }

    /// <summary>Returns whether a local's one reference is a plain, uncaptured read this fix can safely inline into.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="block">The enclosing block.</param>
    /// <param name="useStatement">The statement immediately after the declaration.</param>
    /// <param name="symbol">The local symbol.</param>
    /// <returns><see langword="true"/> when inlining preserves behaviour.</returns>
    private static bool IsSafeSingleUse(SemanticModel model, BlockSyntax block, StatementSyntax useStatement, ILocalSymbol symbol)
        => FindSingleReference(model, block, symbol) is { } reference
            && useStatement.Span.Contains(reference.Span)
            && !IsWriteOrAlias(reference)
            && !IsCaptured(reference, block)
            && !HasSideEffectBeforeReference(useStatement, reference);

    /// <summary>Reports a single-use local whose initializer can be safely inlined into its one read.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (GetInlinableShape(local) is not { } shape)
        {
            return;
        }

        var (block, declarator) = shape;
        var declarationIndex = block.Statements.IndexOf(local);
        if (declarationIndex < 0 || declarationIndex + 1 >= block.Statements.Count)
        {
            return;
        }

        var useStatement = block.Statements[declarationIndex + 1];
        if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not ILocalSymbol symbol
            || !IsSafeSingleUse(context.SemanticModel, block, useStatement, symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.InlineSingleUseLocal, declarator.Identifier.GetLocation(), symbol.Name));
    }
}
