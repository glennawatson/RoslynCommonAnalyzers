// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Decides whether a declaration is in scope for the documentation-coverage rules
/// (SST1600/SST1601/SST1602/SST1654), using only syntax (modifiers + containing
/// types) so no semantic model binding is needed. The decision honours the
/// <see cref="DocumentationCoverage"/> options, mirroring StyleCop's
/// <c>documentExposedElements</c> / <c>documentInternalElements</c> /
/// <c>documentPrivateElements</c> / <c>documentInterfaces</c> settings and their
/// effective-accessibility handling for nested types.
/// </summary>
internal static class DocumentationVisibility
{
    /// <summary>The coverage bucket a declaration with no containing type would occupy (exposed).</summary>
    private const int ExposedBucket = 2;

    /// <summary>The coverage bucket for internal-visible declarations.</summary>
    private const int InternalBucket = 1;

    /// <summary>The sentinel bucket for a declaration that carries no accessibility of its own (an extension block); a containing type always narrows it.</summary>
    private const int UnsetBucket = 3;

    /// <summary>Returns whether <paramref name="member"/> requires documentation under the configured coverage.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="coverage">The configured documentation-coverage scope.</param>
    /// <returns><see langword="true"/> when the member must be documented.</returns>
    public static bool NeedsDocumentation(SyntaxNode member, in DocumentationCoverage coverage)
    {
        var declared = DeclaredAccessibilityOf(member);

        // An interface declaration is governed solely by the interface mode and its own declared accessibility.
        if (member is InterfaceDeclarationSyntax)
        {
            return coverage.Interfaces switch
            {
                DocumentationInterfaceMode.All => true,
                DocumentationInterfaceMode.Exposed => declared != Accessibility.Internal,
                _ => false,
            };
        }

        // Members declared directly inside an interface follow the interface mode (public members)
        // or the private-elements toggle (explicitly non-public members).
        if (member.Parent is InterfaceDeclarationSyntax)
        {
            return declared is Accessibility.Public or Accessibility.NotApplicable
                ? coverage.Interfaces != DocumentationInterfaceMode.None
                : coverage.PrivateElements;
        }

        // With private documentation enabled every remaining element is in scope (StyleCop only carves
        // out private fields here, which this analyzer does not document).
        if (coverage.PrivateElements)
        {
            return true;
        }

        return NeedsByEffective(member, declared, coverage);
    }

    /// <summary>
    /// Returns whether a declaration whose visibility comes entirely from its containing types — such as a
    /// C# 14 extension block, which carries no accessibility of its own — requires documentation. The
    /// containing types' effective accessibility is mapped onto the configured coverage.
    /// </summary>
    /// <param name="node">The declaration whose containers decide its visibility.</param>
    /// <param name="coverage">The configured documentation-coverage scope.</param>
    /// <returns><see langword="true"/> when the declaration must be documented.</returns>
    public static bool NeedsContainerDocumentation(SyntaxNode node, in DocumentationCoverage coverage) =>
        coverage.PrivateElements || NeedsByEffective(node, Accessibility.NotApplicable, coverage);

    /// <summary>
    /// Returns whether a declaration is in scope once its own accessibility is narrowed by every containing
    /// type. The effective visibility collapses to three buckets — exposed, internal, hidden — which is all
    /// the exposed/internal coverage toggles distinguish; the most restrictive bucket along the chain wins.
    /// </summary>
    /// <param name="start">The declaration node.</param>
    /// <param name="own">The declaration's own declared accessibility.</param>
    /// <param name="coverage">The configured documentation-coverage scope.</param>
    /// <returns><see langword="true"/> when the effective visibility is in scope.</returns>
    private static bool NeedsByEffective(SyntaxNode start, Accessibility own, in DocumentationCoverage coverage)
    {
        var bucket = Bucket(own);
        for (var parent = start.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case BaseTypeDeclarationSyntax type:
                {
                    var container = Bucket(DeclaredAccessibilityOf(type));
                    if (container < bucket)
                    {
                        bucket = container;
                    }

                    break;
                }

                case BaseNamespaceDeclarationSyntax:
                case CompilationUnitSyntax:
                    return BucketInScope(bucket, coverage);
            }
        }

        return BucketInScope(bucket, coverage);
    }

    /// <summary>Maps a coverage bucket onto the exposed / internal toggles (hidden and unset are never in scope).</summary>
    /// <param name="bucket">The coverage bucket.</param>
    /// <param name="coverage">The configured documentation-coverage scope.</param>
    /// <returns><see langword="true"/> when the bucket is in scope.</returns>
    private static bool BucketInScope(int bucket, in DocumentationCoverage coverage) => bucket switch
    {
        ExposedBucket => coverage.ExposedElements,
        InternalBucket => coverage.InternalElements,
        _ => false,
    };

    /// <summary>Collapses an accessibility into its coverage bucket.</summary>
    /// <param name="accessibility">The accessibility.</param>
    /// <returns>The bucket: exposed, internal, hidden (0), or unset (for <see cref="Accessibility.NotApplicable"/>).</returns>
    private static int Bucket(Accessibility accessibility) => accessibility switch
    {
        Accessibility.NotApplicable => UnsetBucket,
        Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal => ExposedBucket,
        Accessibility.Internal or Accessibility.ProtectedAndInternal => InternalBucket,
        _ => 0,
    };

    /// <summary>Computes the declared accessibility of a declaration from its modifiers and the language default for its context.</summary>
    /// <param name="node">The declaration node.</param>
    /// <returns>The declared accessibility (<see cref="Accessibility.NotApplicable"/> for nodes that carry none).</returns>
    private static Accessibility DeclaredAccessibilityOf(SyntaxNode node)
    {
        if (node is EnumMemberDeclarationSyntax)
        {
            return Accessibility.Public;
        }

        return node is MemberDeclarationSyntax declaration
            ? ExplicitAccessibility(declaration.Modifiers) ?? DefaultAccessibility(node)
            : Accessibility.NotApplicable;
    }

    /// <summary>Reads the accessibility stated by explicit modifiers, or <see langword="null"/> when none are present.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns>The explicit accessibility, or <see langword="null"/>.</returns>
    private static Accessibility? ExplicitAccessibility(SyntaxTokenList modifiers)
    {
        if (ModifierListHelper.Contains(modifiers, SyntaxKind.PublicKeyword))
        {
            return Accessibility.Public;
        }

        var hasProtected = ModifierListHelper.Contains(modifiers, SyntaxKind.ProtectedKeyword);
        var hasInternal = ModifierListHelper.Contains(modifiers, SyntaxKind.InternalKeyword);
        var hasPrivate = ModifierListHelper.Contains(modifiers, SyntaxKind.PrivateKeyword);

        if (hasProtected && hasInternal)
        {
            return Accessibility.ProtectedOrInternal;
        }

        if (hasProtected && hasPrivate)
        {
            return Accessibility.ProtectedAndInternal;
        }

        if (hasProtected)
        {
            return Accessibility.Protected;
        }

        if (hasInternal)
        {
            return Accessibility.Internal;
        }

        return hasPrivate ? Accessibility.Private : null;
    }

    /// <summary>Returns the implicit accessibility for a declaration with no explicit modifier, based on its container.</summary>
    /// <param name="node">The declaration node.</param>
    /// <returns>The language-default accessibility for the context.</returns>
    private static Accessibility DefaultAccessibility(SyntaxNode node) => node.Parent switch
    {
        InterfaceDeclarationSyntax => Accessibility.Public,
        BaseNamespaceDeclarationSyntax or CompilationUnitSyntax or null => Accessibility.Internal,
        _ => Accessibility.Private,
    };
}
