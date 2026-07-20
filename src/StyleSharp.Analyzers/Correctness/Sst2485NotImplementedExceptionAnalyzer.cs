// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>throw new NotImplementedException(…)</c> left in shipped code (SST2485). It marks a member that
/// was stubbed out and never finished: the code compiles, but any path that reaches the throw crashes at runtime.
/// A <c>throw new NotSupportedException(…)</c> is deliberately left alone — it is a valid, permanent signal that
/// an operation is genuinely unsupported, not an unfinished stub.
/// </summary>
/// <remarks>
/// <para>
/// The clean path is syntactic: the throw's operand must be an object creation whose type, as written, has the
/// simple name <c>NotImplementedException</c>. A rethrow (<c>throw;</c>), a throw of an already-built exception
/// value, a generic exception type, or any other exception name returns before the semantic model is touched.
/// </para>
/// <para>
/// Only when the name matches is the type bound, to confirm it is the framework's
/// <c>System.NotImplementedException</c> and not one of the project's own types that happens to share the name.
/// A same-named type in another namespace is a different symbol and is never reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2485NotImplementedExceptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The simple name of the framework type this rule reports.</summary>
    private const string NotImplementedExceptionName = "NotImplementedException";

    /// <summary>The namespace the reported type lives in.</summary>
    private const string SystemNamespace = "System";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.NotImplementedExceptionThrown);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement, SyntaxKind.ThrowExpression);
    }

    /// <summary>Analyzes one throw for a <c>new NotImplementedException(…)</c> operand.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (GetThrownExpression(context.Node) is not ObjectCreationExpressionSyntax creation
            || GetSimpleName(creation.Type) != NotImplementedExceptionName)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol is not INamedTypeSymbol type
            || !IsInSystemNamespace(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.NotImplementedExceptionThrown,
            creation.GetLocation()));
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

    /// <summary>Gets the rightmost name of a possibly qualified type.</summary>
    /// <param name="type">The type as written.</param>
    /// <returns>The simple name, or <see langword="null"/> when the type is not a plain name.</returns>
    private static string? GetSimpleName(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a bound type sits directly in the <c>System</c> namespace.</summary>
    /// <param name="type">The thrown type.</param>
    /// <returns><see langword="true"/> when the type is the framework's, not one of the project's own with the same name.</returns>
    private static bool IsInSystemNamespace(INamedTypeSymbol type)
        => type.ContainingNamespace is { Name: SystemNamespace } ns
            && ns.ContainingNamespace is { IsGlobalNamespace: true };
}
