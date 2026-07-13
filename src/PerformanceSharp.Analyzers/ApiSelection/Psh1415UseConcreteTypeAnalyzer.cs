// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a local or private field declared as an interface that only ever holds one concrete type
/// (PSH1415). Every call through the interface is a virtual dispatch the JIT cannot inline;
/// declaring the concrete type turns those calls direct and lets the JIT see through them.
/// <para>
/// The rule stays away from anything that is part of an API. Only locals and <c>private</c> fields
/// are considered, never a parameter, a return type, or a visible field — those are contracts, and
/// widening them is not a performance decision. A candidate qualifies only when every value it is
/// ever given is a <c>new</c> of the <em>same</em> concrete type, so a variable that is reassigned
/// from a factory, or from a second implementation, is left alone.
/// </para>
/// <para>
/// Two things would silently break the rewrite, and both are excluded. A candidate used as a
/// <c>ref</c> or <c>out</c> argument is skipped, because the argument type must match the
/// parameter exactly. A candidate that <em>calls</em> a member the concrete type implements
/// explicitly is skipped too, since such a member is reachable only through the interface. That
/// test is made per call, not per type: <c>List&lt;T&gt;</c> hides
/// <c>ICollection&lt;T&gt;.IsReadOnly</c> behind an explicit implementation and is still a
/// perfectly good declaration for code that only ever calls <c>Add</c> and <c>Count</c>. A private
/// field in a type declared across several files is skipped as well, since an assignment in
/// another file cannot be seen from here.
/// </para>
/// <para>
/// A candidate nothing is ever dispatched through is left alone, because there is no call to make
/// direct and therefore nothing to win. The archetype is a sentinel: a value that exists only to be
/// compared by reference and swapped in by <c>Interlocked.Exchange</c> never has a member invoked on
/// it, so naming its concrete type would be churn rather than a speed-up. The rule therefore reports
/// only where the symbol actually carries a dispatch — a member access, an indexer, or a
/// <c>foreach</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1415UseConcreteTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.UseConcreteType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports PSH1415 for a local that only ever holds one concrete type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context)
    {
        var declaration = (LocalDeclarationStatementSyntax)context.Node;
        if (declaration.IsConst
            || declaration.Declaration.Variables.Count != 1
            || declaration.Declaration.Type.IsVar
            || FindScope(declaration) is not { } scope)
        {
            return;
        }

        var variable = declaration.Declaration.Variables[0];
        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not ILocalSymbol symbol)
        {
            return;
        }

        ReportIfSingleConcreteType(context, declaration.Declaration, scope, symbol, symbol.Type);
    }

    /// <summary>Reports PSH1415 for a private field that only ever holds one concrete type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var declaration = (FieldDeclarationSyntax)context.Node;
        if (!declaration.Modifiers.Any(SyntaxKind.PrivateKeyword)
            || declaration.Modifiers.Any(SyntaxKind.ConstKeyword)
            || declaration.Declaration.Variables.Count != 1
            || declaration.Parent is not TypeDeclarationSyntax scope)
        {
            return;
        }

        var variable = declaration.Declaration.Variables[0];
        if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol symbol
            || symbol.ContainingType.DeclaringSyntaxReferences.Length != 1)
        {
            return;
        }

        ReportIfSingleConcreteType(context, declaration.Declaration, scope, symbol, symbol.Type);
    }

    /// <summary>Reports the declaration when its interface type only ever holds one concrete type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="declaration">The variable declaration carrying the type syntax to rewrite.</param>
    /// <param name="scope">The syntax to scan for every value the symbol is given.</param>
    /// <param name="symbol">The declared local or field.</param>
    /// <param name="declaredType">The symbol's declared type.</param>
    private static void ReportIfSingleConcreteType(
        SyntaxNodeAnalysisContext context,
        VariableDeclarationSyntax declaration,
        SyntaxNode scope,
        ISymbol symbol,
        ITypeSymbol declaredType)
    {
        if (declaredType is not INamedTypeSymbol { TypeKind: TypeKind.Interface } interfaceType
            || TryGetCreatedType(context, declaration.Variables[0]) is not { } concrete
            || !CanNarrowProfitably(context, scope, symbol, concrete))
        {
            return;
        }

        var position = declaration.Type.SpanStart;
        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseConcreteType,
            declaration.Type.GetLocation(),
            symbol.Name,
            interfaceType.ToMinimalDisplayString(context.SemanticModel, position),
            concrete.ToMinimalDisplayString(context.SemanticModel, position)));
    }

    /// <summary>Returns the concrete type a variable's initializer constructs.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="variable">The declared variable.</param>
    /// <returns>The constructed type, or <see langword="null"/> when the initializer is not a plain <c>new</c>.</returns>
    private static INamedTypeSymbol? TryGetCreatedType(SyntaxNodeAnalysisContext context, VariableDeclaratorSyntax variable)
        => variable.Initializer?.Value is ObjectCreationExpressionSyntax creation
            && context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } concrete
            ? concrete
            : null;

    /// <summary>Returns whether the declaration can be narrowed safely, and is worth narrowing at all.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="scope">The syntax to scan.</param>
    /// <param name="symbol">The declared local or field.</param>
    /// <param name="concrete">The concrete type the initializer constructed.</param>
    /// <returns><see langword="true"/> when narrowing is safe and turns at least one call direct.</returns>
    private static bool CanNarrowProfitably(SyntaxNodeAnalysisContext context, SyntaxNode scope, ISymbol symbol, INamedTypeSymbol concrete)
    {
        var state = new HolderScanState(symbol, concrete, context.SemanticModel, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, HolderScanState>(scope, ref state, VisitUsage);
        return !state.Disqualified && state.Dispatched;
    }

    /// <summary>Classifies one usage of the candidate: a rebinding to something else, a ref/out escape, or a dispatch.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once disqualified.</returns>
    private static bool VisitUsage(IdentifierNameSyntax identifier, ref HolderScanState state)
    {
        if (identifier.Identifier.ValueText != state.Symbol.Name
            || !SymbolEqualityComparer.Default.Equals(
                state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol,
                state.Symbol))
        {
            return true;
        }

        if (IsDispatchReceiver(identifier))
        {
            state.Dispatched = true;
        }

        if (!IsRefOrOutArgument(identifier)
            && !IsAssignedSomethingElse(identifier, ref state)
            && !ReachesExplicitImplementation(identifier, ref state))
        {
            return true;
        }

        state.Disqualified = true;
        return false;
    }

    /// <summary>Returns whether a usage dispatches through the candidate's declared interface.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <returns><see langword="true"/> when the usage is a member access, an indexer, or a <c>foreach</c> source.</returns>
    /// <remarks>
    /// These are the three ways a call can go through the interface, and so the only three that a
    /// concrete declaration could turn direct. Every other use — comparing the symbol, passing it,
    /// returning it, assigning it — dispatches nothing and gains nothing.
    /// </remarks>
    private static bool IsDispatchReceiver(IdentifierNameSyntax identifier)
        => identifier.Parent switch
        {
            MemberAccessExpressionSyntax access => access.Expression == identifier,
            ElementAccessExpressionSyntax element => element.Expression == identifier,
            ForEachStatementSyntax forEach => forEach.Expression == identifier,
            _ => false,
        };

    /// <summary>Returns whether a usage calls a member the concrete type implements explicitly.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> when the member is reachable only through the interface, so narrowing would hide it.</returns>
    private static bool ReachesExplicitImplementation(IdentifierNameSyntax identifier, ref HolderScanState state)
    {
        if (identifier.Parent is not MemberAccessExpressionSyntax access || access.Expression != identifier)
        {
            return false;
        }

        // Only a member reached *through this variable* matters. A concrete type may implement
        // plenty of the interface explicitly and still be a fine declaration — List<T> hides
        // ICollection<T>.IsReadOnly, and narrowing to List<T> is still correct for code that
        // only ever calls Add and Count.
        return state.Model.GetSymbolInfo(access, state.CancellationToken).Symbol is { ContainingType.TypeKind: TypeKind.Interface } member
            && state.Concrete.FindImplementationForInterfaceMember(member) is { } implementation
            && IsExplicit(implementation);
    }

    /// <summary>Returns whether a usage passes the candidate by reference, which pins its declared type.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <returns><see langword="true"/> when the usage is a ref, out, or in argument.</returns>
    private static bool IsRefOrOutArgument(IdentifierNameSyntax identifier)
        => identifier.Parent is ArgumentSyntax argument && !argument.RefKindKeyword.IsKind(SyntaxKind.None);

    /// <summary>Returns whether a usage assigns the candidate anything other than a <c>new</c> of its concrete type.</summary>
    /// <param name="identifier">The identifier usage.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> when the assignment would not fit the narrowed declaration.</returns>
    private static bool IsAssignedSomethingElse(IdentifierNameSyntax identifier, ref HolderScanState state)
    {
        if (identifier.Parent is not AssignmentExpressionSyntax assignment || assignment.Left != identifier)
        {
            return false;
        }

        return assignment.Right is not ObjectCreationExpressionSyntax creation
            || state.Model.GetTypeInfo(creation, state.CancellationToken).Type is not { } assigned
            || !SymbolEqualityComparer.Default.Equals(assigned, state.Concrete);
    }

    /// <summary>Returns whether an implementing member is an explicit interface implementation.</summary>
    /// <param name="implementation">The implementing member.</param>
    /// <returns><see langword="true"/> when the member is only reachable through the interface.</returns>
    private static bool IsExplicit(ISymbol implementation)
        => implementation switch
        {
            IMethodSymbol method => !method.ExplicitInterfaceImplementations.IsEmpty,
            IPropertySymbol property => !property.ExplicitInterfaceImplementations.IsEmpty,
            IEventSymbol @event => !@event.ExplicitInterfaceImplementations.IsEmpty,
            _ => false,
        };

    /// <summary>Finds the member or function body that bounds a local's usages.</summary>
    /// <param name="node">The local declaration.</param>
    /// <returns>The enclosing declaration to scan, or <see langword="null"/>.</returns>
    private static SyntaxNode? FindScope(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Tracks one candidate while scanning for the values it is given.</summary>
    /// <param name="Symbol">The declared local or field.</param>
    /// <param name="Concrete">The concrete type the initializer constructed.</param>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct HolderScanState(
        ISymbol Symbol,
        INamedTypeSymbol Concrete,
        SemanticModel Model,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether the candidate cannot be narrowed after all.</summary>
        public bool Disqualified { get; set; }

        /// <summary>Gets or sets a value indicating whether at least one call goes through the candidate.</summary>
        public bool Dispatched { get; set; }
    }
}
