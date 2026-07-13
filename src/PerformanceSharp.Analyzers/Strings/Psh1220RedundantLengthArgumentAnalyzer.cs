// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a slice length that is computed to land exactly on the end (PSH1220) —
/// <c>s.Substring(i, s.Length - i)</c>, and the same shape on <c>AsSpan</c>, <c>AsMemory</c>, and
/// <c>Slice</c>. The overload that takes only a start index already means "to the end", so the
/// arithmetic restates the contract, and restates it in a form that quietly goes wrong when the code
/// around it changes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The arithmetic is proved, not guessed.</b> Two forms are reported and no others: a length of
/// <c>recv.Length - start</c> whose subtrahend is the very expression passed as the start, and a
/// length of <c>recv.Length</c> paired with a start of <c>0</c>. A length that merely happens to reach
/// the end at run time — <c>s.Substring(i, n)</c> where <c>n</c> was computed elsewhere — proves
/// nothing and is never touched. Forms that do reach the end by a longer route, such as
/// <c>s.Substring(i + 1, s.Length - i - 1)</c>, are not reported either: missing them costs nothing,
/// and a rule that guessed at them would eventually guess wrong.
/// </para>
/// <para>
/// <b>Both spellings must be safe to drop.</b> The fix deletes one reading of the receiver and one of
/// the start, so both have to be expressions that can be evaluated twice — or once fewer — without the
/// program noticing. A name, a dotted name path, or a literal qualifies;
/// <c>GetText().Substring(i, GetText().Length - i)</c> does not, and is left alone, because dropping
/// the second call is not a no-op.
/// </para>
/// <para>
/// <b>Only slices that mean "to the end".</b> The one-argument overload is only equivalent on the
/// types where it is defined to run to the end: <see cref="string"/>, the <c>MemoryExtensions</c>
/// slices, and the span and memory <c>Slice</c> methods. A user-defined <c>Slice(int, int)</c> is not
/// assumed to have a sibling that means the same thing, and the rewrite is bound before it is offered
/// in any case, so a target framework without the shorter overload never sees the diagnostic.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1220RedundantLengthArgumentAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The length property whose reading proves the slice reaches the end.</summary>
    internal const string LengthPropertyName = "Length";

    /// <summary>The argument count of a slice that passes both a start and a length.</summary>
    private const int StartAndLengthArgumentCount = 2;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(StringRules.RedundantLengthArgument);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeSlice, SyntaxKind.InvocationExpression);
    }

    /// <summary>Returns whether an invocation is a plain two-argument slice, before any binding.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the shape matches.</returns>
    internal static bool IsSliceShape(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        return arguments.Count == StartAndLengthArgumentCount
            && arguments[0] is { NameColon: null, RefOrOutKeyword.RawKind: (int)SyntaxKind.None }
            && arguments[1] is { NameColon: null, RefOrOutKeyword.RawKind: (int)SyntaxKind.None }
            && invocation.Expression is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && IsSliceName(access.Name.Identifier.ValueText);
    }

    /// <summary>Returns whether a member name is one of the slices whose one-argument form runs to the end.</summary>
    /// <param name="name">The invoked member name.</param>
    /// <returns><see langword="true"/> for a slice the rule can shorten.</returns>
    internal static bool IsSliceName(string name)
        => name is "Substring" or "AsSpan" or "AsMemory" or "Slice";

    /// <summary>Builds the shortened slice, keeping only the start argument.</summary>
    /// <param name="invocation">The reported slice invocation.</param>
    /// <returns>The invocation without its length argument.</returns>
    internal static InvocationExpressionSyntax BuildShortenedSlice(InvocationExpressionSyntax invocation)
        => invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(
                SyntaxFactory.SingletonSeparatedList(invocation.ArgumentList.Arguments[0].WithoutTrivia())));

    /// <summary>Confirms the shortened slice binds to the same slice on the same type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The reported slice invocation.</param>
    /// <param name="slice">The bound two-argument slice.</param>
    /// <returns><see langword="true"/> when the shorter overload exists and returns the same thing.</returns>
    internal static bool ShortenedSliceBinds(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol slice)
        => model.GetSpeculativeSymbolInfo(
                invocation.SpanStart,
                BuildShortenedSlice(invocation),
                SpeculativeBindingOption.BindAsExpression).Symbol is IMethodSymbol resolved
            && resolved.Name == slice.Name
            && SymbolEqualityComparer.Default.Equals(resolved.ReturnType, slice.ReturnType)
            && SymbolEqualityComparer.Default.Equals(resolved.ContainingType, slice.ContainingType);

    /// <summary>Reports PSH1220 for a length argument that restates the end of the receiver.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeSlice(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!IsSliceShape(invocation))
        {
            return;
        }

        var receiver = ((MemberAccessExpressionSyntax)invocation.Expression).Expression;
        var start = invocation.ArgumentList.Arguments[0].Expression;
        var length = invocation.ArgumentList.Arguments[1].Expression;
        if (!SpanRewriteGuard.IsRepeatable(receiver) || !SpanRewriteGuard.IsRepeatable(start))
        {
            return;
        }

        var model = context.SemanticModel;
        var cancellationToken = context.CancellationToken;
        if (BindEndToEndSlice(model, invocation, cancellationToken) is not { } slice
            || !ReachesEnd(model, receiver, start, length, cancellationToken)
            || SpanRewriteGuard.IsInsideExpressionTree(invocation, model, cancellationToken)
            || !ShortenedSliceBinds(model, invocation, slice))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            StringRules.RedundantLengthArgument,
            length.SyntaxTree,
            length.Span,
            receiver.ToString()));
    }

    /// <summary>Binds the slice and keeps it only when its one-argument form is defined to run to the end.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="invocation">The slice invocation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The bound slice, or <see langword="null"/> when the shorter form would not mean the same thing.</returns>
    /// <remarks>
    /// The closed list matters. On <see cref="string"/>, on the <c>MemoryExtensions</c> slices, and on
    /// the span and memory <c>Slice</c> methods, dropping the length is defined to run to the end. On
    /// somebody's own <c>Slice(int, int)</c> it is defined to do whatever they wrote, which might be to
    /// take that many elements — so those are not touched however well the arithmetic lines up.
    /// </remarks>
    private static IMethodSymbol? BindEndToEndSlice(SemanticModel model, InvocationExpressionSyntax invocation, CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol slice
            || slice.Parameters is not [{ Type.SpecialType: SpecialType.System_Int32 }, { Type.SpecialType: SpecialType.System_Int32 }])
        {
            return null;
        }

        var owner = slice.ContainingType;
        var declaresEndToEndSlice = owner.SpecialType == SpecialType.System_String
            || IsInSystem(owner, "MemoryExtensions")
            || IsInSystem(owner, "Span")
            || IsInSystem(owner, "ReadOnlySpan")
            || IsInSystem(owner, "Memory")
            || IsInSystem(owner, "ReadOnlyMemory");

        return declaresEndToEndSlice ? slice : null;
    }

    /// <summary>Returns whether a type is the named type in the <c>System</c> namespace.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="name">The expected simple name.</param>
    /// <returns><see langword="true"/> when the type matches.</returns>
    private static bool IsInSystem(INamedTypeSymbol type, string name)
        => type.Name == name
            && type.ContainingNamespace is { Name: nameof(System), ContainingNamespace.IsGlobalNamespace: true };

    /// <summary>Proves the length argument lands exactly on the end of the receiver.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="receiver">The sliced receiver.</param>
    /// <param name="start">The start argument.</param>
    /// <param name="length">The length argument.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the length is provably the remainder.</returns>
    private static bool ReachesEnd(
        SemanticModel model,
        ExpressionSyntax receiver,
        ExpressionSyntax start,
        ExpressionSyntax length,
        CancellationToken cancellationToken)
    {
        if (Unwrap(length) is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.SubtractExpression } subtraction)
        {
            return IsLengthOf(model, receiver, subtraction.Left, cancellationToken)
                && SyntaxFactory.AreEquivalent(Unwrap(subtraction.Right), Unwrap(start));
        }

        return Unwrap(start) is LiteralExpressionSyntax { Token.ValueText: "0" }
            && IsLengthOf(model, receiver, length, cancellationToken);
    }

    /// <summary>Returns whether an expression reads the receiver's own <c>Length</c>.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="receiver">The sliced receiver.</param>
    /// <param name="expression">The candidate length read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the expression is <c>receiver.Length</c>.</returns>
    private static bool IsLengthOf(SemanticModel model, ExpressionSyntax receiver, ExpressionSyntax expression, CancellationToken cancellationToken)
        => Unwrap(expression) is MemberAccessExpressionSyntax { RawKind: (int)SyntaxKind.SimpleMemberAccessExpression } access
            && access.Name.Identifier.ValueText == LengthPropertyName
            && SyntaxFactory.AreEquivalent(Unwrap(access.Expression), Unwrap(receiver))
            && model.GetSymbolInfo(access, cancellationToken).Symbol is IPropertySymbol { Type.SpecialType: SpecialType.System_Int32 };

    /// <summary>Strips redundant parentheses so two spellings of the same expression can be compared.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost non-parenthesized expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (current is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized.Expression;
        }

        return current;
    }
}
