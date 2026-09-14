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
/// those loops stay clean. <c>await foreach</c> is skipped, and <c>System.Linq.Enumerable</c>
/// is resolved only after the loop passes these syntax checks.
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

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CollectionRules.DoNotMaterializeToEnumerate);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        CompilationStateRegistration.RegisterSyntaxNodeAction(
            context,
            static compilation => new LazyMetadataType(compilation, EnumerableMetadataName),
            AnalyzeForEach,
            SyntaxKind.ForEachStatement);
    }

    /// <summary>Returns whether an invocation is a parameterless member-access ToList/ToArray call, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the call has the materialization shape.</returns>
    internal static bool IsMaterializeInvocationShape(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && memberAccess.Name.Identifier.ValueText is ToListMethodName or ToArrayMethodName;

    /// <summary>Reports PSH1120 for a foreach that enumerates a ToList/ToArray copy it then discards.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeCache">The compilation's deferred LINQ type lookup.</param>
    private static void AnalyzeForEach(in SyntaxNodeAnalysisContext context, LazyMetadataType typeCache)
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

        if (typeCache.Get() is not { } enumerableType
            || !EnumerableInvocationHelper.IsSourceOnlyExtensionOn(context.SemanticModel, invocation, enumerableType, context.CancellationToken))
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
        return !guardIdentifier.IsKind(SyntaxKind.None) && IdentifierReferences.ContainsIdentifierToken(body, guardIdentifier.ValueText);
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
}
