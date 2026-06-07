// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires fields to be private (SST1401). Constants may be any accessibility;
/// every other exposed field is reported so it can be hidden behind a property.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldVisibilityAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.FieldsPrivate);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports a field that exposes more than private accessibility.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var modifiers = field.Modifiers;
        if (ModifierListHelper.Contains(modifiers, SyntaxKind.ConstKeyword)
            || (!ModifierListHelper.ContainsEither(modifiers, SyntaxKind.PublicKeyword, SyntaxKind.InternalKeyword)
                && !ModifierListHelper.Contains(modifiers, SyntaxKind.ProtectedKeyword)))
        {
            return;
        }

        var variables = field.Declaration.Variables;
        if (variables.Count == 0)
        {
            return;
        }

        var token = variables[0].Identifier;
        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.FieldsPrivate, token.GetLocation(), token.ValueText));
    }
}
