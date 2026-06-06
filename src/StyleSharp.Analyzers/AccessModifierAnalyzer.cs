// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires types and members to declare an explicit access modifier (SST1400).
/// Elements where an access modifier is not permitted — interface members, explicit
/// interface implementations, static constructors, partial methods — are skipped.
/// The implicit default (<c>internal</c> for top-level types, <c>private</c>
/// otherwise) is stashed for the fix.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AccessModifierAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property key holding the implicit access modifier to insert.</summary>
    internal const string ModifierKey = "Modifier";

    /// <summary>The declaration kinds the rule inspects.</summary>
    private static readonly ImmutableArray<SyntaxKind> HandledKinds = ImmutableArrays.Of(
        SyntaxKind.ClassDeclaration,
        SyntaxKind.StructDeclaration,
        SyntaxKind.InterfaceDeclaration,
        SyntaxKind.EnumDeclaration,
        SyntaxKind.RecordDeclaration,
        SyntaxKind.RecordStructDeclaration,
        SyntaxKind.DelegateDeclaration,
        SyntaxKind.FieldDeclaration,
        SyntaxKind.EventFieldDeclaration,
        SyntaxKind.EventDeclaration,
        SyntaxKind.MethodDeclaration,
        SyntaxKind.PropertyDeclaration,
        SyntaxKind.IndexerDeclaration,
        SyntaxKind.ConstructorDeclaration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.AccessModifierDeclared);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Reports a declaration that omits an access modifier where one is allowed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (HasAccessModifier(member.Modifiers) || !RequiresModifier(member))
        {
            return;
        }

        var token = MemberOrder.NameToken(member);
        var properties = ImmutableDictionary<string, string?>.Empty.Add(ModifierKey, DefaultModifier(member));
        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.AccessModifierDeclared, token.GetLocation(), properties, token.ValueText));
    }

    /// <summary>Returns whether the modifier list already declares accessibility (including <c>file</c>).</summary>
    /// <param name="modifiers">The declaration modifiers.</param>
    /// <returns><see langword="true"/> when an access modifier is present.</returns>
    private static bool HasAccessModifier(SyntaxTokenList modifiers)
        => ModifierListHelper.ContainsEither(modifiers, SyntaxKind.PublicKeyword, SyntaxKind.PrivateKeyword)
            || ModifierListHelper.ContainsEither(modifiers, SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.FileKeyword);

    /// <summary>Returns whether an access modifier may and should be declared on the member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when a modifier is required.</returns>
    private static bool RequiresModifier(MemberDeclarationSyntax member)
    {
        if (member.Parent is InterfaceDeclarationSyntax)
        {
            return false;
        }

        return member switch
        {
            ConstructorDeclarationSyntax constructor => !ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword),
            MethodDeclarationSyntax method => method.ExplicitInterfaceSpecifier is null && !ModifierListHelper.Contains(method.Modifiers, SyntaxKind.PartialKeyword),
            PropertyDeclarationSyntax property => property.ExplicitInterfaceSpecifier is null,
            EventDeclarationSyntax @event => @event.ExplicitInterfaceSpecifier is null,
            IndexerDeclarationSyntax indexer => indexer.ExplicitInterfaceSpecifier is null,
            _ => true,
        };
    }

    /// <summary>Returns the implicit default access modifier for a member's context.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><c>internal</c> for top-level types, otherwise <c>private</c>.</returns>
    private static string DefaultModifier(MemberDeclarationSyntax member)
        => member.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax ? "internal" : "private";
}
