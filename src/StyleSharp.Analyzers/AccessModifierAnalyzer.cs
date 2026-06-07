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

    /// <summary>The cached diagnostic properties for top-level declarations that default to internal.</summary>
    private static readonly ImmutableDictionary<string, string?> InternalModifierProperties = ImmutableDictionary<string, string?>.Empty.Add(ModifierKey, "internal");

    /// <summary>The cached diagnostic properties for nested declarations that default to private.</summary>
    private static readonly ImmutableDictionary<string, string?> PrivateModifierProperties = ImmutableDictionary<string, string?>.Empty.Add(ModifierKey, "private");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.AccessModifierDeclared);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, HandledKinds);
    }

    /// <summary>Returns whether the modifier list already declares accessibility (including <c>file</c>).</summary>
    /// <param name="modifiers">The declaration modifiers.</param>
    /// <returns><see langword="true"/> when an access modifier is present.</returns>
    internal static bool HasAccessModifierFast(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.PublicKeyword
                or SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword
                or SyntaxKind.InternalKeyword
                or SyntaxKind.FileKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an access modifier may and should be declared on the member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when a modifier is required.</returns>
    internal static bool RequiresModifierFast(MemberDeclarationSyntax member)
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

    /// <summary>Returns whether the declaration is top-level and therefore defaults to <c>internal</c>.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> for compilation-unit and namespace children.</returns>
    internal static bool IsTopLevelDeclaration(MemberDeclarationSyntax member)
        => member.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;

    /// <summary>Returns the cached diagnostic properties for the member's implicit default access modifier.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The cached diagnostic properties dictionary.</returns>
    internal static ImmutableDictionary<string, string?> ModifierProperties(MemberDeclarationSyntax member)
        => IsTopLevelDeclaration(member) ? InternalModifierProperties : PrivateModifierProperties;

    /// <summary>Reports a declaration that omits an access modifier where one is allowed.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (HasAccessModifierFast(member.Modifiers) || !RequiresModifierFast(member))
        {
            return;
        }

        var token = MemberOrder.NameToken(member);
        var properties = ModifierProperties(member);
        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.AccessModifierDeclared, token.GetLocation(), properties, token.ValueText));
    }
}
