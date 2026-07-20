// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a visible instance field or property whose type is a raw native pointer — <c>IntPtr</c>,
/// <c>UIntPtr</c>, <c>nint</c>, or <c>nuint</c> (SST2328). Handing the bare handle out across the type
/// boundary lets a caller read it, overwrite it with a rogue value, or pass it to a native free, corrupting
/// or double-freeing the native memory the type owns. The fix is to keep the value in a <c>private</c> field
/// and expose the resource through a <c>SafeHandle</c>.
/// </summary>
/// <remarks>
/// A member is reported only when its declared accessibility is one the outside world can reach —
/// <c>public</c>, <c>protected</c>, or <c>protected internal</c> — and it is an instance member; an
/// <c>internal</c>, <c>private protected</c>, or <c>private</c> member keeps the handle under the assembly's
/// own control, and a <c>static</c> member is not part of an instance's surface. A member whose accessibility
/// a contract fixes is left alone: an <c>override</c> matches its base, and an explicit interface
/// implementation is private in any case. Indexers and compiler-synthesized members are not considered. Only
/// a concrete <c>class</c>, <c>struct</c>, or record — a type that can actually own the memory and hold a
/// <c>private</c> replacement — is examined; an interface, which has no author-written accessibility and no
/// field to hide, is skipped. The clean path is symbol-only and allocation-free: each member's kind, static
/// flag, accessibility, and special type are cheap checks, and no syntax is touched.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2328ExposedNativePointerAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ExposedNativePointer);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>Reports each visible instance field or property of a concrete type that exposes a raw native pointer.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Only a concrete class, struct, or record owns native memory and can hold a private replacement; an
        // interface has no author-written accessibility and no field to hide, and a static type no instance surface.
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || type.IsStatic)
        {
            return;
        }

        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (!ExposesNativePointer(member))
            {
                continue;
            }

            // A source field or property always carries the identifier's location; the switch never admits a
            // synthesized member, so there is no locationless case to guard against here.
            context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.ExposedNativePointer, member.Locations[0], member.Name));
        }
    }

    /// <summary>Returns whether a member hands a raw native pointer out across the type boundary.</summary>
    /// <param name="member">The declared member.</param>
    /// <returns><see langword="true"/> for a visible instance field or property whose type is a native pointer.</returns>
    private static bool ExposesNativePointer(ISymbol member)
    {
        // A synthesized member (a backing field, a record's copy machinery), a static member, and an override —
        // whose accessibility its base fixes — are none of them something the author can move behind a private field.
        if (member.IsImplicitlyDeclared || member.IsStatic || member.IsOverride || !IsExternallyReachable(member.DeclaredAccessibility))
        {
            return false;
        }

        return member switch
        {
            IFieldSymbol field => IsNativePointer(field.Type),

            // An indexer exposes a computed value, not a stored handle, so it is not a raw address to hide. An
            // explicit interface implementation needs no arm of its own: it is private, so the accessibility
            // gate above has already turned it away.
            IPropertySymbol { IsIndexer: false } property => IsNativePointer(property.Type),
            _ => false,
        };
    }

    /// <summary>Returns whether an accessibility admits a caller outside the declaring assembly.</summary>
    /// <param name="accessibility">The member's declared accessibility.</param>
    /// <returns><see langword="true"/> for <c>public</c>, <c>protected</c>, or <c>protected internal</c>.</returns>
    private static bool IsExternallyReachable(Accessibility accessibility)
        => accessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    /// <summary>Returns whether a type is a raw native pointer whose value a SafeHandle would replace.</summary>
    /// <param name="type">The member type.</param>
    /// <returns><see langword="true"/> for <c>IntPtr</c>/<c>nint</c> or <c>UIntPtr</c>/<c>nuint</c>.</returns>
    private static bool IsNativePointer(ITypeSymbol type)
        => type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr;
}
