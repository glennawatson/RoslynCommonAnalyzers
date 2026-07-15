// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped maintainability analyzer for empty or redundant code constructs — empty constructors,
/// namespaces, types, methods, and loop/guard bodies — in a single tree walk. The empty-finalizer
/// rule moved to PerformanceSharp.Analyzers as PSH1002.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1138 — a free-standing block declares nothing and only nests its statements.</description></item>
/// <item><description>SST1433 — a type's only constructor is a public, parameterless, empty constructor.</description></item>
/// <item><description>SST1435 — a namespace declaration has no members.</description></item>
/// <item><description>SST1436 — a class, struct, or record has no members (opt-in).</description></item>
/// <item><description>SST1437 — an interface has no members (opt-in).</description></item>
/// <item><description>SST1438 — a method has an empty body (opt-in).</description></item>
/// <item><description>SST1439 — a loop or guard statement has an empty embedded block.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptyCodeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        ReadabilityRules.FreeStandingBlock,
        MaintainabilityRules.NoRedundantConstructor,
        MaintainabilityRules.NoEmptyNamespace,
        MaintainabilityRules.NoEmptyType,
        MaintainabilityRules.NoEmptyInterface,
        MaintainabilityRules.NoEmptyMethod,
        MaintainabilityRules.NoEmptyNestedBlock);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNamespace, SyntaxKind.NamespaceDeclaration, SyntaxKind.FileScopedNamespaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration, SyntaxKind.RecordDeclaration, SyntaxKind.RecordStructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeInterface, SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeEmbeddedBlock, SyntaxKind.Block);
    }

    /// <summary>Returns whether a block body is present and contains no statements.</summary>
    /// <param name="body">The block to test.</param>
    /// <returns><see langword="true"/> for a non-null, statement-free block.</returns>
    internal static bool IsEmptyBlock(BlockSyntax? body) => body is { Statements.Count: 0 };

    /// <summary>Returns whether an empty block carries a comment that documents its intentional emptiness.</summary>
    /// <param name="block">The empty block.</param>
    /// <returns><see langword="true"/> when a comment sits between the braces.</returns>
    internal static bool ContainsComment(BlockSyntax block)
        => HasCommentTrivia(block.OpenBraceToken.TrailingTrivia) || HasCommentTrivia(block.CloseBraceToken.LeadingTrivia);

    /// <summary>Returns whether a trivia list contains a single-line or multi-line comment.</summary>
    /// <param name="trivia">The trivia list to scan.</param>
    /// <returns><see langword="true"/> when a comment is present.</returns>
    private static bool HasCommentTrivia(SyntaxTriviaList trivia)
    {
        for (var i = 0; i < trivia.Count; i++)
        {
            if (trivia[i].IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia[i].IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reports SST1433 for a type whose sole constructor is a public, parameterless, empty default.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        // Keep it safe and syntactic: only a public, parameterless, attribute-free, empty constructor with
        // no chained initializer, and only when it is the type's single constructor, restates the default.
        if (!IsEmptyBlock(constructor.Body)
            || constructor.ParameterList.Parameters.Count != 0
            || constructor.Initializer is not null
            || constructor.AttributeLists.Count > 0
            || !ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.PublicKeyword)
            || ModifierListHelper.Contains(constructor.Modifiers, SyntaxKind.StaticKeyword)
            || constructor.Parent is not TypeDeclarationSyntax type
            || CountConstructors(type) != 1)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoRedundantConstructor, constructor.Identifier.GetLocation()));
    }

    /// <summary>Reports SST1435 for a namespace declaration with no members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeNamespace(SyntaxNodeAnalysisContext context)
    {
        var declaration = (BaseNamespaceDeclarationSyntax)context.Node;
        if (declaration.Members.Count != 0)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyNamespace, declaration.Name.GetLocation()));
    }

    /// <summary>Reports SST1436 for a class, struct, or record with no members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        var declaration = (TypeDeclarationSyntax)context.Node;

        // A primary-constructor parameter list or a base list means the type is not really empty.
        if (declaration.Members.Count != 0
            || declaration.BaseList is not null
            || declaration.ParameterList is not null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyType, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText));
    }

    /// <summary>Reports SST1437 for an interface with no members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeInterface(SyntaxNodeAnalysisContext context)
    {
        var declaration = (InterfaceDeclarationSyntax)context.Node;
        if (declaration.Members.Count != 0 || declaration.BaseList is not null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyInterface, declaration.Identifier.GetLocation(), declaration.Identifier.ValueText));
    }

    /// <summary>Reports SST1438 for a concrete method with an empty body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // 'partial', 'abstract', and 'extern' methods have no body to fill; overrides are a common
        // legitimate empty hook; and a comment inside the body documents an intentional no-op.
        if (!IsEmptyBlock(method.Body)
            || method.AttributeLists.Count > 0
            || ModifierListHelper.Contains(method.Modifiers, SyntaxKind.OverrideKeyword)
            || ModifierListHelper.Contains(method.Modifiers, SyntaxKind.PartialKeyword)
            || ModifierListHelper.Contains(method.Modifiers, SyntaxKind.VirtualKeyword)
            || ContainsComment(method.Body!))
        {
            return;
        }

        // Bind only for an empty, undocumented, non-override method (rare): an empty body that
        // implements an interface member is a legitimate no-op (a null IDisposable, a stub handler).
        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is { } symbol
            && MethodNamingAnalyzer.ResolveBaseMethod(symbol) is not null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyMethod, method.Identifier.GetLocation(), method.Identifier.ValueText));
    }

    /// <summary>Reports SST1138 for a free-standing block, or SST1439 for an empty loop or guard body.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeEmbeddedBlock(SyntaxNodeAnalysisContext context)
    {
        var block = (BlockSyntax)context.Node;

        // A block whose parent is itself a block introduces no scope of its own; when it declares nothing it
        // is dead weight, the residue of a removed 'if'/'using'/'lock'/'fixed'.
        if (block.Parent is BlockSyntax)
        {
            if (DeclaresNothing(block) && !ContainsComment(block))
            {
                context.ReportDiagnostic(Diagnostic.Create(ReadabilityRules.FreeStandingBlock, block.GetLocation()));
            }

            return;
        }

        if (block.Statements.Count != 0 || !IsLoopOrGuardBody(block) || ContainsComment(block))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MaintainabilityRules.NoEmptyNestedBlock, block.GetLocation()));
    }

    /// <summary>Returns whether a block declares nothing that a splice into its parent would leak or lose.</summary>
    /// <param name="block">The block to inspect.</param>
    /// <returns><see langword="true"/> when the block declares no local, local function, or label.</returns>
    private static bool DeclaresNothing(BlockSyntax block)
    {
        var statements = block.Statements;
        for (var i = 0; i < statements.Count; i++)
        {
            if (statements[i] is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the number of instance constructors declared directly in a type.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The instance constructor count.</returns>
    private static int CountConstructors(TypeDeclarationSyntax type)
    {
        var count = 0;
        var members = type.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i] is ConstructorDeclarationSyntax declaration
                && !ModifierListHelper.Contains(declaration.Modifiers, SyntaxKind.StaticKeyword))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Returns whether a block is the embedded body of a loop or guard statement.</summary>
    /// <param name="block">The block to classify.</param>
    /// <returns><see langword="true"/> when the block is a loop or guard body whose emptiness is suspect.</returns>
    private static bool IsLoopOrGuardBody(BlockSyntax block) => block.Parent switch
    {
        ForStatementSyntax => true,
        ForEachStatementSyntax => true,
        WhileStatementSyntax => true,
        DoStatementSyntax => true,
        LockStatementSyntax => true,
        FixedStatementSyntax => true,
        UsingStatementSyntax => true,

        // The 'then' branch only; an empty 'else' is the dedicated SST1180 rule.
        IfStatementSyntax ifStatement => ifStatement.Statement == block,
        _ => false
    };
}
