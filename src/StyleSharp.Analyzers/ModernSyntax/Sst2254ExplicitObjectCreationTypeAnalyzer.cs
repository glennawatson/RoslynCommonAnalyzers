// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a target-typed object creation — <c>new(...)</c> or <c>new() { ... }</c> — that could instead
/// name the created type at the creation site (SST2254), the inverse of preferring a target-typed <c>new</c>.
/// The rule is opt-in and off by default.
/// </summary>
/// <remarks>
/// <para>
/// The rule is registered on <see cref="SyntaxKind.ImplicitObjectCreationExpression"/> alone, so a source
/// with no target-typed creations never reaches the semantic model. The created type is resolved once from the
/// creation's type info — its <see cref="TypeInfo.Type"/>, or its <see cref="TypeInfo.ConvertedType"/> when the
/// constructed type is only known through the conversion. Anything that cannot be written as a plain type name
/// in source is left alone: a missing or error type, an anonymous type, a tuple, a pointer or function pointer,
/// and <c>dynamic</c>.
/// </para>
/// <para>
/// Safety is a speculative bind, never a guess. The minimally-qualified name of the resolved type is parsed,
/// spliced between the <c>new</c> keyword and the original argument list and initializer, and that explicit
/// <c>new Type(...)</c> is bound speculatively at the creation's position. The diagnostic is raised only when
/// the rebuilt form resolves to the identical type, so a name that would collide, fail to bind, or construct a
/// different type is left alone rather than reported with a fix that would not compile.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2254ExplicitObjectCreationTypeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseExplicitObjectCreationType);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ImplicitObjectCreationExpression);
    }

    /// <summary>Returns whether a resolved type can be written as a plain type name in source.</summary>
    /// <param name="type">The resolved created type, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the type has a source-expressible name.</returns>
    /// <remarks>
    /// A missing or error type, an anonymous type, a tuple, a pointer, a function pointer, and <c>dynamic</c>
    /// have no plain type-name spelling that can stand between <c>new</c> and the argument list, so they are
    /// rejected here before any display string is allocated.
    /// </remarks>
    internal static bool IsExpressibleTypeName(ITypeSymbol? type)
        => type is { IsAnonymousType: false } resolved
            && resolved.TypeKind is not (TypeKind.Error or TypeKind.Dynamic or TypeKind.Pointer or TypeKind.FunctionPointer)
            && resolved is not INamedTypeSymbol { IsTupleType: true };

    /// <summary>Returns whether naming the type explicitly binds back to the same constructed type.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="creation">The target-typed creation being inspected.</param>
    /// <param name="type">The resolved created type.</param>
    /// <param name="typeName">The minimally-qualified name to splice in.</param>
    /// <returns><see langword="true"/> when the explicit form resolves to the identical type.</returns>
    internal static bool ExplicitFormBindsToSameType(
        SemanticModel model,
        ImplicitObjectCreationExpressionSyntax creation,
        ITypeSymbol type,
        string typeName)
    {
        var explicitCreation = SyntaxFactory.ObjectCreationExpression(
            creation.NewKeyword,
            SyntaxFactory.ParseTypeName(typeName),
            creation.ArgumentList,
            creation.Initializer);

        var bound = model.GetSpeculativeTypeInfo(creation.SpanStart, explicitCreation, SpeculativeBindingOption.BindAsExpression).Type;
        return bound is not null && SymbolEqualityComparer.Default.Equals(bound, type);
    }

    /// <summary>Reports a target-typed creation whose type can be named explicitly.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var creation = (ImplicitObjectCreationExpressionSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        var type = typeInfo.Type ?? typeInfo.ConvertedType;
        if (!IsExpressibleTypeName(type))
        {
            return;
        }

        var typeName = type!.ToMinimalDisplayString(context.SemanticModel, creation.SpanStart);
        if (!ExplicitFormBindsToSameType(context.SemanticModel, creation, type, typeName))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseExplicitObjectCreationType,
            creation.NewKeyword.GetLocation(),
            typeName));
    }
}
