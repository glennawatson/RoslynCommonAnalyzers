// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Suggests the modern runtime argument-guard helpers in place of hand-written
/// checks: <c>ArgumentNullException.ThrowIfNull</c> (SST2000),
/// <c>ArgumentException.ThrowIfNullOrEmpty</c> (SST2001), and
/// <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> (SST2002). Each helper is resolved
/// once per compilation by probing for the method, so a rule fires only where the
/// replacement actually exists in the referenced framework — no target framework
/// string is parsed and nothing is reported on older runtimes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArgumentGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernizationRules.UseThrowIfNull,
        ModernizationRules.UseThrowIfNullOrEmpty,
        ModernizationRules.UseThrowIfNullOrWhiteSpace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var helpers = new GuardHelpers(
                HasStaticMethod(start.Compilation, "System.ArgumentNullException", "ThrowIfNull"),
                HasStaticMethod(start.Compilation, "System.ArgumentException", "ThrowIfNullOrEmpty"),
                HasStaticMethod(start.Compilation, "System.ArgumentException", "ThrowIfNullOrWhiteSpace"));

            if (!helpers.Any)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeIf(nodeContext, helpers), SyntaxKind.IfStatement);
        });
    }

    /// <summary>Reports the applicable guard-helper suggestion for one if statement.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="helpers">The guard helpers available in this compilation.</param>
    private static void AnalyzeIf(SyntaxNodeAnalysisContext context, GuardHelpers helpers)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        if (helpers.ThrowIfNull && ThrowGuardPatterns.TryMatchArgumentNull(ifStatement, out var nullChecked))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernizationRules.UseThrowIfNull, ifStatement.GetLocation(), nullChecked!.ToString()));
            return;
        }

        if (!ThrowGuardPatterns.TryMatchStringGuard(ifStatement, out var guardMethod, out var stringChecked))
        {
            return;
        }

        ReportStringGuard(context, ifStatement, helpers, guardMethod!, stringChecked!);
    }

    /// <summary>Reports SST2001/SST2002 for a matched string guard when its helper is available.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="ifStatement">The matched if statement.</param>
    /// <param name="helpers">The guard helpers available in this compilation.</param>
    /// <param name="guardMethod">The matched guard method name.</param>
    /// <param name="checkedExpression">The checked string expression.</param>
    private static void ReportStringGuard(
        SyntaxNodeAnalysisContext context,
        IfStatementSyntax ifStatement,
        GuardHelpers helpers,
        string guardMethod,
        ExpressionSyntax checkedExpression)
    {
        var (available, rule) = guardMethod == ThrowGuardPatterns.IsNullOrEmpty
            ? (helpers.ThrowIfNullOrEmpty, ModernizationRules.UseThrowIfNullOrEmpty)
            : (helpers.ThrowIfNullOrWhiteSpace, ModernizationRules.UseThrowIfNullOrWhiteSpace);

        if (!available)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, ifStatement.GetLocation(), checkedExpression.ToString()));
    }

    /// <summary>Returns whether a type has a static method with the given name.</summary>
    /// <param name="compilation">The compilation to resolve the type in.</param>
    /// <param name="typeMetadataName">The metadata name of the declaring type.</param>
    /// <param name="methodName">The method name to look for.</param>
    /// <returns><see langword="true"/> when the type exists and declares such a method.</returns>
    private static bool HasStaticMethod(Compilation compilation, string typeMetadataName, string methodName)
        => compilation.GetTypeByMetadataName(typeMetadataName) is { } type
            && type.GetMembers(methodName).Any(member => member is IMethodSymbol { IsStatic: true });

    /// <summary>The guard helpers available in a compilation's referenced framework.</summary>
    /// <param name="ThrowIfNull">Whether <c>ArgumentNullException.ThrowIfNull</c> exists.</param>
    /// <param name="ThrowIfNullOrEmpty">Whether <c>ArgumentException.ThrowIfNullOrEmpty</c> exists.</param>
    /// <param name="ThrowIfNullOrWhiteSpace">Whether <c>ArgumentException.ThrowIfNullOrWhiteSpace</c> exists.</param>
    private readonly record struct GuardHelpers(bool ThrowIfNull, bool ThrowIfNullOrEmpty, bool ThrowIfNullOrWhiteSpace)
    {
        /// <summary>Gets a value indicating whether any guard helper is available.</summary>
        public bool Any => ThrowIfNull || ThrowIfNullOrEmpty || ThrowIfNullOrWhiteSpace;
    }
}
