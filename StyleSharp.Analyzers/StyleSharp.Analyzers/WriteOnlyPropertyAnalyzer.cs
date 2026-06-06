// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports properties that expose a setter or initializer without a getter (SST1421).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WriteOnlyPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoWriteOnlyProperty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports a non-overriding, non-explicit property with a write accessor and no read accessor.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.AccessorList is not { } accessorList
            || property.ExplicitInterfaceSpecifier is not null
            || property.Modifiers.Any(SyntaxKind.OverrideKeyword)
            || property.Modifiers.Any(SyntaxKind.AbstractKeyword)
            || property.Parent is InterfaceDeclarationSyntax)
        {
            return;
        }

        var hasWrite = false;
        for (var i = 0; i < accessorList.Accessors.Count; i++)
        {
            var keyword = accessorList.Accessors[i].Keyword;
            if (keyword.IsKind(SyntaxKind.GetKeyword))
            {
                return;
            }

            hasWrite |= keyword.IsKind(SyntaxKind.SetKeyword) || keyword.IsKind(SyntaxKind.InitKeyword);
        }

        if (!hasWrite)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoWriteOnlyProperty, property.Identifier.GetLocation()));
    }
}
