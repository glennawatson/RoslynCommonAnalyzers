// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Suggests <c>ArgumentNullException.ThrowIfNull</c> in place of a hand-written
/// null check that throws <see cref="System.ArgumentNullException"/> (SST2000). The
/// rule is resolved once per compilation by probing for the helper method, so it
/// fires only where the replacement actually exists in the referenced framework —
/// no target framework string is parsed and nothing is reported on older runtimes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernizationRules.UseThrowIfNull);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (!HasStaticMethod(start.Compilation, "System.ArgumentNullException", "ThrowIfNull"))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeIf, SyntaxKind.IfStatement);
        });
    }

    /// <summary>Reports SST2000 when an if statement is a replaceable null guard.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context)
    {
        var ifStatement = (IfStatementSyntax)context.Node;
        if (!ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var checkedExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseThrowIfNull, ifStatement.GetLocation(), checkedExpression!.ToString()));
    }

    /// <summary>Returns whether a type has a static method with the given name.</summary>
    /// <param name="compilation">The compilation to resolve the type in.</param>
    /// <param name="typeMetadataName">The metadata name of the declaring type.</param>
    /// <param name="methodName">The method name to look for.</param>
    /// <returns><see langword="true"/> when the type exists and declares such a method.</returns>
    private static bool HasStaticMethod(Compilation compilation, string typeMetadataName, string methodName)
        => compilation.GetTypeByMetadataName(typeMetadataName) is { } type
            && type.GetMembers(methodName).Any(member => member is IMethodSymbol { IsStatic: true });
}
