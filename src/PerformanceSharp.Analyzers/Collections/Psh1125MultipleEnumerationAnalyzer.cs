// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a parameter or local typed as a <em>lazy</em> sequence that is walked more than once in
/// the same method (PSH1125) — two <c>foreach</c> loops, a <c>Count()</c> then a <c>foreach</c>,
/// an <c>Any()</c> guard then a <c>First()</c>. The second walk re-runs whatever produced the
/// sequence, and against a one-shot iterator it yields nothing at all. The bug is invisible at
/// the call site: the same code is correct for a <c>List</c> and wrong for a lazy sequence.
/// <para>
/// A false positive here is noise, so the rule is deliberately narrow. It reports only when the
/// <em>declared</em> type is exactly <c>IEnumerable&lt;T&gt;</c> or the non-generic
/// <c>IEnumerable</c>. Anything that carries a materialized-collection contract —
/// <c>List&lt;T&gt;</c>, <c>T[]</c>, <c>ICollection&lt;T&gt;</c>,
/// <c>IReadOnlyCollection&lt;T&gt;</c>, <c>IList&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c>,
/// <c>HashSet&lt;T&gt;</c> — can be walked twice safely and is never reported. A candidate that
/// is assigned, passed by <c>ref</c>/<c>out</c>, or initialized from a materializing call
/// (<c>= source.ToList()</c>, a <c>new</c>, an array or collection expression) is dropped
/// entirely, and two walks that cannot both run — the arms of an <c>if</c>/<c>else</c>,
/// <c>switch</c> sections, the branches of a <c>?:</c>, a <c>try</c> body versus its
/// <c>catch</c> — are not counted as a pair.
/// </para>
/// <para>
/// Passing the sequence to another method is not treated as a walk, so anything the rule cannot
/// see it does not guess at. Enumeration is proved, not assumed: an eager call is reported only
/// once it binds to a <c>System.Linq.Enumerable</c> extension. There is no code fix —
/// materializing changes the allocation behavior of the method, and that is the author's call.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1125MultipleEnumerationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The type name the syntax prepass requires before any binding.</summary>
    internal const string EnumerableTypeName = "IEnumerable";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <summary>The minimum number of walks that can constitute a re-enumeration.</summary>
    private const int MinimumWalkCount = 2;

    /// <summary>The <c>System.Linq.Enumerable</c> methods that walk their source immediately.</summary>
    private static readonly HashSet<string> EagerMethodNames = new(StringComparer.Ordinal)
    {
        "Aggregate", "All", "Any", "Average", "Contains", "Count", "ElementAt", "ElementAtOrDefault",
        "First", "FirstOrDefault", "Last", "LastOrDefault", "LongCount", "Max", "Min", "SequenceEqual",
        "Single", "SingleOrDefault", "Sum", "ToArray", "ToDictionary", "ToHashSet", "ToList", "ToLookup",
    };

    /// <summary>The call names that materialize a sequence, so a local initialized from one is safe to re-read.</summary>
    private static readonly HashSet<string> MaterializingMethodNames = new(StringComparer.Ordinal)
    {
        "AsReadOnly", "ToArray", "ToDictionary", "ToHashSet", "ToList", "ToLookup",
    };

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.MultipleEnumeration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var enumerableType = start.Compilation.GetTypeByMetadataName(EnumerableMetadataName);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, enumerableType), SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>Returns whether a type syntax's rightmost name is <c>IEnumerable</c>, before any binding.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns><see langword="true"/> when the syntax names IEnumerable.</returns>
    internal static bool IsEnumerableTypeSyntax(TypeSyntax type)
    {
        var current = type;
        while (true)
        {
            switch (current)
            {
                case QualifiedNameSyntax qualified:
                {
                    current = qualified.Right;
                    continue;
                }

                case NullableTypeSyntax nullable:
                {
                    current = nullable.ElementType;
                    continue;
                }

                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText == EnumerableTypeName;
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText == EnumerableTypeName;
                default:
                    return false;
            }
        }
    }

    /// <summary>Returns whether a type is one of the two lazy sequence contracts the rule reports on.</summary>
    /// <param name="type">The declared symbol's type.</param>
    /// <returns><see langword="true"/> only for <c>IEnumerable&lt;T&gt;</c> and the non-generic <c>IEnumerable</c>.</returns>
    internal static bool IsLazySequenceType(ITypeSymbol type)
        => type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            || type.SpecialType == SpecialType.System_Collections_IEnumerable;

    /// <summary>Reports PSH1125 for each lazy-sequence parameter or local the method walks twice.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, INamedTypeSymbol? enumerableType)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body is null || !MentionsEnumerable(method))
        {
            return;
        }

        AnalyzeParameters(context, method, body, enumerableType);
        AnalyzeLocals(context, body, enumerableType);
    }

    /// <summary>Runs the free syntax prepass, which asks whether the method mentions <c>IEnumerable</c> at all.</summary>
    /// <param name="method">The method to scan.</param>
    /// <returns><see langword="true"/> when an IEnumerable identifier token appears, so binding is worth it.</returns>
    private static bool MentionsEnumerable(MethodDeclarationSyntax method)
    {
        var state = default(MentionScanState);
        DescendantTraversalHelper.VisitDescendantTokens(method, ref state, VisitTypeNameToken);
        return state.Found;
    }

    /// <summary>Classifies one token during the prepass.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once IEnumerable is seen.</returns>
    private static bool VisitTypeNameToken(in SyntaxToken token, ref MentionScanState state)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != EnumerableTypeName)
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Reports each lazy-sequence parameter the method walks twice.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The method being analyzed.</param>
    /// <param name="body">The method's body or expression body.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    private static void AnalyzeParameters(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax method,
        SyntaxNode body,
        INamedTypeSymbol? enumerableType)
    {
        var parameters = method.ParameterList.Parameters;
        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i];
            if (parameter.Modifiers.Count > 0
                || parameter.Type is not { } type
                || !IsEnumerableTypeSyntax(type)
                || context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol
                || !IsLazySequenceType(symbol.Type))
            {
                continue;
            }

            ReportIfWalkedTwice(context, body, symbol, parameter.Identifier.ValueText, enumerableType);
        }
    }

    /// <summary>Reports each lazy-sequence local the method walks twice.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The method's body or expression body.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    private static void AnalyzeLocals(SyntaxNodeAnalysisContext context, SyntaxNode body, INamedTypeSymbol? enumerableType)
    {
        var declarations = new List<LocalDeclarationStatementSyntax>(2);
        var state = new LocalScanState(declarations);
        DescendantTraversalHelper.VisitDescendants<LocalDeclarationStatementSyntax, LocalScanState>(body, ref state, VisitLocalDeclaration);

        for (var i = 0; i < declarations.Count; i++)
        {
            AnalyzeLocalDeclaration(context, body, declarations[i], enumerableType);
        }
    }

    /// <summary>Collects one local declaration whose declared type names IEnumerable.</summary>
    /// <param name="declaration">The visited local declaration.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning.</returns>
    private static bool VisitLocalDeclaration(LocalDeclarationStatementSyntax declaration, ref LocalScanState state)
    {
        if (!IsEnumerableTypeSyntax(declaration.Declaration.Type))
        {
            return true;
        }

        state.Declarations.Add(declaration);
        return true;
    }

    /// <summary>Reports each lazy-sequence variable of one local declaration that the method walks twice.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The method's body or expression body.</param>
    /// <param name="declaration">The local declaration to inspect.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    private static void AnalyzeLocalDeclaration(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        LocalDeclarationStatementSyntax declaration,
        INamedTypeSymbol? enumerableType)
    {
        var variables = declaration.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (IsMaterializedInitializer(variable.Initializer?.Value)
                || context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol symbol
                || !IsLazySequenceType(symbol.Type))
            {
                continue;
            }

            ReportIfWalkedTwice(context, body, symbol, variable.Identifier.ValueText, enumerableType);
        }
    }

    /// <summary>Returns whether an initializer already materialized the sequence, making re-reads safe.</summary>
    /// <param name="initializer">The local's initializer expression, when it has one.</param>
    /// <returns><see langword="true"/> when the local holds a materialized collection.</returns>
    private static bool IsMaterializedInitializer(ExpressionSyntax? initializer)
    {
        if (initializer is ObjectCreationExpressionSyntax
            or ArrayCreationExpressionSyntax
            or ImplicitArrayCreationExpressionSyntax
            or InitializerExpressionSyntax
            or CollectionExpressionSyntax)
        {
            return true;
        }

        return initializer is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            && MaterializingMethodNames.Contains(access.Name.Identifier.ValueText);
    }

    /// <summary>Scans the body for a symbol's walks and reports the second one when two can both run.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The method's body or expression body.</param>
    /// <param name="symbol">The candidate parameter or local.</param>
    /// <param name="name">The candidate's name, used for the message and the identifier prefilter.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    private static void ReportIfWalkedTwice(
        SyntaxNodeAnalysisContext context,
        SyntaxNode body,
        ISymbol symbol,
        string name,
        INamedTypeSymbol? enumerableType)
    {
        var walks = new List<IdentifierNameSyntax>(MinimumWalkCount);
        var state = new UsageScanState(name, symbol, context.SemanticModel, enumerableType, walks, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, UsageScanState>(body, ref state, VisitIdentifier);

        if (state.Disqualified || walks.Count < MinimumWalkCount || FindReenumeration(walks) is not { } offending)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.MultipleEnumeration,
            offending.GetLocation(),
            name));
    }

    /// <summary>Classifies one identifier usage of the candidate: a write that disqualifies it, or a walk.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once the candidate is disqualified.</returns>
    private static bool VisitIdentifier(IdentifierNameSyntax identifier, ref UsageScanState state)
    {
        if (identifier.Identifier.ValueText != state.Name
            || !SymbolEqualityComparer.Default.Equals(
                state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol,
                state.Symbol))
        {
            return true;
        }

        if (IsWrittenThrough(identifier))
        {
            state.Disqualified = true;
            return false;
        }

        if (!IsWalk(identifier, state.Model, state.EnumerableType, state.CancellationToken))
        {
            return true;
        }

        state.Walks.Add(identifier);
        return true;
    }

    /// <summary>Returns whether a usage rebinds the candidate, which makes any later walk a walk of something else.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <returns><see langword="true"/> for an assignment target or a ref/out/in argument.</returns>
    private static bool IsWrittenThrough(IdentifierNameSyntax identifier)
        => (identifier.Parent is AssignmentExpressionSyntax assignment && assignment.Left == identifier)
            || (identifier.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None));

    /// <summary>Returns whether a usage actually walks the sequence.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the usage is a foreach source or roots a chain that ends in an eager LINQ call.</returns>
    private static bool IsWalk(
        IdentifierNameSyntax identifier,
        SemanticModel model,
        INamedTypeSymbol? enumerableType,
        CancellationToken cancellationToken)
    {
        SyntaxNode current = identifier;
        if (IsForEachSource(current))
        {
            return true;
        }

        while (current.Parent is MemberAccessExpressionSyntax access
            && access.Expression == current
            && access.Parent is InvocationExpressionSyntax invocation)
        {
            if (EagerMethodNames.Contains(access.Name.Identifier.ValueText)
                && IsEnumerableExtension(model, invocation, enumerableType, cancellationToken))
            {
                return true;
            }

            current = invocation;
            if (IsForEachSource(current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a node is the source expression of a foreach loop.</summary>
    /// <param name="node">The node to test.</param>
    /// <returns><see langword="true"/> when the node is what the loop walks.</returns>
    private static bool IsForEachSource(SyntaxNode node)
        => node.Parent is CommonForEachStatementSyntax forEach && forEach.Expression == node;

    /// <summary>Returns whether an invocation binds to a reduced <c>System.Linq.Enumerable</c> extension.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call really is the LINQ operator its name suggests.</returns>
    private static bool IsEnumerableExtension(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol? enumerableType,
        CancellationToken cancellationToken)
        => enumerableType is not null
            && model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>Finds the first walk that can run after an earlier one, and so re-enumerates the sequence.</summary>
    /// <param name="walks">The walks found in the body, in document order.</param>
    /// <returns>The offending second walk, or <see langword="null"/> when no two walks can both run.</returns>
    private static IdentifierNameSyntax? FindReenumeration(List<IdentifierNameSyntax> walks)
    {
        for (var i = 0; i < walks.Count; i++)
        {
            for (var j = i + 1; j < walks.Count; j++)
            {
                if (!AreMutuallyExclusive(walks[i], walks[j]))
                {
                    return walks[j];
                }
            }
        }

        return null;
    }

    /// <summary>Returns whether two walks sit on branches that cannot both run.</summary>
    /// <param name="first">The earlier walk.</param>
    /// <param name="second">The later walk.</param>
    /// <returns><see langword="true"/> when at most one of the two can execute.</returns>
    private static bool AreMutuallyExclusive(SyntaxNode first, SyntaxNode second)
    {
        if (FindCommonAncestor(first, second) is not { } ancestor
            || FindBranch(ancestor, first) is not { } firstBranch
            || FindBranch(ancestor, second) is not { } secondBranch
            || firstBranch == secondBranch)
        {
            return false;
        }

        return IsExclusiveBranching(ancestor, firstBranch, secondBranch);
    }

    /// <summary>Returns whether a node's two branches are alternatives rather than a sequence.</summary>
    /// <param name="ancestor">The node that owns both branches.</param>
    /// <param name="first">The branch holding the earlier walk.</param>
    /// <param name="second">The branch holding the later walk.</param>
    /// <returns><see langword="true"/> when the branching construct runs at most one of them.</returns>
    private static bool IsExclusiveBranching(SyntaxNode ancestor, SyntaxNode first, SyntaxNode second)
        => ancestor switch
        {
            IfStatementSyntax ifStatement => IsBranchPair(ifStatement.Statement, ifStatement.Else, first, second),
            ConditionalExpressionSyntax conditional => IsBranchPair(conditional.WhenTrue, conditional.WhenFalse, first, second),
            SwitchStatementSyntax => first is SwitchSectionSyntax && second is SwitchSectionSyntax,
            SwitchExpressionSyntax => first is SwitchExpressionArmSyntax && second is SwitchExpressionArmSyntax,
            TryStatementSyntax tryStatement => IsCatchPair(tryStatement, first, second),
            _ => false,
        };

    /// <summary>Returns whether two branches are exactly the two alternatives given, in either order.</summary>
    /// <param name="whenTrue">The first alternative.</param>
    /// <param name="whenFalse">The second alternative, when the construct has one.</param>
    /// <param name="first">The branch holding the earlier walk.</param>
    /// <param name="second">The branch holding the later walk.</param>
    /// <returns><see langword="true"/> when the branches are the two alternatives.</returns>
    private static bool IsBranchPair(SyntaxNode whenTrue, SyntaxNode? whenFalse, SyntaxNode first, SyntaxNode second)
        => whenFalse is not null
            && ((first == whenTrue && second == whenFalse) || (first == whenFalse && second == whenTrue));

    /// <summary>Returns whether one walk is in a try body and the other in a catch, which only runs on failure.</summary>
    /// <param name="tryStatement">The try statement owning both branches.</param>
    /// <param name="first">The branch holding the earlier walk.</param>
    /// <param name="second">The branch holding the later walk.</param>
    /// <returns><see langword="true"/> when the two branches are the try body and a catch clause.</returns>
    private static bool IsCatchPair(TryStatementSyntax tryStatement, SyntaxNode first, SyntaxNode second)
        => (first == tryStatement.Block && second is CatchClauseSyntax)
            || (second == tryStatement.Block && first is CatchClauseSyntax);

    /// <summary>Finds the nearest node that contains both walks.</summary>
    /// <param name="first">The earlier walk.</param>
    /// <param name="second">The later walk.</param>
    /// <returns>The common ancestor, or <see langword="null"/> when there is none.</returns>
    private static SyntaxNode? FindCommonAncestor(SyntaxNode first, SyntaxNode second)
    {
        for (var candidate = first.Parent; candidate is not null; candidate = candidate.Parent)
        {
            if (candidate.FullSpan.Contains(second.FullSpan))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Finds which direct child of the ancestor contains a walk.</summary>
    /// <param name="ancestor">The common ancestor.</param>
    /// <param name="node">The walk.</param>
    /// <returns>The ancestor's child holding the walk, or <see langword="null"/>.</returns>
    private static SyntaxNode? FindBranch(SyntaxNode ancestor, SyntaxNode node)
    {
        var current = node;
        while (current.Parent is { } parent)
        {
            if (parent == ancestor)
            {
                return current;
            }

            current = parent;
        }

        return null;
    }

    /// <summary>Tracks whether the prepass saw an IEnumerable mention.</summary>
    private record struct MentionScanState
    {
        /// <summary>Gets or sets a value indicating whether an IEnumerable identifier token was found.</summary>
        public bool Found { get; set; }
    }

    /// <summary>Collects the IEnumerable-typed local declarations of a method body.</summary>
    /// <param name="Declarations">The declarations found so far.</param>
    private record struct LocalScanState(List<LocalDeclarationStatementSyntax> Declarations);

    /// <summary>Tracks one candidate's walks while scanning the method body.</summary>
    /// <param name="Name">The candidate's name, used as a free prefilter before binding.</param>
    /// <param name="Symbol">The candidate parameter or local.</param>
    /// <param name="Model">The semantic model.</param>
    /// <param name="EnumerableType">The <c>System.Linq.Enumerable</c> type, when the compilation has LINQ.</param>
    /// <param name="Walks">The walks found so far, in document order.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct UsageScanState(
        string Name,
        ISymbol Symbol,
        SemanticModel Model,
        INamedTypeSymbol? EnumerableType,
        List<IdentifierNameSyntax> Walks,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether the candidate was rebound, dropping it from the rule.</summary>
        public bool Disqualified { get; set; }
    }
}
