// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an array creation whose element type is named against the configured style (SST2270): an implicit
/// <c>new[] { ... }</c> when <c>stylesharp.array_creation_type_style</c> is <c>explicit</c>, or an explicit
/// <c>new T[] { ... }</c> when it is <c>implicit</c> or <c>implicit_when_obvious</c> (the default). The rule is
/// opt-in and off by default, and governs only the explicit-versus-implicit element-type axis — it never
/// suggests a collection expression.
/// </summary>
/// <remarks>
/// Only a single-dimensional array with an initializer and no explicit size is convertible. Safety is a
/// speculative bind: the rewritten form is bound at the creation's position, and the diagnostic is raised only
/// when it resolves to the identical array type, so a rewrite that would infer a different element type or fail
/// to bind is never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2270ArrayCreationTypeStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reported target when the codebase names the element type explicitly.</summary>
    internal const string ExplicitTarget = "explicit";

    /// <summary>The reported target when the codebase infers the element type.</summary>
    internal const string ImplicitTarget = "implicit";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeArrayCreationTypeStyle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ArrayCreationExpression, SyntaxKind.ImplicitArrayCreationExpression);
    }

    /// <summary>Returns whether an explicit array creation is a single-dimensional initialized array with no size.</summary>
    /// <param name="creation">The explicit array creation to inspect.</param>
    /// <returns><see langword="true"/> when the element type can be dropped for an implicit form.</returns>
    internal static bool IsConvertibleExplicit(ArrayCreationExpressionSyntax creation)
    {
        if (creation.Initializer is null || creation.Type.RankSpecifiers.Count != 1)
        {
            return false;
        }

        var rank = creation.Type.RankSpecifiers[0];
        if (rank.Sizes.Count != 1)
        {
            return false;
        }

        return rank.Sizes[0].IsKind(SyntaxKind.OmittedArraySizeExpression);
    }

    /// <summary>Returns whether every element of an initializer names its own type at a glance.</summary>
    /// <param name="initializer">The array initializer to inspect.</param>
    /// <returns><see langword="true"/> when each element is a literal, a cast, or an object/array creation.</returns>
    internal static bool AllElementsObvious(InitializerExpressionSyntax initializer)
    {
        foreach (var element in initializer.Expressions)
        {
            if (element is not (LiteralExpressionSyntax or CastExpressionSyntax or ObjectCreationExpressionSyntax or ArrayCreationExpressionSyntax))
            {
                return false;
            }
        }

        return initializer.Expressions.Count != 0;
    }

    /// <summary>Builds the implicit form of an explicit array creation, dropping the element type.</summary>
    /// <param name="creation">The explicit array creation; callers must have validated the shape.</param>
    /// <returns>The implicit array creation.</returns>
    internal static ImplicitArrayCreationExpressionSyntax BuildImplicit(ArrayCreationExpressionSyntax creation)
        => SyntaxFactory.ImplicitArrayCreationExpression(
            creation.NewKeyword.WithTrailingTrivia(),
            SyntaxFactory.Token(SyntaxKind.OpenBracketToken),
            default,
            SyntaxFactory.Token(SyntaxKind.CloseBracketToken),
            creation.Initializer!);

    /// <summary>Builds the explicit form of an implicit array creation, naming the element type.</summary>
    /// <param name="creation">The implicit array creation; callers must have validated the shape.</param>
    /// <param name="typeName">The minimally-qualified element type name to splice in.</param>
    /// <returns>The explicit array creation.</returns>
    internal static ArrayCreationExpressionSyntax BuildExplicit(ImplicitArrayCreationExpressionSyntax creation, string typeName)
        => SyntaxFactory.ArrayCreationExpression(
            creation.NewKeyword.WithTrailingTrivia(SyntaxFactory.Space),
            SyntaxFactory.ArrayType(
                SyntaxFactory.ParseTypeName(typeName),
                SyntaxFactory.SingletonList(SyntaxFactory.ArrayRankSpecifier(SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(SyntaxFactory.OmittedArraySizeExpression())))),
            creation.Initializer);

    /// <summary>Returns the implicit rewrite of an explicit array creation when it binds to the same type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The explicit array creation.</param>
    /// <returns>The implicit rewrite, or <see langword="null"/> when it is not bind-safe.</returns>
    internal static ImplicitArrayCreationExpressionSyntax? TryConvertToImplicit(SemanticModel model, ArrayCreationExpressionSyntax creation)
    {
        if (model.GetTypeInfo(creation).Type is not IArrayTypeSymbol arrayType)
        {
            return null;
        }

        var implicitForm = BuildImplicit(creation);
        var bound = model.GetSpeculativeTypeInfo(creation.SpanStart, implicitForm, SpeculativeBindingOption.BindAsExpression).Type;
        return bound is not null && SymbolEqualityComparer.Default.Equals(bound, arrayType) ? implicitForm : null;
    }

    /// <summary>Returns the explicit rewrite of an implicit array creation when it binds to the same type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The implicit array creation.</param>
    /// <returns>The explicit rewrite, or <see langword="null"/> when it is not bind-safe or unnameable.</returns>
    internal static ArrayCreationExpressionSyntax? TryConvertToExplicit(SemanticModel model, ImplicitArrayCreationExpressionSyntax creation)
    {
        if (model.GetTypeInfo(creation).Type is not IArrayTypeSymbol arrayType
            || !Sst2254ExplicitObjectCreationTypeAnalyzer.IsExpressibleTypeName(arrayType.ElementType))
        {
            return null;
        }

        var typeName = arrayType.ElementType.ToMinimalDisplayString(model, creation.SpanStart);
        var explicitForm = BuildExplicit(creation, typeName);
        var bound = model.GetSpeculativeTypeInfo(creation.SpanStart, explicitForm, SpeculativeBindingOption.BindAsExpression).Type;
        return bound is not null && SymbolEqualityComparer.Default.Equals(bound, arrayType) ? explicitForm : null;
    }

    /// <summary>Reports an array creation whose element-type style does not match the configured one.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var style = ModernSyntaxStyleOptions.ReadArrayCreationTypeStyle(context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Node.SyntaxTree));
        switch (context.Node)
        {
            case ImplicitArrayCreationExpressionSyntax implicitCreation:
                {
                    AnalyzeImplicit(context, implicitCreation, style);
                    break;
                }

            case ArrayCreationExpressionSyntax explicitCreation:
                {
                    AnalyzeExplicit(context, explicitCreation, style);
                    break;
                }
        }
    }

    /// <summary>Reports an implicit array creation when the codebase names element types explicitly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The implicit array creation.</param>
    /// <param name="style">The configured element-type style.</param>
    private static void AnalyzeImplicit(SyntaxNodeAnalysisContext context, ImplicitArrayCreationExpressionSyntax creation, ArrayCreationTypeStyle style)
    {
        if (style != ArrayCreationTypeStyle.Explicit || TryConvertToExplicit(context.SemanticModel, creation) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeArrayCreationTypeStyle, creation.NewKeyword.GetLocation(), ExplicitTarget));
    }

    /// <summary>Reports an explicit array creation when the codebase infers element types.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="creation">The explicit array creation.</param>
    /// <param name="style">The configured element-type style.</param>
    private static void AnalyzeExplicit(SyntaxNodeAnalysisContext context, ArrayCreationExpressionSyntax creation, ArrayCreationTypeStyle style)
    {
        if (style == ArrayCreationTypeStyle.Explicit || !IsConvertibleExplicit(creation))
        {
            return;
        }

        if (style == ArrayCreationTypeStyle.ImplicitWhenObvious && !AllElementsObvious(creation.Initializer!))
        {
            return;
        }

        if (TryConvertToImplicit(context.SemanticModel, creation) is null)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeArrayCreationTypeStyle, creation.Type.GetLocation(), ImplicitTarget));
    }
}
