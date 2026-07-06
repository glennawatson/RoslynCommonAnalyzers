// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags a <c>foreach</c> statement that enumerates the result of a parameterless
/// <c>System.Linq.Enumerable</c> <c>ToList()</c> or <c>ToArray()</c> call (PSH1120) — the copy
/// is consumed once by the loop and discarded, so the source can be enumerated directly. Before
/// reporting, the loop body is scanned for the receiver's root identifier: a body that mentions
/// the source again may be materializing on purpose to survive mutation during enumeration, so
/// those loops stay clean. <c>await foreach</c> is skipped, and the rule is resolved once per
/// compilation by probing for <c>System.Linq.Enumerable</c>, so it costs nothing when LINQ is
/// absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1120DoNotMaterializeToEnumerateAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The list materialization method name.</summary>
    internal const string ToListMethodName = "ToList";

    /// <summary>The array materialization method name.</summary>
    internal const string ToArrayMethodName = "ToArray";

    /// <summary>The metadata name of the LINQ extension-method host type.</summary>
    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.DoNotMaterializeToEnumerate);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(EnumerableMetadataName) is not { } enumerableType)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeForEach(nodeContext, enumerableType), SyntaxKind.ForEachStatement);
        });
    }

    /// <summary>Returns whether an invocation is a parameterless member-access ToList/ToArray call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the call has the materialization shape.</returns>
    internal static bool IsMaterializeInvocationShape(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name.Identifier.ValueText is ToListMethodName or ToArrayMethodName;

    /// <summary>Reports PSH1120 for a foreach that enumerates a ToList/ToArray copy it then discards.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context, INamedTypeSymbol enumerableType)
    {
        var forEach = (ForEachStatementSyntax)context.Node;
        if (forEach.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
            || forEach.Expression is not InvocationExpressionSyntax invocation
            || !IsMaterializeInvocationShape(invocation))
        {
            return;
        }

        var memberAccess = (MemberAccessExpressionSyntax)invocation.Expression;
        if (BodyMentionsReceiverRoot(forEach.Statement, memberAccess.Expression))
        {
            return;
        }

        if (!IsSourceOnlyEnumerableExtension(context.SemanticModel, invocation, enumerableType, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.DoNotMaterializeToEnumerate,
            memberAccess.Name.GetLocation(),
            memberAccess.Name.Identifier.ValueText));
    }

    /// <summary>Returns whether the loop body mentions the materialized receiver's root identifier.</summary>
    /// <param name="body">The foreach statement's body.</param>
    /// <param name="receiver">The receiver of the ToList/ToArray call.</param>
    /// <returns><see langword="true"/> when the body reuses the identifier, disqualifying the report.</returns>
    private static bool BodyMentionsReceiverRoot(StatementSyntax body, ExpressionSyntax receiver)
    {
        var guardIdentifier = FindGuardIdentifier(receiver);
        if (guardIdentifier.IsKind(SyntaxKind.None))
        {
            return false;
        }

        var state = new IdentifierScanState(guardIdentifier.ValueText);
        DescendantTraversalHelper.VisitDescendantTokens(body, ref state, VisitIdentifierToken);
        return state.Found;
    }

    /// <summary>Walks a receiver down to its root identifier, or the nearest name when the root is not a simple identifier.</summary>
    /// <param name="receiver">The receiver of the ToList/ToArray call.</param>
    /// <returns>The identifier to guard on, or a <see cref="SyntaxKind.None"/> token when nothing is identifiable.</returns>
    private static SyntaxToken FindGuardIdentifier(ExpressionSyntax receiver)
    {
        var nearestName = default(SyntaxToken);
        var current = receiver;
        while (true)
        {
            switch (current)
            {
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier;
                case MemberAccessExpressionSyntax memberAccess:
                {
                    nearestName = memberAccess.Name.Identifier;
                    current = memberAccess.Expression;
                    continue;
                }

                case InvocationExpressionSyntax invocation:
                {
                    current = invocation.Expression;
                    continue;
                }

                case ElementAccessExpressionSyntax elementAccess:
                {
                    current = elementAccess.Expression;
                    continue;
                }

                case ParenthesizedExpressionSyntax parenthesized:
                {
                    current = parenthesized.Expression;
                    continue;
                }

                default:
                    return nearestName;
            }
        }
    }

    /// <summary>Classifies one token encountered during the loop-body scan.</summary>
    /// <param name="token">The visited token.</param>
    /// <param name="state">The current scan state.</param>
    /// <returns><see langword="true"/> to continue scanning, or <see langword="false"/> once the identifier is found.</returns>
    private static bool VisitIdentifierToken(in SyntaxToken token, ref IdentifierScanState state)
    {
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != state.Name)
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an invocation binds to an Enumerable extension whose only parameter is the source.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The invocation to bind.</param>
    /// <param name="enumerableType">The <c>System.Linq.Enumerable</c> type in the current compilation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the call is a reduced source-only Enumerable extension.</returns>
    private static bool IsSourceOnlyEnumerableExtension(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol enumerableType,
        CancellationToken cancellationToken)
        => model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { ReducedFrom: { Parameters.Length: 1 } reduced }
            && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, enumerableType);

    /// <summary>Tracks the guarded identifier while scanning the loop body.</summary>
    /// <param name="Name">The receiver's root identifier text.</param>
    private record struct IdentifierScanState(string Name)
    {
        /// <summary>Gets or sets a value indicating whether the identifier was found in the body.</summary>
        public bool Found { get; set; }
    }
}
