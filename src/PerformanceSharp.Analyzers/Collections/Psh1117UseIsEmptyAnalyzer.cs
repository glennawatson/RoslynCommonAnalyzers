// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags emptiness checks written as <c>Count</c> or <c>Length</c> comparisons against zero
/// on receivers that expose a bool <c>IsEmpty</c> property (PSH1117). On concurrent and
/// immutable collections, spans, and memory, counting can be O(n) or synchronize while
/// <c>IsEmpty</c> is a cheap dedicated check. The comparison shape and the member name gate
/// syntactically before the receiver is bound; both operand orders and the zero/one literal
/// forms (<c>== 0</c>, <c>!= 0</c>, <c>&gt; 0</c>, <c>&lt;= 0</c>, <c>&lt; 1</c>,
/// <c>&gt;= 1</c>) are recognized.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1117UseIsEmptyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The property the rule moves emptiness checks to.</summary>
    internal const string IsEmptyPropertyName = "IsEmpty";

    /// <summary>The count member name the syntax gate accepts.</summary>
    private const string CountPropertyName = "Count";

    /// <summary>The length member name the syntax gate accepts.</summary>
    private const string LengthPropertyName = "Length";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CollectionRules.UseIsEmpty);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            AnalyzeComparison,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanOrEqualExpression);
    }

    /// <summary>Classifies an emptiness comparison, before any binding.</summary>
    /// <param name="binary">The comparison to inspect.</param>
    /// <returns>The count access and whether the check means empty, or <see langword="null"/>.</returns>
    internal static (MemberAccessExpressionSyntax Count, bool IsEmpty)? TryGetEmptinessShape(BinaryExpressionSyntax binary)
    {
        if (TryGetCountAccess(binary.Left) is { } leftCount && TryGetLiteralValue(binary.Right) is { } rightLiteral)
        {
            return MapComparison(binary.Kind(), rightLiteral) is { } meansEmpty ? (leftCount, meansEmpty) : null;
        }

        if (TryGetCountAccess(binary.Right) is { } rightCount && TryGetLiteralValue(binary.Left) is { } leftLiteral)
        {
            return MapComparison(Mirror(binary.Kind()), leftLiteral) is { } meansEmpty ? (rightCount, meansEmpty) : null;
        }

        return null;
    }

    /// <summary>Returns a member access when it reads <c>Count</c> or <c>Length</c>.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns>The member access, or <see langword="null"/>.</returns>
    private static MemberAccessExpressionSyntax? TryGetCountAccess(ExpressionSyntax expression)
        => expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText is CountPropertyName or LengthPropertyName
            ? access
            : null;

    /// <summary>Returns the integer value of a zero or one literal operand.</summary>
    /// <param name="expression">The comparison operand.</param>
    /// <returns>0 or 1, or <see langword="null"/>.</returns>
    private static int? TryGetLiteralValue(ExpressionSyntax expression)
    {
        if (expression is not LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } literal)
        {
            return null;
        }

        return literal.Token.ValueText switch
        {
            "0" => 0,
            "1" => 1,
            _ => null,
        };
    }

    /// <summary>Mirrors a comparison kind for reversed operand order.</summary>
    /// <param name="kind">The original comparison kind.</param>
    /// <returns>The kind with the count on the left.</returns>
    private static SyntaxKind Mirror(SyntaxKind kind)
        => kind switch
        {
            SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
            _ => kind,
        };

    /// <summary>Maps a count-on-the-left comparison to its emptiness meaning.</summary>
    /// <param name="kind">The comparison kind.</param>
    /// <param name="literal">The literal operand value.</param>
    /// <returns><see langword="true"/> for empty, <see langword="false"/> for non-empty, or <see langword="null"/> when the shape is not an emptiness check.</returns>
    private static bool? MapComparison(SyntaxKind kind, int literal)
        => (kind, literal) switch
        {
            (SyntaxKind.EqualsExpression, 0) => true,
            (SyntaxKind.LessThanOrEqualExpression, 0) => true,
            (SyntaxKind.LessThanExpression, 1) => true,
            (SyntaxKind.NotEqualsExpression, 0) => false,
            (SyntaxKind.GreaterThanExpression, 0) => false,
            (SyntaxKind.GreaterThanOrEqualExpression, 1) => false,
            _ => null,
        };

    /// <summary>Reports PSH1117 for an emptiness comparison whose receiver exposes IsEmpty.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context)
    {
        var binary = (BinaryExpressionSyntax)context.Node;
        if (TryGetEmptinessShape(binary) is not { } shape)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(shape.Count.Expression, context.CancellationToken).Type;
        if (receiverType is null || !HasIsEmptyProperty(receiverType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CollectionRules.UseIsEmpty,
            binary.SyntaxTree,
            binary.Span,
            shape.Count.Name.Identifier.ValueText));
    }

    /// <summary>Returns whether a type or one of its bases exposes a bool <c>IsEmpty</c> property.</summary>
    /// <param name="type">The receiver type.</param>
    /// <returns><see langword="true"/> when the emptiness property exists.</returns>
    private static bool HasIsEmptyProperty(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers(IsEmptyPropertyName))
            {
                if (member is IPropertySymbol { Type.SpecialType: SpecialType.System_Boolean, IsIndexer: false })
                {
                    return true;
                }
            }
        }

        return false;
    }
}
