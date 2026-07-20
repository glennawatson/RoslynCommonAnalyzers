// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports a class that names an MVC exception filter interface directly in its base list (PSH1505),
/// suggesting a centralized <c>IExceptionHandler</c> instead.
/// </summary>
/// <remarks>
/// <para>
/// <c>IExceptionFilter</c> and <c>IAsyncExceptionFilter</c> run inside the MVC action-invocation
/// pipeline — after routing has chosen an action and the heavier filter path has been entered — and
/// they fire per matched action. Cross-cutting error handling is cheaper in an <c>IExceptionHandler</c>,
/// which the middleware pipeline invokes once for any unhandled exception.
/// </para>
/// <para>
/// The clean path costs nothing on a non-web compilation: the whole rule is gated on
/// <c>Microsoft.AspNetCore.Diagnostics.IExceptionHandler</c> resolving (the modern replacement must exist
/// for the suggestion to be actionable) and on at least one of the two filter interfaces resolving. Only a
/// class whose own base list syntactically names <c>IExceptionFilter</c> or <c>IAsyncExceptionFilter</c>
/// is bound, and the bound interface is confirmed to be the ASP.NET Core one so a same-named interface in
/// another namespace is never reported.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1505PreferExceptionHandlerAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The metadata name of the modern replacement that gates the rule.</summary>
    private const string ExceptionHandlerMetadataName = "Microsoft.AspNetCore.Diagnostics.IExceptionHandler";

    /// <summary>The metadata name of the synchronous MVC exception filter interface.</summary>
    private const string ExceptionFilterMetadataName = "Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter";

    /// <summary>The metadata name of the asynchronous MVC exception filter interface.</summary>
    private const string AsyncExceptionFilterMetadataName = "Microsoft.AspNetCore.Mvc.Filters.IAsyncExceptionFilter";

    /// <summary>The unqualified name of the synchronous MVC exception filter interface.</summary>
    private const string ExceptionFilterName = "IExceptionFilter";

    /// <summary>The unqualified name of the asynchronous MVC exception filter interface.</summary>
    private const string AsyncExceptionFilterName = "IAsyncExceptionFilter";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(AspNetCoreRules.PreferExceptionHandlerOverMvcFilter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Marker-gate: the modern replacement must exist for the suggestion to be actionable.
            if (start.Compilation.GetTypeByMetadataName(ExceptionHandlerMetadataName) is null)
            {
                return;
            }

            var exceptionFilter = start.Compilation.GetTypeByMetadataName(ExceptionFilterMetadataName);
            var asyncExceptionFilter = start.Compilation.GetTypeByMetadataName(AsyncExceptionFilterMetadataName);
            if (exceptionFilter is null && asyncExceptionFilter is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeClassDeclaration(nodeContext, exceptionFilter, asyncExceptionFilter),
                SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>Reports a class that names an MVC exception filter interface directly in its base list.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="exceptionFilter">The resolved synchronous filter interface, or <see langword="null"/> when absent.</param>
    /// <param name="asyncExceptionFilter">The resolved asynchronous filter interface, or <see langword="null"/> when absent.</param>
    private static void AnalyzeClassDeclaration(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? exceptionFilter,
        INamedTypeSymbol? asyncExceptionFilter)
    {
        var declaration = (ClassDeclarationSyntax)context.Node;
        if (declaration.BaseList is not { } baseList)
        {
            return;
        }

        var baseTypes = baseList.Types;
        for (var i = 0; i < baseTypes.Count; i++)
        {
            // Free syntactic prefilter: only bind a base type whose written name is one of the filter interfaces.
            if (baseTypes[i].Type is not NameSyntax name || !IsFilterInterfaceName(GetSimpleName(name)))
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is not INamedTypeSymbol bound
                || !MatchesFilterInterface(bound, exceptionFilter, asyncExceptionFilter))
            {
                continue;
            }

            context.ReportDiagnostic(DiagnosticHelper.Create(
                AspNetCoreRules.PreferExceptionHandlerOverMvcFilter,
                declaration.SyntaxTree,
                declaration.Identifier.Span,
                declaration.Identifier.ValueText,
                bound.Name));
            return;
        }
    }

    /// <summary>Returns whether an unqualified name matches one of the MVC exception filter interfaces.</summary>
    /// <param name="name">The base type's written simple name.</param>
    /// <returns><see langword="true"/> for <c>IExceptionFilter</c> or <c>IAsyncExceptionFilter</c>.</returns>
    private static bool IsFilterInterfaceName(string name)
        => string.Equals(name, ExceptionFilterName, StringComparison.Ordinal)
            || string.Equals(name, AsyncExceptionFilterName, StringComparison.Ordinal);

    /// <summary>Returns whether a bound interface is one of the resolved ASP.NET Core filter interfaces.</summary>
    /// <param name="bound">The interface symbol the base type bound to.</param>
    /// <param name="exceptionFilter">The resolved synchronous filter interface, or <see langword="null"/> when absent.</param>
    /// <param name="asyncExceptionFilter">The resolved asynchronous filter interface, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the bound interface is the ASP.NET Core filter, not a same-named look-alike.</returns>
    private static bool MatchesFilterInterface(INamedTypeSymbol bound, INamedTypeSymbol? exceptionFilter, INamedTypeSymbol? asyncExceptionFilter)
        => (exceptionFilter is not null && SymbolEqualityComparer.Default.Equals(bound, exceptionFilter))
            || (asyncExceptionFilter is not null && SymbolEqualityComparer.Default.Equals(bound, asyncExceptionFilter));

    /// <summary>Gets the rightmost identifier of a possibly qualified or aliased base type name.</summary>
    /// <param name="name">The base type name as written.</param>
    /// <returns>The simple name, or an empty string.</returns>
    private static string GetSimpleName(NameSyntax name) => name switch
    {
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax aliased => aliased.Name.Identifier.ValueText,
        _ => string.Empty,
    };
}
