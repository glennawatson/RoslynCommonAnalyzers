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
        foreach (var node in type.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (identifier.Identifier.ValueText != field.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field))
            {
                continue;
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
        }

        return methodStart >= 0;
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
