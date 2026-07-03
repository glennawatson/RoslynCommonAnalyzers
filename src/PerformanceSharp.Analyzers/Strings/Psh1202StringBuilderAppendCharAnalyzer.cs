// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests appending single characters to a <c>System.Text.StringBuilder</c> as
/// <see cref="char"/> rather than a one-character string literal (PSH1202). Reports
/// <c>Append("x")</c> and <c>Insert(index, "x")</c> when the receiver is a
/// <c>StringBuilder</c> and the argument is a plain single-character string literal.
/// The <c>StringBuilder</c> type and its <c>Append(char)</c>/<c>Insert(int, char)</c>
/// overloads are probed once per compilation, so the rule costs nothing where they
/// are missing.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1202StringBuilderAppendCharAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the string builder type.</summary>
    private const string StringBuilderMetadataName = "System.Text.StringBuilder";

    /// <summary>The argument count of the <c>Append(string)</c> shape.</summary>
    private const int AppendArgumentCount = 1;

    /// <summary>The argument count of the <c>Insert(int, string)</c> shape.</summary>
    private const int InsertArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.StringBuilderAppendChar);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            if (!StringBuilderOverloads.TryResolve(start.Compilation, out var overloads))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, overloads), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1202 for a single-character literal passed to <c>Append</c> or <c>Insert</c>.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="overloads">The string builder char overloads available in this compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, StringBuilderOverloads overloads)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetCandidateLiteral(invocation, overloads, out var literal, out var methodName, out var literalIndex)
            || !IsReportableMethod(context.SemanticModel, invocation, overloads, literalIndex, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.StringBuilderAppendChar,
            literal!.SyntaxTree,
            literal.Span,
            methodName!));
    }

    /// <summary>Runs the syntax-only checks: member name, gated shape, argument count, and literal shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="overloads">The string builder char overloads available in this compilation.</param>
    /// <param name="literal">The matched single-character literal argument.</param>
    /// <param name="methodName">The invoked member name (<c>Append</c> or <c>Insert</c>).</param>
    /// <param name="literalIndex">The argument index holding the literal.</param>
    /// <returns><see langword="true"/> when the invocation is a syntactic candidate.</returns>
    private static bool TryGetCandidateLiteral(
        InvocationExpressionSyntax invocation,
        in StringBuilderOverloads overloads,
        out LiteralExpressionSyntax? literal,
        out string? methodName,
        out int literalIndex)
    {
        literal = null;
        methodName = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax name
            || !TryGetLiteralIndex(name.Identifier.ValueText, invocation.ArgumentList.Arguments.Count, overloads, out literalIndex))
        {
            literalIndex = 0;
            return false;
        }

        methodName = name.Identifier.ValueText;
        return StringLiteralHelper.TryGetSingleCharacterLiteral(invocation.ArgumentList.Arguments[literalIndex].Expression, out literal, out _);
    }

    /// <summary>Runs the semantic checks: the invocation must bind to the string overload on <c>StringBuilder</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="overloads">The string builder char overloads available in this compilation.</param>
    /// <param name="literalIndex">The argument index holding the literal.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to a reportable overload.</returns>
    private static bool IsReportableMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        in StringBuilderOverloads overloads,
        int literalIndex,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.IsStatic
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, overloads.BuilderType)
            || method.Parameters.Length != invocation.ArgumentList.Arguments.Count
            || method.Parameters[literalIndex].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        return literalIndex == 0 || method.Parameters[0].Type.SpecialType == SpecialType.System_Int32;
    }

    /// <summary>Maps a member name and argument count to the literal argument position, honoring the overload gates.</summary>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="argumentCount">The invocation's argument count.</param>
    /// <param name="overloads">The string builder char overloads available in this compilation.</param>
    /// <param name="literalIndex">The argument index that must be the single-character literal.</param>
    /// <returns><see langword="true"/> when the member is a gated append shape.</returns>
    private static bool TryGetLiteralIndex(string methodName, int argumentCount, in StringBuilderOverloads overloads, out int literalIndex)
    {
        if (methodName == "Append" && argumentCount == AppendArgumentCount && overloads.HasAppendChar)
        {
            literalIndex = 0;
            return true;
        }

        if (methodName == "Insert" && argumentCount == InsertArgumentCount && overloads.HasInsertChar)
        {
            literalIndex = 1;
            return true;
        }

        literalIndex = 0;
        return false;
    }

    /// <summary>The <c>StringBuilder</c> type and its char overload availability for one compilation.</summary>
    /// <param name="BuilderType">The resolved string builder type.</param>
    /// <param name="HasAppendChar">Whether <c>Append(char)</c> exists.</param>
    /// <param name="HasInsertChar">Whether <c>Insert(int, char)</c> exists.</param>
    internal readonly record struct StringBuilderOverloads(INamedTypeSymbol BuilderType, bool HasAppendChar, bool HasInsertChar)
    {
        /// <summary>Probes the compilation once for <c>StringBuilder</c> and its char overloads.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <param name="overloads">The resolved overload availability.</param>
        /// <returns><see langword="true"/> when the type exists and at least one char overload is available.</returns>
        public static bool TryResolve(Compilation compilation, out StringBuilderOverloads overloads)
        {
            if (compilation.GetTypeByMetadataName(StringBuilderMetadataName) is not { } builderType)
            {
                overloads = default;
                return false;
            }

            var hasAppendChar = HasAppendCharOverload(builderType);
            var hasInsertChar = HasInsertCharOverload(builderType);
            if (!hasAppendChar && !hasInsertChar)
            {
                overloads = default;
                return false;
            }

            overloads = new(builderType, hasAppendChar, hasInsertChar);
            return true;
        }

        /// <summary>Returns whether <c>Append(char)</c> exists on the string builder type.</summary>
        /// <param name="builderType">The string builder type.</param>
        /// <returns><see langword="true"/> when the overload exists.</returns>
        private static bool HasAppendCharOverload(INamedTypeSymbol builderType)
        {
            foreach (var member in builderType.GetMembers("Append"))
            {
                if (member is IMethodSymbol { IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_Char }] })
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns whether <c>Insert(int, char)</c> exists on the string builder type.</summary>
        /// <param name="builderType">The string builder type.</param>
        /// <returns><see langword="true"/> when the overload exists.</returns>
        private static bool HasInsertCharOverload(INamedTypeSymbol builderType)
        {
            foreach (var member in builderType.GetMembers("Insert"))
            {
                if (member is IMethodSymbol
                    {
                        IsStatic: false,
                        Parameters: [{ Type.SpecialType: SpecialType.System_Int32 }, { Type.SpecialType: SpecialType.System_Char }]
                    })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
