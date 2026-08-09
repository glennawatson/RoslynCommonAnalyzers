// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>Reports nullable syntax that no longer changes flow or file-local nullable state.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableSyntaxCleanupAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ModernSyntaxRules.RemoveUnneededNullForgiving,
        ModernSyntaxRules.RemoveRepeatedNullableDirective,
        ModernSyntaxRules.RemoveUnusedNullableRestore);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeNullForgiving, SyntaxKind.SuppressNullableWarningExpression);
        context.RegisterSyntaxTreeAction(AnalyzeNullableDirectives);
    }

    /// <summary>Reports a null-forgiving operator applied to a value that cannot be null.</summary>
    /// <param name="context">The syntax node context.</param>
    private static void AnalyzeNullForgiving(SyntaxNodeAnalysisContext context)
    {
        var suppression = (PostfixUnaryExpressionSyntax)context.Node;
        if (IsNullLikeDefault(suppression.Operand))
        {
            return;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(suppression.Operand, context.CancellationToken);
        if (!IsProvablyNonNull(typeInfo, suppression.Operand))
        {
            return;
        }

        if (CarriesNestedNullability(typeInfo.Type))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveUnneededNullForgiving, suppression.OperatorToken.GetLocation()));
    }

    /// <summary>Returns whether the operand is a null/default value whose suppression can be target-context meaningful.</summary>
    /// <param name="operand">The suppressed expression.</param>
    /// <returns><see langword="true"/> when the operand should not be treated as a no-op suppression.</returns>
    private static bool IsNullLikeDefault(ExpressionSyntax operand)
        => operand.IsKind(SyntaxKind.NullLiteralExpression)
            || operand.IsKind(SyntaxKind.DefaultLiteralExpression)
            || operand.IsKind(SyntaxKind.DefaultExpression);

    /// <summary>Reports repeated nullable directives in file order.</summary>
    /// <param name="context">The syntax tree context.</param>
    private static void AnalyzeNullableDirectives(SyntaxTreeAnalysisContext context)
    {
        var root = context.Tree.GetRoot(context.CancellationToken);
        string? currentState = null;
        var sawFileStateChange = false;

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.GetStructure() is not NullableDirectiveTriviaSyntax directive)
            {
                continue;
            }

            var setting = directive.SettingToken.ValueText;
            if (setting == "restore")
            {
                if (!sawFileStateChange)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveUnusedNullableRestore, directive.GetLocation()));
                }

                currentState = null;
                continue;
            }

            sawFileStateChange = true;
            var state = StateKey(directive);
            if (currentState == state)
            {
                context.ReportDiagnostic(Diagnostic.Create(ModernSyntaxRules.RemoveRepeatedNullableDirective, directive.GetLocation()));
            }

            currentState = state;
        }
    }

    /// <summary>Returns whether the operand cannot be null whatever the surrounding flow decided.</summary>
    /// <param name="typeInfo">The operand type information.</param>
    /// <param name="operand">The suppressed expression.</param>
    /// <returns><see langword="true"/> when suppressing nullability has no effect.</returns>
    /// <remarks>
    /// The operand's own flow state cannot answer this. Nullable analysis applies the suppression to the
    /// operand itself, so asking the model about it always reports the operand as not-null — including for the
    /// load-bearing case, where removing the <c>!</c> would produce a warning. Only shapes that are non-null
    /// by construction are reported.
    /// </remarks>
    private static bool IsProvablyNonNull(TypeInfo typeInfo, ExpressionSyntax operand)
        => typeInfo.Type is { IsValueType: true, OriginalDefinition.SpecialType: not SpecialType.System_Nullable_T }
            || CreatesANewInstance(operand)
            || ProducesAConstantOrSelf(operand);

    /// <summary>Returns whether a type has nullability nested inside it that a conversion could disagree on.</summary>
    /// <param name="type">The suppressed operand's type.</param>
    /// <returns><see langword="true"/> for an array, a pointer, and any constructed generic type or tuple.</returns>
    /// <remarks>
    /// <para>
    /// The null-forgiving operator does not only silence a maybe-null dereference; it also silences the
    /// nullability of an identity conversion at that expression. A value that is non-null by construction can
    /// still be the source of one — <c>new List&lt;string&gt;()!</c> assigned to a <c>List&lt;string?&gt;</c>,
    /// a <c>(string?, string?)</c> handed to a <c>(string, string)</c>, a <c>ReadOnlySpan&lt;string?&gt;</c>
    /// widened to <c>ReadOnlySpan&lt;string&gt;</c>. Removing the <c>!</c> there does not tidy anything, it
    /// uncovers a CS8619.
    /// </para>
    /// <para>
    /// The semantic model cannot be asked which case this is: nullable analysis applies the suppression to the
    /// operand before answering, so the operand and the conversion target always look like they agree. What is
    /// still visible is the shape of the type, and only a type with a nullable component nested inside it —
    /// an array's element, a pointer's target, a generic argument, a tuple element — can carry such a
    /// conversion at all. Those are left alone; a type with nothing nested has no hidden nullability to
    /// convert, so its suppression really is a no-op.
    /// </para>
    /// </remarks>
    private static bool CarriesNestedNullability(ITypeSymbol? type) => type switch
    {
        IArrayTypeSymbol => true,
        IPointerTypeSymbol => true,
        INamedTypeSymbol { TypeArguments.Length: > 0 } => true,
        _ => false,
    };

    /// <summary>Returns whether an expression allocates the value it yields.</summary>
    /// <param name="operand">The suppressed expression.</param>
    /// <returns><see langword="true"/> for an object, array, or collection creation.</returns>
    private static bool CreatesANewInstance(ExpressionSyntax operand) => operand
        is ObjectCreationExpressionSyntax
        or ImplicitObjectCreationExpressionSyntax
        or AnonymousObjectCreationExpressionSyntax
        or ArrayCreationExpressionSyntax
        or ImplicitArrayCreationExpressionSyntax
        or CollectionExpressionSyntax;

    /// <summary>Returns whether an expression yields a value the language guarantees is present.</summary>
    /// <param name="operand">The suppressed expression.</param>
    /// <returns><see langword="true"/> for a literal, an interpolated string, a <c>typeof</c>, or the enclosing instance.</returns>
    private static bool ProducesAConstantOrSelf(ExpressionSyntax operand) => operand
        is LiteralExpressionSyntax
        or InterpolatedStringExpressionSyntax
        or TypeOfExpressionSyntax
        or ThisExpressionSyntax
        or BaseExpressionSyntax;

    /// <summary>Builds a compact comparable key for a nullable directive state.</summary>
    /// <param name="directive">The nullable directive.</param>
    /// <returns>The directive state key.</returns>
    private static string StateKey(NullableDirectiveTriviaSyntax directive)
        => directive.TargetToken.ValueText.Length == 0
            ? directive.SettingToken.ValueText
            : $"{directive.SettingToken.ValueText}:{directive.TargetToken.ValueText}";
}
