// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests appending the parts of a concatenation to a <c>System.Text.StringBuilder</c>
/// separately instead of building the whole string first (PSH1214). Reports
/// <c>Append(a + b)</c> and <c>AppendLine(a + b)</c> when the receiver is a
/// <c>StringBuilder</c>, the argument is a built-in string concatenation, and the
/// argument is not a compile-time constant — the compiler folds all-constant
/// concatenations already. User-defined <c>+</c> operators are never reported.
/// The <c>StringBuilder</c> type is probed only after an invocation passes the syntax checks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1214SplitConcatenatedAppendAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(StringRules.SplitConcatenatedAppend);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var frameworkType = new FrameworkType(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, frameworkType), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1214 for a concatenated Append or AppendLine argument.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="frameworkType">The deferred type lookup shared by this compilation's callbacks.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, FrameworkType frameworkType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetConcatenationArgument(invocation, out var concatenation)
            || frameworkType.Get() is not { } builderType
            || !IsStringBuilderAppendString(context.SemanticModel, invocation, builderType, context.CancellationToken)
            || !IsBuiltInStringConcatenation(context.SemanticModel, concatenation!, context.CancellationToken)
            || context.SemanticModel.GetConstantValue(concatenation!, context.CancellationToken).HasValue)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.SplitConcatenatedAppend,
            concatenation!.SyntaxTree,
            concatenation.Span));
    }

    /// <summary>Runs the syntax-only checks: member name, argument count, and argument kind.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="concatenation">The single <c>+</c>-expression argument when the shape matches.</param>
    /// <returns><see langword="true"/> when the invocation is <c>receiver.Append(a + b)</c> or <c>receiver.AppendLine(a + b)</c>.</returns>
    private static bool TryGetConcatenationArgument(InvocationExpressionSyntax invocation, out BinaryExpressionSyntax? concatenation)
    {
        concatenation = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax { Identifier.ValueText: "Append" or "AppendLine" }
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not BinaryExpressionSyntax binary
            || !binary.IsKind(SyntaxKind.AddExpression))
        {
            return false;
        }

        concatenation = binary;
        return true;
    }

    /// <summary>Runs the receiver semantic check: the invocation must bind to a one-string-parameter <c>StringBuilder</c> method.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="builderType">The resolved string builder type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to <c>Append(string)</c> or <c>AppendLine(string)</c>.</returns>
    private static bool IsStringBuilderAppendString(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol builderType,
        CancellationToken cancellationToken) =>
        model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_String }] } method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, builderType);

    /// <summary>Runs the argument semantic check: the <c>+</c> must be the built-in string concatenation operator.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="concatenation">The candidate <c>+</c> expression.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the operator is the built-in string concatenation; user-defined operators fail.</returns>
    private static bool IsBuiltInStringConcatenation(SemanticModel model, BinaryExpressionSyntax concatenation, CancellationToken cancellationToken) =>
        model.GetSymbolInfo(concatenation, cancellationToken).Symbol is IMethodSymbol
        {
            MethodKind: MethodKind.BuiltinOperator,
            ContainingType.SpecialType: SpecialType.System_String,
        };

    /// <summary>Resolves the string builder type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose references supply the type.</param>
    private sealed class FrameworkType(Compilation compilation)
    {
        /// <summary>The metadata name of the string builder type.</summary>
        private const string StringBuilderMetadataName = "System.Text.StringBuilder";

        /// <summary>The cached type, or an empty array when unavailable; null until first demand.</summary>
        private INamedTypeSymbol[]? _resolved;

        /// <summary>Gets the string builder type, caching absent types as well as successful lookups.</summary>
        /// <returns>The resolved type, or null when unavailable.</returns>
        public INamedTypeSymbol? Get()
        {
            var resolved = _resolved ??= compilation.GetTypeByMetadataName(StringBuilderMetadataName) is { } type ? [type] : [];
            return resolved.Length == 0 ? null : resolved[0];
        }
    }
}
