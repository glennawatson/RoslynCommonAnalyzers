// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires types and members to use the .NET PascalCase convention (SST1300):
/// classes, structs, records, enums, delegates, methods, properties, events, and
/// enum members. Overrides and explicit interface implementations are skipped
/// because their names are dictated elsewhere. Interfaces are handled by SST1302.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ElementNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.ElementPascalCase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration,
            SyntaxKind.EnumMemberDeclaration);
    }

    /// <summary>Dispatches a declaration to the PascalCase check, extracting its identifier and skipping inherited members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        switch (context.Node)
        {
            case EventFieldDeclarationSyntax eventField:
            {
                foreach (var variable in eventField.Declaration.Variables)
                {
                    CheckPascalCase(context, variable.Identifier);
                }

                break;
            }

            case MethodDeclarationSyntax method when !IsInherited(method.Modifiers, method.ExplicitInterfaceSpecifier):
            {
                CheckPascalCase(context, method.Identifier);
                break;
            }

            case PropertyDeclarationSyntax property when !IsInherited(property.Modifiers, property.ExplicitInterfaceSpecifier):
            {
                CheckPascalCase(context, property.Identifier);
                break;
            }

            case EventDeclarationSyntax @event when !IsInherited(@event.Modifiers, @event.ExplicitInterfaceSpecifier):
            {
                CheckPascalCase(context, @event.Identifier);
                break;
            }

            case TypeDeclarationSyntax type:
            {
                CheckPascalCase(context, type.Identifier);
                break;
            }

            case EnumDeclarationSyntax @enum:
            {
                CheckPascalCase(context, @enum.Identifier);
                break;
            }

            case DelegateDeclarationSyntax @delegate:
            {
                CheckPascalCase(context, @delegate.Identifier);
                break;
            }

            case EnumMemberDeclarationSyntax enumMember:
            {
                CheckPascalCase(context, enumMember.Identifier);
                break;
            }
        }
    }

    /// <summary>Reports SST1300 when <paramref name="identifier"/> does not begin with an upper-case letter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="identifier">The identifier token to check.</param>
    private static void CheckPascalCase(SyntaxNodeAnalysisContext context, SyntaxToken identifier)
    {
        var name = identifier.ValueText;
        if (NamingHelper.IsAllUnderscores(name) || NamingHelper.BeginsWithUpperCase(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.ElementPascalCase, identifier, NamingHelper.SuggestPascalCase(name));
    }

    /// <summary>Returns whether a member is an override or explicit interface implementation (its name is dictated elsewhere).</summary>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <param name="explicitInterface">The member's explicit interface specifier, if any.</param>
    /// <returns><see langword="true"/> when the member should be skipped.</returns>
    private static bool IsInherited(SyntaxTokenList modifiers, ExplicitInterfaceSpecifierSyntax? explicitInterface)
        => explicitInterface is not null || ModifierListHelper.Contains(modifiers, SyntaxKind.OverrideKeyword);
}
