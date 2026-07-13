// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a private instance method or property that never reads <c>this</c> (PSH1414). The
/// receiver is still passed at every call as a hidden argument, and the JIT must prove it
/// non-null before it can dispatch; <c>static</c> says what the member actually is and lets the
/// call go direct.
/// <para>
/// The rule is deliberately conservative about what it will touch. Only <c>private</c> members are
/// reported: making a visible member static is a breaking change to an API and a decision the
/// analyzer has no business making, whereas a private member's call sites are all inside its own
/// type. Anything <c>virtual</c>, <c>abstract</c>, or <c>override</c> is bound to instance
/// dispatch by definition. Any member carrying an attribute is skipped, as is every member of a
/// type carrying one, because serialization, dependency injection, and test frameworks all reach
/// members by reflection and an attribute is the only hint of it visible from here. An
/// auto-property is skipped too — it <em>is</em> instance state.
/// </para>
/// <para>
/// A member "uses instance state" when it mentions <c>this</c> or <c>base</c>, or names any
/// non-static member of its own type or a base type. A call to itself does not count: an
/// unqualified recursive call still binds once the member is static.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1414MarkMembersStaticAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ApiSelectionRules.MarkMembersStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeMember, SyntaxKind.MethodDeclaration, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Returns whether a member's modifiers and attributes even allow it to become static.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is a plain private instance member.</returns>
    internal static bool IsEligibleDeclaration(MemberDeclarationSyntax member)
    {
        if (member.AttributeLists.Count > 0 || member.Parent is not TypeDeclarationSyntax)
        {
            return false;
        }

        var modifiers = member.Modifiers;
        return modifiers.Any(SyntaxKind.PrivateKeyword)
            && !modifiers.Any(SyntaxKind.StaticKeyword)
            && !modifiers.Any(SyntaxKind.VirtualKeyword)
            && !modifiers.Any(SyntaxKind.AbstractKeyword)
            && !modifiers.Any(SyntaxKind.OverrideKeyword)
            && !modifiers.Any(SyntaxKind.ExternKeyword)
            && !modifiers.Any(SyntaxKind.PartialKeyword);
    }

    /// <summary>Reports PSH1414 for a private instance member that never reads <c>this</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMember(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!IsEligibleDeclaration(member)
            || TryGetExecutableBody(member) is not { } body
            || context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { } symbol
            || symbol.GetAttributes().Length > 0
            || symbol.ContainingType.GetAttributes().Length > 0)
        {
            return;
        }

        if (UsesInstanceState(context, body, symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.MarkMembersStatic,
            GetIdentifier(member).GetLocation(),
            symbol.Name));
    }

    /// <summary>Returns the member's executable body, or nothing when it has none to inspect.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The body to scan, or <see langword="null"/> for an abstract or auto-implemented member.</returns>
    private static SyntaxNode? TryGetExecutableBody(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax method => (SyntaxNode?)method.Body ?? method.ExpressionBody,
            PropertyDeclarationSyntax { ExpressionBody: { } expressionBody } => expressionBody,
            PropertyDeclarationSyntax { AccessorList: { } accessors } => HasAccessorBody(accessors) ? accessors : null,
            _ => null,
        };

    /// <summary>Returns whether a property's accessors have real bodies, so it is not an auto-property.</summary>
    /// <param name="accessors">The property's accessor list.</param>
    /// <returns><see langword="true"/> when at least one accessor has a body, and none is auto-implemented.</returns>
    private static bool HasAccessorBody(AccessorListSyntax accessors)
    {
        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Body is null && list[i].ExpressionBody is null)
            {
                return false;
            }
        }

        return list.Count > 0;
    }

    /// <summary>Returns the identifier token the diagnostic is reported on.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns>The member's name token.</returns>
    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax member)
        => member switch
        {
            MethodDeclarationSyntax method => method.Identifier,
            PropertyDeclarationSyntax property => property.Identifier,
            _ => default,
        };

    /// <summary>Returns whether a member's body reads <c>this</c>, directly or through an unqualified member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="body">The member's executable body.</param>
    /// <param name="symbol">The member being analyzed, whose own self-references do not count.</param>
    /// <returns><see langword="true"/> when the member depends on its receiver.</returns>
    private static bool UsesInstanceState(SyntaxNodeAnalysisContext context, SyntaxNode body, ISymbol symbol)
    {
        var state = new InstanceUseScanState(symbol, symbol.ContainingType, context.SemanticModel, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<ExpressionSyntax, InstanceUseScanState>(body, ref state, VisitBodyExpression);
        return state.UsesInstance;
    }

    /// <summary>Classifies one expression in the body as instance-dependent or not.</summary>
    /// <param name="node">The visited expression.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once instance use is proved.</returns>
    private static bool VisitBodyExpression(ExpressionSyntax node, ref InstanceUseScanState state)
    {
        if (node is ThisExpressionSyntax or BaseExpressionSyntax)
        {
            state.UsesInstance = true;
            return false;
        }

        if (node is not IdentifierNameSyntax identifier)
        {
            return true;
        }

        if (state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol is not { IsStatic: false } referenced
            || SymbolEqualityComparer.Default.Equals(referenced, state.Symbol)
            || !IsInstanceMemberOfHierarchy(referenced, state.ContainingType))
        {
            return true;
        }

        state.UsesInstance = true;
        return false;
    }

    /// <summary>Returns whether a symbol is instance state of the analyzed type or one of its bases.</summary>
    /// <param name="symbol">The referenced symbol.</param>
    /// <param name="containingType">The analyzed member's containing type.</param>
    /// <returns><see langword="true"/> when reading the symbol requires a receiver.</returns>
    /// <remarks>
    /// A primary constructor parameter counts. Naming one from a member body captures it into a
    /// synthesized instance field, so the member does depend on its receiver — and the compiler says so:
    /// a static member that names a primary constructor parameter does not compile. Missing this would
    /// leave the reader a diagnostic whose only stated remedy is a build error.
    /// </remarks>
    private static bool IsInstanceMemberOfHierarchy(ISymbol symbol, INamedTypeSymbol containingType)
    {
        if (symbol is IParameterSymbol { ContainingSymbol: IMethodSymbol { MethodKind: MethodKind.Constructor } primaryConstructor })
        {
            return SymbolEqualityComparer.Default.Equals(primaryConstructor.ContainingType, containingType);
        }

        if (symbol.Kind is not (SymbolKind.Field or SymbolKind.Property or SymbolKind.Method or SymbolKind.Event))
        {
            return false;
        }

        for (var current = containingType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(symbol.ContainingType, current))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tracks whether a member's body was shown to depend on its receiver.</summary>
    /// <param name="Symbol">The member being analyzed.</param>
    /// <param name="ContainingType">The member's containing type.</param>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private record struct InstanceUseScanState(
        ISymbol Symbol,
        INamedTypeSymbol ContainingType,
        SemanticModel Model,
        CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets a value indicating whether the body reads instance state.</summary>
        public bool UsesInstance { get; set; }
    }
}
