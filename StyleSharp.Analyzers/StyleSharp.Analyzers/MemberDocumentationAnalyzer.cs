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
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        DocumentationRules.ElementsMustBeDocumented,
        DocumentationRules.EnumItemsMustBeDocumented,
        DocumentationRules.MustHaveSummary,
        DocumentationRules.SummaryMustHaveText,
        DocumentationRules.ParametersMustBeDocumented,
        DocumentationRules.ReturnValueMustBeDocumented,
        DocumentationRules.VoidMustNotHaveReturn,
        DocumentationRules.TypeParametersMustBeDocumented,
        DocumentationRules.PropertySummaryAccessors,
        DocumentationRules.ConstructorStandardText,
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
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.EnumMemberDeclaration);
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

        BaseTypeDeclarationSyntax type => new MemberDoc(
            type.Identifier,
            default,
            TypeParametersOf((type as TypeDeclarationSyntax)?.TypeParameterList),
            ReturnType: null,
            type.Modifiers.Any(SyntaxKind.PartialKeyword),
            DocumentationRules.ElementsMustBeDocumented,
            SummaryRequirement: null),

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

        PropertyDeclarationSyntax property => new MemberDoc(
            property.Identifier,
            default,
            default,
            ReturnType: null,
            Skips(property.Modifiers, property.ExplicitInterfaceSpecifier),
            DocumentationRules.ElementsMustBeDocumented,
            new SummaryPrefix(DocumentationConventions.PropertyAccessorPrefix(property), DocumentationRules.PropertySummaryAccessors)),

        EnumMemberDeclarationSyntax enumMember => new MemberDoc(
            enumMember.Identifier,
            default,
            default,
            ReturnType: null,
            SkipCoverage: false,
            DocumentationRules.EnumItemsMustBeDocumented,
            SummaryRequirement: null),

        _ => null,
    };

    /// <summary>Returns the standard-text requirement for a non-static constructor, or <see langword="null"/>.</summary>
    /// <param name="constructor">The constructor declaration.</param>
    /// <returns>The summary requirement, or <see langword="null"/>.</returns>
    private static SummaryPrefix? ConstructorRequirement(ConstructorDeclarationSyntax constructor)
        => constructor.Modifiers.Any(SyntaxKind.StaticKeyword)
            ? null
            : new SummaryPrefix(DocumentationConventions.ConstructorStandardPrefix, DocumentationRules.ConstructorStandardText);

    /// <summary>Runs every applicable documentation check for one member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="member">The member declaration.</param>
    /// <param name="shape">The member's documentation shape.</param>
    private static void Check(SyntaxNodeAnalysisContext context, SyntaxNode member, in MemberDoc shape)
    {
        var documentation = XmlDocumentationHelper.GetDocumentationComment(member);
        if (documentation is null)
        {
            if (!shape.SkipCoverage && DocumentationVisibility.IsExposed(member))
            {
                context.ReportDiagnostic(Diagnostic.Create(shape.MissingDocRule, shape.NameToken.GetLocation(), shape.NameToken.ValueText));
            }

            return;
        }

        if (XmlDocumentationHelper.IsInheritDoc(documentation))
        {
            return;
        }

        CheckSummary(context, documentation, shape.NameToken, shape.SummaryRequirement);
        CheckParameters(context, documentation, shape.Parameters);
        CheckTypeParameters(context, documentation, shape.TypeParameters);
        CheckReturns(context, documentation, shape.NameToken, shape.ReturnType);
        CheckTerminalPeriods(context, documentation);
    }

    /// <summary>Reports a missing or empty summary, or one that does not follow its required leading-text convention.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="nameToken">The member's identifier.</param>
    /// <param name="requirement">A required leading-text convention, or <see langword="null"/>.</param>
    private static void CheckSummary(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation, SyntaxToken nameToken, SummaryPrefix? requirement)
    {
        var summary = XmlDocumentationHelper.FindElement(documentation, "summary");
        if (summary is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.MustHaveSummary, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (!XmlDocumentationHelper.HasText(summary))
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.SummaryMustHaveText, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (requirement is not { } required
            || summary is not XmlElementSyntax element
            || XmlDocumentationHelper.LeadingTextStartsWith(element, required.Text.AsSpan()))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(required.Rule, element.GetLocation(), required.Text));
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
            if (name.Length == 0 || XmlDocumentationHelper.IsParameterDocumented(documentation, name))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ParametersMustBeDocumented, parameter.Identifier.GetLocation(), name));
        }
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
            if (name.Length == 0 || XmlDocumentationHelper.IsTypeParameterDocumented(documentation, name))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TypeParametersMustBeDocumented, typeParameter.Identifier.GetLocation(), name));
        }
    }

    /// <summary>Reports a missing <c>&lt;returns&gt;</c> for a non-void member, or a present one for a void member.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    /// <param name="nameToken">The member's identifier.</param>
    /// <param name="returnType">The return type, or <see langword="null"/>.</param>
    private static void CheckReturns(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation, SyntaxToken nameToken, TypeSyntax? returnType)
    {
        if (returnType is null)
        {
            return;
        }

        var isVoid = returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword);
        var hasReturns = XmlDocumentationHelper.HasElement(documentation, "returns");

        if (isVoid && hasReturns)
        {
            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.VoidMustNotHaveReturn, nameToken.GetLocation(), nameToken.ValueText));
            return;
        }

        if (isVoid || hasReturns)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ReturnValueMustBeDocumented, nameToken.GetLocation(), nameToken.ValueText));
    }

    /// <summary>Reports prose elements (summary, returns, remarks, value) whose text lacks terminal punctuation.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="documentation">The documentation comment.</param>
    private static void CheckTerminalPeriods(SyntaxNodeAnalysisContext context, DocumentationCommentTriviaSyntax documentation)
    {
        foreach (var node in documentation.Content)
        {
            if (node is not XmlElementSyntax element
                || !IsProseElement(XmlDocumentationHelper.GetElementName(element))
                || !XmlDocumentationHelper.NeedsTerminalPeriod(element, out _))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.TextMustEndWithPeriod, element.GetLocation()));
        }
    }

    /// <summary>Returns whether a member is an override or explicit interface implementation, or partial.</summary>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <param name="explicitInterface">The explicit interface specifier, if any.</param>
    /// <returns><see langword="true"/> when coverage should be skipped.</returns>
    private static bool Skips(SyntaxTokenList modifiers, ExplicitInterfaceSpecifierSyntax? explicitInterface)
        => explicitInterface is not null
            || modifiers.Any(SyntaxKind.OverrideKeyword)
            || modifiers.Any(SyntaxKind.PartialKeyword);

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
    private readonly record struct SummaryPrefix(string Text, DiagnosticDescriptor Rule);
}
