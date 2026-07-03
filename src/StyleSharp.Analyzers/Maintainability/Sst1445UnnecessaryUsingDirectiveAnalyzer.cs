// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags using directives nothing in the file resolves through (SST1445). The scan walks the
/// file's simple names once, marking each directive used the moment a bound symbol resolves
/// through it, and stops as soon as every directive is accounted for — a file that uses all of
/// its usings never pays for a full walk. Extension members, query clauses, foreach enumerators,
/// deconstructions, collection-initializer adds, and awaiter lookups are covered by a fallback
/// pass, and XML doc crefs by a final pass, each run only while a directive is still unaccounted
/// for. Global usings are never reported, and whenever usage cannot be proven either way the
/// directive counts as used, so the rule under-reports instead of suggesting a removal that
/// would break the build.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1445UnnecessaryUsingDirectiveAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.UnnecessaryUsingDirective);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSemanticModelAction(AnalyzeSemanticModel);
    }

    /// <summary>Runs the staged usage scan over one file and reports the leftover directives.</summary>
    /// <param name="context">The semantic model analysis context.</param>
    private static void AnalyzeSemanticModel(SemanticModelAnalysisContext context)
    {
        if (context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
        {
            return;
        }

        var tracker = UsageTracker.Create(root, context.SemanticModel, context.CancellationToken);
        if (tracker is null)
        {
            return;
        }

        new SimpleNameWalker(tracker).Visit(root);
        if (tracker.Remaining > 0)
        {
            new FallbackWalker(tracker).Visit(root);
            ScanDocumentationComments(root, tracker);
        }

        tracker.Report(context);
    }

    /// <summary>Marks directives used only from XML documentation crefs (rare-path pass).</summary>
    /// <param name="root">The compilation unit.</param>
    /// <param name="tracker">The usage tracker.</param>
    private static void ScanDocumentationComments(CompilationUnitSyntax root, UsageTracker tracker)
    {
        // HasStructuredTrivia is a cached flag; when the file has no doc comments (or other
        // structured trivia) the whole pass is free instead of a token walk.
        if (!root.HasStructuredTrivia)
        {
            return;
        }

        var walker = new SimpleNameWalker(tracker);
        DescendantTraversalHelper.VisitDescendantTokens(root, ref walker, static (in SyntaxToken token, ref SimpleNameWalker state) => VisitDocumentationTrivia(token, state));
    }

    /// <summary>Scans one token's leading doc-comment structures for cref usages.</summary>
    /// <param name="token">The current descendant token.</param>
    /// <param name="walker">The shared cref name walker.</param>
    /// <returns><see langword="false"/> once every directive is accounted for.</returns>
    private static bool VisitDocumentationTrivia(in SyntaxToken token, SimpleNameWalker walker)
    {
        if (token.HasStructuredTrivia && token.HasLeadingTrivia)
        {
            foreach (var trivia in token.LeadingTrivia)
            {
                if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    walker.Visit(trivia.GetStructure());
                }
            }
        }

        return walker.HasWork;
    }

    /// <summary>
    /// Tracks the file's non-global using directives and marks them used as bound symbols resolve
    /// through them. All marking is deliberately one-directional: over-marking only costs a missed
    /// report, never a false one.
    /// </summary>
    private sealed class UsageTracker
    {
        /// <summary>The tracked directives.</summary>
        private readonly Entry[] _entries;

        /// <summary>The per-entry used flags, parallel to <see cref="_entries"/>.</summary>
        private readonly bool[] _used;

        /// <summary>The number of tracked alias directives not yet marked used.</summary>
        private int _aliasRemaining;

        /// <summary>Initializes a new instance of the <see cref="UsageTracker"/> class.</summary>
        /// <param name="entries">The tracked directives.</param>
        /// <param name="model">The file's semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        private UsageTracker(Entry[] entries, SemanticModel model, CancellationToken cancellationToken)
        {
            _entries = entries;
            _used = new bool[entries.Length];
            Model = model;
            CancellationToken = cancellationToken;
            Remaining = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                if (entries[i].AliasName is not null)
                {
                    _aliasRemaining++;
                }
            }
        }

        /// <summary>Gets the number of directives not yet marked used.</summary>
        public int Remaining { get; private set; }

        /// <summary>Gets the file's semantic model.</summary>
        public SemanticModel Model { get; }

        /// <summary>Gets the analysis cancellation token.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Collects and binds the file's non-global using directives.</summary>
        /// <param name="root">The compilation unit.</param>
        /// <param name="model">The file's semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <returns>The tracker, or <see langword="null"/> when the file has nothing to track.</returns>
        public static UsageTracker? Create(CompilationUnitSyntax root, SemanticModel model, CancellationToken cancellationToken)
        {
            List<Entry>? entries = null;
            CollectUsings(root.Usings, model, cancellationToken, ref entries);
            CollectNamespaceUsings(root.Members, model, cancellationToken, ref entries);
            return entries is { Count: > 0 }
                ? new UsageTracker([.. entries], model, cancellationToken)
                : null;
        }

        /// <summary>Marks the alias directive a resolved alias symbol declares.</summary>
        /// <param name="alias">The resolved alias symbol.</param>
        public void MarkAlias(IAliasSymbol alias)
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                if (_used[i] || _entries[i].AliasName is null)
                {
                    continue;
                }

                if (IsDeclaredBy(alias, _entries[i].Directive))
                {
                    MarkUsed(i);
                    return;
                }
            }
        }

        /// <summary>Marks the namespace directives whose target contains the resolved symbol.</summary>
        /// <param name="containingNamespace">The resolved symbol's containing namespace.</param>
        public void MarkNamespace(INamespaceSymbol? containingNamespace)
        {
            if (containingNamespace is not { IsGlobalNamespace: false })
            {
                return;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (!_used[i] && _entries[i] is { AliasName: null, IsStatic: false, Target: INamespaceSymbol target } && NamespaceMatches(target, containingNamespace))
                {
                    MarkUsed(i);
                }
            }
        }

        /// <summary>Marks the using-static directives whose target type contains the resolved symbol.</summary>
        /// <param name="containingType">The resolved symbol's containing type.</param>
        public void MarkStaticType(INamedTypeSymbol? containingType)
        {
            if (containingType is null)
            {
                return;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (!_used[i] && _entries[i].IsStatic && SymbolEqualityComparer.Default.Equals(_entries[i].Target, containingType))
                {
                    MarkUsed(i);
                }
            }
        }

        /// <summary>Marks every directive still unaccounted for as used (conservative bail-out).</summary>
        public void MarkAllRemaining()
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                if (!_used[i])
                {
                    MarkUsed(i);
                }
            }
        }

        /// <summary>Returns whether a simple name should attempt alias resolution.</summary>
        /// <param name="name">The identifier text.</param>
        /// <returns><see langword="true"/> when an unused alias directive has that name.</returns>
        public bool MatchesUnusedAlias(string name)
        {
            if (_aliasRemaining == 0)
            {
                return false;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (!_used[i] && _entries[i].AliasName == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Marks the directives a symbol bound from a simple name can resolve through.</summary>
        /// <param name="symbol">The bound symbol.</param>
        public void MarkSimpleNameSymbol(ISymbol symbol)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Namespace:
                {
                    MarkNamespace(((INamespaceSymbol)symbol).ContainingNamespace);
                    break;
                }

                case SymbolKind.NamedType:
                {
                    MarkNamespace(symbol.ContainingNamespace);
                    MarkStaticType(symbol.ContainingType);
                    break;
                }

                case SymbolKind.Method:
                {
                    MarkMethodSymbol((IMethodSymbol)symbol);
                    break;
                }

                case SymbolKind.Field:
                case SymbolKind.Property:
                case SymbolKind.Event:
                {
                    MarkStaticType(symbol.ContainingType);
                    break;
                }
            }
        }

        /// <summary>Marks the directives a member-access binding can resolve through (extension members).</summary>
        /// <param name="symbol">The bound member symbol.</param>
        public void MarkMemberAccessSymbol(ISymbol symbol)
        {
            if (symbol is IMethodSymbol method)
            {
                MarkExtensionMethod(method);
                return;
            }

            // C# 14 extension members surface as members of a static class; treat any member of a
            // static class as potentially import-dependent. Over-marking is the safe direction.
            if (symbol.ContainingType is not { IsStatic: true } holder)
            {
                return;
            }

            MarkNamespace(holder.ContainingNamespace);
            MarkStaticType(holder);
        }

        /// <summary>Marks the directives an extension method in reduced form resolves through.</summary>
        /// <param name="method">The bound method.</param>
        public void MarkExtensionMethod(IMethodSymbol? method)
        {
            if (method is null)
            {
                return;
            }

            if (!method.IsExtensionMethod && method.ReducedFrom is null && method.ContainingType is not { IsStatic: true })
            {
                return;
            }

            MarkNamespace(method.ContainingType?.ContainingNamespace);
            MarkStaticType(method.ContainingType);
        }

        /// <summary>Reports every directive that stayed unaccounted for.</summary>
        /// <param name="context">The semantic model analysis context.</param>
        public void Report(SemanticModelAnalysisContext context)
        {
            if (Remaining == 0)
            {
                return;
            }

            for (var i = 0; i < _entries.Length; i++)
            {
                if (_used[i])
                {
                    continue;
                }

                var directive = _entries[i].Directive;
                var display = _entries[i].AliasName ?? directive.NamespaceOrType?.ToString() ?? directive.ToString();
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    MaintainabilityRules.UnnecessaryUsingDirective,
                    directive.SyntaxTree,
                    directive.Span,
                    display));
            }
        }

        /// <summary>Collects the trackable directives from one using list.</summary>
        /// <param name="usings">The using directives of one scope.</param>
        /// <param name="model">The file's semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <param name="entries">The entry list, created on first use.</param>
        private static void CollectUsings(SyntaxList<UsingDirectiveSyntax> usings, SemanticModel model, CancellationToken cancellationToken, ref List<Entry>? entries)
        {
            for (var i = 0; i < usings.Count; i++)
            {
                var directive = usings[i];
                if (!directive.GlobalKeyword.IsKind(SyntaxKind.None))
                {
                    continue;
                }

                if (TryCreateEntry(directive, model, cancellationToken, out var entry))
                {
                    entries ??= new List<Entry>(capacity: 8);
                    entries.Add(entry);
                }
            }
        }

        /// <summary>Collects trackable directives declared inside namespace declarations.</summary>
        /// <param name="members">The members of one scope.</param>
        /// <param name="model">The file's semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <param name="entries">The entry list, created on first use.</param>
        private static void CollectNamespaceUsings(SyntaxList<MemberDeclarationSyntax> members, SemanticModel model, CancellationToken cancellationToken, ref List<Entry>? entries)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i] is BaseNamespaceDeclarationSyntax ns)
                {
                    CollectUsings(ns.Usings, model, cancellationToken, ref entries);
                    CollectNamespaceUsings(ns.Members, model, cancellationToken, ref entries);
                }
            }
        }

        /// <summary>Builds one tracked entry, binding non-alias targets.</summary>
        /// <param name="directive">The using directive.</param>
        /// <param name="model">The file's semantic model.</param>
        /// <param name="cancellationToken">The analysis cancellation token.</param>
        /// <param name="entry">The created entry.</param>
        /// <returns><see langword="true"/> when the directive is trackable.</returns>
        private static bool TryCreateEntry(UsingDirectiveSyntax directive, SemanticModel model, CancellationToken cancellationToken, out Entry entry)
        {
            entry = default;
            if (directive.Alias is not null)
            {
                entry = new Entry(directive, Target: null, directive.Alias.Name.Identifier.ValueText, IsStatic: false);
                return true;
            }

            if (directive.NamespaceOrType is not { } target)
            {
                return false;
            }

            var symbol = model.GetSymbolInfo(target, cancellationToken).Symbol;
            if (!directive.StaticKeyword.IsKind(SyntaxKind.None))
            {
                if (symbol is not INamedTypeSymbol type)
                {
                    return false;
                }

                entry = new Entry(directive, type, AliasName: null, IsStatic: true);
                return true;
            }

            if (symbol is not INamespaceSymbol namespaceSymbol)
            {
                return false;
            }

            entry = new Entry(directive, namespaceSymbol, AliasName: null, IsStatic: false);
            return true;
        }

        /// <summary>
        /// Returns whether two namespace symbols name the same namespace. Symbol equality cannot
        /// be used here: the using target binds to the compilation's merged namespace while a
        /// metadata symbol's containing namespace is the per-module symbol, and the two never
        /// compare equal. Walking the name chain is allocation-free.
        /// </summary>
        /// <param name="target">The using directive's bound namespace.</param>
        /// <param name="candidate">The resolved symbol's containing namespace.</param>
        /// <returns><see langword="true"/> when both chains spell the same namespace.</returns>
        private static bool NamespaceMatches(INamespaceSymbol target, INamespaceSymbol candidate)
        {
            var left = target;
            var right = candidate;
            while (true)
            {
                if (left.IsGlobalNamespace || right.IsGlobalNamespace)
                {
                    return left.IsGlobalNamespace && right.IsGlobalNamespace;
                }

                if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal))
                {
                    return false;
                }

                left = left.ContainingNamespace;
                right = right.ContainingNamespace;
            }
        }

        /// <summary>Returns whether an alias symbol is declared by a specific directive.</summary>
        /// <param name="alias">The resolved alias symbol.</param>
        /// <param name="directive">The candidate directive.</param>
        /// <returns><see langword="true"/> when the alias was declared by that directive.</returns>
        private static bool IsDeclaredBy(IAliasSymbol alias, UsingDirectiveSyntax directive)
        {
            var references = alias.DeclaringSyntaxReferences;
            for (var i = 0; i < references.Length; i++)
            {
                var reference = references[i];
                if (reference.Span == directive.Span && reference.SyntaxTree == directive.SyntaxTree)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Marks the directives a bound method resolves through.</summary>
        /// <param name="method">The bound method.</param>
        private void MarkMethodSymbol(IMethodSymbol method)
        {
            if (method.MethodKind == MethodKind.Constructor)
            {
                // An attribute name binds to its constructor; the usage is really the type.
                MarkNamespace(method.ContainingType?.ContainingNamespace);
                MarkStaticType(method.ContainingType?.ContainingType);
                return;
            }

            MarkStaticType(method.ContainingType);
            if (!method.IsExtensionMethod && method.ReducedFrom is null)
            {
                return;
            }

            MarkNamespace(method.ContainingType?.ContainingNamespace);
        }

        /// <summary>Marks one entry used and updates the counters.</summary>
        /// <param name="index">The entry index.</param>
        private void MarkUsed(int index)
        {
            _used[index] = true;
            Remaining--;
            if (_entries[index].AliasName is null)
            {
                return;
            }

            _aliasRemaining--;
        }

        /// <summary>One tracked using directive.</summary>
        /// <param name="Directive">The tracked directive.</param>
        /// <param name="Target">The bound namespace or type target; <see langword="null"/> for aliases.</param>
        /// <param name="AliasName">The alias identifier text; <see langword="null"/> for non-alias directives.</param>
        /// <param name="IsStatic">Whether the directive is a <c>using static</c>.</param>
        private readonly record struct Entry(
            UsingDirectiveSyntax Directive,
            ISymbol? Target,
            string? AliasName,
            bool IsStatic);
    }

    /// <summary>
    /// The cheap first pass: binds only leftmost simple names (the way namespace and using-static
    /// imports are consumed) and stops as soon as every directive is marked. Also reused to scan
    /// XML documentation cref structures.
    /// </summary>
    private sealed class SimpleNameWalker : CSharpSyntaxWalker
    {
        /// <summary>The shared usage tracker.</summary>
        private readonly UsageTracker _tracker;

        /// <summary>Initializes a new instance of the <see cref="SimpleNameWalker"/> class.</summary>
        /// <param name="tracker">The shared usage tracker.</param>
        public SimpleNameWalker(UsageTracker tracker)
        {
            _tracker = tracker;
        }

        /// <summary>Gets a value indicating whether any directive is still unaccounted for.</summary>
        public bool HasWork => _tracker.Remaining > 0;

        /// <inheritdoc/>
        public override void Visit(SyntaxNode? node)
        {
            if (node is null || _tracker.Remaining == 0)
            {
                return;
            }

            base.Visit(node);
        }

        /// <inheritdoc/>
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            // Global usings resolve independently of file usings; skip their interiors. Non-global
            // directive interiors still count: nested-namespace usings can resolve through outer ones.
            if (!node.GlobalKeyword.IsKind(SyntaxKind.None))
            {
                return;
            }

            base.VisitUsingDirective(node);
        }

        /// <inheritdoc/>
        public override void VisitIdentifierName(IdentifierNameSyntax node)
            => MarkSimpleName(node, node.Identifier.ValueText, node.IsVar);

        /// <inheritdoc/>
        public override void VisitGenericName(GenericNameSyntax node)
        {
            MarkSimpleName(node, node.Identifier.ValueText, isVar: false);
            base.VisitGenericName(node);
        }

        /// <summary>Returns whether a simple name is a leftmost usage position worth binding.</summary>
        /// <param name="node">The simple name node.</param>
        /// <returns><see langword="true"/> when binding the name can prove an import usage.</returns>
        private static bool IsUsageCandidate(SimpleNameSyntax node)
            => node.Parent switch
            {
                QualifiedNameSyntax qualified when qualified.Right == node => false,
                MemberAccessExpressionSyntax memberAccess when memberAccess.Name == node => false,
                MemberBindingExpressionSyntax => false,
                AliasQualifiedNameSyntax aliasQualified when aliasQualified.Name == node => false,
                NameEqualsSyntax => false,
                NameColonSyntax => false,
                AssignmentExpressionSyntax assignment when assignment.Left == node && assignment.Parent is InitializerExpressionSyntax => false,
                _ => true,
            };

        /// <summary>Marks the directives a leftmost simple name resolves through.</summary>
        /// <param name="node">The simple name node.</param>
        /// <param name="text">The identifier text.</param>
        /// <param name="isVar">Whether the identifier is a contextual <c>var</c>.</param>
        private void MarkSimpleName(SimpleNameSyntax node, string text, bool isVar)
        {
            if (isVar || _tracker.Remaining == 0 || !IsUsageCandidate(node))
            {
                return;
            }

            if (node.Parent is AliasQualifiedNameSyntax aliasQualified && aliasQualified.Alias == node)
            {
                TryMarkAlias(node, text);
                return;
            }

            if (TryMarkAlias(node, text))
            {
                return;
            }

            var info = _tracker.Model.GetSymbolInfo(node, _tracker.CancellationToken);
            if (info.Symbol is { } symbol)
            {
                _tracker.MarkSimpleNameSymbol(symbol);
                return;
            }

            // Ambiguous or inaccessible candidates still prove the import is load-bearing.
            var candidates = info.CandidateSymbols;
            for (var i = 0; i < candidates.Length; i++)
            {
                _tracker.MarkSimpleNameSymbol(candidates[i]);
            }
        }

        /// <summary>Resolves and marks an alias usage.</summary>
        /// <param name="node">The simple name node.</param>
        /// <param name="text">The identifier text.</param>
        /// <returns><see langword="true"/> when the name resolved to an alias.</returns>
        private bool TryMarkAlias(SimpleNameSyntax node, string text)
        {
            if (!_tracker.MatchesUnusedAlias(text))
            {
                return false;
            }

            if (_tracker.Model.GetAliasInfo(node, _tracker.CancellationToken) is not { } alias)
            {
                return false;
            }

            _tracker.MarkAlias(alias);
            return true;
        }
    }

    /// <summary>
    /// The rare-path second pass, run only when a directive is still unaccounted for after the
    /// simple-name scan. Binds the usage shapes that consume an import without a leftmost simple
    /// name: reduced extension members, query clauses, foreach enumerators, deconstructions,
    /// collection-initializer adds, and awaiter lookups.
    /// </summary>
    private sealed class FallbackWalker : CSharpSyntaxWalker
    {
        /// <summary>The shared usage tracker.</summary>
        private readonly UsageTracker _tracker;

        /// <summary>Initializes a new instance of the <see cref="FallbackWalker"/> class.</summary>
        /// <param name="tracker">The shared usage tracker.</param>
        public FallbackWalker(UsageTracker tracker)
        {
            _tracker = tracker;
        }

        /// <inheritdoc/>
        public override void Visit(SyntaxNode? node)
        {
            if (node is null || _tracker.Remaining == 0)
            {
                return;
            }

            base.Visit(node);
        }

        /// <inheritdoc/>
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            // Directive interiors were fully handled by the simple-name pass.
        }

        /// <inheritdoc/>
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Only the pathological "type literally named var" case is deferred to this pass.
            if (!node.IsVar)
            {
                return;
            }

            if (_tracker.Model.GetSymbolInfo(node, _tracker.CancellationToken).Symbol is not INamedTypeSymbol named)
            {
                return;
            }

            _tracker.MarkSimpleNameSymbol(named);
        }

        /// <inheritdoc/>
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax)
            {
                _tracker.MarkExtensionMethod(_tracker.Model.GetSymbolInfo(node, _tracker.CancellationToken).Symbol as IMethodSymbol);
            }

            base.VisitInvocationExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            MarkMemberSymbol(node.Name);
            base.VisitMemberAccessExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
        {
            MarkMemberSymbol(node.Name);
            base.VisitMemberBindingExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitQueryExpression(QueryExpressionSyntax node)
        {
            MarkQueryBody(node.Body);
            base.VisitQueryExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            MarkForEach(node);
            base.VisitForEachStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            MarkForEach(node);
            MarkDeconstruction(_tracker.Model.GetDeconstructionInfo(node));
            base.VisitForEachVariableStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.SimpleAssignmentExpression) && node.Left is TupleExpressionSyntax or DeclarationExpressionSyntax)
            {
                MarkDeconstruction(_tracker.Model.GetDeconstructionInfo(node));
            }

            base.VisitAssignmentExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitRecursivePattern(RecursivePatternSyntax node)
        {
            // There is no public binding API for a positional pattern's Deconstruct; when the
            // matched type is not a tuple and declares no instance Deconstruct, an extension
            // method must be doing the work, so bail conservatively.
            if (node.PositionalPatternClause is not null
                && _tracker.Model.GetTypeInfo(node, _tracker.CancellationToken).ConvertedType is { IsTupleType: false } matched
                && !HasInstanceMethod(matched, "Deconstruct"))
            {
                _tracker.MarkAllRemaining();
            }

            base.VisitRecursivePattern(node);
        }

        /// <inheritdoc/>
        public override void VisitInitializerExpression(InitializerExpressionSyntax node)
        {
            if (node.IsKind(SyntaxKind.CollectionInitializerExpression))
            {
                var expressions = node.Expressions;
                for (var i = 0; i < expressions.Count; i++)
                {
                    _tracker.MarkExtensionMethod(_tracker.Model.GetCollectionInitializerSymbolInfo(expressions[i], _tracker.CancellationToken).Symbol as IMethodSymbol);
                }
            }

            base.VisitInitializerExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitAwaitExpression(AwaitExpressionSyntax node)
        {
            _tracker.MarkExtensionMethod(_tracker.Model.GetAwaitExpressionInfo(node).GetAwaiterMethod);
            base.VisitAwaitExpression(node);
        }

        /// <inheritdoc/>
        public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (!node.AwaitKeyword.IsKind(SyntaxKind.None))
            {
                MarkAwaitUsingResource(node.Declaration.Type);
            }

            base.VisitLocalDeclarationStatement(node);
        }

        /// <inheritdoc/>
        public override void VisitUsingStatement(UsingStatementSyntax node)
        {
            if (!node.AwaitKeyword.IsKind(SyntaxKind.None))
            {
                MarkAwaitUsingResource((SyntaxNode?)node.Expression ?? node.Declaration?.Type);
            }

            base.VisitUsingStatement(node);
        }

        /// <summary>Returns whether a type declares a named instance method anywhere in its base chain.</summary>
        /// <param name="type">The type to search.</param>
        /// <param name="name">The method name.</param>
        /// <returns><see langword="true"/> when a matching instance method is declared.</returns>
        private static bool HasInstanceMethod(ITypeSymbol type, string name)
        {
            for (var current = type; current is not null; current = current.BaseType)
            {
                var members = current.GetMembers(name);
                for (var i = 0; i < members.Length; i++)
                {
                    if (members[i] is IMethodSymbol { IsStatic: false })
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Returns whether a type has an instance DisposeAsync (declared or via IAsyncDisposable).</summary>
        /// <param name="type">The resource type.</param>
        /// <returns><see langword="true"/> when disposal cannot involve an extension method.</returns>
        private static bool HasDisposeAsyncMember(ITypeSymbol type)
        {
            if (HasInstanceMethod(type, "DisposeAsync"))
            {
                return true;
            }

            var interfaces = type.AllInterfaces;
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].Name == "IAsyncDisposable" && interfaces[i].ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true })
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Marks extension members bound through a member access name.</summary>
        /// <param name="name">The accessed member name.</param>
        private void MarkMemberSymbol(SimpleNameSyntax name)
        {
            if (_tracker.Model.GetSymbolInfo(name, _tracker.CancellationToken).Symbol is not { } symbol)
            {
                return;
            }

            _tracker.MarkMemberAccessSymbol(symbol);
        }

        /// <summary>Marks the query-operator methods a query expression binds.</summary>
        /// <param name="body">The query body.</param>
        private void MarkQueryBody(QueryBodySyntax? body)
        {
            while (body is not null && _tracker.Remaining > 0)
            {
                var clauses = body.Clauses;
                for (var i = 0; i < clauses.Count; i++)
                {
                    var info = _tracker.Model.GetQueryClauseInfo(clauses[i], _tracker.CancellationToken);
                    _tracker.MarkExtensionMethod(info.CastInfo.Symbol as IMethodSymbol);
                    _tracker.MarkExtensionMethod(info.OperationInfo.Symbol as IMethodSymbol);
                }

                _tracker.MarkExtensionMethod(_tracker.Model.GetSymbolInfo(body.SelectOrGroup, _tracker.CancellationToken).Symbol as IMethodSymbol);
                body = body.Continuation?.Body;
            }
        }

        /// <summary>Marks the enumerator method a foreach statement binds.</summary>
        /// <param name="node">The foreach statement.</param>
        private void MarkForEach(CommonForEachStatementSyntax node)
            => _tracker.MarkExtensionMethod(_tracker.Model.GetForEachStatementInfo(node).GetEnumeratorMethod);

        /// <summary>Marks the Deconstruct methods a deconstruction binds, including nested ones.</summary>
        /// <param name="info">The deconstruction info.</param>
        private void MarkDeconstruction(DeconstructionInfo info)
        {
            _tracker.MarkExtensionMethod(info.Method);
            var nested = info.Nested;
            for (var i = 0; i < nested.Length; i++)
            {
                MarkDeconstruction(nested[i]);
            }
        }

        /// <summary>
        /// Conservatively bails when an <c>await using</c> resource has no instance DisposeAsync:
        /// the disposal then binds an extension method this walker cannot attribute, so every
        /// remaining directive is treated as used.
        /// </summary>
        /// <param name="resource">The resource expression or declared type.</param>
        private void MarkAwaitUsingResource(SyntaxNode? resource)
        {
            if (resource is null)
            {
                return;
            }

            var type = _tracker.Model.GetTypeInfo(resource, _tracker.CancellationToken).Type;
            if (type is null || HasDisposeAsyncMember(type))
            {
                return;
            }

            _tracker.MarkAllRemaining();
        }
    }
}
