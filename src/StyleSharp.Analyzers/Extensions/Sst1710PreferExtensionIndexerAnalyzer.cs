// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a classic extension method that indexes its receiver, where a C# 15 extension indexer lets
/// callers write <c>receiver[index]</c> instead (SST1710).
/// </summary>
/// <remarks>
/// Scoped to <c>this</c>-parameter methods. An indexer is always an instance member, so the rewrite
/// target is an <c>extension(Receiver) { … }</c> block either way, and starting from the classic form
/// keeps the shape being matched unambiguous.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1710PreferExtensionIndexerAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method names that read as an element accessor rather than as a computation.</summary>
    private static readonly string[] AccessorNames = ["ElementAt", "ItemAt", "GetAt", "GetItem", "At", "Get", "Item"];

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(ExtensionRules.PreferExtensionIndexer);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(Analyze, SymbolKind.Method);
    }

    /// <summary>Reports one extension method whose shape is an indexer.</summary>
    /// <param name="context">The symbol analysis context.</param>
    private static void Analyze(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!IsAccessorShaped(method))
        {
            return;
        }

        if (method.DeclaringSyntaxReferences.IsEmpty
            || method.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax declaration
            || !LanguageVersions.SupportsCSharp15(declaration))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ExtensionRules.PreferExtensionIndexer,
            declaration.Identifier.GetLocation(),
            method.Name));
    }

    /// <summary>Gets whether an extension method takes a receiver and one index and returns an element.</summary>
    /// <param name="method">The method symbol.</param>
    /// <returns><see langword="true"/> when the shape matches an indexer.</returns>
    private static bool IsAccessorShaped(IMethodSymbol method) =>
        method.IsExtensionMethod
            && method.Parameters.Length == 2
            && !method.ReturnsVoid
            && method.TypeParameters.IsEmpty
            && IsIndexLike(method.Parameters[1])
            && IsAccessorName(method.Name);

    /// <summary>Gets whether a parameter could be an index.</summary>
    /// <param name="parameter">The parameter after the receiver.</param>
    /// <returns><see langword="true"/> for a by-value index-like parameter.</returns>
    private static bool IsIndexLike(IParameterSymbol parameter)
    {
        if (parameter.RefKind != RefKind.None || parameter.IsParams)
        {
            return false;
        }

        var type = parameter.Type;
        return type.SpecialType is SpecialType.System_Int32 or SpecialType.System_String
            || type.Name is "Index" or "Range";
    }

    /// <summary>Gets whether a method name reads as an element accessor.</summary>
    /// <param name="name">The method name.</param>
    /// <returns><see langword="true"/> when the name is one of the accessor names.</returns>
    private static bool IsAccessorName(string name)
    {
        foreach (var candidate in AccessorNames)
        {
            if (string.Equals(name, candidate, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
