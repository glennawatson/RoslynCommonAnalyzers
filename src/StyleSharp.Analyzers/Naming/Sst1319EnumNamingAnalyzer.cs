// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Requires an enumeration's type name to be PascalCase (SST1319) — the whole name, not just its
/// first letter: no underscore, and an acronym longer than two letters written as a word
/// (<c>HttpStatus</c>, not <c>HTTPStatus</c>), as the .NET capitalization guidelines have it.
/// </summary>
/// <remarks>
/// <para>
/// SST1300 already owns the <em>first character</em> of every type name, an enum's included. This
/// rule takes the rest of the enum's name — the underscore, the all-capitals acronym — and reports
/// only a name that already starts upper-case, so the two rules never report the same name twice.
/// A name that starts lower-case is SST1300's, and is left alone here.
/// </para>
/// <para>
/// The clean path is a scan of the identifier's characters and nothing else — no symbol is bound,
/// and the corrected name is built only once a violation has been found.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1319EnumNamingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.EnumPascalCase);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.EnumDeclaration);
    }

    /// <summary>Analyzes one enum declaration and reports a type name that is not PascalCase.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var declaration = (EnumDeclarationSyntax)context.Node;
        var identifier = declaration.Identifier;
        var name = identifier.Text is ['@', ..] ? identifier.ValueText : identifier.Text;

        // A name that does not start upper-case is SST1300's to report; taking it here as well would
        // put two diagnostics on one declaration.
        if (!NamingHelper.BeginsWithUpperCase(name) || NamingHelper.IsPascalCase(name))
        {
            return;
        }

        NamingDiagnostic.Report(context, NamingRules.EnumPascalCase, identifier, name, NamingHelper.SuggestPascalCaseName(name));
    }
}
