// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a parameter whose name appears nowhere in the body that declares it (SST1461). Every caller
/// computes and passes a value that is discarded, and the next reader has to prove for themselves that it
/// does not matter.
/// </summary>
/// <remarks>
/// <para>
/// What is skipped is what the author is not free to change: an override, a virtual or abstract member, an
/// interface implementation, a partial or extern member, an attribute constructor, an event-handler or
/// framework callback shape, and a method handed on as a method group. A body that only throws is a
/// deliberate stub. Externally visible members are excluded unless
/// <c>stylesharp.SST1461.unread_parameter_include_public_api</c> says otherwise, since removing a parameter
/// there breaks callers in other assemblies.
/// </para>
/// <para>
/// The scan is syntactic and bounded by a 64-parameter bitmask, and it stops as soon as every parameter has
/// been seen. Accessibility comes from the containing symbol the driver already supplies; the semantic model
/// is consulted only for the two-parameter object-first shape, and the method-group and contract checks run
/// only once a parameter has already been found unread.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1461UnusedParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The largest parameter count tracked by the bitmask scan.</summary>
    private const int MaximumTrackedParameters = 64;

    /// <summary>Parameter types whose presence means a framework, not the author, chose the signature.</summary>
    /// <remarks>
    /// Plain <c>EventArgs</c> is deliberately absent: the conventional handler is recognised by its
    /// <c>(object, EventArgs)</c> pair instead, so a method that merely happens to take an
    /// <c>EventArgs</c> alongside an unrelated parameter is still reported.
    /// </remarks>
    private static readonly string[] CallbackParameterTypes =
    [
        "StreamingContext",
        "DependencyPropertyChangedEventArgs",
        "DependencyObject",
        "AvaloniaPropertyChangedEventArgs",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveUnusedPrivateParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var optionsByTree = new ConcurrentDictionary<SyntaxTree, UnreadParameterOptions>();
            var methodGroupNamesByType = new ConcurrentDictionary<TypeDeclarationSyntax, HashSet<string>>();
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeMember(nodeContext, optionsByTree, methodGroupNamesByType),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration);
            start.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
        });
    }

    /// <summary>Reports unread parameters on a method or constructor declaration.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="methodGroupNamesByType">The per-type method-group name cache.</param>
    private static void AnalyzeMember(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnreadParameterOptions> optionsByTree,
        ConcurrentDictionary<TypeDeclarationSyntax, HashSet<string>> methodGroupNamesByType)
    {
        // Cheapest first: pure syntax, then accessibility from the symbol the driver already has, and only
        // then the one semantic probe. A member this rule may not report never reaches the model at all.
        var member = (BaseMethodDeclarationSyntax)context.Node;
        if (member.ParameterList.Parameters.Count == 0 || HasExemptShape(member))
        {
            return;
        }

        if (context.ContainingSymbol is not IMethodSymbol method
            || (!GetOptions(context, optionsByTree).IncludePublicApi && IsExternallyVisible(method))
            || IsEventHandler(member, context))
        {
            return;
        }

        var body = member.Body ?? (SyntaxNode)member.ExpressionBody!;
        var initializer = (member as ConstructorDeclarationSyntax)?.Initializer;
        AnalyzeParameters(context, member.ParameterList, body, initializer, method, methodGroupNamesByType);
    }

    /// <summary>Returns whether a declaration's shape puts its parameter list beyond the author's control.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the declaration should not be reported at all.</returns>
    /// <remarks>Syntactic only, so the common no-diagnostic path never touches the semantic model.</remarks>
    private static bool HasExemptShape(BaseMethodDeclarationSyntax member)
        => (member.Body is null && member.ExpressionBody is null)
            || member.AttributeLists.Count > 0
            || HasArityOrDispatchModifier(member.Modifiers)
            || member.Parent is InterfaceDeclarationSyntax
            || ImplementsAnInterfaceExplicitly(member)
            || OnlyThrows(member)
            || DeclaresACallbackShape(member.ParameterList.Parameters);

    /// <summary>Reports unread parameters on a local function.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>A local function is never API surface, so it needs neither the option nor an accessibility test.</remarks>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (localFunction.ParameterList.Parameters.Count == 0
            || (localFunction.Body is null && localFunction.ExpressionBody is null)
            || localFunction.AttributeLists.Count > 0
            || HasArityOrDispatchModifier(localFunction.Modifiers)
            || OnlyThrowsBody(localFunction.Body, localFunction.ExpressionBody)
            || DeclaresACallbackShape(localFunction.ParameterList.Parameters))
        {
            return;
        }

        var body = localFunction.Body ?? (SyntaxNode)localFunction.ExpressionBody!;
        AnalyzeParameters(context, localFunction.ParameterList, body, initializer: null, member: null, methodGroupNamesByType: null);
    }

    /// <summary>Scans a body for parameter identifier reads and reports the unread parameters.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="parameterList">The parameter list.</param>
    /// <param name="body">The declaration body.</param>
    /// <param name="initializer">The constructor initializer, when present.</param>
    /// <param name="member">The declaring member, or <see langword="null"/> for a local function.</param>
    /// <param name="methodGroupNamesByType">The per-type method-group name cache, or <see langword="null"/>.</param>
    private static void AnalyzeParameters(
        SyntaxNodeAnalysisContext context,
        ParameterListSyntax parameterList,
        SyntaxNode body,
        ConstructorInitializerSyntax? initializer,
        IMethodSymbol? member,
        ConcurrentDictionary<TypeDeclarationSyntax, HashSet<string>>? methodGroupNamesByType)
    {
        var parameters = parameterList.Parameters;
        if (parameters.Count > MaximumTrackedParameters)
        {
            return;
        }

        var state = new ParameterScanState(parameters, parameterList.Span);
        DescendantTraversalHelper.VisitDescendantTokens(body, ref state, static (in SyntaxToken token, ref ParameterScanState scan) => scan.Observe(token));

        // A constructor may read its parameters in the base or this initializer, which is outside the body.
        if (initializer is not null)
        {
            DescendantTraversalHelper.VisitDescendantTokens(initializer, ref state, static (in SyntaxToken token, ref ParameterScanState scan) => scan.Observe(token));
        }

        var contractChecked = false;
        for (var i = 0; i < parameters.Count; i++)
        {
            var identifier = parameters[i].Identifier;
            if (state.IsSeen(i) || identifier.ValueText == "_" || parameters[i].Modifiers.Count > 0)
            {
                continue;
            }

            // The interface walk and the method-group scan are the expensive tests, so only a declaration
            // that already has something to report pays for them, and only once.
            if (!contractChecked)
            {
                contractChecked = true;
                if (IsSignatureFixedElsewhere(context.Node, member, methodGroupNamesByType))
                {
                    return;
                }
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.RemoveUnusedPrivateParameter,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Returns whether an interface, an attribute, or a delegate fixes the declaring member's signature.</summary>
    /// <param name="node">The member declaration.</param>
    /// <param name="member">The declaring member, or <see langword="null"/> for a local function.</param>
    /// <param name="cache">The per-type method-group name cache, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the parameter list is not the author's to change here.</returns>
    private static bool IsSignatureFixedElsewhere(
        SyntaxNode node,
        IMethodSymbol? member,
        ConcurrentDictionary<TypeDeclarationSyntax, HashSet<string>>? cache)
        => member is not null && (IsBoundByAContract(member) || IsUsedAsAMethodGroup(node, member.Name, cache));

    /// <summary>Reads the settings for the member's tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static UnreadParameterOptions GetOptions(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnreadParameterOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = UnreadParameterOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Returns whether a method shape should not have parameters removed locally.</summary>
    /// <param name="modifiers">The declaration modifiers.</param>
    /// <returns><see langword="true"/> when the signature may be consumed indirectly.</returns>
    private static bool HasArityOrDispatchModifier(SyntaxTokenList modifiers)
        => ModifierListHelper.Contains(modifiers, SyntaxKind.PartialKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.VirtualKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.AbstractKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.ExternKeyword);

    /// <summary>Returns whether a declaration names the interface member it implements.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when an explicit interface specifier is present.</returns>
    private static bool ImplementsAnInterfaceExplicitly(BaseMethodDeclarationSyntax member)
        => member is MethodDeclarationSyntax { ExplicitInterfaceSpecifier: not null };

    /// <summary>Returns whether a member's body does nothing but throw, which is a deliberate stub.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> for a body whose only statement is a throw.</returns>
    private static bool OnlyThrows(BaseMethodDeclarationSyntax member)
        => OnlyThrowsBody(member.Body, member.ExpressionBody);

    /// <summary>Returns whether a block or expression body does nothing but throw.</summary>
    /// <param name="body">The block body, when present.</param>
    /// <param name="expressionBody">The expression body, when present.</param>
    /// <returns><see langword="true"/> for a body whose only statement is a throw.</returns>
    private static bool OnlyThrowsBody(BlockSyntax? body, ArrowExpressionClauseSyntax? expressionBody)
        => body is { Statements: [ThrowStatementSyntax] }
            || expressionBody is { Expression: ThrowExpressionSyntax };

    /// <summary>Returns whether a parameter list is a framework callback shape rather than one the author chose.</summary>
    /// <param name="parameters">The declared parameters.</param>
    /// <returns><see langword="true"/> when a well-known callback parameter type is present.</returns>
    /// <remarks>
    /// A serialization callback and a dependency-property callback take their shape from a delegate the
    /// framework declares. The parameter that goes unread there is the price of matching the delegate, not a
    /// mistake, and removing it would stop the method being usable at all.
    /// </remarks>
    private static bool DeclaresACallbackShape(SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Type is { } type && IsCallbackTypeName(type))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type syntax names one of the framework callback types.</summary>
    /// <param name="type">The declared type syntax.</param>
    /// <returns><see langword="true"/> when the written name ends with a callback type name.</returns>
    private static bool IsCallbackTypeName(TypeSyntax type)
    {
        var name = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
        {
            return false;
        }

        for (var i = 0; i < CallbackParameterTypes.Length; i++)
        {
            if (name.EndsWith(CallbackParameterTypes[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method matches the conventional event-handler signature.</summary>
    /// <param name="member">The member declaration.</param>
    /// <param name="context">The syntax node context supplying the semantic model.</param>
    /// <returns><see langword="true"/> for a two-parameter <c>(object, EventArgs)</c> method whose parameters the delegate fixes.</returns>
    private static bool IsEventHandler(BaseMethodDeclarationSyntax member, SyntaxNodeAnalysisContext context)
    {
        var parameters = member.ParameterList.Parameters;
        if (parameters.Count != 2 || !IsObjectType(parameters[0].Type) || parameters[1].Type is not { } secondType)
        {
            return false;
        }

        return InheritsFromEventArgs(context.SemanticModel.GetTypeInfo(secondType, context.CancellationToken).Type);
    }

    /// <summary>Returns whether a type syntax is <c>object</c> or <c>object?</c>.</summary>
    /// <param name="type">The type syntax.</param>
    /// <returns><see langword="true"/> when the type is the object keyword.</returns>
    private static bool IsObjectType(TypeSyntax? type)
    {
        if (type is NullableTypeSyntax nullable)
        {
            type = nullable.ElementType;
        }

        return type is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword);
    }

    /// <summary>Returns whether a type is <c>System.EventArgs</c> or derives from it.</summary>
    /// <param name="type">The candidate type.</param>
    /// <returns><see langword="true"/> when the type is an EventArgs subtype.</returns>
    private static bool InheritsFromEventArgs(ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.Name == "EventArgs"
                && current.ContainingNamespace is { Name: "System", ContainingNamespace.IsGlobalNamespace: true })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether an interface or an attribute's usage fixes the declaring member's signature.</summary>
    /// <param name="method">The declaring method.</param>
    /// <returns><see langword="true"/> when the signature answers to something outside the member.</returns>
    private static bool IsBoundByAContract(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        return IsAttributeType(containingType) || ImplementsInterfaceMember(method, containingType);
    }

    /// <summary>Returns whether a type derives from <see cref="Attribute"/>.</summary>
    /// <param name="type">The containing type.</param>
    /// <returns><see langword="true"/> for an attribute class.</returns>
    private static bool IsAttributeType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is { Name: "Attribute", ContainingNamespace.Name: "System" })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a method implicitly implements an interface member.</summary>
    /// <param name="method">The declaring method.</param>
    /// <param name="containingType">The method's containing type.</param>
    /// <returns><see langword="true"/> when an interface dictates the signature.</returns>
    private static bool ImplementsInterfaceMember(IMethodSymbol method, INamedTypeSymbol containingType)
    {
        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var candidates = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < candidates.Length; j++)
            {
                if (SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidates[j]), method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a symbol can be seen from outside the assembly that declares it.</summary>
    /// <param name="symbol">The member that declares the parameter.</param>
    /// <returns><see langword="true"/> when removing a parameter is a break for consumers.</returns>
    private static bool IsExternallyVisible(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current is INamespaceSymbol)
            {
                break;
            }

            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether the declaring member's name is handed on as a method group.</summary>
    /// <param name="node">The member declaration.</param>
    /// <param name="memberName">The declaring member's name.</param>
    /// <param name="cache">The per-type method-group name cache, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the name is used other than as a call.</returns>
    /// <remarks>
    /// A method assigned to a delegate — an event subscription, a callback registration — has the delegate's
    /// signature, not one of its own, so a parameter it never reads still has to be there. The search is
    /// bounded to the declaring type, which is where a member this rule can report is reachable from.
    /// The names are collected once per type: scanning the type per reporting member is quadratic, and a
    /// type where many members are reported is exactly the case that made it hurt.
    /// </remarks>
    private static bool IsUsedAsAMethodGroup(
        SyntaxNode node,
        string memberName,
        ConcurrentDictionary<TypeDeclarationSyntax, HashSet<string>>? cache)
    {
        if (cache is null || node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaringType)
        {
            return false;
        }

        return cache.GetOrAdd(declaringType, static type => CollectMethodGroupNames(type)).Contains(memberName);
    }

    /// <summary>Collects every name a type hands on as a method group rather than calling.</summary>
    /// <param name="declaringType">The type declaration to scan.</param>
    /// <returns>The set of names used other than as a call.</returns>
    private static HashSet<string> CollectMethodGroupNames(TypeDeclarationSyntax declaringType)
    {
        var scan = new MethodGroupScan(new HashSet<string>(StringComparer.Ordinal));
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, MethodGroupScan>(declaringType, ref scan, VisitMethodGroup);
        return scan.Names;
    }

    /// <summary>Records a name used somewhere other than the callee position of a call.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns>Always <see langword="true"/>, so the whole type is scanned.</returns>
    private static bool VisitMethodGroup(IdentifierNameSyntax identifier, ref MethodGroupScan state)
    {
        // 'M(...)' and 'x.M(...)' are calls; anything else that names M hands the method itself on.
        var invoked = identifier.Parent is InvocationExpressionSyntax invocation && invocation.Expression == identifier;
        var invokedThroughAccess = identifier.Parent is MemberAccessExpressionSyntax access
            && access.Name == identifier
            && access.Parent is InvocationExpressionSyntax accessInvocation
            && accessInvocation.Expression == access;

        if (invoked || invokedThroughAccess)
        {
            return true;
        }

        state.Names.Add(identifier.Identifier.ValueText);
        return true;
    }

    /// <summary>The state threaded through a method-group scan.</summary>
    /// <param name="Names">The names seen outside a callee position.</param>
    private readonly record struct MethodGroupScan(HashSet<string> Names);

    /// <summary>Tracks parameters read by identifier token.</summary>
    private struct ParameterScanState : IEquatable<ParameterScanState>
    {
        /// <summary>The parameter list.</summary>
        private readonly SeparatedSyntaxList<ParameterSyntax> _parameters;

        /// <summary>The parameter-list span excluded from usage.</summary>
        private readonly TextSpan _parameterListSpan;

        /// <summary>The bitmask of seen parameters.</summary>
        private ulong _seenMask;

        /// <summary>The remaining unread parameter count.</summary>
        private int _remaining;

        /// <summary>Initializes a new instance of the <see cref="ParameterScanState"/> struct.</summary>
        /// <param name="parameters">The parameter list.</param>
        /// <param name="parameterListSpan">The parameter-list span.</param>
        public ParameterScanState(SeparatedSyntaxList<ParameterSyntax> parameters, TextSpan parameterListSpan)
        {
            _parameters = parameters;
            _parameterListSpan = parameterListSpan;
            _seenMask = 0;
            _remaining = parameters.Count;
        }

        /// <summary>Returns whether a parameter has been read.</summary>
        /// <param name="index">The parameter index.</param>
        /// <returns><see langword="true"/> when the parameter is seen.</returns>
        public readonly bool IsSeen(int index) => (_seenMask & (1UL << index)) != 0;

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(ParameterScanState other) => _seenMask == other._seenMask && _remaining == other._remaining;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is ParameterScanState other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked(((int)_seenMask * 397) ^ _remaining);

        /// <summary>Observes one token and returns whether scanning should continue.</summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="false"/> once every parameter has been seen.</returns>
        public bool Observe(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || _parameterListSpan.Contains(token.Span))
            {
                return _remaining > 0;
            }

            var text = token.ValueText;
            for (var i = 0; i < _parameters.Count; i++)
            {
                if (!IsSeen(i) && _parameters[i].Identifier.ValueText == text)
                {
                    _seenMask |= 1UL << i;
                    _remaining--;
                    break;
                }
            }

            return _remaining > 0;
        }
    }
}
