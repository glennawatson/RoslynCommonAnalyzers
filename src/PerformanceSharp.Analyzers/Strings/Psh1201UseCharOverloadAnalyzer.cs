// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Suggests the char overload when a single-character plain string literal is passed to a
/// string search method (PSH1201). Only ordinal-equivalent shapes are reported so the fix
/// never changes semantics: <c>Contains("x")</c> (already ordinal), and
/// <c>StartsWith</c>/<c>EndsWith</c>/<c>IndexOf</c>/<c>LastIndexOf</c> with an explicit
/// <c>StringComparison.Ordinal</c> argument. Bare <c>StartsWith("x")</c> and
/// <c>IndexOf("x")</c> are culture-sensitive and stay untouched. Each shape is gated once
/// per compilation on the corresponding char overload existing on
/// <see cref="string"/>, so the rule is silent on targets that lack them.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1201UseCharOverloadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The argument count of the ordinal-by-default shape (<c>Contains("x")</c>).</summary>
    private const int LiteralOnlyArgumentCount = 1;

    /// <summary>The argument count of the shapes that need an explicit <c>StringComparison.Ordinal</c>.</summary>
    private const int LiteralAndComparisonArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.UseCharOverload);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var overloads = CharOverloads.Resolve(start.Compilation);
            if (!overloads.HasAny)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, overloads), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports PSH1201 for a single-character literal passed to an ordinal-safe string search shape.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="overloads">The char overloads available in this compilation.</param>
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, CharOverloads overloads)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!TryGetCandidateLiteral(invocation, overloads, out var literal, out var value, out var requiresOrdinalArgument)
            || !IsReportableMethod(context.SemanticModel, invocation, requiresOrdinalArgument, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.UseCharOverload,
            literal!.SyntaxTree,
            literal.Span,
            SyntaxFactory.Literal(value).Text));
    }

    /// <summary>Runs the syntax-only checks: member name, gated shape, argument count, literal, and comparison spelling.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <param name="overloads">The char overloads available in this compilation.</param>
    /// <param name="literal">The matched single-character literal argument.</param>
    /// <param name="value">The literal's single character.</param>
    /// <param name="requiresOrdinalArgument">Whether the shape needs an explicit <c>StringComparison.Ordinal</c> argument.</param>
    /// <returns><see langword="true"/> when the invocation is a syntactic candidate.</returns>
    private static bool TryGetCandidateLiteral(
        InvocationExpressionSyntax invocation,
        in CharOverloads overloads,
        out LiteralExpressionSyntax? literal,
        out char value,
        out bool requiresOrdinalArgument)
    {
        literal = null;
        value = default;

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name is not IdentifierNameSyntax name
            || !TryClassifyMethod(name.Identifier.ValueText, overloads, out requiresOrdinalArgument))
        {
            requiresOrdinalArgument = false;
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var expectedCount = requiresOrdinalArgument ? LiteralAndComparisonArgumentCount : LiteralOnlyArgumentCount;
        if (arguments.Count != expectedCount
            || !StringLiteralHelper.TryGetSingleCharacterLiteral(arguments[0].Expression, out literal, out value))
        {
            return false;
        }

        return !requiresOrdinalArgument
            || arguments[1].Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Ordinal" };
    }

    /// <summary>Runs the semantic checks: the string overload binding and, when required, the ordinal argument.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The candidate invocation.</param>
    /// <param name="requiresOrdinalArgument">Whether the shape needs an explicit <c>StringComparison.Ordinal</c> argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the invocation binds to a reportable string overload.</returns>
    private static bool IsReportableMethod(
        SemanticModel model,
        InvocationExpressionSyntax invocation,
        bool requiresOrdinalArgument,
        CancellationToken cancellationToken)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || method.IsStatic
            || method.ContainingType.SpecialType != SpecialType.System_String
            || method.Parameters.Length != arguments.Count
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        return !requiresOrdinalArgument
            || IsOrdinalComparison(model, arguments[1].Expression, method.Parameters[1].Type, cancellationToken);
    }

    /// <summary>Maps a member name to its reported shape, honoring the per-shape overload gates.</summary>
    /// <param name="methodName">The invoked member name.</param>
    /// <param name="overloads">The char overloads available in this compilation.</param>
    /// <param name="requiresOrdinalArgument">Whether the shape needs an explicit <c>StringComparison.Ordinal</c> argument.</param>
    /// <returns><see langword="true"/> when the member is a gated search method.</returns>
    private static bool TryClassifyMethod(string methodName, in CharOverloads overloads, out bool requiresOrdinalArgument)
    {
        switch (methodName)
        {
            case "Contains":
            {
                requiresOrdinalArgument = false;
                return overloads.Contains;
            }

            case "StartsWith":
            {
                requiresOrdinalArgument = true;
                return overloads.StartsWith;
            }

            case "EndsWith":
            {
                requiresOrdinalArgument = true;
                return overloads.EndsWith;
            }

            case "IndexOf":
            {
                requiresOrdinalArgument = true;
                return overloads.IndexOf;
            }

            case "LastIndexOf":
            {
                requiresOrdinalArgument = true;
                return overloads.LastIndexOf;
            }

            default:
            {
                requiresOrdinalArgument = false;
                return false;
            }
        }
    }

    /// <summary>Returns whether a comparison argument binds to the <c>Ordinal</c> member of the method's comparison enum.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="expression">The comparison argument expression.</param>
    /// <param name="comparisonType">The method's declared comparison parameter type.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> for a genuine <c>StringComparison.Ordinal</c> argument.</returns>
    private static bool IsOrdinalComparison(SemanticModel model, ExpressionSyntax expression, ITypeSymbol comparisonType, CancellationToken cancellationToken)
        => comparisonType is { TypeKind: TypeKind.Enum, Name: "StringComparison" }
            && model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol field
            && string.Equals(field.Name, "Ordinal", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(field.ContainingType, comparisonType);

    /// <summary>The char search overloads available on <see cref="string"/> in the current compilation.</summary>
    /// <param name="Contains">Whether <c>Contains(char)</c> exists.</param>
    /// <param name="StartsWith">Whether <c>StartsWith(char)</c> exists.</param>
    /// <param name="EndsWith">Whether <c>EndsWith(char)</c> exists.</param>
    /// <param name="IndexOf">Whether <c>IndexOf(char)</c> exists.</param>
    /// <param name="LastIndexOf">Whether <c>LastIndexOf(char)</c> exists.</param>
    internal readonly record struct CharOverloads(bool Contains, bool StartsWith, bool EndsWith, bool IndexOf, bool LastIndexOf)
    {
        /// <summary>Gets a value indicating whether at least one gated shape can ever be reported.</summary>
        public bool HasAny => Contains || StartsWith || EndsWith || IndexOf || LastIndexOf;

        /// <summary>Probes the compilation's <see cref="string"/> member list once for the char overloads.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The available char overloads.</returns>
        public static CharOverloads Resolve(Compilation compilation)
        {
            var stringType = compilation.GetSpecialType(SpecialType.System_String);
            return new(
                HasCharOverload(stringType, "Contains"),
                HasCharOverload(stringType, "StartsWith"),
                HasCharOverload(stringType, "EndsWith"),
                HasCharOverload(stringType, "IndexOf"),
                HasCharOverload(stringType, "LastIndexOf"));
        }

        /// <summary>Returns whether a named instance method with a single char parameter exists on a type.</summary>
        /// <param name="type">The type to probe.</param>
        /// <param name="methodName">The method name to probe.</param>
        /// <returns><see langword="true"/> when the char overload exists.</returns>
        private static bool HasCharOverload(INamedTypeSymbol type, string methodName)
        {
            foreach (var member in type.GetMembers(methodName))
            {
                if (member is IMethodSymbol { IsStatic: false, Parameters: [{ Type.SpecialType: SpecialType.System_Char }] })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
