// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>throw new Exception(…)</c>, and the other types that say just as little or worse:
/// <c>SystemException</c> and <c>ApplicationException</c>, plus the three the runtime reserves for itself —
/// <c>NullReferenceException</c>, <c>IndexOutOfRangeException</c> and <c>OutOfMemoryException</c> (SST2409).
/// </summary>
/// <remarks>
/// <para>
/// The three general types give a caller nothing to catch selectively: to handle one failure it has to catch
/// all of them — including the ones it has never heard of — so the <c>catch</c> that was meant for a missing
/// file also swallows the bug three frames down. The three runtime-reserved types fail differently: the
/// runtime raises them to report a bug in the process, so throwing one yourself makes deliberate code look
/// like a runtime failure.
/// </para>
/// <para>
/// Only the reserved types themselves are reported, never a type derived from them: deriving from
/// <c>Exception</c> is exactly how a project names its own failures, and that is the fix, not the problem.
/// The clean path is a comparison of the thrown type's name, and only one of the reserved names reaches the
/// semantic model — where the bind confirms it really is the framework's type and not one of the project's
/// own with the same name.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2409ThrowsGeneralExceptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The namespace the reported types live in.</summary>
    private const string SystemNamespace = "System";

    /// <summary>Why the three general types are reported.</summary>
    private const string GeneralReason = "gives callers nothing to catch selectively";

    /// <summary>Why the three runtime-reserved types are reported.</summary>
    private const string RuntimeReason = "impersonates a failure only the runtime should raise";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.ThrowsGeneralException);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    /// <summary>Analyzes one throw.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (GetThrownExpression(context.Node) is not ObjectCreationExpressionSyntax creation
            || GetReservedReason(GetSimpleName(creation.Type)) is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol is not INamedTypeSymbol type
            || !IsInSystemNamespace(type)
            || GetReservedReason(type.Name) is not { } reason)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ThrowsGeneralException,
            creation.Type.GetLocation(),
            type.Name,
            reason));
    }

    /// <summary>Gets the expression a throw throws.</summary>
    /// <param name="node">The throw statement or throw expression.</param>
    /// <returns>The thrown expression, or <see langword="null"/> for a rethrow.</returns>
    private static ExpressionSyntax? GetThrownExpression(SyntaxNode node) => node switch
    {
        ThrowStatementSyntax statement => statement.Expression,
        ThrowExpressionSyntax expression => expression.Expression,
        _ => null,
    };

    /// <summary>Returns why a thrown type is reserved, or <see langword="null"/> when it is not one this rule reports.</summary>
    /// <param name="name">The thrown type's simple name.</param>
    /// <returns>The reason clause for the diagnostic message, or <see langword="null"/> when the name is not reserved.</returns>
    private static string? GetReservedReason(string? name) => name switch
    {
        "Exception" or "SystemException" or "ApplicationException" => GeneralReason,
        "NullReferenceException" or "IndexOutOfRangeException" or "OutOfMemoryException" => RuntimeReason,
        _ => null,
    };

    /// <summary>Returns whether a bound type sits directly in the <c>System</c> namespace.</summary>
    /// <param name="type">The thrown type.</param>
    /// <returns><see langword="true"/> when the type is the framework's, not one of the project's own with the same name.</returns>
    private static bool IsInSystemNamespace(INamedTypeSymbol type)
        => type.ContainingNamespace is { Name: SystemNamespace } ns
            && ns.ContainingNamespace is { IsGlobalNamespace: true };

    /// <summary>Gets the rightmost name of a possibly qualified type.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>The simple name, or <see langword="null"/> when the type is not a name.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };
}
