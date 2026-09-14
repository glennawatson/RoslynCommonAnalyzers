// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports narrowing a value whose static type is an interface down to a concrete class that implements it
/// (SST2326). The three forms covered are an explicit cast <c>(Concrete)value</c>, an <c>value as Concrete</c>,
/// and an <c>value is Concrete</c> type test — with or without a declared variable. Each reaches past the
/// abstraction to a named implementation, so substituting a different implementation of the interface makes the
/// cast throw or the type test silently pick the wrong branch.
/// </summary>
/// <remarks>
/// <para>
/// Precision is kept high by binding only after a cheap syntactic dispatch on the node kind, then requiring the
/// operand's static type to be a genuine interface (<see cref="INamedTypeSymbol"/> with
/// <see cref="TypeKind.Interface"/>) and the target to be a non-abstract class that actually appears in the
/// operand interface's implementers — the target's <see cref="ITypeSymbol.AllInterfaces"/> must contain the
/// operand interface. A type parameter, <c>object</c>, or <c>dynamic</c> operand is not a named interface and is
/// skipped; an interface, <c>object</c>, struct, or abstract-class target is skipped; and a target that does not
/// implement the interface — an unrelated narrowing the compiler already rejects or leaves for a subclass — is
/// left alone.
/// </para>
/// <para>
/// A concrete type declared in the <b>same assembly</b> is not reported: narrowing to an implementation the author
/// owns is a closed, in-house set of implementations — effectively a discriminated union — not coupling to an
/// external implementation choice. Only a concrete type from another assembly is the coupling this rule warns
/// about. A specific external type can additionally be exempted through the editorconfig option
/// <c>stylesharp.SST2326.allowed_types</c> (a comma-separated list of fully-qualified metadata names, e.g.
/// <c>System.Collections.Generic.List`1</c>), which declares that narrowing to it is a sanctioned fast path.
/// </para>
/// <para>
/// Syntax rejects targets that cannot be concrete implementations before binding. Owned and allow-listed
/// targets are rejected before enumerating substituted interfaces. Allow-lists are parsed once per option
/// value, with metadata resolutions shared across the compilation; display strings are built only to report.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2326InterfaceToConcreteCastAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The editorconfig key naming concrete types that are a sanctioned narrowing and never reported.</summary>
    private const string AllowedTypesOptionKey = "stylesharp.SST2326.allowed_types";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.InterfaceToConcreteCast);

    /// <summary>The narrowing syntax kinds shared by every compilation registration.</summary>
    private static readonly SyntaxKind[] AnalyzedKinds =
    [
        SyntaxKind.CastExpression,
        SyntaxKind.AsExpression,
        SyntaxKind.IsExpression,
        SyntaxKind.IsPatternExpression,
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new AllowedTypes(compilation),
            Analyze,
            AnalyzedKinds);
    }

    /// <summary>Reports one narrowing of an interface reference to a concrete implementation type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The allow-list types resolved on demand for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, AllowedTypes types)
    {
        if (!TryGetOperandAndTarget(context.Node, out var operand, out var targetType)
            || !CanBeConcreteTarget(targetType))
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (semanticModel.GetTypeInfo(operand, cancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType
            || semanticModel.GetTypeInfo(targetType, cancellationToken).Type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } concreteType)
        {
            return;
        }

        // Reject owned implementations before constructing their substituted interface lists.
        if (SymbolEqualityComparer.Default.Equals(concreteType.ContainingAssembly, semanticModel.Compilation.Assembly)
            || IsAllowedType(context, concreteType, types)
            || !TypeRelations.Implements(concreteType, interfaceType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.InterfaceToConcreteCast,
            targetType.GetLocation(),
            interfaceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            concreteType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    /// <summary>Returns whether a concrete type is named in the file's allowed_types option.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="concreteType">The cross-assembly concrete target type.</param>
    /// <param name="types">The cache shared by all file-specific allow-lists in this compilation.</param>
    /// <returns>Whether the type's original definition matches an allow-list entry.</returns>
    private static bool IsAllowedType(in SyntaxNodeAnalysisContext context, INamedTypeSymbol concreteType, AllowedTypes types)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree);
        return options.TryGetValue(AllowedTypesOptionKey, out var value)
            && value.Length > 0
            && types.Contains(value, concreteType.OriginalDefinition);
    }

    /// <summary>Splits a narrowing node into the operand being narrowed and the target type syntax, on syntax alone.</summary>
    /// <param name="node">The cast, <c>as</c>, <c>is</c>, or <c>is</c>-pattern node.</param>
    /// <param name="operand">The expression whose static type is inspected.</param>
    /// <param name="targetType">The target type syntax the operand is narrowed to.</param>
    /// <returns><see langword="true"/> when the node is a narrowing shape this rule inspects.</returns>
    /// <remarks>
    /// A bare <c>value is Concrete</c> parses as an <see cref="BinaryExpressionSyntax"/> (<c>IsExpression</c>),
    /// not an <c>is</c> pattern; the pattern form is reached only by <c>value is Concrete name</c>, a declaration
    /// pattern. Any other pattern — <c>is not</c>, a constant, a recursive pattern — carries no single target type
    /// to narrow to and is rejected here without binding.
    /// </remarks>
    private static bool TryGetOperandAndTarget(SyntaxNode node, out ExpressionSyntax operand, out TypeSyntax targetType)
    {
        switch (node)
        {
            case CastExpressionSyntax cast:
            {
                operand = cast.Expression;
                targetType = cast.Type;
                return true;
            }

            case BinaryExpressionSyntax binary:
            {
                operand = binary.Left;
                targetType = (TypeSyntax)binary.Right;
                return true;
            }

            case IsPatternExpressionSyntax { Pattern: DeclarationPatternSyntax declaration } isPattern:
            {
                operand = isPattern.Expression;
                targetType = declaration.Type;
                return true;
            }

            default:
            {
                operand = null!;
                targetType = null!;
                return false;
            }
        }
    }

    /// <summary>Rejects target syntax that cannot denote a concrete implementing class.</summary>
    /// <param name="targetType">The narrowing target's syntax.</param>
    /// <returns>Whether semantic type checks are still necessary.</returns>
    private static bool CanBeConcreteTarget(TypeSyntax targetType) =>
        targetType is not (ArrayTypeSyntax or TupleTypeSyntax or PointerTypeSyntax or FunctionPointerTypeSyntax)
        && (targetType is not PredefinedTypeSyntax predefined || predefined.Keyword.IsKind(SyntaxKind.StringKeyword));

    /// <summary>Parses allow-lists on demand and shares resolved entries across the compilation.</summary>
    /// <param name="compilation">The compilation whose allow-list types are resolved.</param>
    private sealed class AllowedTypes(Compilation compilation)
    {
        /// <summary>Serializes parsing and metadata resolution on a cache miss.</summary>
        private readonly object _gate = new();

        /// <summary>The published lists keyed by their original, unsplit option strings.</summary>
        private ImmutableDictionary<string, ImmutableArray<INamedTypeSymbol>> _resolved = ImmutableDictionary<string, ImmutableArray<INamedTypeSymbol>>.Empty.WithComparers(StringComparer.Ordinal);

        /// <summary>Metadata results shared across lists, accessed only while holding the gate.</summary>
        private Dictionary<string, INamedTypeSymbol?>? _metadata;

        /// <summary>Checks a parsed allow-list without allocating entry substrings on cache hits.</summary>
        /// <param name="value">The file's complete allow-list option.</param>
        /// <param name="definition">The candidate type's original definition.</param>
        /// <returns>Whether the definition is one of the allowed types.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(string value, INamedTypeSymbol definition)
        {
            var entries = Volatile.Read(ref _resolved).TryGetValue(value, out var cached) ? cached : Resolve(value);
            for (var i = 0; i < entries.Length; i++)
            {
                if (SymbolEqualityComparer.Default.Equals(entries[i], definition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Parses each option once and resolves each distinct metadata name once.</summary>
        /// <param name="value">The file's complete allow-list option.</param>
        /// <returns>The resolved entries for this option.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ImmutableArray<INamedTypeSymbol> Resolve(string value)
        {
            lock (_gate)
            {
                if (_resolved.TryGetValue(value, out var cached))
                {
                    return cached;
                }

                var entries = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                var start = 0;
                while (start <= value.Length)
                {
                    var comma = value.IndexOf(',', start);
                    var end = comma < 0 ? value.Length : comma;
                    var segment = AnalyzerOptionReader.TrimSegment(value, start, end);
                    if (!segment.IsEmpty)
                    {
                        var entry = segment.Length == value.Length ? value : segment.ToString();
                        _metadata ??= new(StringComparer.Ordinal);
                        if (!_metadata.TryGetValue(entry, out var type))
                        {
                            type = compilation.GetTypeByMetadataName(entry);
                            _metadata.Add(entry, type);
                        }

                        if (type is not null)
                        {
                            entries.Add(type);
                        }
                    }

                    start = end + 1;
                }

                cached = entries.ToImmutable();
                Volatile.Write(ref _resolved, _resolved.Add(value, cached));
                return cached;
            }
        }
    }
}
