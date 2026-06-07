// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Enforces the .NET field naming conventions in a single pass over each field
/// declaration: const, static-readonly, non-private readonly, and other
/// accessible fields are PascalCase (SST1303/SST1311/SST1304/SST1307), while
/// private fields use the runtime <c>_camelCase</c> form (SST1309).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        NamingRules.ConstPascalCase,
        NamingRules.StaticReadonlyPascalCase,
        NamingRules.NonPrivateReadonlyPascalCase,
        NamingRules.AccessibleFieldPascalCase,
        NamingRules.PrivateFieldUnderscoreCamelCase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Analyzes a field declaration and reports each variable that breaks its naming convention.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not FieldDeclarationSyntax field)
        {
            return;
        }

        var classification = FieldClassification.Classify(field.Modifiers);
        var pascalCaseRule = SelectPascalCaseRule(classification);

        foreach (var variable in field.Declaration.Variables)
        {
            Check(context, pascalCaseRule, variable.Identifier);
        }
    }

    /// <summary>Checks a single field variable against its convention and reports a violation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="pascalCaseRule">The PascalCase rule for this field, or <see langword="null"/> for a private (_camelCase) field.</param>
    /// <param name="identifier">The field variable's identifier.</param>
    private static void Check(SyntaxNodeAnalysisContext context, DiagnosticDescriptor? pascalCaseRule, SyntaxToken identifier)
    {
        var name = identifier.ValueText;
        if (NamingHelper.IsAllUnderscores(name))
        {
            return;
        }

        // Private, non-const, not static-readonly fields use the runtime _camelCase form.
        if (pascalCaseRule is null && !NamingHelper.IsUnderscoreCamelCase(name))
        {
            NamingDiagnostic.Report(context, NamingRules.PrivateFieldUnderscoreCamelCase, identifier, NamingHelper.SuggestUnderscoreCamelCase(name));
            return;
        }

        if (pascalCaseRule is null || NamingHelper.BeginsWithUpperCase(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, pascalCaseRule, identifier, NamingHelper.SuggestPascalCase(name));
    }

    /// <summary>Returns the PascalCase rule the field falls under, or <see langword="null"/> for private (_camelCase) fields.</summary>
    /// <param name="classification">The field's classification.</param>
    /// <returns>The applicable PascalCase descriptor, or <see langword="null"/>.</returns>
    private static DiagnosticDescriptor? SelectPascalCaseRule(in FieldClassification classification)
    {
        if (classification.IsConst)
        {
            return NamingRules.ConstPascalCase;
        }

        if (classification.IsStatic && classification.IsReadOnly)
        {
            return NamingRules.StaticReadonlyPascalCase;
        }

        if (classification.IsPrivate)
        {
            return null;
        }

        return classification.IsReadOnly ? NamingRules.NonPrivateReadonlyPascalCase : NamingRules.AccessibleFieldPascalCase;
    }
}
