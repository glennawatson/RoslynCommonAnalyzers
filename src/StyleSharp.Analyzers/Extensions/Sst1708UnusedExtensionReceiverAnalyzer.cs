// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a classic <c>this</c>-parameter extension method whose body never reads the receiver
/// parameter (SST1708). Such a method gains nothing from extension syntax. There is no code fix:
/// removing <c>this</c> would break every instance-style call site, so the author decides how to
/// re-shape it. The body is scanned only after the cheap syntactic gate confirms an extension method
/// with a real body, and the identifier scan stops at the first receiver read.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1708UnusedExtensionReceiverAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ExtensionRules.UnusedExtensionReceiver);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports an extension method whose body never references its receiver parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!ExtensionBlockHelper.IsClassicExtensionMethod(method))
        {
            return;
        }

        var body = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var receiver = method.ParameterList.Parameters[0].Identifier;
        var receiverName = receiver.ValueText;
        if (receiverName.Length == 0 || receiverName == "_")
        {
            return;
        }

        var scan = new ReceiverUsageScan(receiverName);
        DescendantTraversalHelper.VisitDescendantTokens(body, ref scan, static (in SyntaxToken token, ref ReceiverUsageScan state) => state.Observe(token));
        if (scan.Used)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ExtensionRules.UnusedExtensionReceiver,
            receiver.GetLocation(),
            method.Identifier.ValueText,
            receiverName));
    }

    /// <summary>Tracks whether an identifier token naming the receiver has been seen in the body.</summary>
    private struct ReceiverUsageScan : IEquatable<ReceiverUsageScan>
    {
        /// <summary>The receiver parameter name to look for.</summary>
        private readonly string _receiverName;

        /// <summary>Whether the receiver name has been read.</summary>
        private bool _used;

        /// <summary>Initializes a new instance of the <see cref="ReceiverUsageScan"/> struct.</summary>
        /// <param name="receiverName">The receiver parameter name.</param>
        public ReceiverUsageScan(string receiverName)
        {
            _receiverName = receiverName;
            _used = false;
        }

        /// <summary>Gets a value indicating whether the receiver name was read.</summary>
        public readonly bool Used => _used;

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(ReceiverUsageScan other) => _used == other._used && string.Equals(_receiverName, other._receiverName, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is ReceiverUsageScan other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked((_receiverName.GetHashCode() * 397) ^ (_used ? 1 : 0));

        /// <summary>Observes one token and returns whether scanning should continue.</summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="false"/> once the receiver name has been read.</returns>
        public bool Observe(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || !string.Equals(token.ValueText, _receiverName, StringComparison.Ordinal))
            {
                return true;
            }

            _used = true;
            return false;
        }
    }
}
