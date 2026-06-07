// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Shared reporting for the naming analyzers. The suggested replacement name is
/// carried in the diagnostic's <see cref="Diagnostic.Properties"/> under
/// <see cref="NewNameKey"/> so a single rename code fix can serve every rule.
/// </summary>
internal static class NamingDiagnostic
{
    /// <summary>The diagnostic property key holding the suggested replacement name.</summary>
    public const string NewNameKey = "NewName";

    /// <summary>Reports a naming diagnostic on <paramref name="identifier"/>, carrying the suggested rename.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    /// <param name="identifier">The identifier token being named incorrectly.</param>
    /// <param name="suggestedName">The suggested replacement name.</param>
    public static void Report(in SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule, SyntaxToken identifier, string suggestedName)
    {
        var properties = ImmutableDictionary<string, string?>.Empty.Add(NewNameKey, suggestedName);
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, identifier.SyntaxTree!, identifier.Span, properties, identifier.ValueText));
    }

    /// <summary>Reports a naming diagnostic on <paramref name="identifier"/>, carrying the suggested rename and a caller-supplied display name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="rule">The diagnostic descriptor to report.</param>
    /// <param name="identifier">The identifier token being named incorrectly.</param>
    /// <param name="name">The current display name.</param>
    /// <param name="suggestedName">The suggested replacement name.</param>
    public static void Report(in SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule, SyntaxToken identifier, string name, string suggestedName)
    {
        var properties = ImmutableDictionary<string, string?>.Empty.Add(NewNameKey, suggestedName);
        context.ReportDiagnostic(DiagnosticHelper.Create(rule, identifier.SyntaxTree!, identifier.Span, properties, name));
    }
}
