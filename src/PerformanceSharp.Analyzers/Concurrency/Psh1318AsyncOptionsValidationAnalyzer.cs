// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Reports an <c>IValidateOptions&lt;T&gt;</c> validator whose synchronous <c>Validate</c> blocks a
/// thread on asynchronous work, where the asynchronous validation interface exists (PSH1318).
/// </summary>
/// <remarks>
/// Reported once, on the method, rather than at each blocking call: the finding is about which
/// interface the type implements, not about any one wait. The waits themselves are PSH1315's, which
/// reports them wherever they appear; this rule adds the reason the shape exists at all and names the
/// interface that removes it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1318AsyncOptionsValidationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The method the synchronous interface requires.</summary>
    private const string ValidateMethodName = "Validate";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue =
        ImmutableArrays.Of(ConcurrencyRules.PreferAsyncOptionsValidation);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(static startContext =>
        {
            var validationTypes = new ValidationTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, validationTypes), SyntaxKind.MethodDeclaration);
        });
    }

    /// <summary>Reports one synchronous validator that blocks.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="validationTypes">The validation and task types resolved on demand for this compilation.</param>
    private static void Analyze(in SyntaxNodeAnalysisContext context, ValidationTypes validationTypes)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.Identifier.ValueText != ValidateMethodName || !HasBlockingSyntax(declaration))
        {
            return;
        }

        if (validationTypes.GetInterfaces() is not [{ } syncInterface, { } asyncInterface]
            || validationTypes.GetTasks() is not { } tasks)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not { } method
            || ValidatedOptionsType(method.ContainingType, syncInterface, asyncInterface) is not { } options)
        {
            return;
        }

        if (!Blocks(declaration, context.SemanticModel, tasks, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ConcurrencyRules.PreferAsyncOptionsValidation,
            declaration.Identifier.GetLocation(),
            method.ContainingType.Name,
            options.Name));
    }

    /// <summary>Checks for a possible blocking wait before resolving any framework types.</summary>
    /// <param name="declaration">The declaration whose descendants the semantic scan visits.</param>
    /// <returns>True when a descendant has a blocking-wait name and node shape.</returns>
    private static bool HasBlockingSyntax(MethodDeclarationSyntax declaration)
    {
        var found = false;
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, bool>(declaration, ref found, VisitBlockingSyntax);
        return found;
    }

    /// <summary>Stops the syntax scan at a member access or invocation that could block.</summary>
    /// <param name="node">The descendant being checked.</param>
    /// <param name="found">Whether a possible blocking wait has been found.</param>
    /// <returns>False once a possible wait is found; otherwise true.</returns>
    private static bool VisitBlockingSyntax(SyntaxNode node, ref bool found)
    {
        if (node is MemberAccessExpressionSyntax { Name.Identifier.ValueText: BlockingWait.ResultPropertyName }
            || (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
                && access.Name.Identifier.ValueText is BlockingWait.GetResultMethodName or BlockingWait.WaitMethodName
                    or BlockingWait.WaitAllMethodName or BlockingWait.WaitAnyMethodName or BlockingWait.RunSynchronouslyMethodName))
        {
            found = true;
            return false;
        }

        return true;
    }

    /// <summary>Gets the options type a validator validates, when it only implements the synchronous interface.</summary>
    /// <param name="type">The type declaring the method.</param>
    /// <param name="syncInterface">The synchronous validation interface definition.</param>
    /// <param name="asyncInterface">The asynchronous validation interface definition.</param>
    /// <returns>The validated options type, or <see langword="null"/>.</returns>
    private static ITypeSymbol? ValidatedOptionsType(
        INamedTypeSymbol type,
        INamedTypeSymbol syncInterface,
        INamedTypeSymbol asyncInterface)
    {
        ITypeSymbol? options = null;
        foreach (var implemented in type.AllInterfaces)
        {
            var definition = implemented.OriginalDefinition;
            if (SymbolEqualityComparer.Default.Equals(definition, asyncInterface))
            {
                return null;
            }

            if (SymbolEqualityComparer.Default.Equals(definition, syncInterface) && implemented.TypeArguments.Length == 1)
            {
                options = implemented.TypeArguments[0];
            }
        }

        return options;
    }

    /// <summary>Gets whether a method body parks a thread on an awaitable.</summary>
    /// <param name="declaration">The method declaration.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="tasks">The task types resolved for the compilation.</param>
    /// <param name="cancellationToken">A token that cancels analysis.</param>
    /// <returns><see langword="true"/> when the body blocks.</returns>
    private static bool Blocks(
        MethodDeclarationSyntax declaration,
        SemanticModel model,
        in AsyncSiblingResolver.TaskTypes tasks,
        CancellationToken cancellationToken)
    {
        var scan = new BlockingScan(model, tasks, false, cancellationToken);
        _ = DescendantTraversalHelper.VisitDescendants<SyntaxNode, BlockingScan>(declaration, ref scan, Visit);
        return scan.Found;
    }

    /// <summary>Stops the walk at the first blocking wait.</summary>
    /// <param name="node">The descendant node.</param>
    /// <param name="scan">The running scan.</param>
    /// <returns><see langword="false"/> once a blocking wait is found.</returns>
    private static bool Visit(SyntaxNode node, ref BlockingScan scan)
    {
        if (node is not (MemberAccessExpressionSyntax or InvocationExpressionSyntax)
            || BlockingWait.TryMatch(node, scan.Model, scan.Tasks, scan.CancellationToken) is null)
        {
            return true;
        }

        scan.Found = true;
        return false;
    }

    /// <summary>The state threaded through the descendant walk.</summary>
    /// <param name="Model">The semantic model.</param>
    /// <param name="Tasks">The task types resolved for the compilation.</param>
    /// <param name="Found">Whether a blocking wait has been seen.</param>
    /// <param name="CancellationToken">The token that cancels analysis.</param>
    private record struct BlockingScan(
        SemanticModel Model,
        AsyncSiblingResolver.TaskTypes Tasks,
        bool Found,
        CancellationToken CancellationToken);

    /// <summary>Resolves validation interfaces and task types on first demand, including missing types.</summary>
    /// <param name="compilation">The compilation whose framework types are resolved.</param>
    private sealed class ValidationTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the synchronous validation interface.</summary>
        private const string ValidateOptionsMetadataName = "Microsoft.Extensions.Options.IValidateOptions`1";

        /// <summary>The metadata name of the asynchronous validation interface.</summary>
        private const string AsyncValidateOptionsMetadataName = "Microsoft.Extensions.Options.IAsyncValidateOptions`1";

        /// <summary>The cached synchronous and asynchronous interfaces, with null entries for missing types.</summary>
        private INamedTypeSymbol?[]? _interfaces;

        /// <summary>The cached task-type result, with a null entry when tasks are unavailable.</summary>
        private AsyncSiblingResolver.TaskTypes?[]? _tasks;

        /// <summary>Gets both validation interfaces, resolving them only on first demand.</summary>
        /// <returns>The synchronous and asynchronous interfaces, with null entries for missing types.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] GetInterfaces() => _interfaces ??=
        [
            compilation.GetTypeByMetadataName(ValidateOptionsMetadataName),
            compilation.GetTypeByMetadataName(AsyncValidateOptionsMetadataName)
        ];

        /// <summary>Gets the task types, caching their absence as well as their presence.</summary>
        /// <returns>The resolved task types, or null when the framework has no task type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AsyncSiblingResolver.TaskTypes? GetTasks() => (_tasks ??= [AsyncSiblingResolver.TaskTypes.Create(compilation)])[0];
    }
}
