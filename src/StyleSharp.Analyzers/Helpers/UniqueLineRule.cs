// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared descriptor and registration wiring for the "parameters/arguments must be on unique lines"
/// analyzer family (SST1150-SST1171). Every per-syntax-kind analyzer in the family carries an identical
/// descriptor and an identical registration skeleton; only its diagnostic id, whether it reports about
/// parameters or arguments, the syntax kinds it registers for, and how it pulls the list out of its node
/// differ. Those are the only things each analyzer passes here.
/// </summary>
internal static class UniqueLineRule
{
    /// <summary>The category shared by every rule in the family.</summary>
    private const string Category = "Readability";

    /// <summary>Builds the descriptor for a rule that reports parameters spread onto shared lines.</summary>
    /// <param name="id">The diagnostic id (for example <c>SST1150</c>).</param>
    /// <returns>The descriptor carrying the family's parameter-worded resources.</returns>
    public static DiagnosticDescriptor ForParameters(string id)
        => Create(
            id,
            nameof(Resources.ParameterAnalyzerTitle),
            nameof(Resources.ParameterAnalyzerMessageFormat),
            nameof(Resources.ParameterAnalyzerDescription));

    /// <summary>Builds the descriptor for a rule that reports arguments spread onto shared lines.</summary>
    /// <param name="id">The diagnostic id (for example <c>SST1154</c>).</param>
    /// <returns>The descriptor carrying the family's argument-worded resources.</returns>
    public static DiagnosticDescriptor ForArguments(string id)
        => Create(
            id,
            nameof(Resources.ArgumentAnalyzerTitle),
            nameof(Resources.ArgumentAnalyzerMessageFormat),
            nameof(Resources.ArgumentAnalyzerDescription));

    /// <summary>Registers the family's syntax-node action for one node kind set, casting and reporting per node.</summary>
    /// <typeparam name="TNode">The concrete syntax node the diagnostic reports on.</typeparam>
    /// <param name="context">The analysis context to register against.</param>
    /// <param name="rule">The descriptor the reported diagnostic uses.</param>
    /// <param name="handle">Pulls the parameter/argument list out of the node and reports against it.</param>
    /// <param name="syntaxKinds">The syntax kinds the analyzer subscribes to.</param>
    /// <remarks>
    /// <see cref="AnalysisContext.EnableConcurrentExecution"/> and
    /// <see cref="AnalysisContext.ConfigureGeneratedCodeAnalysis"/> stay in each analyzer's
    /// <c>Initialize</c> because the analyzer-design rules that require them look for those calls there.
    /// </remarks>
    public static void Register<TNode>(
        AnalysisContext context,
        DiagnosticDescriptor rule,
        Action<SyntaxNodeAnalysisContext, TNode, DiagnosticDescriptor> handle,
        params SyntaxKind[] syntaxKinds)
        where TNode : SyntaxNode
        => context.RegisterSyntaxNodeAction(
            nodeContext =>
            {
                if (nodeContext.Node is not TNode node)
                {
                    return;
                }

                handle(nodeContext, node, rule);
            },
            syntaxKinds);

    /// <summary>Builds a family descriptor from its id and resource keys.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <param name="titleResource">The resource key for the title.</param>
    /// <param name="messageResource">The resource key for the message format.</param>
    /// <param name="descriptionResource">The resource key for the description.</param>
    /// <returns>The constructed descriptor.</returns>
    private static DiagnosticDescriptor Create(string id, string titleResource, string messageResource, string descriptionResource)
        => new(
            id,
            new LocalizableResourceString(titleResource, Resources.ResourceManager, typeof(Resources)),
            new LocalizableResourceString(messageResource, Resources.ResourceManager, typeof(Resources)),
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: new LocalizableResourceString(descriptionResource, Resources.ResourceManager, typeof(Resources)),
            helpLinkUri: $"https://github.com/glennawatson/RoslynCommonAnalyzers/blob/main/docs/rules/{id}.md");
}
