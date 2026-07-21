// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a local declaration or <c>foreach</c> variable whose type spelling does not match the configured
/// <c>var</c> style (SST2271): <c>stylesharp.use_var</c> is <c>always</c>, <c>never</c>, or <c>when_obvious</c>
/// (the default, where a right-hand side that names its type with a <c>new</c>, a cast, or a literal is
/// obvious). The rule is opt-in and off by default.
/// </summary>
/// <remarks>
/// <para>
/// Explicit-to-<c>var</c> is offered only when <c>var</c> would infer the identical type — the initializer's
/// natural type must equal the declared type, so an interface- or base-typed local such as
/// <c>IList&lt;int&gt; x = new List&lt;int&gt;()</c> is left alone — and never for a target-typed initializer
/// (<c>new()</c>, a collection expression, or <c>default</c>) that <c>var</c> cannot infer.
/// </para>
/// <para>
/// <c>var</c>-to-explicit names the inferred type, gated on that type having a source-expressible name and on
/// the name binding back to it. A <c>foreach</c> has no initializer to judge, so the <c>when_obvious</c> mode
/// leaves loop variables alone; <c>always</c> and <c>never</c> still normalize them.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2271VarStyleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The reported and offered target when the declaration should use <c>var</c>.</summary>
    internal const string VarTarget = "var";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.NormalizeVarStyle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeLocal, SyntaxKind.LocalDeclarationStatement);
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
    }

    /// <summary>Returns whether an initializer names its own type at a glance.</summary>
    /// <param name="expression">The initializer expression.</param>
    /// <returns><see langword="true"/> for a <c>new T(...)</c>, <c>new T[]</c>, cast, <c>default(T)</c>, or a typed literal.</returns>
    internal static bool IsObviousInitializer(ExpressionSyntax expression) => expression switch
    {
        ObjectCreationExpressionSyntax => true,
        ArrayCreationExpressionSyntax => true,
        CastExpressionSyntax => true,
        DefaultExpressionSyntax => true,
        LiteralExpressionSyntax literal => !literal.IsKind(SyntaxKind.NullLiteralExpression) && !literal.IsKind(SyntaxKind.DefaultLiteralExpression),
        _ => false,
    };

    /// <summary>Returns the type a local declaration or <c>foreach</c> variable's type node stands for.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="typeSyntax">The declared or inferred type node.</param>
    /// <returns>The resolved type, or <see langword="null"/> when it cannot be resolved.</returns>
    internal static ITypeSymbol? ResolveVariableType(SemanticModel model, TypeSyntax typeSyntax) => typeSyntax.Parent switch
    {
        VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax } => model.GetTypeInfo(typeSyntax).Type,
        ForEachStatementSyntax forEach when forEach.Type == typeSyntax => model.GetForEachStatementInfo(forEach).ElementType,
        _ => null,
    };

    /// <summary>Returns whether <c>var</c> would infer the declared type from an initializer.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="initializer">The initializer expression.</param>
    /// <param name="declaredType">The declared type of the local.</param>
    /// <returns><see langword="true"/> when the initializer's natural type equals the declared type.</returns>
    internal static bool VarInfersSameType(SemanticModel model, ExpressionSyntax initializer, ITypeSymbol declaredType)
    {
        if (initializer is ImplicitObjectCreationExpressionSyntax or CollectionExpressionSyntax
            || (initializer is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.DefaultLiteralExpression)))
        {
            return false;
        }

        var natural = model.GetTypeInfo(initializer).Type;
        return natural is not null && SymbolEqualityComparer.Default.Equals(natural, declaredType);
    }

    /// <summary>Returns whether a minimally-qualified type name binds back to the intended type at a position.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="position">The source position at which to bind.</param>
    /// <param name="typeName">The type name to bind.</param>
    /// <param name="expected">The type the name must resolve to.</param>
    /// <returns><see langword="true"/> when the name resolves to the expected type.</returns>
    internal static bool TypeNameBindsTo(SemanticModel model, int position, string typeName, ITypeSymbol expected)
    {
        var bound = model.GetSpeculativeTypeInfo(position, SyntaxFactory.ParseTypeName(typeName), SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
        return bound is not null && SymbolEqualityComparer.Default.Equals(bound, expected);
    }

    /// <summary>Reports a local declaration whose type spelling does not match the configured style.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeLocal(SyntaxNodeAnalysisContext context)
    {
        var local = (LocalDeclarationStatementSyntax)context.Node;
        var declaration = local.Declaration;
        if (declaration.Variables.Count != 1 || declaration.Type is RefTypeSyntax || local.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return;
        }

        if (declaration.Variables[0].Initializer is not { } equalsValue)
        {
            return;
        }

        var style = ModernSyntaxStyleOptions.ReadUseVar(context.Options.AnalyzerConfigOptionsProvider.GetOptions(local.SyntaxTree));
        var shouldBeVar = style switch
        {
            UseVarStyle.Always => true,
            UseVarStyle.Never => false,
            _ => IsObviousInitializer(equalsValue.Value),
        };

        var typeSyntax = declaration.Type;
        if (typeSyntax.IsVar == shouldBeVar || context.SemanticModel.GetTypeInfo(typeSyntax).Type is not { } declaredType)
        {
            return;
        }

        Report(context, typeSyntax, shouldBeVar, declaredType, equalsValue.Value);
    }

    /// <summary>Reports a <c>foreach</c> loop variable whose type spelling does not match the configured style.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        var forEach = (ForEachStatementSyntax)context.Node;
        var style = ModernSyntaxStyleOptions.ReadUseVar(context.Options.AnalyzerConfigOptionsProvider.GetOptions(forEach.SyntaxTree));
        if (style == UseVarStyle.WhenObvious)
        {
            return;
        }

        var shouldBeVar = style == UseVarStyle.Always;
        var typeSyntax = forEach.Type;
        if (typeSyntax.IsVar == shouldBeVar || context.SemanticModel.GetForEachStatementInfo(forEach).ElementType is not { } elementType)
        {
            return;
        }

        Report(context, typeSyntax, shouldBeVar, elementType, initializer: null);
    }

    /// <summary>Reports the diagnostic in the chosen direction when the rewrite is provably safe.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="typeSyntax">The declared or inferred type node.</param>
    /// <param name="toVar">Whether the target style is <c>var</c>.</param>
    /// <param name="resolvedType">The declared type of the variable.</param>
    /// <param name="initializer">The initializer, when the declaration has one.</param>
    private static void Report(SyntaxNodeAnalysisContext context, TypeSyntax typeSyntax, bool toVar, ITypeSymbol resolvedType, ExpressionSyntax? initializer)
    {
        if (toVar)
        {
            var safe = initializer is null
                ? SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetTypeInfo(typeSyntax).Type, resolvedType)
                : VarInfersSameType(context.SemanticModel, initializer, resolvedType);
            if (!safe)
            {
                return;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeVarStyle, typeSyntax.GetLocation(), VarTarget));
            return;
        }

        if (!Sst2254ExplicitObjectCreationTypeAnalyzer.IsExpressibleTypeName(resolvedType))
        {
            return;
        }

        var typeName = resolvedType.ToMinimalDisplayString(context.SemanticModel, typeSyntax.SpanStart);
        if (!TypeNameBindsTo(context.SemanticModel, typeSyntax.SpanStart, typeName, resolvedType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.NormalizeVarStyle, typeSyntax.GetLocation(), typeName));
    }
}
