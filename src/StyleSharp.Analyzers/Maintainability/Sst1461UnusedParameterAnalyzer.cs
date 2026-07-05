// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports unused parameters only on private methods and local functions, where removing the
/// parameter is a local refactor rather than an API change. The scan is syntactic and bounded by a
/// 64-parameter bitmask, avoiding semantic binding on the common no-diagnostic path.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1461UnusedParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The largest parameter count tracked by the bitmask scan.</summary>
    private const int MaximumTrackedParameters = 64;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.RemoveUnusedPrivateParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeLocalFunction, SyntaxKind.LocalFunctionStatement);
    }

    /// <summary>Reports unread parameters on private methods.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!ModifierListHelper.Contains(method.Modifiers, SyntaxKind.PrivateKeyword)
            || method.ParameterList.Parameters.Count == 0
            || (method.Body is null && method.ExpressionBody is null)
            || method.AttributeLists.Count > 0
            || method.ExplicitInterfaceSpecifier is not null
            || HasArityOrDispatchModifier(method.Modifiers))
        {
            return;
        }

        AnalyzeParameters(context, method.ParameterList, method.Body ?? (SyntaxNode)method.ExpressionBody!);
    }

    /// <summary>Reports unread parameters on local functions.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (localFunction.ParameterList.Parameters.Count == 0 || (localFunction.Body is null && localFunction.ExpressionBody is null))
        {
            return;
        }

        AnalyzeParameters(context, localFunction.ParameterList, localFunction.Body ?? (SyntaxNode)localFunction.ExpressionBody!);
    }

    /// <summary>Scans a body for parameter identifier reads and reports the unread parameters.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="parameterList">The parameter list.</param>
    /// <param name="body">The declaration body.</param>
    private static void AnalyzeParameters(SyntaxNodeAnalysisContext context, ParameterListSyntax parameterList, SyntaxNode body)
    {
        var parameters = parameterList.Parameters;
        if (parameters.Count > MaximumTrackedParameters)
        {
            return;
        }

        var state = new ParameterScanState(parameters, parameterList.Span);
        DescendantTraversalHelper.VisitDescendantTokens(body, ref state, static (in SyntaxToken token, ref ParameterScanState scan) => scan.Observe(token));

        for (var i = 0; i < parameters.Count; i++)
        {
            var identifier = parameters[i].Identifier;
            if (state.IsSeen(i) || identifier.ValueText == "_" || parameters[i].Modifiers.Count > 0)
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                MaintainabilityRules.RemoveUnusedPrivateParameter,
                identifier.GetLocation(),
                identifier.ValueText));
        }
    }

    /// <summary>Returns whether a method shape should not have parameters removed locally.</summary>
    /// <param name="modifiers">The method modifiers.</param>
    /// <returns><see langword="true"/> when the signature may be consumed indirectly.</returns>
    private static bool HasArityOrDispatchModifier(SyntaxTokenList modifiers)
        => ModifierListHelper.Contains(modifiers, SyntaxKind.PartialKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.VirtualKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.AbstractKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(modifiers, SyntaxKind.ExternKeyword);

    /// <summary>Tracks parameters read by identifier token.</summary>
    private struct ParameterScanState : IEquatable<ParameterScanState>
    {
        /// <summary>The parameter list.</summary>
        private readonly SeparatedSyntaxList<ParameterSyntax> _parameters;

        /// <summary>The parameter-list span excluded from usage.</summary>
        private readonly TextSpan _parameterListSpan;

        /// <summary>The bitmask of seen parameters.</summary>
        private ulong _seenMask;

        /// <summary>The remaining unread parameter count.</summary>
        private int _remaining;

        /// <summary>Initializes a new instance of the <see cref="ParameterScanState"/> struct.</summary>
        /// <param name="parameters">The parameter list.</param>
        /// <param name="parameterListSpan">The parameter-list span.</param>
        public ParameterScanState(SeparatedSyntaxList<ParameterSyntax> parameters, TextSpan parameterListSpan)
        {
            _parameters = parameters;
            _parameterListSpan = parameterListSpan;
            _seenMask = 0;
            _remaining = parameters.Count;
        }

        /// <summary>Returns whether a parameter has been read.</summary>
        /// <param name="index">The parameter index.</param>
        /// <returns><see langword="true"/> when the parameter is seen.</returns>
        public readonly bool IsSeen(int index) => (_seenMask & (1UL << index)) != 0;

        /// <summary>Returns whether two scan states are equivalent.</summary>
        /// <param name="other">The other state.</param>
        /// <returns><see langword="true"/> when the tracked state is equal.</returns>
        public readonly bool Equals(ParameterScanState other) => _seenMask == other._seenMask && _remaining == other._remaining;

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj) => obj is ParameterScanState other && Equals(other);

        /// <inheritdoc/>
        public override readonly int GetHashCode() => unchecked(((int)_seenMask * 397) ^ _remaining);

        /// <summary>Observes one token and returns whether scanning should continue.</summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="false"/> once every parameter has been seen.</returns>
        public bool Observe(in SyntaxToken token)
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) || _parameterListSpan.Contains(token.Span))
            {
                return _remaining > 0;
            }

            var text = token.ValueText;
            for (var i = 0; i < _parameters.Count; i++)
            {
                if (!IsSeen(i) && _parameters[i].Identifier.ValueText == text)
                {
                    _seenMask |= 1UL << i;
                    _remaining--;
                    break;
                }
            }

            return _remaining > 0;
        }
    }
}
