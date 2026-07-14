// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a type declared in the global namespace — that is, in no namespace at all (SST2312). Such a type is
/// visible from every file of every project that references the assembly, with no way for a consumer to opt out
/// of the name or to avoid a collision with one of their own.
/// </summary>
/// <remarks>
/// This is about a type having no namespace, and is not SST2237, which is about how a namespace that already
/// exists is written. The two never report the same declaration: a file with no namespace has nothing for
/// SST2237 to convert, and a file with one has nothing for this rule to report.
/// <para>
/// The <c>Program</c> that top-level statements generate is never reported. It has no declaration of its own to
/// move, and the <c>partial class Program</c> a project writes alongside it — the shape integration tests use —
/// cannot be moved either: in a namespace it would quietly stop being the same type as the entry point rather
/// than fail to compile. Both are recognised the same way, by the compiler pointing the symbol's declaration
/// back at a whole compilation unit.
/// </para>
/// <para>
/// A delegate is not reported, which keeps the rule's firing set identical to the analyzer's the rule that it
/// replaces.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2312TypeInGlobalNamespaceAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.TypeInGlobalNamespace);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.InterfaceDeclaration,
            SyntaxKind.EnumDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    /// <summary>Reports one type that lives outside any namespace.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // A type sitting directly under the compilation unit is in the global namespace; anything nested in a
        // namespace or in another type is not, and is rejected here without ever touching the semantic model.
        var declaration = (BaseTypeDeclarationSyntax)context.Node;
        if (declaration.Parent is not CompilationUnitSyntax)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } type
            || IsTopLevelStatementsProgram(type, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            DesignRules.TypeInGlobalNamespace,
            declaration.Identifier.GetLocation(),
            type.Name));
    }

    /// <summary>Returns whether a type is the one top-level statements generate.</summary>
    /// <param name="type">The global-namespace type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when part of the type is declared by a compilation unit's statements.</returns>
    /// <remarks>
    /// The synthesized entry-point type declares itself at the compilation unit that holds the statements, so a
    /// declaring reference to a <see cref="CompilationUnitSyntax"/> is what identifies it — including when the
    /// project also writes an explicit <c>partial class Program</c>, whose symbol merges with it and inherits
    /// that reference.
    /// </remarks>
    private static bool IsTopLevelStatementsProgram(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var declarations = type.DeclaringSyntaxReferences;
        for (var i = 0; i < declarations.Length; i++)
        {
            if (declarations[i].GetSyntax(cancellationToken) is CompilationUnitSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
