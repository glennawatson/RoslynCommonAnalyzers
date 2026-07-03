// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Flags explicit <c>Nullable&lt;T&gt;</c> spellings that can use the <c>T?</c> shorthand
/// (SST2234). The two forms are the same type; the shorthand is shorter and matches how nullable
/// annotations read everywhere else. Spellings that cannot be rewritten are skipped: unbound
/// generics (<c>typeof(Nullable&lt;&gt;)</c>), <c>nameof</c> operands, member-access qualifiers,
/// and using-directive targets. The check is syntax-gated on the identifier spelling
/// <c>Nullable</c> with exactly one type argument before a single semantic bind confirms
/// <c>System.Nullable&lt;T&gt;</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2234NullableShorthandAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The type name used by the syntax gate.</summary>
    private const string NullableTypeName = "Nullable";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.UseNullableShorthand);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
    }

    /// <summary>Reports a rewritable <c>Nullable&lt;T&gt;</c> spelling.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var name = (GenericNameSyntax)context.Node;
        if (name.Identifier.ValueText != NullableTypeName || name.TypeArgumentList.Arguments.Count != 1)
        {
            return;
        }

        if (name.TypeArgumentList.Arguments[0].IsKind(SyntaxKind.OmittedTypeArgument))
        {
            return;
        }

        if (!CanUseShorthand(name))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is not INamedTypeSymbol
            {
                ConstructedFrom.SpecialType: SpecialType.System_Nullable_T,
            })
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ModernSyntaxRules.UseNullableShorthand,
            name.SyntaxTree,
            name.Span,
            name.TypeArgumentList.Arguments[0].ToString()));
    }

    /// <summary>Returns whether the spelling's position permits the <c>T?</c> shorthand.</summary>
    /// <param name="name">The generic name.</param>
    /// <returns><see langword="true"/> when a shorthand rewrite compiles at this position.</returns>
    private static bool CanUseShorthand(GenericNameSyntax name)
    {
        // Resolve the full name this generic participates in (System.Nullable<int> reports the
        // qualified spelling), then check the surrounding construct.
        SyntaxNode spelling = name;
        while (spelling.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax)
        {
            spelling = spelling.Parent;
        }

        return spelling.Parent switch
        {
            // int?.Member is not valid syntax, so a qualifier position cannot be rewritten.
            MemberAccessExpressionSyntax memberAccess when memberAccess.Expression == spelling => false,

            // nameof(Nullable<int>) evaluates the name itself; the shorthand has no name.
            ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } } => false,

            // Alias targets before C# 12 cannot be arbitrary types; skip usings entirely.
            UsingDirectiveSyntax => false,
            _ => true,
        };
    }
}
