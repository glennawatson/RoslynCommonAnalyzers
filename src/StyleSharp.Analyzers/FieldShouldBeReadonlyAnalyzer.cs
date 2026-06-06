// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports private instance fields that are assigned only during construction (SST1424).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldShouldBeReadonlyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.FieldShouldBeReadonly);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports a candidate whose variables have no writes outside constructors.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (!IsCandidate(declaration)
            || declaration.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
        {
            return;
        }

        for (var i = 0; i < declaration.Declaration.Variables.Count; i++)
        {
            var variable = declaration.Declaration.Variables[i];
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol field
                || HasWriteOutsideConstructor(context.SemanticModel, type, field, context.CancellationToken))
            {
                return;
            }
        }

        var first = declaration.Declaration.Variables[0];
        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.FieldShouldBeReadonly, first.Identifier.GetLocation(), first.Identifier.ValueText));
    }

    /// <summary>Returns whether a field declaration is eligible for readonly analysis.</summary>
    /// <param name="declaration">The field declaration.</param>
    /// <returns><see langword="true"/> when eligible.</returns>
    private static bool IsCandidate(FieldDeclarationSyntax declaration)
    {
        return ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PrivateKeyword)
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword)
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.ReadOnlyKeyword)
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.ConstKeyword)
            && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.VolatileKeyword);
    }

    /// <summary>Returns whether a field is written outside an instance constructor.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The containing type.</param>
    /// <param name="field">The field symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when an invalid write exists.</returns>
    private static bool HasWriteOutsideConstructor(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IFieldSymbol field,
        CancellationToken cancellationToken)
    {
        foreach (var node in type.DescendantNodes())
        {
            if (node is not IdentifierNameSyntax identifier)
            {
                continue;
            }

            if (identifier.Identifier.ValueText != field.Name
                || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, field)
                || !FieldReferenceAnalysis.IsWrite(identifier))
            {
                continue;
            }

            if (identifier.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not null
                || identifier.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() is not null
                || identifier.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not { } constructor
                || constructor.Parent != type)
            {
                return true;
            }
        }

        return false;
    }
}
