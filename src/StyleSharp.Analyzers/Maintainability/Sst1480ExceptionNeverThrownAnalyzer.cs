// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an exception that is constructed as a whole statement and then discarded (SST1480). Creating an
/// exception does nothing to control flow, so <c>new ArgumentNullException(nameof(value));</c> is a forgotten
/// <c>throw</c> and the error path it was meant to stop silently continues.
/// </summary>
/// <remarks>
/// <para>
/// Only a creation whose parent is an expression statement is reported. That single test is what separates
/// the bug from every legitimate exception factory: an exception that is thrown, returned, assigned, passed
/// as an argument, captured in a collection or produced by an expression-bodied lambda all sit somewhere that
/// consumes the value, and none of them is an expression statement.
/// </para>
/// <para>
/// The parent check is a reference test against a syntax node and is false for almost every creation in a
/// compilation, so the bind that confirms the type derives from <see cref="Exception"/> runs only for the
/// handful of statements that already look like the bug. <see cref="Exception"/> itself is resolved lazily,
/// so a compilation that never reaches the bind never pays for the metadata lookup.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1480ExceptionNeverThrownAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(MaintainabilityRules.ExceptionNeverThrown);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Sets up the per-compilation state, then analyzes every object creation.</summary>
    /// <param name="context">The compilation start context.</param>
    /// <remarks>
    /// The implicit <c>new(...)</c> form is registered alongside the explicit one so the rule stays complete
    /// as the language changes: today a target-typed creation cannot stand alone as a statement, because an
    /// expression statement offers it no target type, but the analyzer does not depend on that being true.
    /// </remarks>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        var exceptionType = new Lazy<INamedTypeSymbol?>(
            () => compilation.GetTypeByMetadataName("System.Exception"),
            LazyThreadSafetyMode.ExecutionAndPublication);
        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, exceptionType),
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Reports one exception whose value nothing consumes.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="exceptionType">The lazily resolved <see cref="Exception"/> symbol.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol?> exceptionType)
    {
        var creation = (BaseObjectCreationExpressionSyntax)context.Node;
        if (creation.Parent is not ExpressionStatementSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol created
            || !InheritsFromException(created, exceptionType.Value))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            MaintainabilityRules.ExceptionNeverThrown,
            creation.GetLocation(),
            created.Name));
    }

    /// <summary>Returns whether a type is <see cref="Exception"/> or derives from it.</summary>
    /// <param name="type">The constructed type.</param>
    /// <param name="exceptionType">The resolved <see cref="Exception"/> symbol.</param>
    /// <returns><see langword="true"/> when the created object is an exception.</returns>
    private static bool InheritsFromException(INamedTypeSymbol type, INamedTypeSymbol? exceptionType)
    {
        if (exceptionType is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, exceptionType))
            {
                return true;
            }
        }

        return false;
    }
}
