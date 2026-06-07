// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires a <c>[SuppressMessage]</c> attribute to carry a non-empty
/// <c>Justification</c> (SST1404). The placeholder <c>&lt;Pending&gt;</c> emitted by
/// the IDE counts as missing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionJustificationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The named argument that carries the justification.</summary>
    private const string JustificationArgument = "Justification";

    /// <summary>The IDE-generated placeholder that counts as no justification.</summary>
    private const string PendingPlaceholder = "<Pending>";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.SuppressionJustified);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    /// <summary>Reports a suppression attribute with a missing or empty justification.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsSuppressMessage(attribute.Name))
        {
            return;
        }

        if (HasJustification(attribute))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.SuppressionJustified, attribute.GetLocation()));
    }

    /// <summary>Returns whether the attribute name is <c>SuppressMessage</c>.</summary>
    /// <param name="name">The attribute name syntax.</param>
    /// <returns><see langword="true"/> when it names the suppression attribute.</returns>
    private static bool IsSuppressMessage(NameSyntax name)
    {
        var simple = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            SimpleNameSyntax direct => direct.Identifier.ValueText,
            _ => string.Empty
        };

        return string.Equals(simple, "SuppressMessage", StringComparison.Ordinal)
            || string.Equals(simple, "SuppressMessageAttribute", StringComparison.Ordinal);
    }

    /// <summary>Returns whether the attribute sets a non-empty, non-placeholder justification.</summary>
    /// <param name="attribute">The suppression attribute.</param>
    /// <returns><see langword="true"/> when a real justification is present.</returns>
    private static bool HasJustification(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { } arguments)
        {
            return false;
        }

        foreach (var argument in arguments.Arguments)
        {
            if (argument.NameEquals?.Name.Identifier.ValueText is not JustificationArgument)
            {
                continue;
            }

            return argument.Expression is LiteralExpressionSyntax literal
                && !string.IsNullOrWhiteSpace(literal.Token.ValueText)
                && !string.Equals(literal.Token.ValueText, PendingPlaceholder, StringComparison.Ordinal);
        }

        return false;
    }
}
