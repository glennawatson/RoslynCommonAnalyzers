// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>&lt;exception cref="..."&gt;</c> element that names a type but never says what triggers it
/// (SST1665) — both the paired <c>&lt;exception ...&gt;&lt;/exception&gt;</c> and the self-closing
/// <c>&lt;exception ... /&gt;</c> form.
/// </summary>
/// <remarks>
/// <para>
/// The complement of SST1662: that rule finds a throw with no <c>&lt;exception&gt;</c> element, this one finds
/// the element with no reason. Both leave a caller unable to predict the failure, and an empty element is the
/// worse of the two because it reads as documented to anything counting exception coverage.
/// </para>
/// <para>
/// Only a top-level element of a documentation comment is judged. An <c>&lt;exception&gt;</c> written inside
/// <c>&lt;remarks&gt;</c> or an <c>&lt;example&gt;</c> is prose about exceptions rather than the member's own
/// exception contract, and the documentation pipeline does not read it as one either.
/// </para>
/// <para>
/// The rule is pure syntax over the documentation trivia the driver already parsed; the cref is rendered to
/// text only once a violation is certain, so the clean path allocates nothing.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1665ExceptionDescriptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The documentation element carrying a member's exception contract.</summary>
    private const string ExceptionElement = "exception";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DocumentationRules.ExceptionDescription);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.XmlElement, SyntaxKind.XmlEmptyElement);
    }

    /// <summary>Reports an <c>&lt;exception&gt;</c> element that names a type without describing its trigger.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = (XmlNodeSyntax)context.Node;

        // A nested <exception> is prose inside another section, not the member's exception contract.
        if (node.Parent is not DocumentationCommentTriviaSyntax
            || XmlDocumentationHelper.GetElementName(node) != ExceptionElement
            || !XmlDocumentationHelper.HasCref(node))
        {
            return;
        }

        // The self-closing form has nowhere to put a reason, so it is always empty. The paired form is judged
        // on its content.
        if (node is XmlElementSyntax element && !IsEmpty(element))
        {
            return;
        }

        var name = XmlDocumentationHelper.CrefSimpleName(node);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DocumentationRules.ExceptionDescription, node.GetLocation(), name));
    }

    /// <summary>Returns whether an element offers the reader nothing about when the exception fires.</summary>
    /// <param name="element">The <c>&lt;exception&gt;</c> element.</param>
    /// <returns><see langword="true"/> when the element holds neither prose nor a nested element.</returns>
    /// <remarks>
    /// A nested element is treated as content deliberately: a <c>&lt;paramref&gt;</c> or an
    /// <c>&lt;inheritdoc&gt;</c> standing alone is someone describing the trigger by reference rather than
    /// leaving the element blank, and second-guessing that would report working documentation.
    /// </remarks>
    private static bool IsEmpty(XmlElementSyntax element)
    {
        var content = element.Content;
        for (var i = 0; i < content.Count; i++)
        {
            if (content[i] is XmlElementSyntax or XmlEmptyElementSyntax)
            {
                return false;
            }
        }

        return !XmlDocumentationHelper.HasText(element);
    }
}
