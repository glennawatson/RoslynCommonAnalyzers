// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>set</c>, <c>init</c>, <c>add</c>, or <c>remove</c> accessor whose body never reads the
/// incoming <c>value</c> (SST2429), so the assignment or subscription it was handed is silently discarded.
/// </summary>
/// <remarks>
/// The whole check is a token scan. A local named <c>value</c> cannot be declared inside such an accessor —
/// the compiler forbids it — so an accessor body that contains no <c>value</c> identifier token demonstrably
/// never reads the value, and no semantic model is needed to prove it. An accessor that only throws is left
/// alone (it is refusing the write on purpose), as is an empty body and an auto-implemented accessor, which
/// carries no body to scan.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2429AccessorIgnoresValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The contextual parameter every write accessor is handed.</summary>
    private const string ValueParameterName = "value";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.AccessorIgnoresValue);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.SetAccessorDeclaration,
            SyntaxKind.InitAccessorDeclaration,
            SyntaxKind.AddAccessorDeclaration,
            SyntaxKind.RemoveAccessorDeclaration);
    }

    /// <summary>Analyzes one write accessor for a body that never reads its value.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var accessor = (AccessorDeclarationSyntax)context.Node;

        // An auto-implemented accessor (set;) has neither a block nor an expression body to scan.
        SyntaxNode? body = accessor.Body;
        if (body is BlockSyntax { Statements.Count: 0 })
        {
            // An empty body is a recognised, deliberate no-op.
            return;
        }

        body ??= accessor.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var scan = default(BodyScan);
        DescendantTraversalHelper.VisitDescendantTokens(body, ref scan, VisitToken);
        if (scan.ReadsValue || scan.Throws)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.AccessorIgnoresValue,
            accessor.Keyword.GetLocation(),
            accessor.Keyword.ValueText));
    }

    /// <summary>Records whether a body reads <c>value</c> or throws.</summary>
    /// <param name="token">The token being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once <c>value</c> is seen, which stops the walk.</returns>
    private static bool VisitToken(in SyntaxToken token, ref BodyScan state)
    {
        if (token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == ValueParameterName)
        {
            state.ReadsValue = true;
            return false;
        }

        state.Throws |= token.IsKind(SyntaxKind.ThrowKeyword);
        return true;
    }

    /// <summary>The state threaded through a write accessor's token scan.</summary>
    private record struct BodyScan
    {
        /// <summary>Gets or sets a value indicating whether the body reads <c>value</c>.</summary>
        public bool ReadsValue { get; set; }

        /// <summary>Gets or sets a value indicating whether the body throws.</summary>
        public bool Throws { get; set; }
    }
}
