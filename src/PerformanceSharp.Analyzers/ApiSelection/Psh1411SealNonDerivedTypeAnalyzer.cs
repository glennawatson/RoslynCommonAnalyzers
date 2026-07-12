// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a non-public class that nothing in the compilation derives from and that is not
/// <c>sealed</c> (PSH1411). A call on an unsealed class goes through a virtual dispatch unless the
/// JIT can prove no override exists; <c>sealed</c> states that up front, so its members devirtualize
/// and inline. Configured with <c>performancesharp.PSH1411.include_public</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The report is a symbol diagnostic, not a compilation one.</b> Roslyn categorizes every
/// diagnostic reported from a compilation end action as non-local: it cannot be recomputed for a
/// single document, so it never reaches a code fix. The rule therefore reports each class from the
/// symbol action that visits it, and answers "does anything derive from this?" out of an index of the
/// whole compilation that is built once, on first demand.
/// </para>
/// <para>
/// <b>The blocked set is built once, not per type.</b> Asking that question by walking every class for
/// every candidate would be quadratic. Instead the first candidate to ask forces a single pass that
/// records every source type's base type and every type a type parameter is constrained to; every
/// candidate after it is a hash lookup. A compilation with no candidate at all — every class already
/// sealed, static, abstract, a record, or externally visible — never builds the index, so it pays a
/// handful of flag reads per type and nothing else.
/// </para>
/// <para>
/// <b>Sealing has to still compile.</b> Three things stop it. A type another assembly can derive from
/// — so <c>public</c> and <c>protected</c> classes are off by default, and <c>internal</c> ones are
/// skipped outright when the assembly has an <c>[InternalsVisibleTo]</c>, because a friend assembly's
/// subclasses are invisible from here. A type named as a generic constraint, because a sealed class is
/// not a valid constraint (CS0701). And a class declaring a new <c>virtual</c> member (CS0549) or a
/// new <c>protected</c> one (CS0628), neither of which a sealed class may have.
/// </para>
/// <para>
/// Generated code is analyzed but never reported in: a source generator's partial class deriving from
/// an internal type is exactly the derivation that must not be missed.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1411SealNonDerivedTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the attribute that lets another assembly see internal types.</summary>
    private const string InternalsVisibleToAttributeName = "InternalsVisibleToAttribute";

    /// <summary>The reachability of a <c>private</c> type: only the containing type can see it.</summary>
    private const int PrivateReach = 0;

    /// <summary>The reachability of a <c>private protected</c> type: subclasses inside this assembly.</summary>
    private const int ProtectedAndInternalReach = 1;

    /// <summary>The reachability of an <c>internal</c> type: anything inside this assembly.</summary>
    private const int InternalReach = 2;

    /// <summary>The reachability of a <c>protected</c> type: subclasses in any assembly.</summary>
    private const int ProtectedReach = 3;

    /// <summary>The reachability of a <c>protected internal</c> type: this assembly, plus subclasses anywhere.</summary>
    private const int ProtectedOrInternalReach = 4;

    /// <summary>The reachability of a <c>public</c> type, and of any accessibility the rule does not know: anything, anywhere.</summary>
    private const int PublicReach = 5;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.SealNonDerivedType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Generated code is analyzed so a generated subclass still counts as a derivation, but the
        // driver keeps suppressing diagnostics whose location is in generated code.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Sets up the per-compilation index, then judges every named type against it.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var index = new SealCandidateIndex(context.Compilation, HasInternalsVisibleTo(context.Compilation.Assembly));
        context.RegisterSymbolAction(symbolContext => AnalyzeNamedType(symbolContext, index), SymbolKind.NamedType);
    }

    /// <summary>Returns whether the assembly lets another assembly see its internal types.</summary>
    /// <param name="assembly">The compilation's assembly.</param>
    /// <returns><see langword="true"/> when an <c>[InternalsVisibleTo]</c> is present.</returns>
    /// <remarks>
    /// Matched by name rather than by resolving the attribute type, so a compilation without the
    /// attribute pays nothing to find that out. A friend assembly may derive from an internal class,
    /// and that subclass is not in this compilation, so no internal class can be judged underived once
    /// one of these exists.
    /// </remarks>
    private static bool HasInternalsVisibleTo(IAssemblySymbol assembly)
    {
        var attributes = assembly.GetAttributes();
        for (var i = 0; i < attributes.Length; i++)
        {
            if (attributes[i].AttributeClass is { Name: InternalsVisibleToAttributeName })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports one class when nothing in the compilation derives from or constrains to it.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="index">The per-compilation index.</param>
    private static void AnalyzeNamedType(SymbolAnalysisContext context, SealCandidateIndex index)
    {
        var symbol = (INamedTypeSymbol)context.Symbol;

        // Cheapest first: the shape is a handful of flag reads, the accessibility a short walk up the
        // containers, and only a class that survives both pays for the member scan — and only one that
        // survives all three ever forces the whole-compilation index to be built.
        if (!IsSealableShape(symbol)
            || !IsReportableAccessibility(symbol, context, index)
            || DeclaresMemberSealingWouldReject(symbol)
            || index.IsBlocked(symbol, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.SealNonDerivedType,
            symbol.Locations[0],
            symbol.Name));
    }

    /// <summary>Returns whether a type is a class that could carry the <c>sealed</c> modifier at all.</summary>
    /// <param name="symbol">The declared type.</param>
    /// <returns><see langword="true"/> when only the derived set stands between it and being sealed.</returns>
    /// <remarks>
    /// A record has its own sealing rule (SST1800). A static class is already sealed and abstract in
    /// metadata, and an abstract class exists to be derived from.
    /// </remarks>
    private static bool IsSealableShape(INamedTypeSymbol symbol)
        => symbol is
        {
            TypeKind: TypeKind.Class,
            IsSealed: false,
            IsStatic: false,
            IsAbstract: false,
            IsRecord: false,
            IsImplicitlyDeclared: false,
            DeclaringSyntaxReferences.Length: > 0,
        };

    /// <summary>Returns whether a class declares a member a sealed class may not have.</summary>
    /// <param name="symbol">The declared type.</param>
    /// <returns><see langword="true"/> when adding <c>sealed</c> would not build cleanly.</returns>
    /// <remarks>
    /// A new <c>virtual</c> member in a sealed class is CS0549, and a new <c>protected</c> member is
    /// CS0628 — an error and a warning that a repository treating warnings as errors will not accept.
    /// An <c>override</c> is fine on both counts, so it is not a reason to stay quiet.
    /// </remarks>
    private static bool DeclaresMemberSealingWouldReject(INamedTypeSymbol symbol)
    {
        var members = symbol.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            var member = members[i];
            if (member.IsImplicitlyDeclared || member.IsOverride)
            {
                continue;
            }

            if (member.IsVirtual
                || member.IsAbstract
                || member.DeclaredAccessibility is Accessibility.Protected
                    or Accessibility.ProtectedOrInternal
                    or Accessibility.ProtectedAndInternal)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a class is one this compilation can see every possible subclass of.</summary>
    /// <param name="symbol">The declared type.</param>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="index">The per-compilation index.</param>
    /// <returns><see langword="true"/> when sealing it cannot break an assembly this build cannot see.</returns>
    private static bool IsReportableAccessibility(INamedTypeSymbol symbol, SymbolAnalysisContext context, SealCandidateIndex index)
    {
        // A file-local type is invisible outside its own file, whatever its declared accessibility says
        // and whatever friend assemblies exist.
        if (symbol.IsFileLocal)
        {
            return true;
        }

        switch (GetEffectiveAccessibility(symbol))
        {
            case Accessibility.Private:
            {
                return true;
            }

            case Accessibility.Internal:
            case Accessibility.ProtectedAndInternal:
            {
                return !index.AssemblyExposesInternals;
            }

            default:
            {
                return index.GetOptions(symbol, context).IncludePublic;
            }
        }
    }

    /// <summary>Gets the accessibility a type really has, once its containers are taken into account.</summary>
    /// <param name="symbol">The declared type.</param>
    /// <returns>The most restrictive accessibility on the path from the type to the namespace.</returns>
    /// <remarks>
    /// A <c>public</c> class nested in an <c>internal</c> one is not reachable from outside the
    /// assembly, so it is an internal type for this rule's purposes.
    /// </remarks>
    private static Accessibility GetEffectiveAccessibility(INamedTypeSymbol symbol)
    {
        var effective = Accessibility.Public;
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            var declared = current.DeclaredAccessibility;
            if (Rank(declared) < Rank(effective))
            {
                effective = declared;
            }
        }

        return effective;
    }

    /// <summary>Orders accessibilities from least to most reachable.</summary>
    /// <param name="accessibility">The declared accessibility.</param>
    /// <returns>The rank, where a lower number is less reachable.</returns>
    /// <remarks>
    /// An accessibility the rule does not know is ranked as the most reachable, so it lands in the
    /// opt-in bucket rather than being mistaken for something private.
    /// </remarks>
    private static int Rank(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Private => PrivateReach,
        Accessibility.ProtectedAndInternal => ProtectedAndInternalReach,
        Accessibility.Internal => InternalReach,
        Accessibility.Protected => ProtectedReach,
        Accessibility.ProtectedOrInternal => ProtectedOrInternalReach,
        _ => PublicReach,
    };

    /// <summary>The classes the compilation derives from, or constrains a type parameter to.</summary>
    /// <remarks>
    /// Built in one pass and then only read, so the readers need no synchronization once the finished
    /// set is published.
    /// </remarks>
    private sealed class BlockedTypes
    {
        /// <summary>The classes something derives from, or a type parameter is constrained to.</summary>
        private readonly HashSet<INamedTypeSymbol> _types = new(SymbolEqualityComparer.Default);

        /// <summary>The simple names a local function constrains a type parameter to, allocated on the first one.</summary>
        private HashSet<string>? _localFunctionConstraintNames;

        /// <summary>Records everything in the compilation that forbids sealing a class.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        /// <returns>The finished set.</returns>
        public static BlockedTypes Build(Compilation compilation, CancellationToken cancellationToken)
        {
            var blocked = new BlockedTypes();
            blocked.CollectDeclaredTypes(compilation.Assembly.GlobalNamespace, cancellationToken);
            blocked.CollectLocalFunctionConstraints(compilation, cancellationToken);
            return blocked;
        }

        /// <summary>Returns whether a class may not be sealed.</summary>
        /// <param name="type">The class definition.</param>
        /// <returns><see langword="true"/> when something derives from it or constrains to it.</returns>
        public bool Contains(INamedTypeSymbol type)
            => _types.Contains(type) || _localFunctionConstraintNames?.Contains(type.Name) == true;

        /// <summary>Records a local function's constraint targets while walking a tree.</summary>
        /// <param name="localFunction">The visited local function.</param>
        /// <param name="blocked">The set being built.</param>
        /// <returns>Always <see langword="true"/>, so the whole tree is walked.</returns>
        private static bool VisitLocalFunction(LocalFunctionStatementSyntax localFunction, ref BlockedTypes blocked)
        {
            var clauses = localFunction.ConstraintClauses;
            for (var i = 0; i < clauses.Count; i++)
            {
                var constraints = clauses[i].Constraints;
                for (var j = 0; j < constraints.Count; j++)
                {
                    if (constraints[j] is TypeConstraintSyntax typeConstraint && GetSimpleName(typeConstraint.Type) is { } name)
                    {
                        blocked.BlockName(name);
                    }
                }
            }

            return true;
        }

        /// <summary>Gets the rightmost identifier of a written type name.</summary>
        /// <param name="type">The constraint's type syntax.</param>
        /// <returns>The simple name, or <see langword="null"/> when the syntax names no type.</returns>
        private static string? GetSimpleName(TypeSyntax type) => type switch
        {
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => GetSimpleName(qualified.Right),
            AliasQualifiedNameSyntax alias => GetSimpleName(alias.Name),
            _ => null,
        };

        /// <summary>Records the base types and constraint targets of every class under one namespace.</summary>
        /// <param name="namespaceSymbol">The namespace to walk.</param>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        private void CollectDeclaredTypes(INamespaceSymbol namespaceSymbol, CancellationToken cancellationToken)
        {
            foreach (var child in namespaceSymbol.GetNamespaceMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectDeclaredTypes(child, cancellationToken);
            }

            var types = namespaceSymbol.GetTypeMembers();
            for (var i = 0; i < types.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectDeclaredTypes(types[i], cancellationToken);
            }
        }

        /// <summary>Records what one type declaration blocks, then descends into the types nested in it.</summary>
        /// <param name="type">The declared type.</param>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        private void CollectDeclaredTypes(INamedTypeSymbol type, CancellationToken cancellationToken)
        {
            RecordBaseType(type);
            RecordConstraints(type.TypeParameters);

            var members = type.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol { TypeParameters.Length: > 0 } method)
                {
                    RecordConstraints(method.TypeParameters);
                }
            }

            var nested = type.GetTypeMembers();
            for (var i = 0; i < nested.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectDeclaredTypes(nested[i], cancellationToken);
            }
        }

        /// <summary>Records the class a type declaration derives from, when that base could itself be a candidate.</summary>
        /// <param name="type">The declared type.</param>
        private void RecordBaseType(INamedTypeSymbol type)
        {
            // A struct's ValueType, an enum's Enum, a delegate's MulticastDelegate, and a class's object
            // are all special types, and an interface has no base at all. None of them can be sealed by
            // this rule, so the common case records nothing.
            if (type.BaseType is not { SpecialType: SpecialType.None } baseType)
            {
                return;
            }

            Block(baseType.OriginalDefinition);
        }

        /// <summary>Records the classes a declaration's type parameters are constrained to.</summary>
        /// <param name="typeParameters">The declaration's type parameters.</param>
        private void RecordConstraints(ImmutableArray<ITypeParameterSymbol> typeParameters)
        {
            for (var i = 0; i < typeParameters.Length; i++)
            {
                var constraints = typeParameters[i].ConstraintTypes;
                for (var j = 0; j < constraints.Length; j++)
                {
                    if (constraints[j] is INamedTypeSymbol constrained)
                    {
                        Block(constrained.OriginalDefinition);
                    }
                }
            }
        }

        /// <summary>Records the constraint targets of every local function in the compilation.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        /// <remarks>
        /// A local function is a member of no symbol the compilation exposes, so its type parameters —
        /// and the <c>where T : Widget</c> that forbids sealing <c>Widget</c> just as a method's would —
        /// are reachable only through syntax, and an analyzer may not spin up a semantic model of its
        /// own (RS1030) to bind them. The constraint is therefore matched on its written name: the worst
        /// case is that a class sharing a name with a constrained one keeps its unsealed status, which
        /// costs a suggestion rather than a broken build.
        /// </remarks>
        private void CollectLocalFunctionConstraints(Compilation compilation, CancellationToken cancellationToken)
        {
            var state = this;
            foreach (var tree in compilation.SyntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DescendantTraversalHelper.VisitDescendants<LocalFunctionStatementSyntax, BlockedTypes>(
                    tree.GetRoot(cancellationToken),
                    ref state,
                    VisitLocalFunction);
            }
        }

        /// <summary>Records a class that may not be sealed.</summary>
        /// <param name="type">The class definition.</param>
        private void Block(INamedTypeSymbol type)
        {
            if (type.TypeKind != TypeKind.Class || type.IsSealed || type.DeclaringSyntaxReferences.Length == 0)
            {
                return;
            }

            _types.Add(type);
        }

        /// <summary>Records the written name of a class a local function constrains a type parameter to.</summary>
        /// <param name="name">The constraint's simple name.</param>
        private void BlockName(string name)
        {
            _localFunctionConstraintNames ??= new HashSet<string>(StringComparer.Ordinal);
            _localFunctionConstraintNames.Add(name);
        }
    }

    /// <summary>The per-compilation state the symbol action judges each class against.</summary>
    private sealed class SealCandidateIndex
    {
        /// <summary>Guards the one-time build of the blocked set.</summary>
        private readonly object _gate = new();

        /// <summary>The compilation the blocked set is built from.</summary>
        private readonly Compilation _compilation;

        /// <summary>The per-tree settings cache.</summary>
        private readonly ConcurrentDictionary<SyntaxTree, SealNonDerivedTypeOptions> _optionsByTree = new();

        /// <summary>The blocked set, or <see langword="null"/> until the first candidate asks for it.</summary>
        private BlockedTypes? _blocked;

        /// <summary>Initializes a new instance of the <see cref="SealCandidateIndex"/> class.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <param name="assemblyExposesInternals">Whether the assembly exposes its internals to a friend.</param>
        public SealCandidateIndex(Compilation compilation, bool assemblyExposesInternals)
        {
            _compilation = compilation;
            AssemblyExposesInternals = assemblyExposesInternals;
        }

        /// <summary>Gets a value indicating whether the assembly exposes its internals to a friend.</summary>
        public bool AssemblyExposesInternals { get; }

        /// <summary>Returns whether something derives from, or constrains to, a class.</summary>
        /// <param name="type">The class definition.</param>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        /// <returns><see langword="true"/> when the class may not be sealed.</returns>
        /// <remarks>
        /// The first caller pays for the whole-compilation scan and every caller after it pays for a hash
        /// lookup, so the rule stays linear in the size of the compilation however many candidates it
        /// finds. A cancelled build stores nothing, so the next caller starts it again.
        /// </remarks>
        public bool IsBlocked(INamedTypeSymbol type, CancellationToken cancellationToken)
            => GetBlocked(cancellationToken).Contains(type);

        /// <summary>Reads the settings for a class's tree, parsing each tree's options at most once.</summary>
        /// <param name="symbol">The declared type.</param>
        /// <param name="context">The symbol analysis context.</param>
        /// <returns>The resolved settings.</returns>
        public SealNonDerivedTypeOptions GetOptions(INamedTypeSymbol symbol, SymbolAnalysisContext context)
        {
            if (symbol.Locations[0].SourceTree is not { } tree)
            {
                return default;
            }

            if (_optionsByTree.TryGetValue(tree, out var options))
            {
                return options;
            }

            options = SealNonDerivedTypeOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
            _optionsByTree.TryAdd(tree, options);
            return options;
        }

        /// <summary>Gets the blocked set, building it on the first call.</summary>
        /// <param name="cancellationToken">A token that cancels the build.</param>
        /// <returns>The finished set.</returns>
        private BlockedTypes GetBlocked(CancellationToken cancellationToken)
        {
            var blocked = Volatile.Read(ref _blocked);
            if (blocked is not null)
            {
                return blocked;
            }

            lock (_gate)
            {
                blocked = _blocked;
                if (blocked is not null)
                {
                    return blocked;
                }

                blocked = BlockedTypes.Build(_compilation, cancellationToken);
                Volatile.Write(ref _blocked, blocked);
                return blocked;
            }
        }
    }
}
