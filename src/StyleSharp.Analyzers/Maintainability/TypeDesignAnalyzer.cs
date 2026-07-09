// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer for type-shape conventions: constructor accessibility on abstract
/// types, static members on generic types, and classes that could be static.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1428 — an abstract type declares a <c>public</c> constructor.</description></item>
/// <item><description>SST1431 — a <c>static</c> member of a generic type ignores the type's type parameters.</description></item>
/// <item><description>SST1432 — a class declares only static members and could be marked <c>static</c> (opt-in).</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeDesignAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The rule-specific editorconfig key naming extra property-system owner types.</summary>
    private const string AdditionalOwnerTypesSpecificKey = "stylesharp.SST1431.additional_per_owner_types";

    /// <summary>The general editorconfig key naming extra property-system owner types.</summary>
    private const string AdditionalOwnerTypesGeneralKey = "stylesharp.additional_per_owner_types";

    /// <summary>
    /// Fully-qualified names of property-system registration types. A static member typed as one
    /// of these (or a subtype) is a per-closed-generic registration that must be a static field, so
    /// it is exempt from SST1431 even when its declaration never names a type parameter.
    /// </summary>
    private static readonly ImmutableHashSet<string> PropertySystemOwnerTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "Microsoft.Maui.Controls.BindableProperty",
        "System.Windows.DependencyProperty",
        "Windows.UI.Xaml.DependencyProperty",
        "Microsoft.UI.Xaml.DependencyProperty",
        "Avalonia.AvaloniaProperty",
        "Avalonia.StyledProperty",
        "Avalonia.DirectProperty",
        "Avalonia.AttachedProperty");

    /// <summary>Renders a type's fully-qualified name without type arguments, for owner-type matching.</summary>
    private static readonly SymbolDisplayFormat FullyQualifiedWithoutGenerics = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.NoPublicConstructorOnAbstractType,
        MaintainabilityRules.StaticMemberShouldUseTypeParameter,
        MaintainabilityRules.MakeClassStatic);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(
            AnalyzeStaticMember,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>Returns whether a class member keeps the type eligible to be marked <c>static</c>.</summary>
    /// <param name="member">The member declaration.</param>
    /// <returns><see langword="true"/> when the member is static, a constant, a nested type, or a static constructor.</returns>
    internal static bool IsStaticCompatibleMember(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax => true,
        DelegateDeclarationSyntax => true,
        ConstructorDeclarationSyntax constructor => ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword),
        FieldDeclarationSyntax field => ModifierListHelper.ContainsEither(field.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.ConstKeyword),
        _ => ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword)
    };

    /// <summary>Reports SST1428 for a <c>public</c> constructor declared on an abstract type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || constructor.Parent is not TypeDeclarationSyntax type
            || !IsAbstractType(type, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        for (var i = 0; i < constructor.Modifiers.Count; i++)
        {
            if (constructor.Modifiers[i].IsKind(SyntaxKind.PublicKeyword))
            {
                context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoPublicConstructorOnAbstractType, constructor.Modifiers[i].GetLocation()));
                return;
            }
        }
    }

    /// <summary>Returns whether a type declaration's merged symbol is <c>abstract</c>.</summary>
    /// <param name="type">The type declaration containing the constructor.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the type is abstract, including via another partial part.</returns>
    /// <remarks>
    /// The <c>abstract</c> keyword need only appear on one part, so its absence here does not
    /// mean the type is concrete. The syntactic hit short-circuits the common case; only a
    /// partial part without the keyword pays for the symbol lookup.
    /// </remarks>
    private static bool IsAbstractType(TypeDeclarationSyntax type, SemanticModel model, CancellationToken cancellationToken)
    {
        if (ModifierListHelper.Contains(type.Modifiers, SyntaxKind.AbstractKeyword))
        {
            return true;
        }

        return ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword)
            && model.GetDeclaredSymbol(type, cancellationToken) is { IsAbstract: true };
    }

    /// <summary>Reports SST1432 for a non-static class whose members are all static-compatible.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <remarks>
    /// <c>static</c> applies to the whole type, so a partial class is judged across every part:
    /// one part declaring an instance member or a base list rules out all of them. The report
    /// lands on the first part so the type yields a single diagnostic.
    /// </remarks>
    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (HasStaticDisqualifier(declaration))
        {
            return;
        }

        if (!ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.PartialKeyword))
        {
            if (declaration.Members.Count == 0)
            {
                return;
            }
        }
        else if (!IsFirstPartOfStaticCompatibleType(declaration, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.MakeClassStatic, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText));
    }

    /// <summary>Returns whether a single class declaration rules its type out of being <c>static</c>.</summary>
    /// <param name="declaration">The class declaration, which may be one part of a partial type.</param>
    /// <returns><see langword="true"/> when this declaration alone prevents the type from being static.</returns>
    private static bool HasStaticDisqualifier(ClassDeclarationSyntax declaration)
    {
        // A base list, a primary constructor, or an existing static/abstract marker rules the class out.
        if (declaration.BaseList is not null
            || declaration.ParameterList is not null
            || ModifierListHelper.ContainsEither(declaration.Modifiers, SyntaxKind.StaticKeyword, SyntaxKind.AbstractKeyword))
        {
            return true;
        }

        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (!IsStaticCompatibleMember(members[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether every other part of a partial type also allows <c>static</c>, and this is the first part.</summary>
    /// <param name="declaration">The part being analyzed; already known to carry no disqualifier.</param>
    /// <param name="model">The semantic model for the part's tree.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the whole type qualifies and this part should carry the report.</returns>
    private static bool IsFirstPartOfStaticCompatibleType(
        ClassDeclarationSyntax declaration,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
        {
            return false;
        }

        var references = symbol.DeclaringSyntaxReferences;
        if (references.Length == 0)
        {
            return false;
        }

        // Only the first part carries the report, so later parts stop after one lookup.
        if (references[0].GetSyntax(cancellationToken) is not ClassDeclarationSyntax first
            || first.SyntaxTree != declaration.SyntaxTree
            || first.Span != declaration.Span)
        {
            return false;
        }

        var memberCount = 0;
        for (var i = 0; i < references.Length; i++)
        {
            if (references[i].GetSyntax(cancellationToken) is not ClassDeclarationSyntax part || HasStaticDisqualifier(part))
            {
                return false;
            }

            memberCount += part.Members.Count;
        }

        // An entirely empty type is not worth marking static.
        return memberCount > 0;
    }

    /// <summary>Reports SST1431 for an externally visible static member of a generic type that ignores its type parameters.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeStaticMember(SyntaxNodeAnalysisContext context)
    {
        var member = (MemberDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(member.Modifiers, SyntaxKind.StaticKeyword)
            || !IsExternallyVisible(member.Modifiers)
            || member.Parent is not TypeDeclarationSyntax { TypeParameterList: { } })
        {
            return;
        }

        // The member is exempt when it uses a type parameter anywhere — signature, attributes,
        // initializer, accessor, or body — including inside the closed self-type (typeof(G<T>)),
        // because then the per-closed-generic instantiation is intentional.
        if (MemberMentionsAnyTypeParameter(member))
        {
            return;
        }

        var identifier = GetMemberIdentifier(member);
        if (identifier is null)
        {
            return;
        }

        // A registration field/property (BindableProperty, DependencyProperty, …) must be static
        // and is per-closed-generic by design, so it is exempt even without an explicit type-parameter use.
        if (IsPropertySystemOwnerMember(context, member))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.StaticMemberShouldUseTypeParameter, identifier.Value.GetLocation(), identifier.Value.ValueText));
    }

    /// <summary>Returns whether a member is reachable outside its type (so callers see the awkward type argument).</summary>
    /// <param name="modifiers">The member modifiers.</param>
    /// <returns><see langword="true"/> for a <c>public</c>, <c>internal</c>, or <c>protected</c> member.</returns>
    private static bool IsExternallyVisible(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword or SyntaxKind.ProtectedKeyword)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether a member references any enclosing type parameter anywhere in its declaration —
    /// signature, attributes, initializer, accessors, or body — including a type parameter buried in
    /// a closed generic argument such as <c>typeof(Owner&lt;T&gt;)</c>.
    /// </summary>
    /// <param name="member">The static member.</param>
    /// <returns><see langword="true"/> when at least one type parameter is referenced.</returns>
    /// <remarks>
    /// Runs only on the rare candidate path (an externally visible static member of a generic type),
    /// so the single descendant scan is acceptable. Type parameters always surface as an
    /// <see cref="IdentifierNameSyntax"/>; matching by name errs toward exempting (no false positive)
    /// if a local ever shadows a type parameter name.
    /// </remarks>
    private static bool MemberMentionsAnyTypeParameter(MemberDeclarationSyntax member)
    {
        var state = new TypeParameterScan(member);
        DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, TypeParameterScan>(member, ref state, MatchTypeParameterName);
        return state.Found;
    }

    /// <summary>Records whether an identifier names one of the member's enclosing type parameters.</summary>
    /// <param name="node">The visited identifier name.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> to stop.</returns>
    private static bool MatchTypeParameterName(IdentifierNameSyntax node, ref TypeParameterScan state)
    {
        if (!NamesEnclosingTypeParameter(node.Identifier.ValueText, state.Member))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether a name matches a type parameter of the member's type or any outer generic type.</summary>
    /// <param name="name">The identifier text.</param>
    /// <param name="member">The static member.</param>
    /// <returns><see langword="true"/> when the name is an enclosing type parameter name.</returns>
    private static bool NamesEnclosingTypeParameter(string name, MemberDeclarationSyntax member)
    {
        for (var node = member.Parent; node is TypeDeclarationSyntax type; node = type.Parent)
        {
            if (type.TypeParameterList is not { } typeParameters)
            {
                continue;
            }

            var parameters = typeParameters.Parameters;
            for (var i = 0; i < parameters.Count; i++)
            {
                if (string.Equals(parameters[i].Identifier.ValueText, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a static field or property is typed as a property-system registration type.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The static member.</param>
    /// <returns><see langword="true"/> when the member's type is a built-in or configured owner type.</returns>
    private static bool IsPropertySystemOwnerMember(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member)
    {
        var declaredType = member switch
        {
            FieldDeclarationSyntax field => field.Declaration.Type,
            PropertyDeclarationSyntax property => property.Type,
            _ => null
        };

        if (declaredType is null
            || context.SemanticModel.GetTypeInfo(declaredType, context.CancellationToken).Type is not INamedTypeSymbol type)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            var name = current.OriginalDefinition.ToDisplayString(FullyQualifiedWithoutGenerics);
            if (PropertySystemOwnerTypes.Contains(name) || IsConfiguredOwnerType(context, member, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a fully-qualified type name appears in the editorconfig owner-type allow-list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The static member (its tree supplies the options scope).</param>
    /// <param name="name">The candidate fully-qualified type name.</param>
    /// <returns><see langword="true"/> when the name is configured as an additional owner type.</returns>
    private static bool IsConfiguredOwnerType(SyntaxNodeAnalysisContext context, MemberDeclarationSyntax member, string name)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(member.SyntaxTree);
        return EditorConfigList.ContainsToken(options, AdditionalOwnerTypesSpecificKey, name, StringComparison.Ordinal)
            || EditorConfigList.ContainsToken(options, AdditionalOwnerTypesGeneralKey, name, StringComparison.Ordinal);
    }

    /// <summary>Returns the name token a static member should be reported on, or <see langword="null"/>.</summary>
    /// <param name="member">The static member.</param>
    /// <returns>The member's identifier token, or <see langword="null"/> for a member with no single name.</returns>
    private static SyntaxToken? GetMemberIdentifier(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier,
        PropertyDeclarationSyntax property => property.Identifier,
        FieldDeclarationSyntax { Declaration.Variables: [var single] } => single.Identifier,
        EventFieldDeclarationSyntax { Declaration.Variables: [var single] } => single.Identifier,
        _ => null
    };

    /// <summary>Mutable accumulator for the type-parameter usage scan over a member.</summary>
    /// <param name="Member">The member whose enclosing type parameters are matched against.</param>
    private record struct TypeParameterScan(MemberDeclarationSyntax Member)
    {
        /// <summary>Gets or sets a value indicating whether an enclosing type parameter was referenced.</summary>
        public bool Found { get; set; }
    }
}
