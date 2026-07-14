// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports every <c>[Obsolete]</c> attribute, including one that carries a message (SST2310). Deprecating
/// a member is the first half of removing it; this rule is the standing reminder to finish the job, and it
/// keeps reporting until the code is deleted.
/// </summary>
/// <remarks>
/// This is deliberately a nag rule, and it is the wrong rule for a library that must keep its obsolete
/// members for compatibility — there, <c>severity = none</c> is the right answer. It is the replacement for
/// the analyzer's the rule and matches its firing set: any <c>[Obsolete]</c>, on any declaration, message or
/// no message.
/// <para>
/// It overlaps its siblings by design, because they ask for different things: SST2308 wants the attribute to
/// carry a message at all, SST2314 wants that message to carry a <c>DiagnosticId</c>, and this rule wants the
/// deprecated code gone. An attribute can satisfy both of the others and still be reported here.
/// </para>
/// <para>
/// The attribute's simple name is matched on syntax first, which rejects every other attribute in the file
/// for free; only a candidate that is about to be reported is bound, which is what tells a real
/// <c>System.ObsoleteAttribute</c> from a type of the same name.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2310ObsoleteCodeShouldBeRemovedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.ObsoleteCodeShouldBeRemoved);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    /// <summary>Reports one deprecated declaration.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!ObsoleteAttributeFacts.IsObsoleteName(attribute.Name))
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
            DesignRules.ObsoleteCodeShouldBeRemoved,
            attribute.GetLocation(),
            target));
    }
}
