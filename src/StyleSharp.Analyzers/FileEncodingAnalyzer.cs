// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Text;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports source files whose byte order mark does not match the configured preference:
/// UTF-8 with a BOM (SST1412) or UTF-8 without a BOM (SST1450). Both rules are off by
/// default and mutually exclusive — enable whichever your project standardises on.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileEncodingAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        MaintainabilityRules.Utf8WithBom,
        MaintainabilityRules.Utf8WithoutBom);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxTreeAction(Analyze);
    }

    /// <summary>Reports a mismatch between the file's byte order mark and the configured preference.</summary>
    /// <param name="context">The syntax tree analysis context.</param>
    private static void Analyze(SyntaxTreeAnalysisContext context)
    {
        var encoding = context.Tree.GetText(context.CancellationToken).Encoding;
        var hasBom = encoding?.GetPreamble().Length > 0;
        var location = Location.Create(context.Tree, new(0, 0));

        var rule = hasBom ? MaintainabilityRules.Utf8WithoutBom : MaintainabilityRules.Utf8WithBom;
        context.ReportDiagnostic(Diagnostic.Create(rule, location));
    }
}
