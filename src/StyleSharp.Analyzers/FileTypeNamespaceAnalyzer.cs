// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires a file to declare a single top-level type (SST1402) and a single
/// namespace (SST1403). Partial declarations of the same type count once; nested
/// types are ignored. The file is scanned once.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileTypeNamespaceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.SingleType,
        MaintainabilityRules.SingleNamespace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Scans the file's top-level types and namespaces, reporting any beyond the first.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        var typeKeys = new HashSet<string>(StringComparer.Ordinal);
        var firstTypeSeen = false;
        var firstNamespaceSeen = false;

        foreach (var node in root.DescendantNodes(static descend => descend is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax))
        {
            if (node is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                if (firstNamespaceSeen)
                {
                    var name = namespaceDeclaration.Name;
                    context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.SingleNamespace, name.GetLocation(), name.ToString()));
                }

                firstNamespaceSeen = true;
            }
            else if (node is BaseTypeDeclarationSyntax type && typeKeys.Add(TypeKey(type)))
            {
                if (firstTypeSeen)
                {
                    context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.SingleType, type.Identifier.GetLocation(), type.Identifier.ValueText));
                }

                firstTypeSeen = true;
            }
        }
    }

    /// <summary>Returns a name-and-arity key so partial declarations of the same type count once.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The type key.</returns>
    private static string TypeKey(BaseTypeDeclarationSyntax type)
    {
        var arity = type is TypeDeclarationSyntax declaration ? declaration.TypeParameterList?.Parameters.Count ?? 0 : 0;
        return $"{type.Identifier.ValueText}`{arity}";
    }
}
