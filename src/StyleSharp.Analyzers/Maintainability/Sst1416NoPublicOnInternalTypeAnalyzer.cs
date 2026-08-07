// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>public</c> member declared in a type that is not externally visible (SST1416,
/// opt-in). The effective accessibility of such a member is capped at internal, so the
/// <c>public</c> modifier is misleading. A member whose accessibility is not the containing
/// type's alone to choose is not reported: an <c>override</c> takes it from the member it
/// overrides, a <c>virtual</c> or <c>abstract</c> member gives it to every override, an
/// interface implementation must stay public, and an operator must be public. The common
/// case — an effectively public type — is rejected by a cheap, purely syntactic check.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1416NoPublicOnInternalTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.NoPublicOnInternalType);

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
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Reports SST1416 for each misleading public member of a non-public type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var type = (TypeDeclarationSyntax)context.Node;
        if (IsEffectivelyPublic(type))
        {
            return;
        }

        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            CheckMember(context, members[i]);
        }
    }

    /// <summary>Reports a member when its <c>public</c> modifier is misleading.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The member declaration.</param>
    private static void CheckMember(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
    {
        if (member is OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax or BaseTypeDeclarationSyntax
            || SharesAccessibilityWithAnotherType(member)
            || PublicModifier(member) is not { } publicToken
            || ImplementsInterfaceMember(member, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoPublicOnInternalType, publicToken.GetLocation(), MemberOrder.NameToken(member).ValueText));
    }

    /// <summary>Returns whether a member's accessibility is fixed by, or fixes, another type's member.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> for an <c>override</c>, <c>virtual</c>, or <c>abstract</c> member.</returns>
    /// <remarks>
    /// An <c>override</c> takes its accessibility from the member it overrides — narrowing
    /// <c>public override bool Equals(object)</c> to <c>internal</c> does not compile. A <c>virtual</c> or
    /// <c>abstract</c> member is the other end of that same contract: demoting it strands every override,
    /// which the containing type cannot see. Neither is the containing type's to choose alone.
    /// </remarks>
    private static bool SharesAccessibilityWithAnotherType(MemberDeclarationSyntax member)
        => ModifierListHelper.Contains(member.Modifiers, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(member.Modifiers, SyntaxKind.VirtualKeyword)
            || ModifierListHelper.Contains(member.Modifiers, SyntaxKind.AbstractKeyword);

    /// <summary>Returns whether a type and every enclosing type are declared <c>public</c>.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns><see langword="true"/> when the type is reachable from outside the assembly.</returns>
    private static bool IsEffectivelyPublic(TypeDeclarationSyntax type)
    {
        for (SyntaxNode? node = type; node is BaseTypeDeclarationSyntax declaration; node = node.Parent)
        {
            if (!ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PublicKeyword))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the <c>public</c> modifier token of a member, or null when it has none.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The public modifier token, or <see langword="null"/>.</returns>
    private static SyntaxToken? PublicModifier(MemberDeclarationSyntax member)
    {
        var modifiers = member.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.PublicKeyword))
            {
                return modifiers[i];
            }
        }

        return null;
    }

    /// <summary>Returns whether a member implements an interface member (and so must stay public).</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the member implements an interface member.</returns>
    private static bool ImplementsInterfaceMember(MemberDeclarationSyntax member, SemanticModel model, CancellationToken cancellationToken)
    {
        if (member is not (MethodDeclarationSyntax or PropertyDeclarationSyntax or EventDeclarationSyntax or IndexerDeclarationSyntax)
            || model.GetDeclaredSymbol(member, cancellationToken) is not { ContainingType: { } containingType } symbol)
        {
            return false;
        }

        foreach (var @interface in containingType.AllInterfaces)
        {
            foreach (var interfaceMember in @interface.GetMembers())
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(interfaceMember), symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
