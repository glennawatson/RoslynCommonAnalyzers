// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a non-static, non-sealed class whose every declared member is <c>abstract</c> and that holds no
/// state (SST2323) — an abstract class used where an interface is the better contract. Such a type asks its
/// derived types for exactly what an interface asks for, yet spends the single base type a class is allowed,
/// so the same members declared on an interface leave that slot free and let a type satisfy several
/// contracts at once.
/// </summary>
/// <remarks>
/// <para>
/// The rule runs as a symbol action, so a <c>partial</c> class is judged once, from all of its parts at
/// once — a member declared in another file counts, and the type is never reported twice. The filter is
/// four symbol flags before anything is enumerated: only an abstract, non-static, non-record class that
/// extends nothing but <c>object</c> reaches the member scan.
/// </para>
/// <para>
/// The type is reported only when every member it declares is a <see langword="public"/> abstract member
/// and there is at least one. A field, a hand-written constructor, an implemented member, a nested type, or
/// an abstract member that is not public is exactly what a base class is for and an interface is not, so any
/// one of them leaves the type alone. This is the opposite predicate to the maintainability rule that flags
/// an abstract class declaring <em>nothing</em> abstract, so the two never fire on the same type.
/// </para>
/// <para>
/// An interface can carry those members — member accessibility, default implementations, and static
/// abstract members among them — only from C# 8 onward. The whole rule is gated on that language version at
/// compilation start, so a project on an older version that could not act on the suggestion registers
/// nothing and pays nothing.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2323PreferInterfaceOverAbstractClassAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.PreferInterfaceOverAbstractClass);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only where the language can express the members on an interface.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// Below C# 8 an interface cannot carry the accessibility modifiers, default implementations, or static
    /// abstract members an abstract class may hold, so the "prefer an interface" suggestion could name a
    /// shape the language does not have. Gating here once means an older project registers no symbol action.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation is not CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp8 })
        {
            return;
        }

        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>Reports an all-abstract class that would read better as an interface.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <remarks>
    /// A record is excluded because the compiler writes its equality contract and an abstract record base is
    /// how a closed hierarchy is spelled. A static class is abstract and sealed in metadata, so the explicit
    /// non-static flag filters it out. A class that extends a real base class cannot become an interface,
    /// because an interface cannot inherit a class, so it is dropped before the members are looked at.
    /// </remarks>
    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsStatic: false, IsRecord: false } type)
        {
            return;
        }

        if (type.BaseType is { SpecialType: not SpecialType.System_Object })
        {
            return;
        }

        if (!EveryDeclaredMemberIsPublicAbstract(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.PreferInterfaceOverAbstractClass,
            type.Locations[0],
            type.Name));
    }

    /// <summary>Returns whether the type declares at least one abstract member and nothing an interface cannot carry.</summary>
    /// <param name="type">The abstract class.</param>
    /// <returns><see langword="true"/> when every declared member is a public abstract member and there is one.</returns>
    /// <remarks>
    /// The compiler-written parameterless constructor is not something the type declares and is skipped; a
    /// hand-written constructor is a real member and stops the type here. A nested type, a field, an
    /// implemented member, or an abstract member that is not public each means the type is more than a
    /// contract, so any one of them returns <see langword="false"/> without finishing the scan.
    /// </remarks>
    private static bool EveryDeclaredMemberIsPublicAbstract(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        var sawAbstractMember = false;
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.IsImplicitlyDeclared)
            {
                continue;
            }

            if (member is INamedTypeSymbol)
            {
                return false;
            }

            if (!member.IsAbstract || member.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }

            sawAbstractMember = true;
        }

        return sawAbstractMember;
    }
}
