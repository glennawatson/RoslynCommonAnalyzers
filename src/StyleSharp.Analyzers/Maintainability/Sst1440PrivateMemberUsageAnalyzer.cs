// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>Reports private members that are unused or private fields that are written but never read.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1440PrivateMemberUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        MaintainabilityRules.RemoveUnusedPrivateMember,
        MaintainabilityRules.RemoveUnreadPrivateField);

    /// <summary>Field-like usage kinds.</summary>
    [Flags]
    private enum ValueUsages
    {
        /// <summary>No usage.</summary>
        None = 0,

        /// <summary>The value is read.</summary>
        Read = 1 << 0,

        /// <summary>The value is written.</summary>
        Write = 1 << 1,

        /// <summary>The value is both read and written.</summary>
        ReadWrite = Read | Write,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

        context.RegisterCompilationStartAction(static context =>
        {
            // Only partial types share state across semantic-model callbacks.
            var usages = new ConcurrentDictionary<INamedTypeSymbol, PrivateTypeUsage>(concurrencyLevel: 1, capacity: 4, SymbolEqualityComparer.Default);
            context.RegisterSemanticModelAction(modelContext => AnalyzeSemanticModel(modelContext, usages));
            context.RegisterCompilationEndAction(context => ReportCandidates(context, usages));
        });
    }

    /// <summary>Analyzes one semantic model for private member declarations and references.</summary>
    /// <param name="context">The semantic model context.</param>
    /// <param name="usages">The accumulated type usage state.</param>
    private static void AnalyzeSemanticModel(
        in SemanticModelAnalysisContext context,
        ConcurrentDictionary<INamedTypeSymbol, PrivateTypeUsage> usages)
    {
        var root = context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken);
        var state = (Context: context, Usages: usages);
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, (SemanticModelAnalysisContext Context, ConcurrentDictionary<INamedTypeSymbol, PrivateTypeUsage> Usages)>(
            root,
            ref state,
            static (node, ref current) =>
            {
                current.Context.CancellationToken.ThrowIfCancellationRequested();
                if (node is not TypeDeclarationSyntax typeDeclaration)
                {
                    return true;
                }

                if (current.Context.SemanticModel.GetDeclaredSymbol(typeDeclaration, current.Context.CancellationToken) is not INamedTypeSymbol typeSymbol)
                {
                    return true;
                }

                if (typeSymbol.DeclaringSyntaxReferences.Length == 1)
                {
                    AnalyzeSinglePartType(current.Context, typeDeclaration);
                    return true;
                }

                var usage = current.Usages.GetOrAdd(typeSymbol, static _ => new PrivateTypeUsage());
                CollectCandidates(typeDeclaration, current.Context.SemanticModel, usage, current.Context.CancellationToken);
                CollectReferences(typeDeclaration, usage, current.Context.SemanticModel, current.Context.CancellationToken);
                return true;
            });
    }

    /// <summary>Analyzes one type declaration whose full body is available in the current semantic model.</summary>
    /// <param name="context">The semantic model context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    private static void AnalyzeSinglePartType(in SemanticModelAnalysisContext context, TypeDeclarationSyntax typeDeclaration)
    {
        var usage = new PrivateTypeUsage();
        CollectCandidates(typeDeclaration, context.SemanticModel, usage, context.CancellationToken);
        CollectReferences(typeDeclaration, usage, context.SemanticModel, context.CancellationToken);
        var (candidates, references) = usage.Snapshot();
        if (candidates.Count == 0)
        {
            return;
        }

        MarkReferences(candidates, references, context.CancellationToken);
        ReportCandidates(context.ReportDiagnostic, candidates, context.SemanticModel.Compilation.GetEntryPoint(context.CancellationToken));
    }

    /// <summary>Collects private fields, properties, methods, and events that are safe to analyze locally.</summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void CollectCandidates(
        TypeDeclarationSyntax typeDeclaration,
        SemanticModel model,
        PrivateTypeUsage usage,
        CancellationToken cancellationToken)
    {
        var members = typeDeclaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            switch (members[i])
            {
                case FieldDeclarationSyntax field:
                    {
                        CollectFieldCandidates(field, model, usage, cancellationToken);
                        break;
                    }

                case PropertyDeclarationSyntax property:
                    {
                        AddCandidate(property, property.Identifier, isFieldLike: true, model, usage, cancellationToken);
                        break;
                    }

                case MethodDeclarationSyntax method:
                    {
                        AddMethodCandidate(method, model, usage, cancellationToken);
                        break;
                    }

                case EventFieldDeclarationSyntax eventField:
                    {
                        CollectEventCandidates(eventField, model, usage, cancellationToken);
                        break;
                    }
            }
        }
    }

    /// <summary>Collects private field candidates from one field declaration.</summary>
    /// <param name="field">The field declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void CollectFieldCandidates(
        FieldDeclarationSyntax field,
        SemanticModel model,
        PrivateTypeUsage usage,
        CancellationToken cancellationToken)
    {
        if (!IsPrivate(field.Modifiers)
            || HasModifier(field.Modifiers, SyntaxKind.ConstKeyword)
            || HasAttributes(field))
        {
            return;
        }

        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (model.GetDeclaredSymbol(variables[i], cancellationToken) is IFieldSymbol symbol)
            {
                usage.AddMemberCandidate(new(symbol, field, variables[i].Identifier, isFieldLike: true));
            }
        }
    }

    /// <summary>Adds a private property candidate when it has no attributes.</summary>
    /// <param name="property">The property declaration.</param>
    /// <param name="identifier">The property identifier.</param>
    /// <param name="isFieldLike">Whether reads and writes should be tracked separately.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void AddCandidate(
        PropertyDeclarationSyntax property,
        SyntaxToken identifier,
        bool isFieldLike,
        SemanticModel model,
        PrivateTypeUsage usage,
        CancellationToken cancellationToken)
    {
        if (!IsPrivate(property.Modifiers)
            || HasAttributes(property)
            || model.GetDeclaredSymbol(property, cancellationToken) is not IPropertySymbol symbol)
        {
            return;
        }

        usage.AddMemberCandidate(new(symbol, property, identifier, isFieldLike));
    }

    /// <summary>Adds a private method candidate when it is safe to remove mechanically.</summary>
    /// <param name="method">The method declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void AddMethodCandidate(
        MethodDeclarationSyntax method,
        SemanticModel model,
        PrivateTypeUsage usage,
        CancellationToken cancellationToken)
    {
        if (!IsPrivate(method.Modifiers)
            || HasModifier(method.Modifiers, SyntaxKind.PartialKeyword)
            || HasModifier(method.Modifiers, SyntaxKind.ExternKeyword)
            || HasAttributes(method)
            || model.GetDeclaredSymbol(method, cancellationToken) is not IMethodSymbol symbol)
        {
            return;
        }

        usage.AddMemberCandidate(new(symbol, method, method.Identifier, isFieldLike: false));
    }

    /// <summary>Collects private event candidates from one event-field declaration.</summary>
    /// <param name="eventField">The event-field declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void CollectEventCandidates(
        EventFieldDeclarationSyntax eventField,
        SemanticModel model,
        PrivateTypeUsage usage,
        CancellationToken cancellationToken)
    {
        if (!IsPrivate(eventField.Modifiers) || HasAttributes(eventField))
        {
            return;
        }

        var variables = eventField.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            if (model.GetDeclaredSymbol(variables[i], cancellationToken) is IEventSymbol symbol)
            {
                usage.AddMemberCandidate(new(symbol, eventField, variables[i].Identifier, isFieldLike: false));
            }
        }
    }

    /// <summary>Builds a candidate map keyed by identifier text.</summary>
    /// <param name="candidates">The candidates.</param>
    /// <returns>The candidate map.</returns>
    private static Dictionary<string, List<PrivateMemberCandidate>> BuildNameMap(List<PrivateMemberCandidate> candidates)
    {
        var byName = new Dictionary<string, List<PrivateMemberCandidate>>(StringComparer.Ordinal);
        for (var i = 0; i < candidates.Count; i++)
        {
            var name = candidates[i].Symbol.Name;
            if (!byName.TryGetValue(name, out var list))
            {
                list = [];
                byName.Add(name, list);
            }

            list.Add(candidates[i]);
        }

        return byName;
    }

    /// <summary>Collects references found inside the type declaration.</summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="usage">The type usage state.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void CollectReferences(
        TypeDeclarationSyntax typeDeclaration,
        PrivateTypeUsage usage,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not { } type)
        {
            return;
        }

        // The declaration symbol includes members from every partial declaration, even those not scanned yet.
        var memberNames = new HashSet<string>(type.MemberNames, StringComparer.Ordinal);
        var state = (Usage: usage, Model: model, MemberNames: memberNames, CancellationToken: cancellationToken);
        _ = DescendantTraversalHelper.VisitDescendants<SimpleNameSyntax, (PrivateTypeUsage Usage, SemanticModel Model, HashSet<string> MemberNames, CancellationToken CancellationToken)>(
            typeDeclaration,
            ref state,
            static (simpleName, ref current) =>
            {
                if (!current.MemberNames.Contains(simpleName.Identifier.ValueText))
                {
                    return true;
                }

                var symbol = current.Model.GetSymbolInfo(simpleName, current.CancellationToken).Symbol;
                if (symbol is not null)
                {
                    current.Usage.AddMemberReference(new(symbol, simpleName));
                }

                return true;
            });
    }

    /// <summary>Returns whether a reference symbol matches a candidate declaration symbol.</summary>
    /// <param name="reference">The symbol resolved at the reference site.</param>
    /// <param name="candidate">The candidate declaration symbol.</param>
    /// <returns><see langword="true"/> when the symbols represent the same member.</returns>
    private static bool SymbolMatches(ISymbol reference, ISymbol candidate) =>
        SymbolEqualityComparer.Default.Equals(reference, candidate)
            || SymbolEqualityComparer.Default.Equals(reference.OriginalDefinition, candidate.OriginalDefinition);

    /// <summary>Updates read/write state for one reference.</summary>
    /// <param name="candidate">The candidate member.</param>
    /// <param name="name">The reference name.</param>
    private static void MarkReference(PrivateMemberCandidate candidate, SimpleNameSyntax name)
    {
        if (!candidate.IsFieldLike)
        {
            candidate.Read = true;
            return;
        }

        var usage = GetValueUsages(name);
        candidate.Read |= (usage & ValueUsages.Read) != 0;
        candidate.Written |= (usage & ValueUsages.Write) != 0;
    }

    /// <summary>Reports the unused or unread candidates for every analyzed type.</summary>
    /// <param name="context">The compilation context.</param>
    /// <param name="usages">The accumulated type usage state.</param>
    private static void ReportCandidates(
        in CompilationAnalysisContext context,
        ConcurrentDictionary<INamedTypeSymbol, PrivateTypeUsage> usages)
    {
        foreach (var usage in usages)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            ReportCandidates(context, usage.Value);
        }
    }

    /// <summary>Reports the unused or unread candidates.</summary>
    /// <param name="context">The compilation context.</param>
    /// <param name="usage">The type usage state.</param>
    private static void ReportCandidates(in CompilationAnalysisContext context, PrivateTypeUsage usage)
    {
        var (candidates, references) = usage.Snapshot();
        if (candidates.Count == 0)
        {
            return;
        }

        MarkReferences(candidates, references, context.CancellationToken);
        ReportCandidates(context.ReportDiagnostic, candidates, context.Compilation.GetEntryPoint(context.CancellationToken));
    }

    /// <summary>Reports the unused or unread candidates.</summary>
    /// <param name="reportDiagnostic">The diagnostic reporting callback.</param>
    /// <param name="candidates">The candidate list.</param>
    /// <param name="entryPoint">The compilation's entry point, when it has one.</param>
    /// <remarks>
    /// The runtime calls the entry point, so nothing in the source references it. Removing it leaves a
    /// program that cannot start (CS5001).
    /// </remarks>
    private static void ReportCandidates(Action<Diagnostic> reportDiagnostic, List<PrivateMemberCandidate> candidates, IMethodSymbol? entryPoint)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (entryPoint is not null && SymbolEqualityComparer.Default.Equals(candidate.Symbol, entryPoint))
            {
                continue;
            }

            if (candidate.IsFieldLike)
            {
                if (!candidate.Read && !candidate.Written)
                {
                    reportDiagnostic(Diagnostic.Create(
                        MaintainabilityRules.RemoveUnusedPrivateMember,
                        candidate.Identifier.GetLocation(),
                        candidate.Symbol.Name));
                }
                else if (!candidate.Read && candidate.Written)
                {
                    reportDiagnostic(Diagnostic.Create(
                        MaintainabilityRules.RemoveUnreadPrivateField,
                        candidate.Identifier.GetLocation(),
                        candidate.Symbol.Name));
                }

                continue;
            }

            if (!candidate.Read)
            {
                reportDiagnostic(Diagnostic.Create(
                    MaintainabilityRules.RemoveUnusedPrivateMember,
                    candidate.Identifier.GetLocation(),
                    candidate.Symbol.Name));
            }
        }
    }

    /// <summary>Marks candidate references found across the type declarations.</summary>
    /// <param name="candidates">The candidate list.</param>
    /// <param name="references">The collected references.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    private static void MarkReferences(
        List<PrivateMemberCandidate> candidates,
        List<PrivateMemberReference> references,
        CancellationToken cancellationToken)
    {
        var byName = BuildNameMap(candidates);
        for (var i = 0; i < references.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = references[i];
            if (!byName.TryGetValue(reference.Name.Identifier.ValueText, out var nameCandidates))
            {
                continue;
            }

            for (var j = 0; j < nameCandidates.Count; j++)
            {
                var candidate = nameCandidates[j];
                if (!SymbolMatches(reference.Symbol, candidate.Symbol)
                    || IsInsideDeclaration(reference.Name, candidate.Declaration))
                {
                    continue;
                }

                MarkReference(candidate, reference.Name);
            }
        }
    }

    /// <summary>Returns whether modifiers declare private accessibility.</summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns><see langword="true"/> when the declaration is private.</returns>
    private static bool IsPrivate(in SyntaxTokenList modifiers) =>
        HasModifier(modifiers, SyntaxKind.PrivateKeyword)
            && !HasModifier(modifiers, SyntaxKind.ProtectedKeyword)
            && !HasModifier(modifiers, SyntaxKind.InternalKeyword)
            && !HasModifier(modifiers, SyntaxKind.PublicKeyword);

    /// <summary>Returns whether a modifier list contains a specific modifier kind.</summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <param name="kind">The modifier kind.</param>
    /// <returns><see langword="true"/> when the modifier is present.</returns>
    private static bool HasModifier(in SyntaxTokenList modifiers, SyntaxKind kind)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(kind))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a member has attributes.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member has attributes.</returns>
    private static bool HasAttributes(MemberDeclarationSyntax member) => member.AttributeLists.Count != 0;

    /// <summary>Returns whether a reference is inside the candidate's own declaration.</summary>
    /// <param name="name">The reference name.</param>
    /// <param name="declaration">The candidate declaration.</param>
    /// <returns><see langword="true"/> when the reference is self-contained in the declaration.</returns>
    private static bool IsInsideDeclaration(SyntaxNode name, SyntaxNode declaration) =>
        name.FirstAncestorOrSelf<MemberDeclarationSyntax>() == declaration;

    /// <summary>Gets whether a field-like reference reads, writes, or both.</summary>
    /// <param name="name">The referenced name.</param>
    /// <returns>The usage kind.</returns>
    private static ValueUsages GetValueUsages(SimpleNameSyntax name)
    {
        var expression = name.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == name
            ? (ExpressionSyntax)memberAccess
            : name;

        if (expression.Parent is AssignmentExpressionSyntax assignment && assignment.Left == expression)
        {
            return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? ValueUsages.Write : ValueUsages.ReadWrite;
        }

        return IsIncrementOrDecrement(expression.Parent) ? ValueUsages.ReadWrite : GetArgumentUsages(expression.Parent) ?? ValueUsages.Read;
    }

    /// <summary>Returns whether a parent node is an increment or decrement operation.</summary>
    /// <param name="parent">The parent node.</param>
    /// <returns><see langword="true"/> when the parent reads and writes the operand.</returns>
    private static bool IsIncrementOrDecrement(SyntaxNode? parent) =>
        (parent is PrefixUnaryExpressionSyntax prefix
                && (prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression)))
            || (parent is PostfixUnaryExpressionSyntax postfix
                && (postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression)));

    /// <summary>Gets usage semantics for ref-like argument passing.</summary>
    /// <param name="parent">The parent node.</param>
    /// <returns>The usage kind, or <see langword="null"/> when the parent is not an argument.</returns>
    private static ValueUsages? GetArgumentUsages(SyntaxNode? parent) => parent is not ArgumentSyntax argument ? null : argument.RefOrOutKeyword.RawKind switch
        {
            (int)SyntaxKind.OutKeyword => ValueUsages.Write,
            (int)SyntaxKind.RefKeyword or (int)SyntaxKind.InKeyword => ValueUsages.ReadWrite,
            _ => ValueUsages.Read
        };

    /// <summary>Tracks private member candidates and references for one type symbol.</summary>
    /// <remarks>
    /// The two collections are read together by the compilation-end action, which Roslyn runs only
    /// after every action that fills them, so the pair cannot be observed half-written and the
    /// additions need nothing beyond their own thread safety.
    /// </remarks>
    private sealed class PrivateTypeUsage
    {
        /// <summary>The collected private member candidates.</summary>
        private readonly ConcurrentQueue<PrivateMemberCandidate> _candidates = new();

        /// <summary>The collected member references.</summary>
        private readonly ConcurrentQueue<PrivateMemberReference> _references = new();

        /// <summary>Adds one private member candidate.</summary>
        /// <param name="candidate">The candidate to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddMemberCandidate(PrivateMemberCandidate candidate) => _candidates.Enqueue(candidate);

        /// <summary>Adds one member reference.</summary>
        /// <param name="reference">The reference to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddMemberReference(PrivateMemberReference reference) => _references.Enqueue(reference);

        /// <summary>Creates a stable snapshot of the accumulated candidates and references.</summary>
        /// <returns>The accumulated candidates and references.</returns>
        public (List<PrivateMemberCandidate> Candidates, List<PrivateMemberReference> References) Snapshot() =>
            (new List<PrivateMemberCandidate>(_candidates.ToArray()), new List<PrivateMemberReference>(_references.ToArray()));
    }

    /// <summary>Tracks one private member candidate.</summary>
    private sealed class PrivateMemberCandidate
    {
        /// <summary>Initializes a new instance of the <see cref="PrivateMemberCandidate"/> class.</summary>
        /// <param name="symbol">The declared member symbol.</param>
        /// <param name="declaration">The member declaration syntax.</param>
        /// <param name="identifier">The declaration identifier.</param>
        /// <param name="isFieldLike">Whether reads and writes are tracked separately.</param>
        public PrivateMemberCandidate(ISymbol symbol, MemberDeclarationSyntax declaration, SyntaxToken identifier, bool isFieldLike)
        {
            Symbol = symbol;
            Declaration = declaration;
            Identifier = identifier;
            IsFieldLike = isFieldLike;
        }

        /// <summary>Gets the declared member symbol.</summary>
        public ISymbol Symbol { get; }

        /// <summary>Gets the declaration syntax.</summary>
        public MemberDeclarationSyntax Declaration { get; }

        /// <summary>Gets the declaration identifier.</summary>
        public SyntaxToken Identifier { get; }

        /// <summary>Gets a value indicating whether reads and writes are tracked separately.</summary>
        public bool IsFieldLike { get; }

        /// <summary>Gets or sets a value indicating whether the member is read or otherwise used.</summary>
        public bool Read { get; set; }

        /// <summary>Gets or sets a value indicating whether the member is written.</summary>
        public bool Written { get; set; }
    }

    /// <summary>Tracks one member reference.</summary>
    /// <param name="Symbol">The symbol resolved at the reference site.</param>
    /// <param name="Name">The reference name syntax.</param>
    private sealed record PrivateMemberReference(ISymbol Symbol, SimpleNameSyntax Name);
}
