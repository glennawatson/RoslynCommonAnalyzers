// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>[Obsolete]</c> attribute that carries no message, or a message that is empty or only
/// whitespace (SST2308). The attribute's whole value is the message: it is the one place the author can
/// hand the reader the migration, at exactly the moment the reader needs it. Without one, the compiler
/// tells a caller their code is wrong and nothing else.
/// </summary>
/// <remarks>
/// The attribute's simple name is matched first, on syntax alone, which rejects every other attribute
/// in the file for free. The message argument is then found by position and by name — <c>[Obsolete]</c>,
/// <c>[Obsolete(error: true)]</c> and <c>[Obsolete(DiagnosticId = "…")]</c> all supply none — and a
/// string literal is read straight off the token. Only an argument that is not a literal costs a
/// constant evaluation, and only a declaration that is about to be reported is bound at all, which is
/// what confirms the attribute really is the framework's and not a type of the same name.
/// <para>
/// A message that is supplied but carries no <c>DiagnosticId</c> is SST2314's business, not this rule's.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2308ObsoleteWithoutExplanationAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ObsoleteWithoutExplanation);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    /// <summary>Reports one obsolete attribute that explains nothing.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!ObsoleteAttributeFacts.IsObsoleteName(attribute.Name)
            || !ObsoleteAttributeFacts.HasNoUsableMessage(context.SemanticModel, attribute, context.CancellationToken))
        {
            return;
        }

        var target = ObsoleteAttributeFacts.GetAnnotatedName(attribute.Parent?.Parent);
        if (target.Length == 0
            || !ObsoleteAttributeFacts.IsFrameworkObsoleteAttribute(context.SemanticModel, attribute, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.ObsoleteWithoutExplanation,
            attribute.GetLocation(),
            target));
    }
}
