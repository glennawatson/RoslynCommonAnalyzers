// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>[ConstructorArgument("name")]</c> whose name matches no parameter of any constructor of the
/// property's declaring type (SST2487). The name maps a markup-extension property back to a constructor
/// argument when the markup is written out; a name that binds to nothing breaks that round-trip silently,
/// because the compiler never resolves the string.
/// </summary>
/// <remarks>
/// <para>
/// The attribute type is resolved once at compilation start, so a project that does not reference the markup
/// namespace registers nothing and pays nothing. The clean path is a comparison of the attribute's written
/// name against a constant and a check that its single argument is a string literal; only an attribute that
/// passes both is bound, which confirms it is the markup attribute rather than a same-named one and reads the
/// declaring type's constructors.
/// </para>
/// <para>
/// The comparison against parameter names is exact, matching how the name is resolved at run time. A type with
/// no matching parameter on any constructor — including a type whose only constructor is the implicit
/// parameterless one — is reported, because there is no argument for the property to map to.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2487ConstructorArgumentMismatchAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The attribute's simple name, with and without the suffix, as it may be written.</summary>
    private const string AttributeSimpleName = "ConstructorArgument";

    /// <summary>The attribute's simple name written in full.</summary>
    private const string AttributeQualifiedSimpleName = "ConstructorArgumentAttribute";

    /// <summary>The attribute's full metadata name, used to gate and to bind.</summary>
    private const string AttributeMetadataName = "System.Windows.Markup.ConstructorArgumentAttribute";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArrays.Of(CorrectnessRules.ConstructorArgumentMismatch);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>Registers the rule only when the markup attribute is present in the compilation.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName(AttributeMetadataName) is not { } attributeType)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, attributeType), SyntaxKind.Attribute);
    }

    /// <summary>Analyzes one attribute for a constructor-argument name that binds to nothing.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attributeType">The resolved markup attribute type.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeType)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!IsConstructorArgumentName(attribute.Name)
            || GetStringLiteralArgument(attribute) is not { } literal
            || attribute.Parent!.Parent is not PropertyDeclarationSyntax property)
        {
            return;
        }

        if (!IsMarkupAttribute(context, attribute, attributeType))
        {
            return;
        }

        var name = literal.Token.ValueText;
        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property, context.CancellationToken)!;
        if (DeclaresConstructorParameter(propertySymbol.ContainingType, name))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            CorrectnessRules.ConstructorArgumentMismatch,
            literal.GetLocation(),
            name,
            propertySymbol.ContainingType.Name));
    }

    /// <summary>Returns whether an attribute is written as the constructor-argument one.</summary>
    /// <param name="name">The attribute's name, simple, qualified, or alias-qualified.</param>
    /// <returns><see langword="true"/> when the rightmost identifier matches, with or without the suffix.</returns>
    /// <remarks>
    /// The rightmost token of any of those name shapes is the type's own identifier, so one comparison against
    /// it is the whole syntactic filter — no attribute is bound until this passes.
    /// </remarks>
    private static bool IsConstructorArgumentName(NameSyntax name)
        => name.GetLastToken().ValueText is AttributeSimpleName or AttributeQualifiedSimpleName;

    /// <summary>Gets the attribute's first argument when it is a string literal.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The literal, or <see langword="null"/> when the first argument is not a string literal.</returns>
    /// <remarks>
    /// The markup attribute takes its name as a required positional string, so the first argument is the one
    /// that must resolve. A same-named attribute with a different shape is caught later, when the attribute is
    /// bound.
    /// </remarks>
    private static LiteralExpressionSyntax? GetStringLiteralArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } list)
        {
            return null;
        }

        return list.Arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            ? literal
            : null;
    }

    /// <summary>Binds an attribute to confirm it is the markup constructor-argument attribute.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="attributeType">The resolved markup attribute type.</param>
    /// <returns><see langword="true"/> when the attribute is the markup one and not a same-named other.</returns>
    private static bool IsMarkupAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attribute, INamedTypeSymbol attributeType)
        => SymbolEqualityComparer.Default.Equals(
            context.SemanticModel.GetTypeInfo(attribute, context.CancellationToken).Type,
            attributeType);

    /// <summary>Returns whether any constructor of a type declares a parameter with the given name.</summary>
    /// <param name="type">The property's declaring type.</param>
    /// <param name="name">The name the attribute carries.</param>
    /// <returns><see langword="true"/> when the name maps to a constructor parameter.</returns>
    private static bool DeclaresConstructorParameter(INamedTypeSymbol type, string name)
    {
        var constructors = type.InstanceConstructors;
        for (var i = 0; i < constructors.Length; i++)
        {
            var parameters = constructors[i].Parameters;
            for (var j = 0; j < parameters.Length; j++)
            {
                if (string.Equals(parameters[j].Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
