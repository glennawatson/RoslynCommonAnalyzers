// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a field-like event that nothing in the compilation ever raises (SST2407). Subscribers attach a
/// handler and wait for a call that never comes, and there is nothing at the point of failure to look at —
/// the bug is the absence of code.
/// </summary>
/// <remarks>
/// <para>
/// The diagnostic is reported from a symbol action, on the event's own declaration, so it behaves like any
/// other local diagnostic: it appears live in the editor and it points at a line the author can act on. The
/// whole-compilation knowledge it needs is gathered separately, by an index built once per compilation and
/// shared by every event in it — a compilation-end action would have produced a diagnostic no editor
/// refreshes and no fix can attach to.
/// </para>
/// <para>
/// The index errs towards silence. It records every name in the compilation that is used as a value — that
/// is, everywhere except the left of a <c>+=</c> or a <c>-=</c>, which is subscription rather than raising —
/// and an event is reported only when its name appears nowhere in it. So an event raised through a copy
/// (<c>var handler = Changed; handler?.Invoke(…)</c>), or reached in any other way that still spells its
/// name, is not reported. Sharing a name with an unrelated member elsewhere in the compilation also silences
/// it. That is the trade this rule makes: it would rather miss a never-raised event than accuse one that is
/// raised.
/// </para>
/// <para>
/// Events that cannot be raised where they are declared are excluded outright: an <c>abstract</c> or
/// <c>extern</c> event, an event in an interface, an <c>override</c>, an event implementing an interface
/// member, and a custom event with <c>add</c>/<c>remove</c> accessors — whose raise goes through whatever
/// backing store the accessors chose, under a name of its own. An event whose backing field carries
/// attributes (<c>[field: …]</c>) is left alone for the same reason.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2407EventNeverRaisedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.EventNeverRaised);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static start =>
        {
            var index = new RaisedNameIndex(start.Compilation);
            start.RegisterSymbolAction(symbolContext => AnalyzeEvent(symbolContext, index), SymbolKind.Event);
        });
    }

    /// <summary>Analyzes one event declaration.</summary>
    /// <param name="context">The symbol context.</param>
    /// <param name="index">The compilation's index of names used as values.</param>
    private static void AnalyzeEvent(SymbolAnalysisContext context, RaisedNameIndex index)
    {
        var symbol = (IEventSymbol)context.Symbol;
        if (!IsRaisableWhereDeclared(symbol) || !IsFieldLike(symbol, context.CancellationToken))
        {
            return;
        }

        if (index.Contains(symbol.Name, context.CancellationToken))
        {
            return;
        }

        var locations = symbol.Locations;
        if (locations.Length == 0 || !locations[0].IsInSource)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.EventNeverRaised, locations[0], symbol.Name));
    }

    /// <summary>Returns whether an event is one its own declaration is supposed to raise.</summary>
    /// <param name="symbol">The event.</param>
    /// <returns><see langword="true"/> when the declaring type owns the raising of it.</returns>
    private static bool IsRaisableWhereDeclared(IEventSymbol symbol)
    {
        if (symbol.IsAbstract
            || symbol.IsExtern
            || symbol.IsOverride
            || symbol.IsImplicitlyDeclared
            || symbol.ExplicitInterfaceImplementations.Length > 0
            || symbol.ContainingType is not { } containingType
            || containingType.TypeKind == TypeKind.Interface)
        {
            return false;
        }

        return !ImplementsInterfaceEvent(containingType, symbol);
    }

    /// <summary>Returns whether an event implements one an interface declares.</summary>
    /// <param name="containingType">The declaring type.</param>
    /// <param name="symbol">The event.</param>
    /// <returns><see langword="true"/> when an interface, not this type, decides the event exists.</returns>
    private static bool ImplementsInterfaceEvent(INamedTypeSymbol containingType, IEventSymbol symbol)
    {
        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(symbol.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (candidates[j] is IEventSymbol
                    && SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether an event is declared as a field, with no accessors and no field attributes.</summary>
    /// <param name="symbol">The event.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when raising the event means invoking a delegate of the same name.</returns>
    private static bool IsFieldLike(IEventSymbol symbol, CancellationToken cancellationToken)
    {
        var references = symbol.DeclaringSyntaxReferences;
        if (references.Length != 1)
        {
            return false;
        }

        return references[0].GetSyntax(cancellationToken) is VariableDeclaratorSyntax { Parent.Parent: EventFieldDeclarationSyntax declaration }
            && !HasFieldTargetedAttribute(declaration);
    }

    /// <summary>Returns whether an event-field declaration puts attributes on the backing field.</summary>
    /// <param name="declaration">The event-field declaration.</param>
    /// <returns><see langword="true"/> when a <c>[field: …]</c> list is present.</returns>
    private static bool HasFieldTargetedAttribute(EventFieldDeclarationSyntax declaration)
    {
        var lists = declaration.AttributeLists;
        for (var i = 0; i < lists.Count; i++)
        {
            if (lists[i].Target?.Identifier.IsKind(SyntaxKind.FieldKeyword) == true)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The names the compilation uses as values, built once and shared by every event analyzed in it.
    /// </summary>
    /// <remarks>
    /// Deliberately syntactic. Binding every name in the compilation to decide whether it is <em>this</em>
    /// event would cost far more than the rule is worth, and the answer would only ever make the rule report
    /// more — which is the direction it does not want to be wrong in.
    /// </remarks>
    private sealed class RaisedNameIndex
    {
        /// <summary>Synchronizes the one-off build.</summary>
        private readonly object _gate = new();

        /// <summary>The compilation whose trees are indexed.</summary>
        private readonly Compilation _compilation;

        /// <summary>The names used as values, once built.</summary>
        private volatile HashSet<string>? _names;

        /// <summary>Initializes a new instance of the <see cref="RaisedNameIndex"/> class.</summary>
        /// <param name="compilation">The compilation to index.</param>
        public RaisedNameIndex(Compilation compilation) => _compilation = compilation;

        /// <summary>Returns whether a name is used as a value anywhere in the compilation.</summary>
        /// <param name="name">The event's name.</param>
        /// <param name="cancellationToken">A token that cancels analysis.</param>
        /// <returns><see langword="true"/> when something might raise it.</returns>
        public bool Contains(string name, CancellationToken cancellationToken) => Build(cancellationToken).Contains(name);

        /// <summary>Records one name that is used as a value.</summary>
        /// <param name="identifier">The identifier being visited.</param>
        /// <param name="names">The set being built.</param>
        /// <returns><see langword="true"/>, so the whole tree is indexed.</returns>
        private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref HashSet<string> names)
        {
            if (IsSubscriptionTarget(identifier))
            {
                return true;
            }

            names.Add(identifier.Identifier.ValueText);
            return true;
        }

        /// <summary>Returns whether a name is the left-hand side of an event subscription.</summary>
        /// <param name="identifier">The identifier.</param>
        /// <returns><see langword="true"/> for the <c>E</c> in <c>E += h</c> and <c>obj.E -= h</c>.</returns>
        /// <remarks>Subscribing is the one thing a caller outside the declaring type is allowed to do, and it is not raising.</remarks>
        private static bool IsSubscriptionTarget(IdentifierNameSyntax identifier)
        {
            var target = identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier
                ? (ExpressionSyntax)access
                : identifier;

            return target.Parent is AssignmentExpressionSyntax assignment
                && assignment.Left == target
                && (assignment.IsKind(SyntaxKind.AddAssignmentExpression) || assignment.IsKind(SyntaxKind.SubtractAssignmentExpression));
        }

        /// <summary>Builds the index, at most once.</summary>
        /// <param name="cancellationToken">A token that cancels analysis.</param>
        /// <returns>The names used as values.</returns>
        private HashSet<string> Build(CancellationToken cancellationToken)
        {
            if (_names is { } built)
            {
                return built;
            }

            lock (_gate)
            {
                if (_names is { } raced)
                {
                    return raced;
                }

                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var tree in _compilation.SyntaxTrees)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HashSet<string>>(
                        tree.GetRoot(cancellationToken),
                        ref names,
                        VisitIdentifier);
                }

                _names = names;
                return names;
            }
        }
    }
}
