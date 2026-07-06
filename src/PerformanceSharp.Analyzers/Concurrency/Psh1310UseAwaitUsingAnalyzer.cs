// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags synchronous <c>using</c> statements and using declarations over resources that
/// implement <c>System.IAsyncDisposable</c> when the nearest enclosing method, local function,
/// or lambda is <c>async</c> (PSH1310). A statement that declares several resources is reported
/// only when every declarator's initializer is asynchronously disposable. The whole rule is
/// gated on <c>System.IAsyncDisposable</c> existing in the compilation and on C# 8, where
/// <c>await using</c> became available, so it costs nothing elsewhere.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1310UseAwaitUsingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the async disposable interface the rule is gated on.</summary>
    private const string AsyncDisposableMetadataName = "System.IAsyncDisposable";

    /// <summary>The numeric C# 8 language-version value, where <c>await using</c> became available.</summary>
    private const int CSharp8 = 800;

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.UseAwaitUsing);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            var asyncDisposableType = start.Compilation.GetTypeByMetadataName(AsyncDisposableMetadataName);
            if (asyncDisposableType is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeUsingStatement(nodeContext, asyncDisposableType), SyntaxKind.UsingStatement);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeUsingDeclaration(nodeContext, asyncDisposableType), SyntaxKind.LocalDeclarationStatement);
        });
    }

    /// <summary>Reports PSH1310 for a synchronous using statement over async-disposable resources in an async function.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    private static void AnalyzeUsingStatement(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncDisposableType)
    {
        var usingStatement = (UsingStatementSyntax)context.Node;
        if (!usingStatement.AwaitKeyword.IsKind(SyntaxKind.None)
            || !IsLanguageVersionAtLeast(usingStatement, CSharp8)
            || !Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(usingStatement)
            || !UsingStatementResourcesAreAsyncDisposable(usingStatement, context, asyncDisposableType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UseAwaitUsing,
            usingStatement.SyntaxTree,
            usingStatement.UsingKeyword.Span));
    }

    /// <summary>Reports PSH1310 for a synchronous using declaration over async-disposable resources in an async function.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    private static void AnalyzeUsingDeclaration(SyntaxNodeAnalysisContext context, INamedTypeSymbol asyncDisposableType)
    {
        var declarationStatement = (LocalDeclarationStatementSyntax)context.Node;
        if (!declarationStatement.UsingKeyword.IsKind(SyntaxKind.UsingKeyword)
            || !declarationStatement.AwaitKeyword.IsKind(SyntaxKind.None)
            || !IsLanguageVersionAtLeast(declarationStatement, CSharp8)
            || !Psh1303NoThreadSleepInAsyncAnalyzer.IsInAsyncFunction(declarationStatement)
            || !AllDeclaratorsAreAsyncDisposable(declarationStatement.Declaration, context, asyncDisposableType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.UseAwaitUsing,
            declarationStatement.SyntaxTree,
            declarationStatement.UsingKeyword.Span));
    }

    /// <summary>Returns whether a using statement's resources are all asynchronously disposable.</summary>
    /// <param name="usingStatement">The using statement to inspect.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    /// <returns><see langword="true"/> when every declared or used resource implements the interface.</returns>
    private static bool UsingStatementResourcesAreAsyncDisposable(
        UsingStatementSyntax usingStatement,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol asyncDisposableType)
    {
        if (usingStatement.Declaration is { } declaration)
        {
            return AllDeclaratorsAreAsyncDisposable(declaration, context, asyncDisposableType);
        }

        return usingStatement.Expression is { } expression
            && ExpressionIsAsyncDisposable(expression, context, asyncDisposableType);
    }

    /// <summary>Returns whether every declarator's initializer produces an asynchronously disposable value.</summary>
    /// <param name="declaration">The variable declaration to inspect.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    /// <returns><see langword="true"/> when every declarator qualifies; a declarator without an initializer disqualifies.</returns>
    private static bool AllDeclaratorsAreAsyncDisposable(
        VariableDeclarationSyntax declaration,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol asyncDisposableType)
    {
        var variables = declaration.Variables;
        if (variables.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < variables.Count; i++)
        {
            if (variables[i].Initializer is not { } initializer
                || !ExpressionIsAsyncDisposable(initializer.Value, context, asyncDisposableType))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns whether an expression's type implements the async disposable interface.</summary>
    /// <param name="expression">The resource expression to inspect.</param>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    /// <returns><see langword="true"/> when the expression type is or implements the interface.</returns>
    private static bool ExpressionIsAsyncDisposable(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol asyncDisposableType)
        => context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is { } type
            && ImplementsAsyncDisposable(type, asyncDisposableType);

    /// <summary>Returns whether a type is or implements the async disposable interface.</summary>
    /// <param name="type">The resource type to inspect.</param>
    /// <param name="asyncDisposableType">The async disposable interface.</param>
    /// <returns><see langword="true"/> when the type qualifies.</returns>
    private static bool ImplementsAsyncDisposable(ITypeSymbol type, INamedTypeSymbol asyncDisposableType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, asyncDisposableType))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], asyncDisposableType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether the syntax tree uses at least the supplied language version.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="version">The numeric language version.</param>
    /// <returns><see langword="true"/> when the feature is available.</returns>
    private static bool IsLanguageVersionAtLeast(SyntaxNode node, int version)
        => node.SyntaxTree.Options is CSharpParseOptions options && (int)options.LanguageVersion >= version;
}
