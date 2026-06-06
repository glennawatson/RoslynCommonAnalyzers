// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CSharp;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a classic <c>this</c>-parameter extension method that could be written as a C# 14
/// extension-block member (SST1703). Disabled by default — adopting the new syntax is a deliberate,
/// repo-wide migration. The rule is gated on the compilation's language version being C# 14 or later
/// so it never fires where extension blocks are unavailable; the version is compared numerically to
/// avoid naming the <c>CSharp14</c> enum value, which does not exist on the Roslyn 4.8 floor.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferExtensionBlockAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric value of <c>LanguageVersion.CSharp14</c>, the first version with extension blocks.</summary>
    private const int CSharp14 = 1400;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ExtensionRules.PreferExtensionBlock);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports a classic extension method when the language supports extension blocks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.SyntaxTree.Options is not CSharpParseOptions { } options || (int)options.LanguageVersion < CSharp14)
        {
            return;
        }

        var method = (MethodDeclarationSyntax)context.Node;
        if (method.ParameterList.Parameters.Count == 0
            || !method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ExtensionRules.PreferExtensionBlock, method.Identifier.GetLocation()));
    }
}
