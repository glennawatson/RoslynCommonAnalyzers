// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags <c>foreach</c> loops that enumerate a <c>ConcurrentDictionary</c>'s <c>Keys</c> or
/// <c>Values</c> property (PSH1305). Both properties lock every bucket and copy the whole
/// collection into a fresh list on each access, while enumerating the dictionary itself is
/// lock-free and allocates only an enumerator. The foreach expression is gated on the
/// <c>.Keys</c>/<c>.Values</c> member-access shape before the receiver is bound.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1305NoConcurrentSnapshotEnumerationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The keys property name.</summary>
    internal const string KeysPropertyName = "Keys";

    /// <summary>The values property name.</summary>
    internal const string ValuesPropertyName = "Values";

    /// <summary>The metadata name of the concurrent dictionary type.</summary>
    private const string ConcurrentDictionaryMetadataName = "System.Collections.Concurrent.ConcurrentDictionary`2";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.NoConcurrentSnapshotEnumeration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var dictionaryType = start.Compilation.GetTypeByMetadataName(ConcurrentDictionaryMetadataName);
            if (dictionaryType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForEach(nodeContext, dictionaryType),
                SyntaxKind.ForEachStatement,
                SyntaxKind.ForEachVariableStatement);
        });
    }

    /// <summary>Returns the snapshot property access when a foreach expression has the <c>x.Keys</c>/<c>x.Values</c> shape.</summary>
    /// <param name="statement">The foreach statement to inspect.</param>
    /// <returns>The member access, or <see langword="null"/> when the shape does not match.</returns>
    internal static MemberAccessExpressionSyntax? TryGetSnapshotAccess(CommonForEachStatementSyntax statement)
        => statement.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText is KeysPropertyName or ValuesPropertyName
            ? access
            : null;

    /// <summary>Reports PSH1305 for a foreach over a concurrent dictionary's Keys or Values snapshot.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dictionaryType">The concurrent dictionary type definition.</param>
    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context, INamedTypeSymbol dictionaryType)
    {
        var statement = (CommonForEachStatementSyntax)context.Node;
        if (TryGetSnapshotAccess(statement) is not { } access)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (receiverType is not INamedTypeSymbol named
            || !SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dictionaryType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.NoConcurrentSnapshotEnumeration,
            access.SyntaxTree,
            access.Span,
            access.Name.Identifier.ValueText));
    }
}
