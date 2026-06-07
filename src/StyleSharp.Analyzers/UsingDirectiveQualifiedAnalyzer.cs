// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a using directive that names a namespace or type in a context-relative form rather than
/// fully qualified (SST1135). A fully qualified using does not depend on the enclosing namespace, so
/// it cannot break or change meaning when the file is moved or a namespace is added.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UsingDirectiveQualifiedAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ReadabilityRules.UsingDirectiveQualified);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.UsingDirective);
    }

    /// <summary>Returns the fully qualified name of the symbol without the <c>global::</c> prefix.</summary>
    /// <param name="symbol">The namespace or type symbol.</param>
    /// <returns>The fully qualified name.</returns>
    internal static string QualifiedName(ISymbol symbol)
        => StripGlobal(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

    /// <summary>Reports a using directive whose name is not fully qualified.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var directive = (UsingDirectiveSyntax)context.Node;
        if (directive.Alias is not null || directive.Name is null)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(directive.Name, context.CancellationToken).Symbol;
        if (symbol is not (INamespaceSymbol or INamedTypeSymbol))
        {
            return;
        }

        var qualified = QualifiedName(symbol);
        var written = StripGlobal(directive.Name.ToString());
        if (written == qualified)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.UsingDirectiveQualified, directive.Name.GetLocation(), qualified));
    }

    /// <summary>Removes a leading <c>global::</c> alias qualifier.</summary>
    /// <param name="name">The name to strip.</param>
    /// <returns>The name without the <c>global::</c> prefix.</returns>
    private static string StripGlobal(string name)
        => name.StartsWith("global::", StringComparison.Ordinal) ? name["global::".Length..] : name;
}
