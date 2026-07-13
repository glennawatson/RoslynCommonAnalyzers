// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a modifier that names what the declaration already is (SST1491): the accessibility, abstractness
/// or virtualness an interface member has anyway, the <c>readonly</c> every member of a <c>readonly struct</c>
/// has anyway, and an <c>unsafe</c> nested inside an <c>unsafe</c> that already supplies the context.
/// </summary>
/// <remarks>
/// <para>
/// The bar is deliberately high: a modifier is reported only when deleting it leaves code that still
/// compiles and still means the same thing. Two modifiers that look redundant do not clear it.
/// <c>static</c> on a member of a static class cannot be removed — an instance member in a static class is
/// an error, so the keyword is required rather than restating a default. <c>private</c> on an interface
/// member is not the default either; interface members are public, so dropping it would widen the member.
/// </para>
/// <para>
/// A redundant <c>sealed</c> — on a member that is not an override, or on a member of a sealed type — is
/// reported by SST1419, and an <c>unsafe</c> on a declaration that contains no unsafe syntax at all is
/// reported by SST1455. Neither is repeated here: this rule only takes the <c>unsafe</c> that a genuinely
/// unsafe declaration inherits from an enclosing one.
/// </para>
/// <para>
/// Nothing in the clean path binds a symbol. Every judgment is made from the modifier list, the parent
/// declaration's own modifiers, and the parse options, so a member with no interesting modifier costs one
/// scan of a list that is almost always two tokens long.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1491RedundantModifierAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The first language version whose interface members may state an accessibility.</summary>
    private const int CSharp8 = (int)LanguageVersion.CSharp8;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RedundantModifier);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // An event written with add/remove accessors is an EventDeclaration; one written as a field —
        // 'event EventHandler Changed;', which is the only form an interface can declare — is an
        // EventFieldDeclaration. Both carry modifiers, so both are measured.
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.IndexerDeclaration,
            SyntaxKind.EventDeclaration,
            SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>Returns whether a declaration is inside a declaration that already supplies an unsafe context.</summary>
    /// <param name="declaration">The declaration carrying the <c>unsafe</c> modifier.</param>
    /// <returns><see langword="true"/> when an enclosing type or member is already unsafe.</returns>
    internal static bool IsInsideUnsafeContext(SyntaxNode declaration)
    {
        var node = declaration.Parent;
        while (node is not null)
        {
            if (node is MemberDeclarationSyntax member && ModifierListHelper.Contains(member.Modifiers, SyntaxKind.UnsafeKeyword))
            {
                return true;
            }

            node = node.Parent;
        }

        return false;
    }

    /// <summary>Reports every modifier on one member that restates what the declaration already is.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        var modifiers = member.Modifiers;
        for (var i = 0; i < modifiers.Count; i++)
        {
            var modifier = modifiers[i];
            if (IsCandidateKind(modifier.Kind()))
            {
                ReportIfRedundant(context, member, modifier, modifiers);
            }
        }
    }

    /// <summary>Returns whether a modifier kind is one this rule can ever report.</summary>
    /// <param name="kind">The modifier's syntax kind.</param>
    /// <returns><see langword="true"/> for the kinds the rule judges.</returns>
    private static bool IsCandidateKind(SyntaxKind kind) => kind is SyntaxKind.PublicKeyword
        or SyntaxKind.AbstractKeyword
        or SyntaxKind.VirtualKeyword
        or SyntaxKind.ReadOnlyKeyword
        or SyntaxKind.UnsafeKeyword;

    /// <summary>Reports one candidate modifier when its declaration already guarantees it.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="member">The member declaration.</param>
    /// <param name="modifier">The candidate modifier.</param>
    /// <param name="modifiers">The member's full modifier list.</param>
    private static void ReportIfRedundant(
        SyntaxNodeAnalysisContext context,
        MemberDeclarationSyntax member,
        SyntaxToken modifier,
        SyntaxTokenList modifiers)
    {
        var redundant = modifier.Kind() switch
        {
            SyntaxKind.UnsafeKeyword => IsRedundantUnsafe(member),
            SyntaxKind.ReadOnlyKeyword => IsRedundantReadOnly(member, modifiers),
            _ => IsRedundantOnInterfaceMember(member, modifier.Kind(), modifiers),
        };

        if (!redundant)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(MaintainabilityRules.RedundantModifier, modifier.GetLocation(), modifier.ValueText));
    }

    /// <summary>Returns whether an interface member's modifier restates an interface default.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="kind">The candidate modifier's kind.</param>
    /// <param name="modifiers">The member's full modifier list.</param>
    /// <returns><see langword="true"/> when the interface already guarantees the modifier.</returns>
    /// <remarks>
    /// Interface members are public, are abstract when they have no body, and are virtual when they do — but
    /// only while they are instance members. A static interface member is none of those things by default:
    /// <c>static abstract</c> and <c>static virtual</c> both say something a bare <c>static</c> does not, so
    /// a static member keeps every modifier it declares.
    /// </remarks>
    private static bool IsRedundantOnInterfaceMember(MemberDeclarationSyntax member, SyntaxKind kind, SyntaxTokenList modifiers)
    {
        if (member.Parent is not InterfaceDeclarationSyntax
            || member.SyntaxTree.Options is not CSharpParseOptions options
            || (int)options.LanguageVersion < CSharp8)
        {
            return false;
        }

        if (kind is SyntaxKind.PublicKeyword)
        {
            return true;
        }

        if (ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword))
        {
            return false;
        }

        var hasBody = HasBody(member);
        return kind is SyntaxKind.AbstractKeyword ? !hasBody : hasBody;
    }

    /// <summary>Returns whether a member's <c>readonly</c> restates the <c>readonly</c> of its struct.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="modifiers">The member's full modifier list.</param>
    /// <returns><see langword="true"/> when every instance member of the struct is readonly anyway.</returns>
    /// <remarks>
    /// Only instance members are covered. A static member cannot be readonly, and a field must be readonly —
    /// a readonly struct's instance fields have no choice — so neither is a member kind this rule registers.
    /// </remarks>
    private static bool IsRedundantReadOnly(MemberDeclarationSyntax member, SyntaxTokenList modifiers)
        => member.Parent is TypeDeclarationSyntax parent
            && parent.Kind() is SyntaxKind.StructDeclaration or SyntaxKind.RecordStructDeclaration
            && !ModifierListHelper.Contains(modifiers, SyntaxKind.StaticKeyword)
            && ModifierListHelper.Contains(parent.Modifiers, SyntaxKind.ReadOnlyKeyword);

    /// <summary>Returns whether a member's <c>unsafe</c> is already supplied by an enclosing declaration.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the modifier adds nothing the enclosing context did not.</returns>
    /// <remarks>
    /// A declaration with no unsafe syntax at all belongs to SST1455, which asks for the same deletion for a
    /// different reason. The scan that proves the member really is unsafe runs only for a member that is
    /// nested inside an unsafe one, which is rare enough to keep it off any hot path.
    /// </remarks>
    private static bool IsRedundantUnsafe(MemberDeclarationSyntax member)
        => IsInsideUnsafeContext(member) && ContainsUnsafeSyntax(member);

    /// <summary>Returns whether a member declares a body.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member supplies an implementation.</returns>
    private static bool HasBody(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
        PropertyDeclarationSyntax property => property.ExpressionBody is not null || HasAccessorBody(property.AccessorList),
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null || HasAccessorBody(indexer.AccessorList),
        EventDeclarationSyntax @event => HasAccessorBody(@event.AccessorList),
        _ => false,
    };

    /// <summary>Returns whether any accessor in a list declares a body.</summary>
    /// <param name="accessors">The accessor list, if the member declares one.</param>
    /// <returns><see langword="true"/> when an accessor supplies an implementation.</returns>
    private static bool HasAccessorBody(AccessorListSyntax? accessors)
    {
        if (accessors is null)
        {
            return false;
        }

        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Body is not null || list[i].ExpressionBody is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a declaration contains syntax that requires an unsafe context.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member really does need an unsafe context.</returns>
    private static bool ContainsUnsafeSyntax(MemberDeclarationSyntax member)
    {
        var found = false;
        DescendantTraversalHelper.VisitDescendants<SyntaxNode, bool>(
            member,
            ref found,
            static (SyntaxNode node, ref bool state) =>
            {
                if (!RequiresUnsafeContext(node))
                {
                    return true;
                }

                state = true;
                return false;
            });

        return found;
    }

    /// <summary>Returns whether a node is one of the syntax forms only an unsafe context allows.</summary>
    /// <param name="node">The syntax node.</param>
    /// <returns><see langword="true"/> for pointer, fixed, and address-of forms.</returns>
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
