// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ConcurrencyRules.NoConcurrentSnapshotEnumeration);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var dictionaryTypes = new DictionaryTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeForEach(nodeContext, dictionaryTypes),
                SyntaxKind.ForEachStatement,
                SyntaxKind.ForEachVariableStatement);
        });
    }

    /// <summary>Returns the snapshot property access when a foreach expression has the <c>x.Keys</c>/<c>x.Values</c> shape.</summary>
    /// <param name="statement">The foreach statement to inspect.</param>
    /// <returns>The member access, or <see langword="null"/> when the shape does not match.</returns>
    internal static MemberAccessExpressionSyntax? TryGetSnapshotAccess(CommonForEachStatementSyntax statement) =>
        statement.Expression is MemberAccessExpressionSyntax access
            && access.Name.Identifier.ValueText is KeysPropertyName or ValuesPropertyName
            ? access
            : null;

    /// <summary>Reports PSH1305 for a foreach over a concurrent dictionary's Keys or Values snapshot.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="dictionaryTypes">The deferred concurrent dictionary type definition.</param>
    private static void AnalyzeForEach(in SyntaxNodeAnalysisContext context, DictionaryTypes dictionaryTypes)
    {
        var statement = (CommonForEachStatementSyntax)context.Node;
        if (TryGetSnapshotAccess(statement) is not { } access
            || dictionaryTypes.Get() is not { } dictionaryType)
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

    /// <summary>Resolves the dictionary definition only when a snapshot candidate needs it.</summary>
    /// <param name="compilation">The compilation whose framework types are resolved.</param>
    private sealed class DictionaryTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the concurrent dictionary type.</summary>
        private const string ConcurrentDictionaryMetadataName = "System.Collections.Concurrent.ConcurrentDictionary`2";

        /// <summary>The cached definition, including a missing-type result.</summary>
        private INamedTypeSymbol?[]? _resolved;

        /// <summary>Gets the definition, resolving it on first demand.</summary>
        /// <returns>The dictionary definition, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? Get() => (_resolved ??= [compilation.GetTypeByMetadataName(ConcurrentDictionaryMetadataName)])[0];
    }
}
