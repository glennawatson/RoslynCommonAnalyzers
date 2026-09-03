// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an argument-less <c>ToString</c> call on a receiver the compiler already types as
/// <see cref="string"/> (SST2286): <c>text.ToString()</c> returns the receiver itself, so the call is the
/// expression <c>text</c> written the long way.
/// </summary>
/// <remarks>
/// <para>
/// A call inside an interpolation hole is left to the rule that simplifies interpolations, so the two never
/// report the same <c>ToString</c>.
/// </para>
/// <para>
/// The clean path is syntactic: only an invocation whose invoked name is <c>ToString</c> and whose argument
/// list is empty reaches the semantic model, and the model is asked one question — the receiver's type. A
/// <c>ToString</c> that takes a format or a provider does real work and never becomes a candidate, and a
/// call reached through <c>?.</c> is a member binding rather than a member access, so it is not one either.
/// </para>
/// <para>
/// The receiver's type settles the call on its own: <c>string</c> declares one argument-less
/// <c>ToString</c>, and an extension method cannot displace an applicable instance method, so a zero-argument
/// <c>ToString</c> on a string receiver is always <c>string.ToString()</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2286RedundantStringToStringAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The invoked member name the rule looks for.</summary>
    private const string ToStringName = "ToString";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ModernSyntaxRules.RedundantStringToString);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    /// <summary>Reports one <c>ToString</c> call whose receiver is already a string.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.ArgumentList.Arguments.Count != 0 || invocation.Parent is InterpolationSyntax)
        {
            return;
        }

        const int simpleMemberAccess = (int)SyntaxKind.SimpleMemberAccessExpression;
        if (invocation.Expression is not MemberAccessExpressionSyntax
            { RawKind: simpleMemberAccess, Name: IdentifierNameSyntax { Identifier.ValueText: ToStringName } } access)
        {
            return;
        }

        if (context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type is not { SpecialType: SpecialType.System_String })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.RedundantStringToString,
            invocation.SyntaxTree,
            invocation.Span));
    }
}
