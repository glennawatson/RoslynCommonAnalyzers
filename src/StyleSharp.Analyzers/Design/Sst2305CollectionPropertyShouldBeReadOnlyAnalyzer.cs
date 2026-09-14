// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a property whose type is a mutable collection and whose <c>set</c> accessor lets a caller
/// replace that collection outright (SST2305). Callers want to add and remove items — which a get-only
/// property already allows — not to swap the instance the type, its subscriptions, and everything else
/// holding a reference are still pointing at.
/// </summary>
/// <remarks>
/// <para>
/// The rule reports the setter, never the getter's contents; a getter that hands back a fresh copy on
/// every read is a different problem with a different answer.
/// </para>
/// <para>
/// Every shape the rule leaves alone is recognized on syntax alone, so nothing is bound until a
/// property has an ordinary, caller-visible <c>set</c> accessor: an <c>init</c> accessor is a different
/// syntax kind, and <c>private set</c>, <c>required</c>, <c>override</c>, an explicit interface
/// implementation, and contract attributes on the property or its containing type all reject before
/// the semantic model is touched. The attribute test is the serialization escape hatch: a contract
/// that needs a setter says so with an attribute. Known diagnostic and tooling metadata does not
/// express that contract.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2305CollectionPropertyShouldBeReadOnlyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.CollectionPropertyShouldBeReadOnly);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    /// <summary>Finds the <c>set</c> accessor a fix could remove, rejecting every exempt shape on syntax alone.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The removable setter, or <see langword="null"/> when the property is not a candidate.</returns>
    /// <remarks>
    /// An <c>init</c> accessor parses as its own kind and is therefore never returned: the object is
    /// built once and then settled, which is exactly what the rule is asking for.
    /// </remarks>
    internal static AccessorDeclarationSyntax? FindRemovableSetter(PropertyDeclarationSyntax property)
    {
        if (property.AccessorList is not { } accessorList || IsExemptDeclaration(property))
        {
            return null;
        }

        var accessors = accessorList.Accessors;
        for (var i = 0; i < accessors.Count; i++)
        {
            var accessor = accessors[i];
            if (!accessor.IsKind(SyntaxKind.SetAccessorDeclaration))
            {
                continue;
            }

            var isCallerFacing = !HasContractAttribute(accessor.AttributeLists)
                && !ModifierListHelper.Contains(accessor.Modifiers, SyntaxKind.PrivateKeyword);
            return isCallerFacing ? accessor : null;
        }

        return null;
    }

    /// <summary>Reports one property that hands a caller the power to replace its collection.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (FindRemovableSetter(property) is null)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken) is not { } symbol
            || !CollectionTypeClassification.IsMutableCollection(symbol.Type)
            || InterfaceImplementationLookup.ImplementsInterfaceMember(symbol)
            || IsAssignedWhereOnlyItsOwnTypeCanSee(context, property, symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.CollectionPropertyShouldBeReadOnly,
            property.Identifier.GetLocation(),
            property.Identifier.ValueText));
    }

    /// <summary>Returns whether nothing outside one type declaration can name the property.</summary>
    /// <param name="symbol">The property symbol.</param>
    /// <returns><see langword="true"/> when a private type around it, or the property itself, closes it off.</returns>
    /// <remarks>
    /// A public property on a private nested type is still unreachable from outside the type that declares
    /// that nested type, so the enclosing chain decides this rather than the property's own modifier.
    /// </remarks>
    private static bool IsSealedInsideOneType(IPropertySymbol symbol)
    {
        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.DeclaredAccessibility is Accessibility.Private)
            {
                return true;
            }
        }

        return symbol.DeclaredAccessibility is Accessibility.Private;
    }

    /// <summary>Gets the outermost type declaration around a property.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The outermost enclosing type, or <see langword="null"/>.</returns>
    private static TypeDeclarationSyntax? FindOutermostType(PropertyDeclarationSyntax property)
    {
        var outermost = property.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        for (var candidate = outermost; candidate is not null; candidate = candidate.Parent as TypeDeclarationSyntax)
        {
            outermost = candidate;
        }

        return outermost;
    }

    /// <summary>Returns whether a property only its own type can reach is assigned somewhere in that type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="property">The property declaration.</param>
    /// <param name="symbol">The property symbol.</param>
    /// <returns><see langword="true"/> when removing the setter would break an existing assignment.</returns>
    /// <remarks>
    /// Only a property sealed inside one type declaration can be settled this way: everything that could
    /// assign it is in that declaration. A property anything else can reach is not searched.
    /// </remarks>
    private static bool IsAssignedWhereOnlyItsOwnTypeCanSee(in SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax property, IPropertySymbol symbol)
    {
        if (!IsSealedInsideOneType(symbol) || FindOutermostType(property) is not { } outermost)
        {
            return false;
        }

        var state = new PropertyWriteState(context.SemanticModel, symbol, context.CancellationToken);
        return !DescendantTraversalHelper.VisitDescendants(
            outermost,
            ref state,
            static (SyntaxNode descendant, ref PropertyWriteState scan) =>
            {
                if (RecordAnalyzer.WrittenMemberAccess(descendant) is not { } access
                    || access.Name.Identifier.ValueText != scan.Symbol.Name)
                {
                    return true;
                }

                return !SymbolEqualityComparer.Default.Equals(
                    scan.Model.GetSymbolInfo(access, scan.CancellationToken).Symbol?.OriginalDefinition,
                    scan.Symbol.OriginalDefinition);
            });
    }

    /// <summary>Returns whether the declaration itself puts the property outside the rule.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns><see langword="true"/> when the setter is not the author's to remove, or is deliberate.</returns>
    /// <remarks>
    /// A <c>required</c> property must stay settable for an object initializer to satisfy it, and a
    /// <c>private set</c> keeps the collection under the type's own control. Attributes may express a
    /// serialization contract that needs a setter, so only known tooling metadata is disregarded.
    /// </remarks>
    private static bool IsExemptDeclaration(PropertyDeclarationSyntax property) =>
        HasContractAttribute(property.AttributeLists)
            || property.ExplicitInterfaceSpecifier is not null
            || ModifierListHelper.ContainsEither(property.Modifiers, SyntaxKind.PrivateKeyword, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(property.Modifiers, SyntaxKind.RequiredKeyword)
            || (property.Parent is BaseTypeDeclarationSyntax containingType && HasContractAttribute(containingType.AttributeLists));

    /// <summary>Returns whether an attribute may require the setter as part of a contract.</summary>
    /// <param name="attributeLists">The declaration's attribute lists.</param>
    /// <returns>True when any attribute is not recognized as tooling metadata.</returns>
    private static bool HasContractAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        for (var i = 0; i < attributeLists.Count; i++)
        {
            var attributes = attributeLists[i].Attributes;
            for (var j = 0; j < attributes.Count; j++)
            {
                if (!IsToolingAttribute(attributes[j].Name))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Recognizes metadata that controls tooling without requiring a collection setter.</summary>
    /// <param name="name">The written attribute name, including any namespace qualification.</param>
    /// <returns>True for known diagnostic, coverage, debugger, editor, and compiler metadata.</returns>
    /// <remarks>
    /// Match only explicit names: an unknown attribute may carry a serialization contract. Binding
    /// attributes would add semantic work to every attributed declaration on the syntax clean path.
    /// </remarks>
    private static bool IsToolingAttribute(NameSyntax name)
    {
        var identifier = name switch
        {
            IdentifierNameSyntax simple => simple.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
            _ => string.Empty,
        };

        return IsNonDebuggerToolingAttribute(identifier)
            || IsDebuggerDisplayAttribute(identifier)
            || IsDebuggerExecutionAttribute(identifier);
    }

    /// <summary>Recognizes suppression, coverage, editor, and compiler metadata.</summary>
    /// <param name="name">The unqualified attribute name.</param>
    /// <returns>True when the attribute does not express a setter contract.</returns>
    private static bool IsNonDebuggerToolingAttribute(string name) =>
        name is "SuppressMessage" or "SuppressMessageAttribute"
            or "UnconditionalSuppressMessage" or "UnconditionalSuppressMessageAttribute"
            or "ExcludeFromCodeCoverage" or "ExcludeFromCodeCoverageAttribute"
            or "EditorBrowsable" or "EditorBrowsableAttribute"
            or "CompilerGenerated" or "CompilerGeneratedAttribute";

    /// <summary>Recognizes attributes that change how the debugger displays a value.</summary>
    /// <param name="name">The unqualified attribute name.</param>
    /// <returns>True for debugger display metadata.</returns>
    private static bool IsDebuggerDisplayAttribute(string name) =>
        name is "DebuggerBrowsable" or "DebuggerBrowsableAttribute"
            or "DebuggerDisplay" or "DebuggerDisplayAttribute"
            or "DebuggerTypeProxy" or "DebuggerTypeProxyAttribute"
            or "DebuggerVisualizer" or "DebuggerVisualizerAttribute";

    /// <summary>Recognizes attributes that control debugger stepping and exception handling.</summary>
    /// <param name="name">The unqualified attribute name.</param>
    /// <returns>True for debugger execution metadata.</returns>
    private static bool IsDebuggerExecutionAttribute(string name) =>
        name is "DebuggerHidden" or "DebuggerHiddenAttribute"
            or "DebuggerNonUserCode" or "DebuggerNonUserCodeAttribute"
            or "DebuggerStepThrough" or "DebuggerStepThroughAttribute"
            or "DebuggerStepperBoundary" or "DebuggerStepperBoundaryAttribute"
            or "DebuggerDisableUserUnhandledExceptions" or "DebuggerDisableUserUnhandledExceptionsAttribute";

    /// <summary>Carries the property binding context through the assignment scan.</summary>
    private readonly record struct PropertyWriteState
    {
        /// <summary>Initializes a new instance of the <see cref="PropertyWriteState"/> struct.</summary>
        /// <param name="model">The semantic model.</param>
        /// <param name="symbol">The property whose writes are sought.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        public PropertyWriteState(SemanticModel model, IPropertySymbol symbol, CancellationToken cancellationToken)
        {
            Model = model;
            Symbol = symbol;
            CancellationToken = cancellationToken;
        }

        /// <summary>Gets the semantic model used to bind assignments.</summary>
        public SemanticModel Model { get; }

        /// <summary>Gets the property whose writes are sought.</summary>
        public IPropertySymbol Symbol { get; }

        /// <summary>Gets the token that cancels semantic binding.</summary>
        public CancellationToken CancellationToken { get; }
    }
}
