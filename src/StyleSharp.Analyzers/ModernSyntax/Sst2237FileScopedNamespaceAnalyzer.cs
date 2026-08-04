// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a namespace declared in the form the file does not use (SST2237): by default a single
/// block-scoped namespace that could be file-scoped, and the converse where the project has configured
/// <c>namespace_declaration_style = block_scoped</c>.
/// </summary>
/// <remarks>
/// Both directions belong to one rule and one setting. Shipping a rule per direction lets a project
/// enable both, and then no file satisfies either — converting to one form immediately trips the other.
/// A single setting can only ask for one form at a time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2237FileScopedNamespaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 10 language-version value.</summary>
    private const int CSharp10 = 1000;

    /// <summary>The message argument naming the file-scoped form.</summary>
    private const string FileScopedDescription = "file-scoped";

    /// <summary>The message argument naming the block-scoped form.</summary>
    private const string BlockScopedDescription = "block-scoped";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseFileScopedNamespace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeNamespace, SyntaxKind.NamespaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeFileScopedNamespace, SyntaxKind.FileScopedNamespaceDeclaration);
    }

    /// <summary>Reports a block-scoped namespace when it is the file's only member and file-scoped is wanted.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// The syntactic gate runs first and rejects every multi-namespace file, so the configured style is
    /// only read for a declaration that could actually be rewritten.
    /// </remarks>
    private static void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
    {
        var namespaceDeclaration = (NamespaceDeclarationSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(namespaceDeclaration, CSharp10)
            || namespaceDeclaration.Parent is not CompilationUnitSyntax compilationUnit
            || compilationUnit.Members.Count != 1
            || compilationUnit.Members[0] != namespaceDeclaration
            || ReadStyle(context) != NamespaceDeclarationStyle.FileScoped)
        {
            return;
        }

        Report(context, namespaceDeclaration.Name, FileScopedDescription);
    }

    /// <summary>Reports a file-scoped namespace where the project has asked for the block-scoped form.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <remarks>
    /// A file-scoped namespace is always convertible, so there is no syntactic gate to run before the
    /// setting. The read is two dictionary lookups that allocate nothing while the setting is unset,
    /// which is what the default configuration costs.
    /// </remarks>
    private static void AnalyzeFileScopedNamespace(SyntaxNodeAnalysisContext context)
    {
        if (ReadStyle(context) != NamespaceDeclarationStyle.BlockScoped)
        {
            return;
        }

        Report(context, ((FileScopedNamespaceDeclarationSyntax)context.Node).Name, BlockScopedDescription);
    }

    /// <summary>Reads the namespace form this tree is configured to use.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <returns>The configured style.</returns>
    private static NamespaceDeclarationStyle ReadStyle(SyntaxNodeAnalysisContext context)
        => ModernSyntaxStyleOptions.ReadNamespaceDeclarationStyle(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree));

    /// <summary>Reports one namespace declaration in the wrong form.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="name">The declared namespace name.</param>
    /// <param name="wanted">The form the project wants.</param>
    private static void Report(SyntaxNodeAnalysisContext context, NameSyntax name, string wanted)
        => context.ReportDiagnostic(Diagnostic.Create(
            ModernSyntaxRules.UseFileScopedNamespace,
            name.GetLocation(),
            name.ToString(),
            wanted));

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
