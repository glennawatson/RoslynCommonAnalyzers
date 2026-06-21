// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports modern C# readability opportunities that need only narrow semantic checks after a
/// syntax-first shape match (SST2212-SST2217).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ModernSyntaxReadabilityAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The numeric C# 7 language-version value.</summary>
    private const int CSharp7 = 700;

    /// <summary>The numeric C# 11 language-version value.</summary>
    private const int CSharp11 = 1100;

    /// <summary>Diagnostic properties for UTF-8 span replacements.</summary>
    private static readonly ImmutableDictionary<string, string?> Utf8SpanProperties =
        ImmutableDictionary<string, string?>.Empty.Add(
            ModernSyntaxReadabilityAnalysis.Utf8TargetKey,
            ModernSyntaxReadabilityAnalysis.Utf8SpanTarget);

    /// <summary>Diagnostic properties for UTF-8 array replacements.</summary>
    private static readonly ImmutableDictionary<string, string?> Utf8ArrayProperties =
        ImmutableDictionary<string, string?>.Empty.Add(
            ModernSyntaxReadabilityAnalysis.Utf8TargetKey,
            ModernSyntaxReadabilityAnalysis.Utf8ArrayTarget);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.UseUtf8StringLiteral,
        ModernSyntaxRules.RemoveUnnecessaryDiscard,
        ModernSyntaxRules.UseDeconstruction,
        ModernSyntaxRules.UseTupleSwap,
        ModernSyntaxRules.UseInferredTupleElementName,
        ModernSyntaxRules.UseHashCodeCombine);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var capabilities = ModernSyntaxReadabilityCapabilities.Create(start.Compilation);
            start.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(AnalyzeArrayCreation, SyntaxKind.ArrayCreationExpression, SyntaxKind.ImplicitArrayCreationExpression);
            start.RegisterSyntaxNodeAction(AnalyzeDeclarationPattern, SyntaxKind.DeclarationPattern);
            start.RegisterSyntaxNodeAction(AnalyzeLocalDeclaration, SyntaxKind.LocalDeclarationStatement);
            start.RegisterSyntaxNodeAction(AnalyzeTupleArgument, SyntaxKind.Argument);

            if (!capabilities.HasHashCodeCombine)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeHashReturn, SyntaxKind.ReturnStatement, SyntaxKind.ArrowExpressionClause);
        });
    }

    /// <summary>Reports UTF-8 encoding calls over string literals.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(invocation, CSharp11)
            || invocation.ArgumentList.Arguments.Count != 1
            || invocation.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression)
            || !ModernSyntaxReadabilityAnalysis.IsEncodingUtf8GetBytes(invocation, context.SemanticModel, context.CancellationToken)
            || !ModernSyntaxReadabilityAnalysis.TryGetUtf8Target(invocation, context.SemanticModel, context.CancellationToken, out var target))
        {
            return;
        }

        ReportUtf8(context, invocation, target);
    }

    /// <summary>Reports literal byte array creations that can be written as UTF-8 string literals.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeArrayCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ExpressionSyntax expression
            || !IsLanguageVersionAtLeast(expression, CSharp11)
            || !ModernSyntaxReadabilityAnalysis.TryDecodeUtf8Initializer(expression, out _)
            || !ModernSyntaxReadabilityAnalysis.IsByteArrayExpression(expression, context.SemanticModel, context.CancellationToken)
            || !ModernSyntaxReadabilityAnalysis.TryGetUtf8Target(expression, context.SemanticModel, context.CancellationToken, out var target))
        {
            return;
        }

        ReportUtf8(context, expression, target);
    }

    /// <summary>Reports discard designations in declaration patterns where the type pattern is enough.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeDeclarationPattern(SyntaxNodeAnalysisContext context)
    {
        var pattern = (DeclarationPatternSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(pattern, CSharp7)
            || IsVarPatternType(pattern.Type)
            || !IsDiscardDesignation(pattern.Designation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveUnnecessaryDiscard, pattern.GetLocation()));
    }

    /// <summary>Reports tuple deconstruction and local-swap opportunities.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeLocalDeclaration(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(local, CSharp7))
        {
            return;
        }

        if (ModernSyntaxReadabilityAnalysis.TryGetDeconstructionCandidate(local, context.SemanticModel, context.CancellationToken, out _))
        {
            context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseDeconstruction, local.GetLocation()));
            return;
        }

        if (!ModernSyntaxReadabilityAnalysis.TryGetTupleSwapCandidate(local, context.SemanticModel, context.CancellationToken, out _, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseTupleSwap, local.GetLocation()));
    }

    /// <summary>Reports tuple arguments whose explicit name repeats the compiler-inferred name.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeTupleArgument(SyntaxNodeAnalysisContext context)
    {
        var argument = (ArgumentSyntax)context.Node;
        if (!IsLanguageVersionAtLeast(argument, CSharp7)
            || argument.Parent is not TupleExpressionSyntax
            || !ModernSyntaxReadabilityAnalysis.TryGetInferredTupleElementName(argument, out _))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseInferredTupleElementName, argument.GetLocation()));
    }

    /// <summary>Reports narrow manual hash-code expressions inside <c>GetHashCode</c>.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeHashReturn(SyntaxNodeAnalysisContext context)
    {
        if (!TryGetReturnedExpression(context.Node, out var expression)
            || !ModernSyntaxReadabilityAnalysis.IsGetHashCodeBody(expression, context.SemanticModel, context.CancellationToken)
            || !ModernSyntaxReadabilityAnalysis.HasSafeHashCodeCombineInputs(expression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseHashCodeCombine, expression.GetLocation()));
    }

    /// <summary>Reports a UTF-8 literal diagnostic with the target kind recorded for the code fix.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="expression">The expression to report.</param>
    /// <param name="target">The UTF-8 target kind.</param>
    private static void ReportUtf8(SyntaxNodeAnalysisContext context, ExpressionSyntax expression, string target)
    {
        var properties = target == ModernSyntaxReadabilityAnalysis.Utf8ArrayTarget ? Utf8ArrayProperties : Utf8SpanProperties;
        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.UseUtf8StringLiteral, expression.GetLocation(), properties));
    }

    /// <summary>Extracts the expression returned by a return statement or expression-bodied member.</summary>
    /// <param name="node">The candidate node.</param>
    /// <param name="expression">The returned expression.</param>
    /// <returns><see langword="true"/> when an expression was found.</returns>
    private static bool TryGetReturnedExpression(SyntaxNode node, out ExpressionSyntax expression)
    {
        switch (node)
        {
            case ReturnStatementSyntax { Expression: { } returnExpression }:
                {
                    expression = returnExpression;
                    return true;
                }

            case ArrowExpressionClauseSyntax { Expression: { } arrowExpression }:
                {
                    expression = arrowExpression;
                    return true;
                }

            default:
                {
                    expression = null!;
                    return false;
                }
        }
    }

    /// <summary>Returns whether a declaration pattern uses a discard designation.</summary>
    /// <param name="designation">The candidate designation.</param>
    /// <returns><see langword="true"/> when the declaration binds no useful local.</returns>
    private static bool IsDiscardDesignation(VariableDesignationSyntax designation)
        => designation is DiscardDesignationSyntax
            or SingleVariableDesignationSyntax { Identifier.ValueText: "_" };

    /// <summary>Returns whether a declaration pattern is the broad <c>var _</c> shape.</summary>
    /// <param name="type">The pattern type.</param>
    /// <returns><see langword="true"/> when the pattern is a var pattern.</returns>
    private static bool IsVarPatternType(TypeSyntax type)
        => type is IdentifierNameSyntax { Identifier.ValueText: "var" };

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">A syntax node in the tree.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;

    /// <summary>Capability flags resolved once per compilation.</summary>
    /// <param name="HasHashCodeCombine">Whether <c>System.HashCode.Combine</c> is available.</param>
    private readonly record struct ModernSyntaxReadabilityCapabilities(bool HasHashCodeCombine)
    {
        /// <summary>Resolves modern syntax readability capabilities from the compilation.</summary>
        /// <param name="compilation">The compilation to inspect.</param>
        /// <returns>The capability set.</returns>
        public static ModernSyntaxReadabilityCapabilities Create(Compilation compilation)
            => new(HasStaticHashCodeCombine(compilation.GetTypeByMetadataName("System.HashCode")));

        /// <summary>Returns whether a <c>System.HashCode</c> type exposes a static <c>Combine</c> method.</summary>
        /// <param name="type">The resolved type.</param>
        /// <returns><see langword="true"/> when a combine helper exists.</returns>
        private static bool HasStaticHashCodeCombine(INamedTypeSymbol? type)
        {
            if (type is null)
            {
                return false;
            }

            var members = type.GetMembers("Combine");
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol
                    {
                        IsStatic: true,
                        Parameters.Length: >= ModernSyntaxReadabilityAnalysis.HashCodeCombineMinInputs
                            and <= ModernSyntaxReadabilityAnalysis.HashCodeCombineMaxInputs
                    })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
