// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a <c>const</c> field that a caller in another assembly can read (SST2311). The hazard is entirely
/// a cross-assembly one: a <c>const</c> is not read at run time, it is copied into the call site at the moment
/// the calling assembly is compiled, so a later change to the value never reaches a caller that has not been
/// rebuilt.
/// </summary>
/// <remarks>
/// A <c>static readonly</c> field is read from the declaring assembly at run time and does reach everybody —
/// but it is not a drop-in replacement, and the rule's docs say so plainly: the language requires a real
/// <c>const</c> for an attribute argument, a <c>case</c> label, and a default parameter value. A value feeding
/// one of those has to stay a <c>const</c>, and this rule is then the wrong rule for it.
/// <para>
/// A constant nobody outside the assembly can read is never reported, because the hazard needs a caller that
/// is compiled at a different time: everything <c>private</c>, <c>internal</c> or <c>private protected</c> is
/// left alone, as is anything — however public — declared inside a type the outside world cannot see.
/// </para>
/// <para>
/// The <c>const</c> keyword is matched on syntax first, so a field that is not one costs nothing; only a
/// constant is bound.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2311PublicConstantFieldAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.PublicConstantField);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    /// <summary>Reports each constant in one field declaration that another assembly can bake in.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        if (!field.Modifiers.Any(SyntaxKind.ConstKeyword))
        {
            return;
        }

        // One declaration can introduce several constants, and each is copied into its callers separately.
        var variables = field.Declaration.Variables;
        for (var i = 0; i < variables.Count; i++)
        {
            var variable = variables[i];
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is not IFieldSymbol constant
                || !IsReadableFromAnotherAssembly(constant))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                DesignRules.PublicConstantField,
                variable.Identifier.GetLocation(),
                constant.Name));
        }
    }

    /// <summary>Returns whether code in another assembly can read the constant, and so bake its value in.</summary>
    /// <param name="constant">The constant field.</param>
    /// <returns><see langword="true"/> when an outside caller can reach it.</returns>
    private static bool IsReadableFromAnotherAssembly(IFieldSymbol constant)
    {
        if (constant.ContainingType is not { } containingType || !IsVisibleOutsideAssembly(containingType))
        {
            return false;
        }

        return constant.DeclaredAccessibility switch
        {
            Accessibility.Public => true,

            // A protected constant reaches another assembly only through a derived type, which needs a base
            // that can actually be derived from. In a sealed or static type it reaches nobody.
            Accessibility.Protected or Accessibility.ProtectedOrInternal => IsInheritable(containingType),
            _ => false,
        };
    }

    /// <summary>Returns whether a type, and every type containing it, can be seen from another assembly.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the whole containing chain is visible.</returns>
    private static bool IsVisibleOutsideAssembly(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.ProtectedOrInternal:
                {
                    break;
                }

                default:
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Returns whether another assembly can derive from a type.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is neither sealed nor static.</returns>
    private static bool IsInheritable(INamedTypeSymbol type) => !type.IsSealed && !type.IsStatic;
}
