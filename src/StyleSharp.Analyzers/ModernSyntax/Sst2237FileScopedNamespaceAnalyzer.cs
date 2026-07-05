// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports a single block-scoped namespace that can be represented as file-scoped syntax (SST2237).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2237FileScopedNamespaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 10 language-version value.</summary>
    private const int CSharp10 = 1000;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseFileScopedNamespace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeNamespace, SyntaxKind.NamespaceDeclaration);
    }

    /// <summary>Reports a block-scoped namespace when it is the file's only member.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
    {
        var namespaceDeclaration = (NamespaceDeclarationSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(namespaceDeclaration, CSharp10)
            || namespaceDeclaration.Parent is not CompilationUnitSyntax compilationUnit
            || compilationUnit.Members.Count != 1
            || compilationUnit.Members[0] != namespaceDeclaration)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            ModernSyntaxRules.UseFileScopedNamespace,
            namespaceDeclaration.Name.GetLocation(),
            namespaceDeclaration.Name.ToString()));
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
