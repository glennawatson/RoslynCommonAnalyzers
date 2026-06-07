// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports a private field used as resettable temporary storage by one method (SST1422).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateFieldUsedAsLocalAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.PrivateFieldUsedAsLocal);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports an eligible field reset by the first statement of its only using method.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (!TryGetCandidate(context, declaration, out var variable, out var field, out var method)
            || method!.Body is not { Statements.Count: > 0 } body
            || body.Statements[0] is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
            || !IsFieldTarget(context.SemanticModel, assignment.Left, field!, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.PrivateFieldUsedAsLocal, variable!.Identifier.GetLocation(), field!.Name));
    }

    /// <summary>Extracts an eligible field and its only using method.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="declaration">The field declaration.</param>
    /// <param name="variable">The variable declarator.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="method">The only using method.</param>
    /// <returns><see langword="true"/> when all candidate checks pass.</returns>
    private static bool TryGetCandidate(
        SyntaxNodeAnalysisContext context,
        FieldDeclarationSyntax declaration,
        out VariableDeclaratorSyntax? variable,
        out IFieldSymbol? field,
        out MethodDeclarationSyntax? method)
    {
        variable = null;
        field = null;
        method = null;
        if (declaration.Declaration.Variables is not [{ Initializer: null } candidate]
            || !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PrivateKeyword)
            || ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword)
            || declaration.AttributeLists.Count > 0
            || declaration.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            || context.SemanticModel.GetDeclaredSymbol(candidate, context.CancellationToken) is not IFieldSymbol symbol
            || !TryGetSingleMethod(context.SemanticModel, type, symbol, context.CancellationToken, out method))
        {
            return false;
        }

        variable = candidate;
        field = symbol;
        return true;
    }

    /// <summary>Finds the single method containing every reference to a field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="method">The single using method.</param>
    /// <returns><see langword="true"/> when every reference belongs to one method.</returns>
    private static bool TryGetSingleMethod(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IFieldSymbol field,
        CancellationToken cancellationToken,
        out MethodDeclarationSyntax? method)
    {
        method = null;
        var methodStart = -1;
        return VisitFieldReferences(type, model, field, cancellationToken, ref methodStart, ref method)
            && methodStart >= 0;
    }

    /// <summary>Visits field references with an indexed subtree walk that exits on the first invalid use.</summary>
    /// <param name="node">The current syntax node.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="methodStart">The span start of the single using method.</param>
    /// <param name="method">The single using method.</param>
    /// <returns><see langword="true"/> when every reference belongs to one eligible method.</returns>
    private static bool VisitFieldReferences(
        SyntaxNode node,
        SemanticModel model,
        IFieldSymbol field,
        CancellationToken cancellationToken,
        ref int methodStart,
        ref MethodDeclarationSyntax? method)
    {
        if (node is IdentifierNameSyntax identifier
            && !TryRecordFieldReference(identifier, model, field, cancellationToken, ref methodStart, ref method))
        {
            return false;
        }

        var children = node.ChildNodesAndTokens();
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (!child.IsNode || child.AsNode() is not { } childNode)
            {
                continue;
            }

            if (!VisitFieldReferences(childNode, model, field, cancellationToken, ref methodStart, ref method))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Records one matching field reference and rejects uses outside a single top-level method.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <param name="methodStart">The span start of the single using method.</param>
    /// <param name="method">The single using method.</param>
    /// <returns><see langword="true"/> when the identifier is either unrelated or a valid reference.</returns>
    private static bool TryRecordFieldReference(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        IFieldSymbol field,
        CancellationToken cancellationToken,
        ref int methodStart,
        ref MethodDeclarationSyntax? method)
    {
        if (identifier.Identifier.ValueText != field.Name
            || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field))
        {
            return true;
        }

        if (identifier.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not null
            || identifier.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() is not null
            || identifier.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } containing)
        {
            return false;
        }

        if (methodStart >= 0 && methodStart != containing.SpanStart)
        {
            return false;
        }

        methodStart = containing.SpanStart;
        method = containing;
        return true;
    }

    /// <summary>Returns whether an expression binds to the field.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is the field target.</returns>
    private static bool IsFieldTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        IFieldSymbol field,
        CancellationToken cancellationToken)
        => SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(expression, cancellationToken).Symbol, field);
}
