// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an object creation that carries an initializer and whose empty argument parentheses do not
/// match the configured style (SST2268): <c>new T() { ... }</c> when
/// <c>stylesharp.object_creation_parentheses</c> is <c>omit</c> (the default), or <c>new T { ... }</c> when
/// it is <c>include</c>. The rule is opt-in and off by default.
/// </summary>
/// <remarks>
/// Only a creation that already has an initializer and whose argument list is absent or empty is a candidate;
/// a creation that passes real constructor arguments cannot drop its parentheses and is never touched. The
/// option is read only after that shape is matched.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2268ObjectCreationParenthesesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reported target when the codebase omits the empty parentheses.</summary>
    internal const string OmitTarget = "omit";

    /// <summary>The reported target when the codebase includes the empty parentheses.</summary>
    internal const string IncludeTarget = "include";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeObjectCreationParentheses);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ObjectCreationExpression);
    }

    /// <summary>Returns whether a creation with an initializer is a candidate for normalizing its parentheses.</summary>
    /// <param name="creation">The object creation to inspect.</param>
    /// <returns><see langword="true"/> when it has an initializer and no non-empty argument list.</returns>
    internal static bool IsCandidate(ObjectCreationExpressionSyntax creation)
        => creation.Initializer is not null && creation.ArgumentList is null or { Arguments.Count: 0 };

    /// <summary>Reports a creation whose parentheses style does not match the configured one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        if (!IsCandidate(creation))
        {
            return;
        }

        var hasParentheses = creation.ArgumentList is not null;
        var style = ModernSyntaxStyleOptions.ReadObjectCreationParentheses(context.Options.AnalyzerConfigOptionsProvider.GetOptions(creation.SyntaxTree));
        if (hasParentheses == (style == ObjectCreationParenthesesStyle.Include))
        {
            return;
        }

        var target = style == ObjectCreationParenthesesStyle.Omit ? OmitTarget : IncludeTarget;
        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeObjectCreationParentheses, creation.Type.GetLocation(), target));
    }
}
