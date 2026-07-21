// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an auto-property declared <c>get; private set;</c> whose setter is only ever used during
/// construction (SST2332), so it can be a get-only property. Keeping the private setter tells the reader the
/// value can change after construction when it never does; collapsing to <c>get;</c> says what is true and
/// lets the compiler reject a later stray assignment instead of quietly allowing the mutation.
/// </summary>
/// <remarks>
/// <para>
/// The rule proves there is no post-construction write before it fires: the whole declaring type is scanned
/// for an assignment to the property (including a compound assignment, an increment, a <c>ref</c>/<c>out</c>
/// argument, and an object-initializer target), and any such write that is not inside a constructor of that
/// same type — including one reached through a lambda or a local function — leaves the property alone.
/// </para>
/// <para>
/// A partial declaring type is skipped, because a write could sit in a part this analysis cannot see. The
/// clean path is a syntactic prepass for the exact <c>get; private set;</c> auto-property shape, so an
/// ordinary property never reaches the write scan.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2332PrivateSetterOnlyWrittenDuringConstructionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(DesignRules.PrivateSetterOnlyWrittenDuringConstruction);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Reports a private-set auto-property that is only written during construction.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (!IsPrivateSetAutoProperty(property)
            || property.Parent is not TypeDeclarationSyntax type
            || ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not { } symbol
            || HasWriteOutsideConstructor(context.SemanticModel, type, symbol, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DesignRules.PrivateSetterOnlyWrittenDuringConstruction,
            property.Identifier.GetLocation(),
            property.Identifier.ValueText));
    }

    /// <summary>Returns whether a property is a <c>get; private set;</c> auto-property.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when the property has an auto get accessor and an auto private set accessor.</returns>
    private static bool IsPrivateSetAutoProperty(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is not null || property.AccessorList is not { } accessors)
        {
            return false;
        }

        AccessorDeclarationSyntax? getter = null;
        AccessorDeclarationSyntax? setter = null;
        for (var i = 0; i < accessors.Accessors.Count; i++)
        {
            var accessor = accessors.Accessors[i];
            if (accessor.Body is not null || accessor.ExpressionBody is not null)
            {
                return false;
            }

            if (accessor.IsKind(SyntaxKind.GetAccessorDeclaration))
            {
                getter = accessor;
            }
            else if (accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                setter = accessor;
            }
            else
            {
                return false;
            }
        }

        return getter is not null
            && setter is not null
            && ModifierListHelper.Contains(setter.Modifiers, SyntaxKind.PrivateKeyword);
    }

    /// <summary>Returns whether the property is assigned anywhere in the type outside one of its constructors.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="type">The declaring type.</param>
    /// <param name="property">The property symbol.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a disqualifying post-construction write exists.</returns>
    private static bool HasWriteOutsideConstructor(
        SemanticModel model,
        TypeDeclarationSyntax type,
        IPropertySymbol property,
        CancellationToken cancellationToken)
    {
        var state = new WriteSearchState(model, property, type, Disqualified: false, cancellationToken);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, WriteSearchState>(type, ref state, VisitWriteCandidate);
        return state.Disqualified;
    }

    /// <summary>Records whether an identifier writes to the property outside a constructor of its type.</summary>
    /// <param name="identifier">The visited identifier.</param>
    /// <param name="state">The search state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once disqualified.</returns>
    private static bool VisitWriteCandidate(IdentifierNameSyntax identifier, ref WriteSearchState state)
    {
        if (identifier.Identifier.ValueText != state.Property.Name || !FieldReferenceAnalysis.IsWrite(identifier))
        {
            return true;
        }

        if (!SymbolEqualityComparer.Default.Equals(state.Model.GetSymbolInfo(identifier, state.CancellationToken).Symbol, state.Property))
        {
            return true;
        }

        if (IsConstructionTimeWrite(identifier, state.Type))
        {
            return true;
        }

        state = state with { Disqualified = true };
        return false;
    }

    /// <summary>Returns whether a write to the property happens at construction time and not through an object initializer.</summary>
    /// <param name="identifier">The property reference in write position.</param>
    /// <param name="type">The declaring type.</param>
    /// <returns><see langword="true"/> when the write is inside a constructor body of the type.</returns>
    /// <remarks>
    /// An object-initializer target is never construction-time for this purpose: a get-only property cannot be
    /// set through an initializer, so such a write must keep the setter. A write reached through a lambda or a
    /// local function may run after the constructor returns, so it does not count either.
    /// </remarks>
    private static bool IsConstructionTimeWrite(IdentifierNameSyntax identifier, TypeDeclarationSyntax type)
    {
        if (IsObjectInitializerTarget(identifier)
            || identifier.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is not null
            || identifier.FirstAncestorOrSelf<LocalFunctionStatementSyntax>() is not null)
        {
            return false;
        }

        return identifier.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is { } constructor && constructor.Parent == type;
    }

    /// <summary>Returns whether an identifier is the assigned member of an object initializer.</summary>
    /// <param name="identifier">The identifier to inspect.</param>
    /// <returns><see langword="true"/> when the identifier is <c>X</c> in <c>new T { X = ... }</c>.</returns>
    private static bool IsObjectInitializerTarget(IdentifierNameSyntax identifier)
        => identifier.Parent is AssignmentExpressionSyntax { } assignment
            && assignment.Left == identifier
            && assignment.Parent is InitializerExpressionSyntax initializer
            && initializer.IsKind(SyntaxKind.ObjectInitializerExpression);

    /// <summary>The state threaded through the type-wide write scan.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Property">The property symbol.</param>
    /// <param name="Type">The declaring type.</param>
    /// <param name="Disqualified">Whether a post-construction write has been found.</param>
    /// <param name="CancellationToken">A token that cancels the operation.</param>
    private readonly record struct WriteSearchState(
        SemanticModel Model,
        IPropertySymbol Property,
        TypeDeclarationSyntax Type,
        bool Disqualified,
        CancellationToken CancellationToken);
}
