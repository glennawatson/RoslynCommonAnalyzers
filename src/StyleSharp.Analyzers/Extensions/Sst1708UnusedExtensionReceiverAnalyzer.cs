// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a classic <c>this</c>-parameter extension method whose body never reads the receiver
/// parameter (SST1708). Such a method gains nothing from extension syntax. There is no code fix:
/// removing <c>this</c> would break every instance-style call site, so the author decides how to
/// re-shape it. The body is scanned only after the cheap syntactic gate confirms an extension method
/// with a real body, and the identifier scan stops at the first receiver read.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1708UnusedExtensionReceiverAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ExtensionRules.UnusedExtensionReceiver);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Reports an extension method whose body never references its receiver parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!ExtensionBlockHelper.IsClassicExtensionMethod(method))
        {
            return;
        }

        var body = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (body is null)
        {
            return;
        }

        var receiver = method.ParameterList.Parameters[0].Identifier;
        var receiverName = receiver.ValueText;
        if (receiverName.Length == 0 || receiverName == "_")
        {
            return;
        }

        if (ExtensionBlockHelper.ReadsIdentifier(body, receiverName))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ExtensionRules.UnusedExtensionReceiver,
            receiver.GetLocation(),
            method.Identifier.ValueText,
            receiverName));
    }
}
