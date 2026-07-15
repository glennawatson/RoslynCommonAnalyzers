// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports an <c>async void</c> method, lambda, or local function that is not a genuine event handler
/// (SST1905). An async void member hands control back at the first <c>await</c> with no task to wait
/// on, and any exception its body throws is raised on the resuming thread with no one to catch it —
/// which on the thread pool ends the process.
/// </summary>
/// <remarks>
/// <para>
/// The exemption is decided by shape, not by declaration kind. A member whose delegate is the standard
/// <c>(object sender, TEventArgs e)</c> event-handler shape has no return value to give a <c>Task</c>
/// back through and is left alone; a method that overrides or implements a <c>void</c> member cannot
/// change that signature and is left alone. Every other void-returning delegate — <c>Action</c>,
/// <c>Action&lt;T&gt;</c>, a custom void delegate — is the fire-and-forget lambda that crashes
/// processes, and is reported.
/// </para>
/// <para>
/// The clean path is a token test: a member declaration must carry the <c>async</c> modifier and a
/// <c>void</c> return, which rejects every non-async member for free. The semantic model is only
/// touched for a lambda's converted delegate type and the event-handler exemption, and only after the
/// tokens match.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst1905AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(ConcurrencyRules.DoNotUseAsyncVoid);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;
            var eventArgs = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName("System.EventArgs"));

            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMethod(nodeContext, eventArgs), SyntaxKind.MethodDeclaration);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeLocalFunction(nodeContext, eventArgs), SyntaxKind.LocalFunctionStatement);
            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLambda(nodeContext, eventArgs),
                SyntaxKind.SimpleLambdaExpression,
                SyntaxKind.ParenthesizedLambdaExpression,
                SyntaxKind.AnonymousMethodExpression);
        });
    }

    /// <summary>Reports an <c>async void</c> method that is not an event handler or an inherited signature.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="eventArgs">The lazily resolved <c>System.EventArgs</c> type.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol?> eventArgs)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword) || !IsVoid(method.ReturnType))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } symbol
            || IsEventHandlerShape(symbol, eventArgs.Value)
            || IsInheritedSignature(symbol))
        {
            return;
        }

        Report(context, GetAsyncKeyword(method.Modifiers), "method");
    }

    /// <summary>Reports an <c>async void</c> local function that is not an event handler.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="eventArgs">The lazily resolved <c>System.EventArgs</c> type.</param>
    private static void AnalyzeLocalFunction(SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol?> eventArgs)
    {
        var localFunction = (LocalFunctionStatementSyntax)context.Node;
        if (!localFunction.Modifiers.Any(SyntaxKind.AsyncKeyword) || !IsVoid(localFunction.ReturnType))
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(localFunction, context.CancellationToken) is IMethodSymbol symbol
            && IsEventHandlerShape(symbol, eventArgs.Value))
        {
            return;
        }

        Report(context, GetAsyncKeyword(localFunction.Modifiers), "local function");
    }

    /// <summary>Reports an <c>async void</c> lambda or anonymous method whose converted delegate is not an event handler.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="eventArgs">The lazily resolved <c>System.EventArgs</c> type.</param>
    private static void AnalyzeLambda(SyntaxNodeAnalysisContext context, Lazy<INamedTypeSymbol?> eventArgs)
    {
        var function = (AnonymousFunctionExpressionSyntax)context.Node;
        if (function.AsyncKeyword.IsKind(SyntaxKind.None))
        {
            return;
        }

        // The converted delegate decides whether this is async void: an Action-shaped target returns
        // void, a Func<Task>-shaped target returns a Task and is correct.
        if (context.SemanticModel.GetSymbolInfo(function, context.CancellationToken).Symbol is not IMethodSymbol symbol
            || !symbol.ReturnsVoid
            || IsEventHandlerShape(symbol, eventArgs.Value))
        {
            return;
        }

        Report(context, function.AsyncKeyword, "lambda");
    }

    /// <summary>Returns whether a return type is spelled as <c>void</c>.</summary>
    /// <param name="returnType">The return type syntax.</param>
    /// <returns><see langword="true"/> when the return type is the <c>void</c> keyword.</returns>
    private static bool IsVoid(TypeSyntax returnType)
        => returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

    /// <summary>Returns whether a symbol has the standard <c>(object, TEventArgs)</c> event-handler shape.</summary>
    /// <param name="method">The candidate method symbol.</param>
    /// <param name="eventArgs">The resolved <c>System.EventArgs</c> type, if any.</param>
    /// <returns><see langword="true"/> when the method is a genuine event handler.</returns>
    private static bool IsEventHandlerShape(IMethodSymbol method, INamedTypeSymbol? eventArgs)
    {
        if (eventArgs is null || method.Parameters.Length != 2)
        {
            return false;
        }

        return method.Parameters[0].Type.SpecialType == SpecialType.System_Object
            && DerivesFrom(method.Parameters[1].Type, eventArgs);
    }

    /// <summary>Returns whether a method overrides or implements a signature its author cannot change.</summary>
    /// <param name="method">The method symbol.</param>
    /// <returns><see langword="true"/> when the void return is dictated by a base or an interface.</returns>
    private static bool IsInheritedSignature(IMethodSymbol method)
        => method.IsOverride || method.ExplicitInterfaceImplementations.Length > 0 || ImplementsInterfaceMember(method);

    /// <summary>Returns whether a method implicitly implements an interface member.</summary>
    /// <param name="method">The method symbol.</param>
    /// <returns><see langword="true"/> when the containing type exposes it as an interface implementation.</returns>
    private static bool ImplementsInterfaceMember(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var members = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < members.Length; j++)
            {
                if (members[j] is IMethodSymbol interfaceMethod
                    && SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(interfaceMethod), method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is, or derives from, a base type.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="target">The base type.</param>
    /// <returns><see langword="true"/> when <paramref name="type"/> is or inherits <paramref name="target"/>.</returns>
    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol target)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the <c>async</c> modifier token from a modifier list.</summary>
    /// <param name="modifiers">The modifier list, already known to contain <c>async</c>.</param>
    /// <returns>The <c>async</c> token.</returns>
    private static SyntaxToken GetAsyncKeyword(SyntaxTokenList modifiers)
    {
        for (var i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].IsKind(SyntaxKind.AsyncKeyword))
            {
                return modifiers[i];
            }
        }

        return modifiers[0];
    }

    /// <summary>Reports SST1905 at the <c>async</c> keyword.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="asyncKeyword">The <c>async</c> token to point at.</param>
    /// <param name="kind">The member kind, for the message.</param>
    private static void Report(SyntaxNodeAnalysisContext context, SyntaxToken asyncKeyword, string kind)
        => context.ReportDiagnostic(DiagnosticHelper.Create(ConcurrencyRules.DoNotUseAsyncVoid, asyncKeyword.GetLocation(), kind));
}
