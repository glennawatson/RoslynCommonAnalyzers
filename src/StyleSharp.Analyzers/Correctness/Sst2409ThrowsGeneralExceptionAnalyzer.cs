// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>throw new Exception(…)</c>, and the two other types that say just as little:
/// <c>SystemException</c> and <c>ApplicationException</c> (SST2409).
/// </summary>
/// <remarks>
/// <para>
/// A caller that wants to handle one failure has to catch all of them — including the ones it has never heard
/// of — so the <c>catch</c> that was meant for a missing file also swallows the bug three frames down.
/// </para>
/// <para>
/// Only the three general types themselves are reported, never a type derived from them: deriving from
/// <c>Exception</c> is exactly how a project names its own failures, and that is the fix, not the problem.
/// The clean path is a comparison of the thrown type's name, and only one of those three names reaches the
/// semantic model — where the bind confirms it really is the framework's type and not one of the project's
/// own with the same name.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2409ThrowsGeneralExceptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The namespace the reported types live in.</summary>
    private const string SystemNamespace = "System";

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
            || !IsGeneralName(GetSimpleName(creation.Type)))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol is not INamedTypeSymbol type
            || !IsGeneralExceptionType(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ThrowsGeneralException,
            creation.Type.GetLocation(),
            type.Name));
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

    /// <summary>Returns whether a name is one of the three that say nothing.</summary>
    /// <param name="name">The thrown type's simple name.</param>
    /// <returns><see langword="true"/> when the name is worth binding.</returns>
    private static bool IsGeneralName(string? name)
        => name is "Exception" or "SystemException" or "ApplicationException";

    /// <summary>Returns whether a bound type is one of the framework's three general exception types.</summary>
    /// <param name="type">The thrown type.</param>
    /// <returns><see langword="true"/> for <c>System.Exception</c> and its two general subtypes.</returns>
    private static bool IsGeneralExceptionType(INamedTypeSymbol type)
        => IsGeneralName(type.Name)
            && type.ContainingNamespace is { Name: SystemNamespace } ns
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
