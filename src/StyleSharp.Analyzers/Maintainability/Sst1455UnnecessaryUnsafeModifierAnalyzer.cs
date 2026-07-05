// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports unsafe modifiers on declarations that do not contain unsafe syntax (SST1455).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1455UnnecessaryUnsafeModifierAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoUnnecessaryUnsafeModifier);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.OperatorDeclaration,
            SyntaxKind.ConversionOperatorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration);
    }

    /// <summary>Reports an unsafe modifier when no descendant syntax requires it.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberDeclarationSyntax declaration
            || !TryGetUnsafeModifier(declaration.Modifiers, out var unsafeModifier)
            || ContainsUnsafeSyntax(declaration))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoUnnecessaryUnsafeModifier, unsafeModifier.GetLocation()));
    }

    /// <summary>Finds an unsafe modifier in a modifier list.</summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <param name="unsafeModifier">The unsafe modifier token.</param>
    /// <returns><see langword="true"/> when an unsafe modifier exists.</returns>
    private static bool TryGetUnsafeModifier(SyntaxTokenList modifiers, out SyntaxToken unsafeModifier)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.UnsafeKeyword))
            {
                unsafeModifier = modifiers[i];
                return true;
            }
        }

        unsafeModifier = default;
        return false;
    }

    /// <summary>Returns whether the declaration contains syntax that requires an unsafe context.</summary>
    /// <param name="declaration">The declaration.</param>
    /// <returns><see langword="true"/> when unsafe syntax is present.</returns>
    private static bool ContainsUnsafeSyntax(MemberDeclarationSyntax declaration)
    {
        foreach (var node in declaration.DescendantNodes())
        {
            if (RequiresUnsafeContext(node))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node is an unsafe-only syntax form.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> for pointer and unsafe statement forms.</returns>
    private static bool RequiresUnsafeContext(SyntaxNode node)
        => node.Kind() is SyntaxKind.PointerType
            or SyntaxKind.FunctionPointerType
            or SyntaxKind.FixedStatement
            or SyntaxKind.SizeOfExpression
            or SyntaxKind.PointerIndirectionExpression
            or SyntaxKind.PointerMemberAccessExpression
            or SyntaxKind.AddressOfExpression
            or SyntaxKind.UnsafeStatement;
}
