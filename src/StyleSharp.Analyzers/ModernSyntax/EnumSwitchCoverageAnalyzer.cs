// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports enum switches that omit named enum values and do not provide a catch-all case.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EnumSwitchCoverageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property containing missing enum member expressions.</summary>
    internal const string MissingMembersProperty = "MissingMembers";

    /// <summary>The diagnostic property marking a switch whose only worthwhile fix is a catch-all section.</summary>
    internal const string CatchAllProperty = "CatchAll";

    /// <summary>The separator used in the missing-members diagnostic property.</summary>
    internal const char MissingMembersSeparator = '|';

    /// <summary>The property bag a catch-all diagnostic carries, built once rather than per report.</summary>
    internal static readonly ImmutableDictionary<string, string?> CatchAllProperties =
        ImmutableDictionary<string, string?>.Empty.Add(CatchAllProperty, "true");

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(
        ModernSyntaxRules.CompleteEnumSwitchStatement,
        ModernSyntaxRules.CompleteEnumSwitchExpression);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
    }

    /// <summary>Builds the encoded missing-member list for a switch statement.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <param name="switchStatement">The switch statement.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="missingMembers">The encoded missing members.</param>
    /// <param name="needsCatchAll">Whether a missing value is already named under a guard.</param>
    /// <returns><see langword="true"/> when at least one enum member can be named by a new label.</returns>
    internal static bool TryBuildMissingMembers(
        INamedTypeSymbol enumType,
        SwitchStatementSyntax switchStatement,
        SemanticModel model,
        CancellationToken cancellationToken,
        out string missingMembers,
        out bool needsCatchAll)
    {
        missingMembers = string.Empty;
        needsCatchAll = false;
        System.Text.StringBuilder? builder = null;
        var members = enumType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (!EnumSwitchCoverage.IsEnumValue(members[i], out var field)
                || EnumSwitchCoverage.IsCaseLabelCovered(field, switchStatement, model, cancellationToken))
            {
                continue;
            }

            if (EnumSwitchCoverage.IsNamedByAnyLabel(field, switchStatement, model, cancellationToken))
            {
                needsCatchAll = true;
                return false;
            }

            AppendMember(ref builder, field, model, switchStatement.SpanStart);
        }

        if (builder is null)
        {
            return false;
        }

        missingMembers = builder.ToString();
        return true;
    }

    /// <summary>Reports an enum switch statement that has neither all cases nor a default section.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        if (HasDefaultLabel(switchStatement)
            || !TryGetEnumType(switchStatement.Expression, context.SemanticModel, context.CancellationToken, out var enumType))
        {
            return;
        }

        var built = TryBuildMissingMembers(enumType, switchStatement, context.SemanticModel, context.CancellationToken, out var missingMembers, out var needsCatchAll);
        if (!built && !needsCatchAll)
        {
            return;
        }

        var properties = needsCatchAll
            ? CatchAllProperties
            : ImmutableDictionary<string, string?>.Empty.Add(MissingMembersProperty, missingMembers);
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.CompleteEnumSwitchStatement, switchStatement.SwitchKeyword.GetLocation(), properties));
    }

    /// <summary>Reports an enum switch expression that has neither all arms nor a discard arm.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        if (HasDiscardArm(switchExpression)
            || !TryGetEnumType(switchExpression.GoverningExpression, context.SemanticModel, context.CancellationToken, out var enumType)
            || !TryBuildMissingMembers(enumType, switchExpression, context.SemanticModel, context.CancellationToken, out var missingMembers))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty.Add(MissingMembersProperty, missingMembers);
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.CompleteEnumSwitchExpression, switchExpression.SwitchKeyword.GetLocation(), properties));
    }

    /// <summary>Builds the encoded missing-member list for a switch expression.</summary>
    /// <param name="enumType">The enum type.</param>
    /// <param name="switchExpression">The switch expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="missingMembers">The encoded missing members.</param>
    /// <returns><see langword="true"/> when at least one enum member is missing.</returns>
    private static bool TryBuildMissingMembers(
        INamedTypeSymbol enumType,
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken,
        out string missingMembers)
    {
        missingMembers = string.Empty;
        System.Text.StringBuilder? builder = null;
        var members = enumType.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (!EnumSwitchCoverage.IsEnumValue(members[i], out var field)
                || IsCovered(field, switchExpression, model, cancellationToken))
            {
                continue;
            }

            AppendMember(ref builder, field, model, switchExpression.SpanStart);
        }

        if (builder is null)
        {
            return false;
        }

        missingMembers = builder.ToString();
        return true;
    }

    /// <summary>Gets the enum type of the supplied expression.</summary>
    /// <param name="expression">The switch expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <param name="enumType">The enum type.</param>
    /// <returns><see langword="true"/> when the expression type is an enum.</returns>
    private static bool TryGetEnumType(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken,
        out INamedTypeSymbol enumType)
    {
        enumType = null!;
        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } namedType)
        {
            return false;
        }

        enumType = namedType;
        return true;
    }

    /// <summary>Returns whether the switch statement has a default label.</summary>
    /// <param name="switchStatement">The switch statement.</param>
    /// <returns><see langword="true"/> when a default label exists.</returns>
    private static bool HasDefaultLabel(SwitchStatementSyntax switchStatement)
    {
        var sections = switchStatement.Sections;
        for (var sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
        {
            var labels = sections[sectionIndex].Labels;
            for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
            {
                if (labels[labelIndex].IsKind(SyntaxKind.DefaultSwitchLabel))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether the switch expression has a discard arm.</summary>
    /// <param name="switchExpression">The switch expression.</param>
    /// <returns><see langword="true"/> when a discard pattern exists.</returns>
    private static bool HasDiscardArm(SwitchExpressionSyntax switchExpression)
    {
        var arms = switchExpression.Arms;
        for (var i = 0; i < arms.Count; i++)
        {
            if (arms[i].Pattern.IsKind(SyntaxKind.DiscardPattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a switch expression already covers an enum value.</summary>
    /// <param name="field">The enum value field.</param>
    /// <param name="switchExpression">The switch expression.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when an arm pattern names the field.</returns>
    private static bool IsCovered(
        IFieldSymbol field,
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var arms = switchExpression.Arms;
        for (var i = 0; i < arms.Count; i++)
        {
            if (arms[i].Pattern is not ConstantPatternSyntax constantPattern)
            {
                continue;
            }

            var name = constantPattern.Expression switch
            {
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                _ => null,
            };
            if (name is not null && name != field.Name)
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(field, model.GetSymbolInfo(constantPattern.Expression, cancellationToken).Symbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends a displayable enum member expression to the encoded property value.</summary>
    /// <param name="builder">The lazily allocated string builder.</param>
    /// <param name="field">The enum field.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The position the label is written at.</param>
    private static void AppendMember(ref System.Text.StringBuilder? builder, IFieldSymbol field, SemanticModel model, int position)
    {
        var memberName = EnumSwitchCoverage.NameFor(field, model, position);
        builder ??= new System.Text.StringBuilder(memberName.Length);
        if (builder.Length > 0)
        {
            _ = builder.Append(MissingMembersSeparator);
        }

        _ = builder.Append(memberName);
    }
}
