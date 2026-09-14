// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an enum member that repeats an earlier member's value without saying so (SST2455). A deliberate
/// alias names the member it duplicates — <c>Default = Read</c> — and is left alone; a bare number that
/// happens to collide is reported.
/// </summary>
/// <remarks>
/// Increasing integer literals and implicit successors need no binding. Other initializers use one pass
/// over declared constant values so aliases, expressions, and overflow retain the compiler's semantics.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2455DuplicateEnumValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(CorrectnessRules.DuplicateEnumValue);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Returns whether a member states its value by naming another member of the same enum.</summary>
    /// <param name="member">The enum member.</param>
    /// <param name="declaration">The enum the member belongs to.</param>
    /// <returns><see langword="true"/> when the initializer mentions a sibling member's name.</returns>
    /// <remarks>
    /// The names are compared syntactically. An initializer that reads a sibling is an alias or a combination
    /// written in terms of the enum's own vocabulary, which is the deliberate form this rule exists to allow.
    /// </remarks>
    internal static bool IsExpressedAsAnAlias(EnumMemberDeclarationSyntax member, EnumDeclarationSyntax declaration) =>
        member.EqualsValue is { } initializer
            && ((initializer.Value is IdentifierNameSyntax root && NamesSibling(root, declaration))
                || !DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, EnumDeclarationSyntax>(
                    initializer.Value,
                    ref declaration,
                    static (node, ref enumDeclaration) => !NamesSibling(node, enumDeclaration)));

    /// <summary>Returns whether an identifier names one of the enum's own members.</summary>
    /// <param name="identifier">The identifier to check.</param>
    /// <param name="declaration">The enum declaration.</param>
    /// <returns><see langword="true"/> when a member of the enum carries that name.</returns>
    private static bool NamesSibling(IdentifierNameSyntax identifier, EnumDeclarationSyntax declaration)
    {
        var name = identifier.Identifier.ValueText;
        var members = declaration.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (string.Equals(members[i].Identifier.ValueText, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports every member of one enum that silently repeats an earlier value.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        var members = declaration.Members;
        if (members.Count < 2 || !MayHaveDuplicateValues(members))
        {
            return;
        }

        var seen = new Dictionary<object, string>(members.Count);
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (context.SemanticModel.GetDeclaredSymbol(member, context.CancellationToken) is not { ConstantValue: { } value })
            {
                continue;
            }

            if (!seen.TryGetValue(value, out var first))
            {
                seen.Add(value, member.Identifier.ValueText);
                continue;
            }

            if (IsExpressedAsAnAlias(member, declaration))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                CorrectnessRules.DuplicateEnumValue,
                member.SyntaxTree,
                member.Identifier.Span,
                member.Identifier.ValueText,
                first));
        }
    }

    /// <summary>Checks whether syntax leaves a possible collision in the enum's value sequence.</summary>
    /// <param name="members">The enum's members in declaration order.</param>
    /// <returns>Whether semantic constant evaluation is needed to rule out duplicate values.</returns>
    private static bool MayHaveDuplicateValues(SeparatedSyntaxList<EnumMemberDeclarationSyntax> members)
    {
        long previous = -1;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].EqualsValue is { Value: var expression })
            {
                if (!TryGetLiteralValue(expression, out var value) || (i > 0 && value <= previous))
                {
                    return true;
                }

                previous = value;
            }
            else
            {
                if (previous == long.MaxValue)
                {
                    return true;
                }

                previous++;
            }
        }

        return false;
    }

    /// <summary>Reads an integer literal without asking the semantic model to evaluate constants.</summary>
    /// <param name="expression">The initializer to inspect.</param>
    /// <param name="value">The literal value when its syntax is supported.</param>
    /// <returns>Whether the initializer is a directly representable integer literal.</returns>
    private static bool TryGetLiteralValue(ExpressionSyntax expression, out long value)
    {
        var negative = expression.IsKind(SyntaxKind.UnaryMinusExpression);
        if (expression is PrefixUnaryExpressionSyntax unary
            && (negative || unary.IsKind(SyntaxKind.UnaryPlusExpression)))
        {
            expression = unary.Operand;
        }

        if (expression is LiteralExpressionSyntax literal)
        {
            switch (literal.Token.Value)
            {
                case int signed:
                {
                    value = signed;
                    break;
                }

                case uint unsigned:
                {
                    value = unsigned;
                    break;
                }

                case long wide:
                {
                    value = wide;
                    break;
                }

                case ulong wideUnsigned when wideUnsigned <= long.MaxValue:
                {
                    value = (long)wideUnsigned;
                    break;
                }

                default:
                {
                    value = 0;
                    return false;
                }
            }

            value = negative ? -value : value;
            return true;
        }

        value = 0;
        return false;
    }
}
