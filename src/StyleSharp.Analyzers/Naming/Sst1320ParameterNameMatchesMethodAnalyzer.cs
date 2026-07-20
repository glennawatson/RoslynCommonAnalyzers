// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a method parameter whose name is identical to its containing method's name (SST1320).
/// </summary>
/// <remarks>
/// <para>
/// A parameter that repeats its method's name is almost always a copy-paste slip, and it makes a
/// named argument at the call site read as though the method were being passed to itself
/// (<c>Foo(Foo: x)</c>). Renaming the parameter to describe its role fixes both.
/// </para>
/// <para>
/// Matching is ordinal and case-sensitive, so an idiomatic camelCase parameter that differs from the
/// method only by case (method <c>Value</c>, parameter <c>value</c>) is left alone. Only an ordinary
/// method declaration is considered; constructors, operators, and local functions are out of scope.
/// The clean path reads the method name and compares each parameter identifier against it, binding no
/// symbol.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1320ParameterNameMatchesMethodAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(NamingRules.ParameterNameMatchesMethod);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Analyzes one method declaration and reports any parameter whose name matches the method name.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var methodName = method.Identifier.ValueText;

        foreach (var parameter in method.ParameterList.Parameters)
        {
            var identifier = parameter.Identifier;
            if (string.Equals(identifier.ValueText, methodName, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(DiagnosticHelper.Create(
                    NamingRules.ParameterNameMatchesMethod,
                    context.Node.SyntaxTree,
                    identifier.Span,
                    identifier.ValueText));
            }
        }
    }
}
