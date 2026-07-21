// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a body whose real work is wrapped in a single trailing <c>if</c> that could be inverted into an
/// early-exit guard (SST2273). A method, constructor, accessor, local function, or <c>for</c>/<c>foreach</c>/
/// <c>while</c> loop whose last statement is an <c>else</c>-less <c>if (cond) { work }</c> reads with one less
/// level of nesting as <c>if (!cond) return;</c> (or <c>continue;</c> inside a loop) followed by the unwrapped
/// work. Disabled by default; enable it in <c>.editorconfig</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2273PreferGuardClauseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ModernSyntaxRules.PreferGuardClause);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    /// <summary>
    /// Matches the invertible trailing-guard shape and yields the jump that heads the guard: a <c>return;</c>
    /// for a void or async-Task-returning member, a constructor or a value-free accessor, and a
    /// <c>continue;</c> for a loop body. Reused by the code fix so both re-derive the same structure.
    /// </summary>
    /// <param name="ifStatement">The candidate trailing <c>if</c>.</param>
    /// <param name="jumpKind">The jump statement kind for the guard: <see cref="SyntaxKind.ReturnStatement"/> or <see cref="SyntaxKind.ContinueStatement"/>.</param>
    /// <returns><see langword="true"/> when the <c>if</c> is an invertible trailing guard.</returns>
    internal static bool TryGetGuard(IfStatementSyntax ifStatement, out SyntaxKind jumpKind)
    {
        jumpKind = SyntaxKind.None;

        // An 'else' branch has its own control flow, so inverting the condition would not preserve it.
        if (ifStatement.Else is not null || ifStatement.Parent is not BlockSyntax block)
        {
            return false;
        }

        // Only a trailing 'if' flattens cleanly: nothing may run after it in the block, or the guard would
        // reorder that work ahead of the early exit. The 'if' is always one of the block's statements here,
        // so the list is non-empty.
        var statements = block.Statements;
        if (statements[statements.Count - 1] != ifStatement)
        {
            return false;
        }

        return TryGetOwnerJump(block, out jumpKind);
    }

    /// <summary>Counts the statements the trailing <c>if</c> wraps.</summary>
    /// <param name="ifStatement">The trailing <c>if</c>.</param>
    /// <returns>The block statement count, or 1 for a single embedded statement.</returns>
    internal static int WrappedStatementCount(IfStatementSyntax ifStatement)
        => ifStatement.Statement is BlockSyntax block ? block.Statements.Count : 1;

    /// <summary>Registers the per-compilation options cache, then analyzes every <c>if</c> statement.</summary>
    /// <param name="context">The compilation start context.</param>
    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var optionsByTree = new ConcurrentDictionary<SyntaxTree, TrailingGuardOptions>();
        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, optionsByTree), SyntaxKind.IfStatement);
    }

    /// <summary>Reports a trailing <c>if</c> that could become a guard once it wraps enough work.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    private static void Analyze(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, TrailingGuardOptions> optionsByTree)
    {
        var ifStatement = (IfStatementSyntax)context.Node;

        // The whole match is syntactic: a clean 'if' rejects before any option is read or any symbol bound.
        if (!TryGetGuard(ifStatement, out _))
        {
            return;
        }

        var minimum = GetOptions(context, optionsByTree).MinWrappedStatements;
        if (WrappedStatementCount(ifStatement) < minimum)
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(ModernSyntaxRules.PreferGuardClause, ifStatement.IfKeyword.GetLocation()));
    }

    /// <summary>Reads the settings for the tree, parsing each tree's options at most once.</summary>
    /// <param name="context">The syntax node context.</param>
    /// <param name="optionsByTree">The per-tree settings cache.</param>
    /// <returns>The resolved settings.</returns>
    private static TrailingGuardOptions GetOptions(SyntaxNodeAnalysisContext context, ConcurrentDictionary<SyntaxTree, TrailingGuardOptions> optionsByTree)
    {
        var tree = context.Node.SyntaxTree;
        if (optionsByTree.TryGetValue(tree, out var options))
        {
            return options;
        }

        options = TrailingGuardOptions.Read(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        optionsByTree.TryAdd(tree, options);
        return options;
    }

    /// <summary>Determines the guard jump for the block's owner, or rejects an owner that has no early exit.</summary>
    /// <param name="block">The block that directly contains the trailing <c>if</c>.</param>
    /// <param name="jumpKind">The jump kind: <see cref="SyntaxKind.ReturnStatement"/> or <see cref="SyntaxKind.ContinueStatement"/>.</param>
    /// <returns><see langword="true"/> when the block is a body a guard can exit from.</returns>
    /// <remarks>
    /// A value-returning method, an operator, and a <c>get</c> accessor must produce a value on every path, so
    /// no bare <c>return;</c> guard fits them and they are left alone. A block nested inside an <c>if</c>,
    /// <c>try</c>, or <c>using</c> is not an exit boundary either — a jump there would leave the whole member,
    /// not just that block — so only a direct member or loop body qualifies.
    /// </remarks>
    private static bool TryGetOwnerJump(BlockSyntax block, out SyntaxKind jumpKind)
    {
        switch (block.Parent)
        {
            case WhileStatementSyntax loop when loop.Statement == block:
            case ForStatementSyntax forLoop when forLoop.Statement == block:
            case CommonForEachStatementSyntax forEach when forEach.Statement == block:
            {
                jumpKind = SyntaxKind.ContinueStatement;
                return true;
            }

            case ConstructorDeclarationSyntax constructor when constructor.Body == block:
            {
                jumpKind = SyntaxKind.ReturnStatement;
                return true;
            }

            case MethodDeclarationSyntax method when method.Body == block && ReturnsVoidOrAsyncTask(method.ReturnType, method.Modifiers):
            {
                jumpKind = SyntaxKind.ReturnStatement;
                return true;
            }

            case LocalFunctionStatementSyntax local when local.Body == block && ReturnsVoidOrAsyncTask(local.ReturnType, local.Modifiers):
            {
                jumpKind = SyntaxKind.ReturnStatement;
                return true;
            }

            case AccessorDeclarationSyntax accessor when accessor.Body == block && IsValueFreeAccessor(accessor):
            {
                jumpKind = SyntaxKind.ReturnStatement;
                return true;
            }

            default:
            {
                jumpKind = SyntaxKind.None;
                return false;
            }
        }
    }

    /// <summary>Returns whether a member's return type lets a bare <c>return;</c> stand as its guard exit.</summary>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="modifiers">The member's modifiers.</param>
    /// <returns><see langword="true"/> for a <c>void</c> member or an async non-generic <c>Task</c>/<c>ValueTask</c>.</returns>
    private static bool ReturnsVoidOrAsyncTask(TypeSyntax returnType, SyntaxTokenList modifiers)
    {
        if (returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
        {
            return true;
        }

        // 'return;' is only valid in a Task-returning method when it is async; a non-async Task method would
        // need 'return Task.CompletedTask;'. A generic Task<T> is a GenericNameSyntax, so it never matches here.
        return ModifierListHelper.Contains(modifiers, SyntaxKind.AsyncKeyword) && IsBareTaskName(returnType);
    }

    /// <summary>Returns whether a type names a non-generic <c>Task</c> or <c>ValueTask</c>.</summary>
    /// <param name="type">The return type.</param>
    /// <returns><see langword="true"/> for the bare awaitable task types.</returns>
    private static bool IsBareTaskName(TypeSyntax type)
    {
        var name = type switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.ValueText,
            _ => null,
        };
        return name is "Task" or "ValueTask";
    }

    /// <summary>Returns whether an accessor returns no value, so a bare <c>return;</c> ends it.</summary>
    /// <param name="accessor">The accessor declaration.</param>
    /// <returns><see langword="true"/> for a <c>set</c>, <c>init</c>, <c>add</c>, or <c>remove</c> accessor.</returns>
    private static bool IsValueFreeAccessor(AccessorDeclarationSyntax accessor) => accessor.Kind() is
        SyntaxKind.SetAccessorDeclaration
        or SyntaxKind.InitAccessorDeclaration
        or SyntaxKind.AddAccessorDeclaration
        or SyntaxKind.RemoveAccessorDeclaration;
}
