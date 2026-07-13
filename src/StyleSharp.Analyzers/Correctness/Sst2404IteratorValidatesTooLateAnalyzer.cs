// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an iterator whose body opens with an argument guard (SST2404). The body of a method containing
/// <c>yield</c> does not run when the method is called, so the guard fires on the first <c>MoveNext</c> —
/// somewhere else entirely, on a stack that no longer says who passed the bad value.
/// </summary>
/// <remarks>
/// <para>
/// The guard has to check an argument. An iterator that opens by checking its own state, and nothing it was
/// handed, is doing something this rule has no opinion about. Once one argument check is there, though, every
/// leading guard is part of the report — a disposed check standing in front of a null check is stranded
/// behind the same <c>MoveNext</c>.
/// </para>
/// <para>
/// The clean path is a look at the body's first statement. Only a method that opens with a guard pays for the
/// walk that looks for a <c>yield</c>, and nothing here binds a symbol at all: an iterator is a syntactic
/// fact, and so is a guard.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2404IteratorValidatesTooLateAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(CorrectnessRules.IteratorValidatesTooLate);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Analyzes one method for guards stranded inside an iterator.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Body is not { } body
            || IteratorGuardAnalysis.CountLeadingGuards(body, method.ParameterList) == 0
            || !IteratorGuardAnalysis.IsIterator(body))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.IteratorValidatesTooLate,
            method.Identifier.GetLocation(),
            method.Identifier.ValueText));
    }
}
