// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags loading an assembly whose code cannot be verified before it runs (SES1402). The rule reports
/// <c>System.Reflection.Assembly.Load(byte[])</c> (the raw-bytes overload, always -- assembly bytes are an
/// arbitrary-code-execution source), <c>System.Runtime.Loader.AssemblyLoadContext.LoadFromStream(...)</c>,
/// and <c>Assembly.LoadFrom(...)</c> / <c>Assembly.LoadFile(...)</c> / <c>Assembly.UnsafeLoadFrom(...)</c>
/// whose path argument is <b>not</b> a compile-time constant (a hard-coded literal path is lower risk and out
/// of scope). The safe <c>Assembly.Load(string)</c> / <c>Load(AssemblyName)</c> identity overloads are told
/// apart by their first parameter type and are never reported. For the in-memory shapes, a source argument
/// that is directly <c>&lt;assembly&gt;.GetManifestResourceStream(...)</c> -- a trusted embedded resource -- is
/// left silent. The rule resolves its framework types only after a call passes the syntax prefilter
/// and reports nothing when <c>System.Reflection.Assembly</c> is absent.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1402UnsafeAssemblyLoadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the <c>Assembly.Load</c> method (raw-bytes overload is guarded).</summary>
    private const string LoadMethodName = "Load";

    /// <summary>The name of the <c>Assembly.LoadFrom</c> method (non-constant path is guarded).</summary>
    private const string LoadFromMethodName = "LoadFrom";

    /// <summary>The name of the <c>Assembly.LoadFile</c> method (non-constant path is guarded).</summary>
    private const string LoadFileMethodName = "LoadFile";

    /// <summary>The name of the <c>Assembly.UnsafeLoadFrom</c> method (non-constant path is guarded).</summary>
    private const string UnsafeLoadFromMethodName = "UnsafeLoadFrom";

    /// <summary>The name of the <c>AssemblyLoadContext.LoadFromStream</c> method (always guarded).</summary>
    private const string LoadFromStreamMethodName = "LoadFromStream";

    /// <summary>The name of the trusted embedded-resource accessor that suppresses the diagnostic.</summary>
    private const string ManifestResourceStreamMethodName = "GetManifestResourceStream";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.UnsafeAssemblyLoad);

    /// <summary>The way a load site puts trust at risk, deciding whether and how it is reported.</summary>
    private enum LoadRisk
    {
        /// <summary>Not a guarded load site.</summary>
        None = 0,

        /// <summary>An in-memory load (raw bytes or a stream); reported unless the source is a trusted resource.</summary>
        InMemory = 1,

        /// <summary>A file load; reported only when the path argument is not a compile-time constant.</summary>
        NonConstantPath = 2,
    }

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static startContext =>
        {
            var types = new AssemblyTypes(startContext.Compilation);
            startContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1402 for a guarded assembly-load call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The compilation-scoped framework type cache.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, AssemblyTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a member '.Load/.LoadFrom/.LoadFile/.UnsafeLoadFrom/.LoadFromStream(...)' call
        // carrying at least one argument. Anything else is rejected before the semantic model is touched.
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || invocation.ArgumentList.Arguments.Count == 0
            || !IsCandidateMethodName(memberAccess.Name.Identifier.ValueText))
        {
            return;
        }

        if (types.GetAssembly() is not { } assemblyType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var loadContextType = types.GetLoadContext();
        var risk = Classify(method, assemblyType, loadContextType);
        if (risk == LoadRisk.None
            || GetFirstParameterArgument(invocation.ArgumentList, method) is not { } sourceArgument
            || !IsReported(risk, sourceArgument, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.UnsafeAssemblyLoad,
            invocation.SyntaxTree,
            invocation.Span,
            DescribeCall(method)));
    }

    /// <summary>Returns whether a simple method name is one of the guarded load methods.</summary>
    /// <param name="name">The invoked simple method name.</param>
    /// <returns><see langword="true"/> when the name is a candidate for binding.</returns>
    private static bool IsCandidateMethodName(string name) =>
        name is LoadMethodName or LoadFromMethodName or LoadFileMethodName or UnsafeLoadFromMethodName or LoadFromStreamMethodName;

    /// <summary>Classifies how a bound load method puts trust at risk.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <param name="assemblyType">The resolved <c>System.Reflection.Assembly</c> type.</param>
    /// <param name="loadContextType">The resolved <c>AssemblyLoadContext</c> type, or <see langword="null"/> when absent.</param>
    /// <returns>The load-risk classification, or <see cref="LoadRisk.None"/> when the call is not guarded.</returns>
    private static LoadRisk Classify(IMethodSymbol method, INamedTypeSymbol assemblyType, INamedTypeSymbol? loadContextType)
    {
        if (IsDeclaredOn(method, assemblyType))
        {
            return ClassifyAssemblyMethod(method);
        }

        return method.Name == LoadFromStreamMethodName && IsDeclaredOn(method, loadContextType) ? LoadRisk.InMemory : LoadRisk.None;
    }

    /// <summary>Classifies a load method declared on <c>System.Reflection.Assembly</c>.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns>The load-risk classification for the assembly method.</returns>
    private static LoadRisk ClassifyAssemblyMethod(IMethodSymbol method) =>
        method.Name switch
        {
            // Only the raw-bytes overload is a risk; Load(string) / Load(AssemblyName) resolve a trusted identity.
            LoadMethodName => HasByteArrayFirstParameter(method) ? LoadRisk.InMemory : LoadRisk.None,
            LoadFromMethodName or LoadFileMethodName or UnsafeLoadFromMethodName => LoadRisk.NonConstantPath,
            _ => LoadRisk.None,
        };

    /// <summary>Returns whether a classified load site should be reported for its source argument.</summary>
    /// <param name="risk">The load-risk classification.</param>
    /// <param name="sourceArgument">The bytes/stream/path argument.</param>
    /// <param name="model">The semantic model.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the site is reported.</returns>
    private static bool IsReported(LoadRisk risk, ExpressionSyntax sourceArgument, SemanticModel model, CancellationToken cancellationToken) =>
        risk switch
        {
            // A constant, hard-coded path is lower risk and out of scope.
            LoadRisk.NonConstantPath => !model.GetConstantValue(sourceArgument, cancellationToken).HasValue,

            // A byte[]/stream taken directly from an embedded manifest resource is a trusted source.
            LoadRisk.InMemory => !IsManifestResourceStream(sourceArgument),

            _ => false,
        };

    /// <summary>Returns whether a method's containing type is the resolved type.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <param name="type">The resolved type to compare against, or <see langword="null"/> when absent.</param>
    /// <returns><see langword="true"/> when the method is declared on <paramref name="type"/>.</returns>
    private static bool IsDeclaredOn(IMethodSymbol method, INamedTypeSymbol? type) =>
        type is not null && SymbolEqualityComparer.Default.Equals(method.ContainingType, type);

    /// <summary>Returns whether a method's first parameter is a <c>byte[]</c>.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns><see langword="true"/> for a leading <c>byte[]</c> parameter (the raw-bytes overload).</returns>
    private static bool HasByteArrayFirstParameter(IMethodSymbol method) =>
        !method.Parameters.IsEmpty
           && method.Parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte };

    /// <summary>Returns the argument bound to the method's first parameter, honouring an explicit name.</summary>
    /// <param name="argumentList">The invocation's argument list.</param>
    /// <param name="method">The bound method symbol.</param>
    /// <returns>The source argument expression, or <see langword="null"/> when it cannot be identified positionally.</returns>
    private static ExpressionSyntax? GetFirstParameterArgument(ArgumentListSyntax argumentList, IMethodSymbol method)
    {
        var arguments = argumentList.Arguments;
        if (!method.Parameters.IsEmpty)
        {
            var firstParameterName = method.Parameters[0].Name;
            for (var i = 0; i < arguments.Count; i++)
            {
                var nameColon = arguments[i].NameColon;
                if (nameColon is not null && nameColon.Name.Identifier.ValueText == firstParameterName)
                {
                    return arguments[i].Expression;
                }
            }
        }

        // The bytes/stream/path is the first parameter of every guarded overload, so a leading
        // positional argument is the source.
        return arguments[0].NameColon is null ? arguments[0].Expression : null;
    }

    /// <summary>Returns whether an expression is directly a <c>GetManifestResourceStream</c> call.</summary>
    /// <param name="expression">The source argument expression.</param>
    /// <returns><see langword="true"/> when the source is a trusted embedded manifest resource.</returns>
    private static bool IsManifestResourceStream(ExpressionSyntax expression) =>
        Unwrap(expression) is InvocationExpressionSyntax invocation
           && InvokedName.Of(invocation.Expression) is ManifestResourceStreamMethodName;

    /// <summary>Strips enclosing parentheses and a trailing null-forgiving operator from an expression.</summary>
    /// <param name="expression">The expression to unwrap.</param>
    /// <returns>The innermost expression.</returns>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                {
                    current = parenthesized.Expression;
                    break;
                }

                case PostfixUnaryExpressionSyntax suppress when suppress.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                {
                    current = suppress.Operand;
                    break;
                }

                default:
                    return current;
            }
        }
    }

    /// <summary>Returns a caller-facing description of the reported load call for the message.</summary>
    /// <param name="method">The bound method symbol.</param>
    /// <returns>The call description embedded in the diagnostic message.</returns>
    private static string DescribeCall(IMethodSymbol method) =>
        method.Name switch
        {
            LoadMethodName => "Assembly.Load(byte[])",
            LoadFromStreamMethodName => "AssemblyLoadContext.LoadFromStream",
            _ => $"Assembly.{method.Name}",
        };

    /// <summary>Resolves each assembly-loading type on first demand within a compilation.</summary>
    /// <param name="compilation">The compilation whose framework types are resolved.</param>
    private sealed class AssemblyTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the type that owns the reflection load methods.</summary>
        private const string AssemblyMetadataName = "System.Reflection.Assembly";

        /// <summary>The metadata name of the load context that owns <c>LoadFromStream</c>.</summary>
        private const string AssemblyLoadContextMetadataName = "System.Runtime.Loader.AssemblyLoadContext";

        /// <summary>The cached assembly type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _assembly;

        /// <summary>The cached load-context type, with a null element when the type is absent.</summary>
        private INamedTypeSymbol?[]? _loadContext;

        /// <summary>Resolves the assembly type on first demand and caches its absence too.</summary>
        /// <returns>The assembly type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetAssembly() => (_assembly ??= [compilation.GetTypeByMetadataName(AssemblyMetadataName)])[0];

        /// <summary>Resolves the load-context type on first demand and caches its absence too.</summary>
        /// <returns>The load-context type, or null when unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetLoadContext() => (_loadContext ??= [compilation.GetTypeByMetadataName(AssemblyLoadContextMetadataName)])[0];
    }
}
