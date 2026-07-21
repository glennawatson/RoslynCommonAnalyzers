// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Grouped naming analyzer for method-level conventions. The rules register on the method declaration
/// and share a single symbol bind, which only happens after a cheap syntactic gate so the clean path
/// stays allocation-free.
/// </summary>
/// <remarks>
/// Reports the following diagnostic ids:
/// <list type="bullet">
/// <item><description>SST1317 — a task-returning method name does not end with <c>Async</c>.</description></item>
/// <item><description>SST1318 — an overriding/implementing parameter name differs from the base member.</description></item>
/// <item><description>SST1321 — a method name ends with <c>Async</c> but its return type is not awaitable.</description></item>
/// </list>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MethodNamingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The naming suffix that marks an awaitable method.</summary>
    private const string AsyncSuffix = "Async";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(
        NamingRules.AsyncMethodSuffix,
        NamingRules.ParameterNameMatchesBase,
        NamingRules.AsyncSuffixWithoutAwaitableReturn);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    /// <summary>Returns the simple name of a return type when it is syntactically <c>Task</c> or <c>ValueTask</c>.</summary>
    /// <param name="returnType">The method's declared return type.</param>
    /// <returns><see langword="true"/> when the type name is a task type before binding.</returns>
    internal static bool LooksTaskLike(TypeSyntax returnType)
    {
        var name = returnType switch
        {
            QualifiedNameSyntax qualified => SimpleName(qualified.Right),
            SimpleNameSyntax simple => SimpleName(simple),
            _ => null
        };

        return name is "Task" or "ValueTask";
    }

    /// <summary>Resolves the base method whose parameter names an override or implementation should match.</summary>
    /// <param name="method">The method symbol.</param>
    /// <returns>The overridden or implemented method, or <see langword="null"/> when there is none.</returns>
    internal static IMethodSymbol? ResolveBaseMethod(IMethodSymbol method)
    {
        if (method.OverriddenMethod is { } overridden)
        {
            return overridden;
        }

        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            return method.ExplicitInterfaceImplementations[0];
        }

        return FindImplementedInterfaceMethod(method);
    }

    /// <summary>Analyzes a method declaration for the method-naming rules behind a syntactic gate.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        var wantsAsyncCheck = NeedsAsyncSuffixCheck(method);
        var wantsMismatchCheck = NeedsAsyncSuffixMismatchCheck(method);
        var wantsParameterCheck = NeedsParameterNameCheck(method);
        if (!wantsAsyncCheck && !wantsMismatchCheck && !wantsParameterCheck)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(method, context.CancellationToken) is not { } symbol)
        {
            return;
        }

        if (wantsAsyncCheck)
        {
            AnalyzeAsyncSuffix(context, method, symbol);
        }

        if (wantsMismatchCheck)
        {
            AnalyzeAsyncSuffixMismatch(context, method, symbol);
        }

        if (!wantsParameterCheck)
        {
            return;
        }

        AnalyzeParameterNames(context, method, symbol);
    }

    /// <summary>Returns whether the async-suffix rule could fire, based on name and return type alone.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when a bind is warranted to confirm the async-suffix rule.</returns>
    private static bool NeedsAsyncSuffixCheck(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.ValueText;
        if (name.EndsWith(AsyncSuffix, StringComparison.Ordinal) || string.Equals(name, "Main", StringComparison.Ordinal))
        {
            return false;
        }

        return ModifierListHelper.Contains(method.Modifiers, SyntaxKind.AsyncKeyword) || LooksTaskLike(method.ReturnType);
    }

    /// <summary>Returns whether the async-suffix-mismatch rule could fire, based on name and return type alone.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when a bind is warranted to confirm the suffix is unwarranted.</returns>
    private static bool NeedsAsyncSuffixMismatchCheck(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.ValueText;

        // The suffix is unwarranted only on a non-async method whose name is longer than the bare suffix.
        // An 'async' method is asynchronous regardless of return type; a syntactically task-like return
        // (Task/ValueTask/Task<T>/ValueTask<T>) is the common, correctly named case and needs no bind.
        return name.Length > AsyncSuffix.Length
            && name.EndsWith(AsyncSuffix, StringComparison.Ordinal)
            && !ModifierListHelper.Contains(method.Modifiers, SyntaxKind.AsyncKeyword)
            && !LooksTaskLike(method.ReturnType);
    }

    /// <summary>Returns whether the parameter-name rule could fire, based on syntax alone.</summary>
    /// <param name="method">The method declaration.</param>
    /// <returns><see langword="true"/> when the method overrides or could implement another.</returns>
    private static bool NeedsParameterNameCheck(MethodDeclarationSyntax method)
    {
        if (method.ParameterList.Parameters.Count == 0)
        {
            return false;
        }

        return ModifierListHelper.Contains(method.Modifiers, SyntaxKind.OverrideKeyword)
            || method.ExplicitInterfaceSpecifier is not null
            || method.Parent is TypeDeclarationSyntax { BaseList: not null };
    }

    /// <summary>Reports SST1317 when a task-returning method that is not an override/implementation lacks the suffix.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The method declaration.</param>
    /// <param name="symbol">The bound method symbol.</param>
    private static void AnalyzeAsyncSuffix(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (symbol.IsOverride
            || symbol.ExplicitInterfaceImplementations.Length > 0
            || !ReturnsTaskType(symbol.ReturnType)
            || FindImplementedInterfaceMethod(symbol) is not null)
        {
            return;
        }

        var name = method.Identifier.ValueText;
        NamingDiagnostic.Report(context, NamingRules.AsyncMethodSuffix, method.Identifier, name, name + AsyncSuffix);
    }

    /// <summary>Reports SST1321 when a non-override, non-async method named <c>…Async</c> does not return an awaitable.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The method declaration.</param>
    /// <param name="symbol">The bound method symbol.</param>
    private static void AnalyzeAsyncSuffixMismatch(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (symbol.IsOverride
            || symbol.ExplicitInterfaceImplementations.Length > 0
            || symbol.ReturnType.TypeKind == TypeKind.Error
            || IsAwaitableReturn(symbol.ReturnType)
            || FindImplementedInterfaceMethod(symbol) is not null)
        {
            return;
        }

        var name = method.Identifier.ValueText;
        var suggested = name.Substring(0, name.Length - AsyncSuffix.Length);
        NamingDiagnostic.Report(context, NamingRules.AsyncSuffixWithoutAwaitableReturn, method.Identifier, name, suggested);
    }

    /// <summary>Returns whether a return type can be awaited.</summary>
    /// <param name="type">The return type symbol.</param>
    /// <returns><see langword="true"/> for a task type, <c>IAsyncEnumerable&lt;T&gt;</c>, or any type exposing a <c>GetAwaiter</c>.</returns>
    /// <remarks>
    /// The awaitable pattern is structural, so a custom awaiter is honoured too: a type that offers an
    /// accessible <c>GetAwaiter</c> is treated as awaitable. Staying silent when awaitability cannot be
    /// established avoids flagging a method whose name is, in fact, accurate.
    /// </remarks>
    private static bool IsAwaitableReturn(ITypeSymbol type)
    {
        if (ReturnsTaskType(type) || IsAsyncEnumerable(type))
        {
            return true;
        }

        var members = type.GetMembers("GetAwaiter");
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { Parameters.Length: 0 })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is <c>IAsyncEnumerable&lt;T&gt;</c> from <c>System.Collections.Generic</c>.</summary>
    /// <param name="type">The return type symbol.</param>
    /// <returns><see langword="true"/> for an async-stream type.</returns>
    private static bool IsAsyncEnumerable(ITypeSymbol type)
    {
        if (type.OriginalDefinition.Name != "IAsyncEnumerable")
        {
            return false;
        }

        var containingNamespace = type.ContainingNamespace;
        return containingNamespace is { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace.Name: "System" } };
    }

    /// <summary>Reports SST1318 for each parameter whose name differs from the matched base member's parameter.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="method">The method declaration.</param>
    /// <param name="symbol">The bound method symbol.</param>
    private static void AnalyzeParameterNames(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method, IMethodSymbol symbol)
    {
        if (ResolveBaseMethod(symbol) is not { } baseMethod)
        {
            return;
        }

        var parameters = method.ParameterList.Parameters;
        if (baseMethod.Parameters.Length != parameters.Count)
        {
            return;
        }

        for (var i = 0; i < parameters.Count; i++)
        {
            var declared = parameters[i].Identifier.ValueText;
            var expected = baseMethod.Parameters[i].Name;
            if (expected.Length != 0 && !string.Equals(declared, expected, StringComparison.Ordinal))
            {
                NamingDiagnostic.Report(context, NamingRules.ParameterNameMatchesBase, parameters[i].Identifier, declared, expected);
            }
        }
    }

    /// <summary>Returns the interface method this method implicitly implements, or <see langword="null"/>.</summary>
    /// <param name="method">The method symbol.</param>
    /// <returns>The implemented interface method, or <see langword="null"/>.</returns>
    private static IMethodSymbol? FindImplementedInterfaceMethod(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return null;
        }

        var interfaces = containingType.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            var members = interfaces[i].GetMembers(method.Name);
            for (var j = 0; j < members.Length; j++)
            {
                if (members[j] is IMethodSymbol candidate
                    && SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(candidate), method))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>Returns the identifier text of a simple name, or <see langword="null"/> for other forms.</summary>
    /// <param name="name">The simple name syntax.</param>
    /// <returns>The identifier text.</returns>
    private static string? SimpleName(SimpleNameSyntax name) => name switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null
    };

    /// <summary>Returns whether a type is <c>Task</c>/<c>ValueTask</c> from <c>System.Threading.Tasks</c>.</summary>
    /// <param name="type">The return type symbol.</param>
    /// <returns><see langword="true"/> for a task type (excludes <c>void</c>, so async void is skipped).</returns>
    private static bool ReturnsTaskType(ITypeSymbol type)
    {
        if (type.OriginalDefinition.Name is not ("Task" or "ValueTask"))
        {
            return false;
        }

        var taskNamespace = type.ContainingNamespace;
        return taskNamespace is { Name: "Tasks", ContainingNamespace: { Name: "Threading", ContainingNamespace.Name: "System" } };
    }
}
