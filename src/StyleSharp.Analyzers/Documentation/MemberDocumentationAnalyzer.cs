// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Checks XML documentation for members in a single pass per declaration —
/// coverage (SST1600/SST1602), summary presence and text (SST1604/SST1606),
/// parameter / type-parameter / return-value documentation
/// (SST1611/SST1618/SST1615/SST1617), and terminal punctuation (SST1629).
/// Reading each member's documentation comment once keeps this far cheaper than
/// StyleCop's one-analyzer-per-rule design.
/// </summary>
/// <remarks>
/// Diagnostics: SST1600, SST1602, SST1604, SST1606, SST1608, SST1611, SST1612, SST1613, SST1614, SST1615, SST1616, SST1617, SST1618, SST1620, SST1621, SST1622, SST1623, SST1624,
/// SST1629, SST1642, SST1643.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.ElementsMustBeDocumented,
        DocumentationRules.EnumItemsMustBeDocumented,
        DocumentationRules.MustHaveSummary,
        DocumentationRules.SummaryMustHaveText,
        DocumentationRules.NoDefaultSummary,
        DocumentationRules.ParametersMustBeDocumented,
        DocumentationRules.ParameterDocumentationMustMatch,
        DocumentationRules.ParameterDocumentationMustDeclareName,
        DocumentationRules.ParameterDocumentationMustHaveText,
        DocumentationRules.ReturnValueMustBeDocumented,
        DocumentationRules.ReturnDocumentationMustHaveText,
        DocumentationRules.VoidMustNotHaveReturn,
        DocumentationRules.TypeParametersMustBeDocumented,
        DocumentationRules.TypeParameterDocumentationMustMatch,
        DocumentationRules.TypeParameterDocumentationMustDeclareName,
        DocumentationRules.TypeParameterDocumentationMustHaveText,
        DocumentationRules.PropertySummaryAccessors,
        DocumentationRules.PropertySummaryOmitsRestrictedSetter,
        DocumentationRules.ConstructorStandardText,
        DocumentationRules.DestructorStandardText,
        DocumentationRules.TextMustEndWithPeriod);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.DelegateDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.DestructorDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.EnumMemberDeclaration);

        context.RegisterSyntaxNodeAction(
            AnalyzeField,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.EventFieldDeclaration);
    }

    /// <summary>
    /// Reports each undocumented declarator of a field (or event-field) declaration when the field is in
    /// coverage scope. A multi-declarator field is reported once per name, mirroring the naming rules.
    /// Property / event backing fields carry no syntax of their own, and <c>[GeneratedCode]</c> members are
    /// skipped by <see cref="GeneratedCodeAnalysisFlags.None"/>, so neither reaches this path.
    /// </summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (BaseFieldDeclarationSyntax)context.Node;

        // The whole declaration shares one documentation comment; a present comment satisfies coverage for
        // every declarator (the summary/content rules for documented members are handled elsewhere).
        if (XmlDocumentationHelper.GetDocumentationComment(field) is not null)
        {
            return;
        }

        var coverage = DocumentationOptions.ReadCoverage(context.Options.AnalyzerConfigOptionsProvider.GetOptions(field.SyntaxTree));
        if (!DocumentationVisibility.FieldNeedsDocumentation(field, coverage))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ElementsMustBeDocumented, variable.Identifier.GetLocation(), variable.Identifier.ValueText));
        }
    }

    /// <summary>Dispatches a member declaration to the documentation checks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (Describe(context.Node) is not { } shape)
        {
            return;
        }

        Check(context, context.Node, shape);
    }

    /// <summary>Extracts the documentation-relevant shape of a member, or <see langword="null"/> for unsupported nodes.</summary>
    /// <param name="node">The declaration node.</param>
    /// <returns>The member shape, or <see langword="null"/>.</returns>
    private static MemberDoc? Describe(SyntaxNode node) => node switch
    {
        DelegateDeclarationSyntax @delegate => new MemberDoc(
            @delegate.Identifier,
            @delegate.ParameterList.Parameters,
            TypeParametersOf(@delegate.TypeParameterList),
            @delegate.ReturnType,
            SkipCoverage: false,
            DocumentationRules.ElementsMustBeDocumented,
            SummaryRequirement: null),

        BaseTypeDeclarationSyntax type => DescribeType(type),

        MethodDeclarationSyntax method => new MemberDoc(
            method.Identifier,
            method.ParameterList.Parameters,
            TypeParametersOf(method.TypeParameterList),
            method.ReturnType,
            Skips(method.Modifiers, method.ExplicitInterfaceSpecifier),
            DocumentationRules.ElementsMustBeDocumented,
            SummaryRequirement: null),

        ConstructorDeclarationSyntax constructor => new MemberDoc(
            constructor.Identifier,
            constructor.ParameterList.Parameters,
            default,
            ReturnType: null,
            SkipCoverage: false,
            DocumentationRules.ElementsMustBeDocumented,
            ConstructorRequirement(constructor)),

        DestructorDeclarationSyntax destructor => new MemberDoc(
            destructor.Identifier,
            default,
            default,
            ReturnType: null,
            SkipCoverage: false,
            DocumentationRules.ElementsMustBeDocumented,
            new SummaryPrefix(DocumentationConventions.DestructorStandardPrefix, DocumentationRules.DestructorStandardText)),

        PropertyDeclarationSyntax property => new MemberDoc(
            property.Identifier,
            default,
            default,
            ReturnType: null,
            Skips(property.Modifiers, property.ExplicitInterfaceSpecifier),
            DocumentationRules.ElementsMustBeDocumented,
            PropertySummaryRequirement(property)),

        EnumMemberDeclarationSyntax enumMember => new MemberDoc(
            enumMember.Identifier,
            default,
            default,
            ReturnType: null,
            SkipCoverage: false,
            DocumentationRules.EnumItemsMustBeDocumented,
            SummaryRequirement: null),

        _ => null
    };

    /// <summary>Describes a type declaration, including any primary-constructor / positional-record parameters.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The member shape.</returns>
    private static MemberDoc DescribeType(BaseTypeDeclarationSyntax type)
    {
        var declaration = type as TypeDeclarationSyntax;
        return new(
            type.Identifier,
            declaration?.ParameterList?.Parameters ?? default,
            TypeParametersOf(declaration?.TypeParameterList),
            ReturnType: null,
            ModifierListHelper.Contains(type.Modifiers, SyntaxKind.PartialKeyword),
            DocumentationRules.ElementsMustBeDocumented,
            SummaryRequirement: null);
    }

    /// <summary>Returns the standard-text requirement for a non-static constructor, or <see langword="null"/>.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns>The summary requirement, or <see langword="null"/>.</returns>
    private static SummaryPrefix? ConstructorRequirement(ConstructorDeclarationSyntax constructor)
    {
        if (ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword))
        {
            return null;
        }

        // An explicitly-private constructor may instead use the "Prevents a default instance of the
        // <see cref="..."/> class from being created." phrasing, matching StyleCop's SA1642.
        var alternative = ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.PrivateKeyword)
            ? DocumentationConventions.PrivateConstructorStandardPrefix
            : null;
        return new SummaryPrefix(DocumentationConventions.ConstructorStandardPrefix, DocumentationRules.ConstructorStandardText, alternative);
    }

    /// <summary>Returns the accessor-summary requirement for a property.</summary>
    /// <param name="property">The property declaration.</param>
    /// <returns>The required prefix and diagnostic rule.</returns>
    private static SummaryPrefix PropertySummaryRequirement(PropertyDeclarationSyntax property)
        => DocumentationConventions.HasRestrictedWriteAccessor(property)
            ? new("Gets ", DocumentationRules.PropertySummaryOmitsRestrictedSetter)
            : new SummaryPrefix(DocumentationConventions.PropertyAccessorPrefix(property), DocumentationRules.PropertySummaryAccessors);

    /// <summary>Runs every applicable documentation check for one member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The member declaration.</param>
    /// <param name="shape">The member's documentation shape.</param>
    private static void Check(SyntaxNodeAnalysisContext context, SyntaxNode member, in MemberDoc shape)
    {
        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null)
        {
            if (!shape.SkipCoverage)
            {
                var coverage = DocumentationOptions.ReadCoverage(context.Options.AnalyzerConfigOptionsProvider.GetOptions(member.SyntaxTree));
                if (DocumentationVisibility.NeedsDocumentation(member, coverage))
                {
                    context.ReportDiagnostic(Diagnostic.Create(shape.MissingDocRule, shape.NameToken.GetLocation(), shape.NameToken.ValueText));
                }
            }

            return;
        }

        // A top-level <inheritdoc> takes the whole member's documentation from the
        // base member, so no content rule applies anywhere in the comment. This
        // mirrors the original up-front IsInheritDoc guard: it must suppress every
        // other diagnostic, including those for content appearing before the
        // <inheritdoc> in document order.
        if (XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        // Single pass over the documentation content: classify each child node once
        // (computing its element name a single time), capture the summary/returns
        // elements, and run the per-element <param>/<typeparam>/prose checks inline.
        // This replaces the former independent CheckParameterDocs / CheckTypeParameterDocs
        // / CheckTerminalPeriods passes over the same content.
        ScanContent(context, documentation, in shape, out var summary, out var returns);

        CheckSummary(context, summary, shape.NameToken, shape.SummaryRequirement);
        CheckParameters(context, documentation, shape.Parameters);
        CheckTypeParameters(context, documentation, shape.TypeParameters);
        CheckReturns(context, returns, shape.NameToken, shape.ReturnType);
    }

    /// <summary>Scans the documentation content once, capturing the summary/returns elements and running the per-node checks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="shape">The member's documentation shape.</param>
    /// <param name="summary">The first <c>&lt;summary&gt;</c> element, if any.</param>
    /// <param name="returns">The first <c>&lt;returns&gt;</c> element, if any.</param>
    private static void ScanContent(
        SyntaxNodeAnalysisContext context,
        DocumentationCommentTriviaSyntax documentation,
        in MemberDoc shape,
        out XmlNodeSyntax? summary,
        out XmlNodeSyntax? returns)
    {
        summary = null;
        returns = null;
        foreach (var node in documentation.Content)
        {
            var name = XmlDocumentationHelper.GetElementName(node);
            switch (name)
            {
                case "summary":
                {
                    summary ??= node;
                    break;
                }

                case "returns":
                {
                    returns ??= node;
                    break;
                }

                case "param":
                {
                    CheckParameterDoc(context, node, shape.Parameters);
                    break;
                }

                case "typeparam":
                {
                    CheckTypeParameterDoc(context, node, shape.TypeParameters);
                    break;
                }
            }

            CheckProseTerminalPeriod(context, node, name);
        }
    }

    /// <summary>Reports SST1629 when a prose element does not end with a period.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The documentation content node.</param>
    /// <param name="name">The node's element name.</param>
    private static void CheckProseTerminalPeriod(SyntaxNodeAnalysisContext context, XmlNodeSyntax node, string? name)
    {
        if (node is not XmlElementSyntax prose
            || !IsProseElement(name)
            || !XmlDocumentationHelper.NeedsTerminalPeriod(prose, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextMustEndWithPeriod, prose.GetLocation()));
    }

    /// <summary>Reports a missing or empty summary, or one that does not follow its required leading-text convention.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="summary">The first <c>&lt;summary&gt;</c> element captured during the single content pass, or <see langword="null"/>.</param>
    /// <param name="nameToken">The member's identifier.</param>
    /// <param name="requirement">A required leading-text convention, or <see langword="null"/>.</param>
    private static void CheckSummary(SyntaxNodeAnalysisContext context, XmlNodeSyntax? summary, SyntaxToken nameToken, SummaryPrefix? requirement)
    {
        if (summary is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.MustHaveSummary, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (summary is not XmlElementSyntax element)
        {
            // A self-closing <summary/> has no content.
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SummaryMustHaveText, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        // A <summary> containing <inheritdoc> takes its content from the base member.
        if (XmlDocumentationHelper.ContainsInheritDoc(element))
        {
            return;
        }

        if (!XmlDocumentationHelper.HasText(element))
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SummaryMustHaveText, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (XmlDocumentationHelper.LeadingTextStartsWith(element, "Summary description here".AsSpan()))
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.NoDefaultSummary, element.GetLocation(), nameToken.ValueText));
            return;
        }

        if (requirement is not { } required)
        {
            return;
        }

        if (SummaryHasRequiredPrefix(element, required))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(required.Rule, element.GetLocation(), required.Text));
    }

    /// <summary>Returns whether a summary's leading text satisfies its required convention.</summary>
    /// <param name="element">The <c>&lt;summary&gt;</c> element.</param>
    /// <param name="required">The required leading-text convention.</param>
    /// <returns><see langword="true"/> when the leading text matches the convention.</returns>
    private static bool SummaryHasRequiredPrefix(XmlElementSyntax element, SummaryPrefix required)
    {
        if (required.Rule.Id == DocumentationRules.PropertySummaryOmitsRestrictedSetter.Id)
        {
            // A "Gets " requirement for a restricted setter must not be satisfied by "Gets or sets ".
            return XmlDocumentationHelper.LeadingTextStartsWith(element, required.Text.AsSpan())
                && !XmlDocumentationHelper.LeadingTextStartsWith(element, "Gets or sets ".AsSpan());
        }

        return XmlDocumentationHelper.LeadingTextStartsWith(element, required.Text.AsSpan())
            || (required.AlternativeText is { } alternative && XmlDocumentationHelper.LeadingTextStartsWith(element, alternative.AsSpan()));
    }

    /// <summary>Reports parameters that lack a matching <c>&lt;param&gt;</c> element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="parameters">The member's parameters.</param>
    private static void CheckParameters(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation, in SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        foreach (var parameter in parameters)
        {
            var name = parameter.Identifier.ValueText;
            if (name.Length == 0)
            {
                continue;
            }

            var element = XmlDocumentationHelper.FindParameterElement(documentation, name);
            if (element is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ParametersMustBeDocumented, parameter.Identifier.GetLocation(), name));
                continue;
            }

            if (!XmlDocumentationHelper.HasText(element))
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ParameterDocumentationMustHaveText, parameter.Identifier.GetLocation(), name));
            }
        }
    }

    /// <summary>Reports a single <c>&lt;param&gt;</c> element that lacks a name (SST1613) or names a non-existent parameter (SST1612).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The <c>&lt;param&gt;</c> element node.</param>
    /// <param name="parameters">The member's parameters.</param>
    private static void CheckParameterDoc(SyntaxNodeAnalysisContext context, XmlNodeSyntax node, in SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        var name = XmlDocumentationHelper.NameAttribute(node);
        if (name is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ParameterDocumentationMustDeclareName, node.GetLocation()));
            return;
        }

        if (ContainsName(parameters, name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ParameterDocumentationMustMatch, node.GetLocation(), name));
    }

    /// <summary>Returns whether <paramref name="parameters"/> contains a parameter named <paramref name="name"/>.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when present.</returns>
    private static bool ContainsName(in SeparatedSyntaxList<ParameterSyntax> parameters, string name)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports type parameters that lack a matching <c>&lt;typeparam&gt;</c> element.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="typeParameters">The member's type parameters.</param>
    private static void CheckTypeParameters(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation, in SeparatedSyntaxList<TypeParameterSyntax> typeParameters)
    {
        foreach (var typeParameter in typeParameters)
        {
            var name = typeParameter.Identifier.ValueText;
            if (name.Length == 0)
            {
                continue;
            }

            var element = XmlDocumentationHelper.FindTypeParameterElement(documentation, name);
            if (element is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TypeParametersMustBeDocumented, typeParameter.Identifier.GetLocation(), name));
                continue;
            }

            if (!XmlDocumentationHelper.HasText(element))
            {
                context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TypeParameterDocumentationMustHaveText, typeParameter.Identifier.GetLocation(), name));
            }
        }
    }

    /// <summary>Reports a single <c>&lt;typeparam&gt;</c> element that lacks a name (SST1621) or names a non-existent type parameter (SST1620).</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="node">The <c>&lt;typeparam&gt;</c> element node.</param>
    /// <param name="typeParameters">The member's type parameters.</param>
    private static void CheckTypeParameterDoc(SyntaxNodeAnalysisContext context, XmlNodeSyntax node, in SeparatedSyntaxList<TypeParameterSyntax> typeParameters)
    {
        var name = XmlDocumentationHelper.NameAttribute(node);
        if (name is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TypeParameterDocumentationMustDeclareName, node.GetLocation()));
            return;
        }

        if (ContainsTypeName(typeParameters, name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TypeParameterDocumentationMustMatch, node.GetLocation(), name));
    }

    /// <summary>Returns whether <paramref name="typeParameters"/> contains a type parameter named <paramref name="name"/>.</summary>
    /// <param name="typeParameters">The type parameters.</param>
    /// <param name="name">The name to find.</param>
    /// <returns><see langword="true"/> when present.</returns>
    private static bool ContainsTypeName(in SeparatedSyntaxList<TypeParameterSyntax> typeParameters, string name)
    {
        foreach (var typeParameter in typeParameters)
        {
            if (typeParameter.Identifier.ValueText == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports a missing <c>&lt;returns&gt;</c> for a non-void member, or a present one for a void member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="returns">The first <c>&lt;returns&gt;</c> element captured during the single content pass, or <see langword="null"/>.</param>
    /// <param name="nameToken">The member's identifier.</param>
    /// <param name="returnType">The return type, or <see langword="null"/>.</param>
    private static void CheckReturns(SyntaxNodeAnalysisContext context, XmlNodeSyntax? returns, SyntaxToken nameToken, TypeSyntax? returnType)
    {
        if (returnType is null)
        {
            return;
        }

        switch (IsVoidLike(returnType))
        {
            case true when returns is not null:
                {
                    context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.VoidMustNotHaveReturn, nameToken.GetLocation(), nameToken.ValueText));
                    return;
                }

            case true:
                return;
        }

        if (returns is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ReturnValueMustBeDocumented, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (XmlDocumentationHelper.HasText(returns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ReturnDocumentationMustHaveText, returns.GetLocation()));
    }

    /// <summary>Returns whether a member is an override or explicit interface implementation, or partial.</summary>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <param name="explicitInterface">The explicit interface specifier, if any.</param>
    /// <returns><see langword="true"/> when coverage should be skipped.</returns>
    private static bool Skips(SyntaxTokenList modifiers, ExplicitInterfaceSpecifierSyntax? explicitInterface)
        => explicitInterface is not null
            || ModifierListHelper.ContainsEither(modifiers, SyntaxKind.OverrideKeyword, SyntaxKind.PartialKeyword);

    /// <summary>Returns whether a return type is <see langword="void"/>.</summary>
    /// <param name="returnType">The return type.</param>
    /// <returns><see langword="true"/> when the type carries no documentable return value.</returns>
    private static bool IsVoidLike(TypeSyntax returnType)
        => returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);

    /// <summary>Returns the type parameters of an optional type parameter list.</summary>
    /// <param name="list">The type parameter list, or <see langword="null"/>.</param>
    /// <returns>The type parameters, or an empty list.</returns>
    private static SeparatedSyntaxList<TypeParameterSyntax> TypeParametersOf(TypeParameterListSyntax? list)
        => list?.Parameters ?? default;

    /// <summary>Returns whether an element name carries prose subject to the terminal-period rule.</summary>
    /// <param name="name">The element name.</param>
    /// <returns><see langword="true"/> for prose elements.</returns>
    private static bool IsProseElement(string? name)
        => name is "summary" or "returns" or "remarks" or "value";

    /// <summary>The documentation-relevant shape of a member declaration.</summary>
    /// <param name="NameToken">The member's identifier (used for naming and report locations).</param>
    /// <param name="Parameters">The member's parameters, if any.</param>
    /// <param name="TypeParameters">The member's type parameters, if any.</param>
    /// <param name="ReturnType">The member's return type, or <see langword="null"/> when it has none.</param>
    /// <param name="SkipCoverage">Whether the "must be documented" rule is skipped (override / partial / explicit interface).</param>
    /// <param name="MissingDocRule">The rule to report when an exposed member has no documentation.</param>
    /// <param name="SummaryRequirement">A required leading-text convention for the summary, or <see langword="null"/>.</param>
    private readonly record struct MemberDoc(
        SyntaxToken NameToken,
        SeparatedSyntaxList<ParameterSyntax> Parameters,
        SeparatedSyntaxList<TypeParameterSyntax> TypeParameters,
        TypeSyntax? ReturnType,
        bool SkipCoverage,
        DiagnosticDescriptor MissingDocRule,
        SummaryPrefix? SummaryRequirement);

    /// <summary>A required leading-text convention for a summary (e.g. "Gets or sets ").</summary>
    /// <param name="Text">The expected leading text.</param>
    /// <param name="Rule">The rule reported when the summary does not start with the text.</param>
    /// <param name="AlternativeText">An additional accepted leading text, or <see langword="null"/>.</param>
    private readonly record struct SummaryPrefix(string Text, DiagnosticDescriptor Rule, string? AlternativeText = null);
}
