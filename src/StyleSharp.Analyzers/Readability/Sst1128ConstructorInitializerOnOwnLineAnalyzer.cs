// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a constructor initializer (<c>: base(…)</c> or <c>: this(…)</c>) that shares its line
/// with the constructor signature (SST1128). Placing it on its own line separates the call to the
/// other constructor from the parameter list.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1128ConstructorInitializerOnOwnLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ReadabilityRules.ConstructorInitializerOnOwnLine);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(
            static nodeContext => LayoutHelpers.ReportWhenSharesLineWithPreviousToken(
                nodeContext,
                ((ConstructorInitializerSyntax)nodeContext.Node).ColonToken,
                ReadabilityRules.ConstructorInitializerOnOwnLine),
            SyntaxKind.BaseConstructorInitializer,
            SyntaxKind.ThisConstructorInitializer);
    }
}
