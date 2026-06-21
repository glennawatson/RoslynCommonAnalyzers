// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports small flow-shape syntax upgrades that keep locals close to their first use.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxFlowAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 7 language-version value.</summary>
    private const int CSharp7 = 700;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.UseThrowExpression,
        ModernSyntaxRules.InlineOutVariableDeclaration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeIfStatement, SyntaxKind.IfStatement);
        context.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
    }

    /// <summary>Returns whether an if statement can be folded into a throw expression.</summary>
    /// <param name="ifStatement">The if statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="throwExpression">The expression to throw.</param>
    /// <returns><see langword="true"/> when the next statement returns the same guarded symbol.</returns>
    internal static bool TryGetThrowExpressionCandidate(
        IfStatementSyntax ifStatement,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ExpressionSyntax throwExpression)
    {
        throwExpression = null!;
        if (ifStatement.Else is not null
            || HasNonWhitespaceTrivia(ifStatement)
            || !TryGetGuardedIdentifier(ifStatement.Condition, out var guardedIdentifier)
            || !TryGetThrowStatement(ifStatement.Statement, out var throwStatement)
            || throwStatement.Expression is null
            || !TryGetNextStatement(ifStatement, out var nextStatement)
            || nextStatement is not ReturnStatementSyntax { Expression: IdentifierNameSyntax returnedIdentifier })
        {
            return false;
        }

        var guardedSymbol = model.GetSymbolInfo(guardedIdentifier, cancellationToken).Symbol;
        var returnedSymbol = model.GetSymbolInfo(returnedIdentifier, cancellationToken).Symbol;
        if (guardedSymbol is null || !SymbolEqualityComparer.Default.Equals(guardedSymbol, returnedSymbol))
        {
            return false;
        }

        throwExpression = throwStatement.Expression;
        return true;
    }

    /// <summary>Finds the out argument that can absorb the supplied local declaration.</summary>
    /// <param name="declaration">The local declaration.</param>
    /// <param name="nextStatement">The statement immediately after the declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="argument">The out argument.</param>
    /// <returns><see langword="true"/> when the declaration can be safely inlined.</returns>
    internal static bool TryGetInlineOutArgument(
        LocalDeclarationStatementSyntax declaration,
        StatementSyntax nextStatement,
        SemanticModel model,
        CancellationToken cancellationToken,
        out ArgumentSyntax argument)
    {
        argument = null!;
        var variable = declaration.Declaration.Variables[0];
        var local = model.GetDeclaredSymbol(variable, cancellationToken);
        if (local is null)
        {
            return false;
        }

        ArgumentSyntax? match = null;
        foreach (var node in nextStatement.DescendantNodes(static node => node is not AnonymousFunctionExpressionSyntax))
        {
            if (node is not ArgumentSyntax candidate
                || !candidate.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)
                || candidate.Expression is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (!SymbolEqualityComparer.Default.Equals(local, symbol))
            {
                continue;
            }

            if (match is not null)
            {
                return false;
            }

            match = candidate;
        }

        if (match is null)
        {
            return false;
        }

        argument = match;
        return true;
    }

    /// <summary>Gets the statement immediately after the supplied statement in its block.</summary>
    /// <param name="statement">The current statement.</param>
    /// <param name="nextStatement">The next statement.</param>
    /// <returns><see langword="true"/> when there is a following statement in the same block.</returns>
    internal static bool TryGetNextStatement(StatementSyntax statement, out StatementSyntax nextStatement)
    {
        nextStatement = null!;
        if (statement.Parent is not BlockSyntax block)
        {
            return false;
        }

        var index = block.Statements.IndexOf(statement);
        if (index < 0 || index + 1 >= block.Statements.Count)
        {
            return false;
        }

        nextStatement = block.Statements[index + 1];
        return true;
    }

    /// <summary>Reports a null guard followed by returning the guarded value.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeIfStatement(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(ifStatement, CSharp7)
            || !TryGetThrowExpressionCandidate(ifStatement, context.SemanticModel, context.CancellationToken, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseThrowExpression, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Reports a local declaration used only as the next statement's out argument.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (declaration.Declaration.Variables.Count != 1
            || declaration.Declaration.Type is IdentifierNameSyntax { Identifier.ValueText: "var" }
            || declaration.Declaration.Variables[0].Initializer is not null
            || HasNonWhitespaceTrivia(declaration)
            || !TryGetNextStatement(declaration, out var nextStatement)
            || !TryGetInlineOutArgument(
                declaration,
                nextStatement,
                context.SemanticModel,
                context.CancellationToken,
                out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.InlineOutVariableDeclaration, declaration.Declaration.Variables[0].Identifier.GetLocation()));
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">A syntax node in the tree.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;

    /// <summary>Finds the identifier checked against <see langword="null"/>.</summary>
    /// <param name="condition">The condition expression.</param>
    /// <param name="identifier">The guarded identifier.</param>
    /// <returns><see langword="true"/> when the condition is a supported null check.</returns>
    private static bool TryGetGuardedIdentifier(ExpressionSyntax condition, out IdentifierNameSyntax identifier)
    {
        condition = ExpressionSimplificationAnalyzer.Unwrap(condition);
        if (condition is IsPatternExpressionSyntax
            {
                Expression: IdentifierNameSyntax patternIdentifier,
                Pattern: ConstantPatternSyntax { Expression.RawKind: (int)SyntaxKind.NullLiteralExpression }
            })
        {
            identifier = patternIdentifier;
            return true;
        }

        if (condition is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.EqualsExpression))
        {
            if (ExpressionSimplificationAnalyzer.Unwrap(binary.Left) is IdentifierNameSyntax leftIdentifier
                && ExpressionSimplificationAnalyzer.Unwrap(binary.Right).IsKind(SyntaxKind.NullLiteralExpression))
            {
                identifier = leftIdentifier;
                return true;
            }

            if (ExpressionSimplificationAnalyzer.Unwrap(binary.Right) is IdentifierNameSyntax rightIdentifier
                && ExpressionSimplificationAnalyzer.Unwrap(binary.Left).IsKind(SyntaxKind.NullLiteralExpression))
            {
                identifier = rightIdentifier;
                return true;
            }
        }

        identifier = null!;
        return false;
    }

    /// <summary>Gets a single throw statement from a statement or block.</summary>
    /// <param name="statement">The statement to inspect.</param>
    /// <param name="throwStatement">The throw statement.</param>
    /// <returns><see langword="true"/> when the statement only throws.</returns>
    private static bool TryGetThrowStatement(StatementSyntax statement, out ThrowStatementSyntax throwStatement)
    {
        if (statement is ThrowStatementSyntax directThrow)
        {
            throwStatement = directThrow;
            return true;
        }

        if (statement is BlockSyntax { Statements.Count: 1 } block && block.Statements[0] is ThrowStatementSyntax blockThrow)
        {
            throwStatement = blockThrow;
            return true;
        }

        throwStatement = null!;
        return false;
    }

    /// <summary>Returns whether preserving trivia would require moving comments.</summary>
    /// <param name="node">The node to inspect.</param>
    /// <returns><see langword="true"/> when the node has non-whitespace leading or trailing trivia.</returns>
    private static bool HasNonWhitespaceTrivia(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        foreach (var trivia in node.GetTrailingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }
}
