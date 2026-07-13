// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>catch</c> that targets <see cref="NullReferenceException"/> (SST2401), whether it names the
/// type in the clause or reaches it through an exception filter.
/// </summary>
/// <remarks>
/// The clean path is a token-text comparison: a clause whose type is not spelled
/// <c>NullReferenceException</c>, and a filter that does not mention the name anywhere, are both rejected
/// before the semantic model is touched. The bind that follows only confirms that the name really is
/// <c>System.NullReferenceException</c> and not a type of the project's own with the same name.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2401CatchNullReferenceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the caught type.</summary>
    private const string NullReferenceExceptionName = "NullReferenceException";

    /// <summary>The namespace the caught type must live in.</summary>
    private const string SystemNamespace = "System";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.CatchNullReference);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.CatchClause);
    }

    /// <summary>Analyzes one catch clause.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var catchClause = (CatchClauseSyntax)context.Node;
        if (catchClause.Declaration?.Type is { } declared && IsNullReferenceName(declared))
        {
            ReportWhenNullReference(context, declared);
            return;
        }

        if (catchClause.Filter is not { } filter)
        {
            return;
        }

        AnalyzeFilter(context, filter);
    }

    /// <summary>Analyzes an exception filter for a reference to the type.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="filter">The exception filter.</param>
    /// <remarks>
    /// <c>catch (Exception e) when (e is NullReferenceException)</c> is the same handler written the long
    /// way round, so the rule follows the name into the filter — through a pattern, a <c>typeof</c>, or
    /// whatever else names the type.
    /// </remarks>
    private static void AnalyzeFilter(SyntaxNodeAnalysisContext context, CatchFilterClauseSyntax filter)
    {
        var state = new FilterScan(context.SemanticModel, context.CancellationToken);
        DescendantTraversalHelper.VisitDescendants<SimpleNameSyntax, FilterScan>(filter, ref state, VisitFilterName);
        if (state.Match is not { } match)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CatchNullReference, match.GetLocation(), NullReferenceExceptionName));
    }

    /// <summary>Records the first filter name that binds to the type.</summary>
    /// <param name="name">The name being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once a match is found, which stops the walk.</returns>
    private static bool VisitFilterName(SimpleNameSyntax name, ref FilterScan state)
    {
        if (name.Identifier.ValueText != NullReferenceExceptionName)
        {
            return true;
        }

        if (!IsSystemNullReferenceException(state.Model.GetSymbolInfo(name, state.CancellationToken).Symbol))
        {
            return true;
        }

        state.Match = name;
        return false;
    }

    /// <summary>Reports a caught type once it binds to <see cref="NullReferenceException"/>.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="type">The caught type as written.</param>
    private static void ReportWhenNullReference(SyntaxNodeAnalysisContext context, TypeSyntax type)
    {
        if (!IsSystemNullReferenceException(context.SemanticModel.GetSymbolInfo(type, context.CancellationToken).Symbol))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(CorrectnessRules.CatchNullReference, type.GetLocation(), NullReferenceExceptionName));
    }

    /// <summary>Returns whether a type is written with the caught type's simple name.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns><see langword="true"/> when the rightmost name matches, aliases and qualifiers aside.</returns>
    private static bool IsNullReferenceName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText == NullReferenceExceptionName,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == NullReferenceExceptionName,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText == NullReferenceExceptionName,
        _ => false,
    };

    /// <summary>Returns whether a bound symbol is <see cref="NullReferenceException"/> itself.</summary>
    /// <param name="symbol">The bound symbol.</param>
    /// <returns><see langword="true"/> for <c>System.NullReferenceException</c>.</returns>
    /// <remarks>
    /// Matched on the symbol's own namespace rather than through a well-known-type lookup, so a project that
    /// declares its own <c>NullReferenceException</c> elsewhere is left alone and no compilation pays for a
    /// metadata lookup it never needed.
    /// </remarks>
    private static bool IsSystemNullReferenceException(ISymbol? symbol)
        => symbol is INamedTypeSymbol { Name: NullReferenceExceptionName, ContainingNamespace: { Name: SystemNamespace } ns }
            && ns.ContainingNamespace is { IsGlobalNamespace: true };

    /// <summary>The state threaded through an exception filter's name scan.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="CancellationToken">A token that cancels analysis.</param>
    private record struct FilterScan(SemanticModel Model, CancellationToken CancellationToken)
    {
        /// <summary>Gets or sets the first name that bound to the caught type.</summary>
        public SimpleNameSyntax? Match { get; set; }
    }
}
