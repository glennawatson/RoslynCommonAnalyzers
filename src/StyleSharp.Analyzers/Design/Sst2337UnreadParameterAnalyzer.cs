// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a parameter whose name appears nowhere in the body that declares it (SST2337). Every caller
/// computes and passes a value that is discarded, and the next reader has to prove for themselves that it
/// does not matter.
/// </summary>
/// <remarks>
/// <para>
/// The test is deliberately by name, not by binding: a parameter is reported only when its identifier
/// occurs nowhere in the body, the constructor initializer, or an expression body. That makes a use inside
/// a lambda, a local function, a <c>nameof</c>, or a string interpolation count automatically, and it makes
/// a shadowed name a silence rather than a false report. It also means the clean path never touches the
/// semantic model.
/// </para>
/// <para>
/// What is skipped is what the author is not free to change. A signature fixed from outside — an override,
/// a virtual or abstract member, an interface implementation, a partial or extern member, an attribute
/// constructor, an event-handler or framework callback shape, a method handed on as a method group — would
/// stop compiling, or stop being called, if the parameter went. A body that only throws is a deliberate
/// stub. Externally visible members are excluded unless
/// <c>stylesharp.SST2337.unread_parameter_include_public_api</c> says otherwise, since removing a parameter
/// there breaks callers in other assemblies.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2337UnreadParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Parameter types whose presence means a framework, not the author, chose the signature.</summary>
    private static readonly string[] CallbackParameterTypes =
    [
        "EventArgs",
        "StreamingContext",
        "DependencyPropertyChangedEventArgs",
        "DependencyObject",
        "AvaloniaPropertyChangedEventArgs",
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.UnreadParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var optionsByTree = new ConcurrentDictionary<SyntaxTree, UnreadParameterOptions>();
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, optionsByTree),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.LocalFunctionStatement);
        });
    }

    /// <summary>Reports each parameter of one member that the member never reads.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, UnreadParameterOptions> optionsByTree)
    {
        var parameters = GetParameters(context.Node);
        if (parameters.Count == 0 || HasFixedSignature(context.Node) || GetBody(context.Node) is not { } body)
        {
            return;
        }

        if (OnlyThrows(body) || DeclaresACallbackShape(parameters))
        {
            return;
        }

        var mentioned = CollectMentionedNames(context.Node, body);
        for (var i = 0; i < parameters.Count; i++)
        {
            AnalyzeParameter(context, optionsByTree, parameters[i], mentioned);
        }
    }

    /// <summary>Reports one parameter that the body never mentions.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <param name="parameter">The parameter to inspect.</param>
    /// <param name="mentioned">The identifiers the body mentions.</param>
    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, UnreadParameterOptions> optionsByTree,
        ParameterSyntax parameter,
        HashSet<string> mentioned)
    {
        var name = parameter.Identifier.ValueText;
        if (name.Length == 0 || name == "_" || mentioned.Contains(name))
        {
            return;
        }

        // Everything below needs the model, and only a parameter that is already unmentioned gets here.
        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { } symbol
            || IsDeclaredBySomethingElse(symbol))
        {
            return;
        }

        if (!GetOptions(context, optionsByTree).IncludePublicApi && IsExternallyVisible(symbol.ContainingSymbol))
        {
            return;
        }

        if (IsUsedAsAMethodGroup(context.Node, symbol.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.UnreadParameter, parameter.Identifier.GetLocation(), name));
    }

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

    /// <summary>Gets a declaration's parameters.</summary>
    /// <param name="node">The member declaration.</param>
    /// <returns>The parameter list, or an empty list.</returns>
    private static SeparatedSyntaxList<ParameterSyntax> GetParameters(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
        LocalFunctionStatementSyntax local => local.ParameterList.Parameters,
        _ => default,
    };

    /// <summary>Gets the node holding a declaration's executable body.</summary>
    /// <param name="node">The member declaration.</param>
    /// <returns>The body, or <see langword="null"/> when the declaration has none.</returns>
    private static SyntaxNode? GetBody(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
        LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody,
        _ => null,
    };

    /// <summary>Returns whether something other than this declaration decides its parameter list.</summary>
    /// <param name="node">The member declaration.</param>
    /// <returns><see langword="true"/> when the signature may not be changed here.</returns>
    private static bool HasFixedSignature(SyntaxNode node)
    {
        var modifiers = node switch
        {
            MemberDeclarationSyntax member => member.Modifiers,
            LocalFunctionStatementSyntax local => local.Modifiers,
            _ => default,
        };

        return HasFixedModifier(modifiers) || ImplementsAnInterfaceExplicitly(node) || node.Parent is InterfaceDeclarationSyntax;
    }

    /// <summary>Returns whether a modifier list marks a signature that answers to something else.</summary>
    /// <param name="modifiers">The declaration's modifiers.</param>
    /// <returns><see langword="true"/> for an inherited, partial, or external contract.</returns>
    private static bool HasFixedModifier(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.OverrideKeyword
                or SyntaxKind.VirtualKeyword
                or SyntaxKind.AbstractKeyword
                or SyntaxKind.PartialKeyword
                or SyntaxKind.ExternKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a declaration names the interface member it implements.</summary>
    /// <param name="node">The member declaration.</param>
    /// <returns><see langword="true"/> when an explicit interface specifier is present.</returns>
    private static bool ImplementsAnInterfaceExplicitly(SyntaxNode node)
        => node is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier is not null;

    /// <summary>Returns whether a body does nothing but throw, which is a deliberate stub.</summary>
    /// <param name="body">The member's body.</param>
    /// <returns><see langword="true"/> for a body whose only statement is a throw.</returns>
    private static bool OnlyThrows(SyntaxNode body) => body switch
    {
        BlockSyntax { Statements: [ThrowStatementSyntax] } => true,
        ArrowExpressionClauseSyntax { Expression: ThrowExpressionSyntax } => true,
        _ => false,
    };

    /// <summary>Returns whether a parameter list is a framework callback shape rather than one the author chose.</summary>
    /// <param name="parameters">The declared parameters.</param>
    /// <returns><see langword="true"/> when a well-known callback parameter type is present.</returns>
    /// <remarks>
    /// An event handler, a serialization callback and a dependency-property callback all take their shape
    /// from a delegate the framework declares. The parameter that goes unread there is the price of matching
    /// the delegate, not a mistake, and removing it would stop the method being usable at all.
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

    /// <summary>Collects every identifier the member's body and initializer mention.</summary>
    /// <param name="node">The member declaration.</param>
    /// <param name="body">The member's body.</param>
    /// <returns>The set of mentioned identifier names.</returns>
    private static HashSet<string> CollectMentionedNames(SyntaxNode node, SyntaxNode body)
    {
        var scan = new NameScan(new HashSet<string>(StringComparer.Ordinal));
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, NameScan>(body, ref scan, VisitName);

        // A constructor may read its parameters in the base or this initializer, which is outside the body.
        if (node is ConstructorDeclarationSyntax { Initializer: { } initializer })
        {
            DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, NameScan>(initializer, ref scan, VisitName);
        }

        return scan.Names;
    }

    /// <summary>Records one mentioned identifier.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns>Always <see langword="true"/>, so the whole body is scanned.</returns>
    private static bool VisitName(IdentifierNameSyntax identifier, ref NameScan state)
    {
        state.Names.Add(identifier.Identifier.ValueText);
        return true;
    }

    /// <summary>Returns whether something other than this declaration put the parameter there.</summary>
    /// <param name="parameter">The parameter symbol.</param>
    /// <returns><see langword="true"/> when the parameter is not the author's to remove here.</returns>
    private static bool IsDeclaredBySomethingElse(IParameterSymbol parameter)
        => parameter.IsThis || IsExtensionReceiver(parameter) || IsBoundByAContract(parameter);

    /// <summary>Returns whether a parameter is the receiver an extension member extends.</summary>
    /// <param name="parameter">The parameter symbol.</param>
    /// <returns><see langword="true"/> for the first parameter of an extension method.</returns>
    /// <remarks>
    /// The receiver is what makes the member an extension at all, so it cannot be removed the way this rule
    /// asks. A receiver the body never reads is its own defect and belongs to SST1708, which says to make the
    /// member static instead; reporting it here as well would be two rules asking for opposite edits.
    /// </remarks>
    private static bool IsExtensionReceiver(IParameterSymbol parameter)
        => parameter.Ordinal == 0 && parameter.ContainingSymbol is IMethodSymbol { IsExtensionMethod: true };

    /// <summary>Returns whether an interface or an attribute's usage fixes the declaring member's signature.</summary>
    /// <param name="parameter">The parameter symbol.</param>
    /// <returns><see langword="true"/> when the signature answers to something outside the member.</returns>
    private static bool IsBoundByAContract(IParameterSymbol parameter)
    {
        if (parameter.ContainingSymbol is not IMethodSymbol method)
        {
            return true;
        }

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
    /// <param name="member">The declaring member symbol.</param>
    /// <returns><see langword="true"/> when the name is used other than as a call.</returns>
    /// <remarks>
    /// A method assigned to a delegate — an event subscription, a callback registration — has the delegate's
    /// signature, not one of its own, so a parameter it never reads still has to be there. The search is
    /// bounded to the declaring type, which is where a member this rule can report is reachable from.
    /// </remarks>
    private static bool IsUsedAsAMethodGroup(SyntaxNode node, ISymbol member)
    {
        if (node.FirstAncestorOrSelf<TypeDeclarationSyntax>() is not { } declaringType)
        {
            return false;
        }

        var scan = new MethodGroupScan(member.Name);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, MethodGroupScan>(declaringType, ref scan, VisitMethodGroup);
        return scan.Found;
    }

    /// <summary>Records a use of the member's name that is not a call.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a method-group use is found.</returns>
    private static bool VisitMethodGroup(IdentifierNameSyntax identifier, ref MethodGroupScan state)
    {
        if (identifier.Identifier.ValueText != state.Name)
        {
            return true;
        }

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

        state.Found = true;
        return false;
    }

    /// <summary>The state threaded through a body name scan.</summary>
    /// <param name="Names">The identifiers seen so far.</param>
    private readonly record struct NameScan(HashSet<string> Names);

    /// <summary>The state threaded through a method-group scan.</summary>
    /// <param name="Name">The member name to look for.</param>
    private record struct MethodGroupScan(string Name)
    {
        /// <summary>Gets or sets a value indicating whether a method-group use was found.</summary>
        public bool Found { get; set; }
    }
}
