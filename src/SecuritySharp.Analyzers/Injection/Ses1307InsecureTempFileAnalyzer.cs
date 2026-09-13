// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a call to the static <c>System.IO.Path.GetTempFileName()</c> (SES1307). That method creates a
/// zero-byte file with a predictable <c>tmpXXXX.tmp</c> name in the shared, world-readable temp directory,
/// which exposes it to a time-of-check/time-of-use race and fails once 65535 undeleted files exist
/// (CWE-377). The rule binds the invoked symbol and reports only when the method is the parameterless
/// <c>GetTempFileName</c> whose containing type is the resolved <c>System.IO.Path</c> -- never on a
/// same-named method elsewhere. The clean path is syntactic: an invocation is ignored unless its member or
/// identifier name is <c>GetTempFileName</c> and it carries no arguments, so ordinary calls cost no symbol
/// query. The suggested replacement is chosen once per compilation: <c>Directory.CreateTempSubdirectory</c>
/// is named only when it resolves in the analyzed compilation (.NET 7+), otherwise the message suggests the
/// always-available <c>Path.GetRandomFileName()</c>. Only the local call shape is inspected -- no data-flow
/// or interprocedural tracking is performed. There is no code fix: <c>GetTempFileName</c> also creates the
/// file, so the replacement is not a mechanical swap.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1307InsecureTempFileAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The name of the insecure temp-file method that is reported.</summary>
    private const string GetTempFileNameMethodName = "GetTempFileName";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.InsecureTempFile);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new TempFileTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
        });
    }

    /// <summary>Reports SES1307 for a bound, parameterless <c>Path.GetTempFileName()</c> call.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The path type and replacement guidance, each resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, TempFileTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Syntactic prefilter: a '...GetTempFileName()' call with no arguments, whether written as a
        // member access ('Path.GetTempFileName()') or as an identifier under 'using static System.IO.Path'.
        if (invocation.ArgumentList.Arguments.Count != 0
            || GetInvokedName(invocation.Expression) is not GetTempFileNameMethodName)
        {
            return;
        }

        if (types.GetPathType() is not { } resolvedType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: GetTempFileNameMethodName, IsStatic: true } method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, resolvedType))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.InsecureTempFile,
            invocation.SyntaxTree,
            invocation.Span,
            types.GetSuggestion()));
    }

    /// <summary>Returns the simple invoked name from a member access or bare identifier expression.</summary>
    /// <param name="expression">The invocation's callee expression.</param>
    /// <returns>The simple method name, or <see langword="null"/> when the callee is neither a member access nor an identifier.</returns>
    private static string? GetInvokedName(ExpressionSyntax expression) =>
        expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

    /// <summary>Resolves the path type and diagnostic guidance independently, once per compilation.</summary>
    /// <param name="compilation">The compilation whose filesystem APIs are inspected.</param>
    private sealed class TempFileTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the type whose insecure temp-file factory is guarded.</summary>
        private const string PathMetadataName = "System.IO.Path";

        /// <summary>The metadata name of the type carrying the isolated-directory replacement.</summary>
        private const string DirectoryMetadataName = "System.IO.Directory";

        /// <summary>The name of the .NET 7+ isolated-directory replacement method.</summary>
        private const string CreateTempSubdirectoryMethodName = "CreateTempSubdirectory";

        /// <summary>The replacement suggestion when the isolated-directory API is unavailable.</summary>
        private const string RandomNameSuggestion = "'Path.GetRandomFileName()' for an unpredictable name";

        /// <summary>The replacement suggestion when the isolated-directory API resolves (.NET 7+).</summary>
        private const string RandomNameOrSubdirectorySuggestion =
            "'Path.GetRandomFileName()' for an unpredictable name, or 'Directory.CreateTempSubdirectory()' for an isolated directory";

        /// <summary>Serializes the initial metadata lookups across callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The path type, or null when it is absent.</summary>
        private INamedTypeSymbol? _pathType;

        /// <summary>The guidance, populated only when a diagnostic is reported.</summary>
        private string? _suggestion;

        /// <summary>Publishes completion of the path lookup, including a missing type.</summary>
        private bool _pathResolved;

        /// <summary>Gets the path type after a call passes the syntax filter.</summary>
        /// <returns>The path type, or null when it is absent.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetPathType() => Volatile.Read(ref _pathResolved) ? _pathType : ResolvePathType();

        /// <summary>Gets the replacement guidance when a diagnostic needs it.</summary>
        /// <returns>The cached diagnostic suggestion.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetSuggestion() => Volatile.Read(ref _suggestion) ?? ResolveSuggestion();

        /// <summary>Chooses the replacement guidance, naming the isolated-directory API only when it resolves.</summary>
        /// <param name="compilation">The compilation to probe for the .NET 7+ replacement.</param>
        /// <returns>The suggestion text embedded in the diagnostic message.</returns>
        private static string BuildSuggestion(Compilation compilation) =>
            compilation.GetTypeByMetadataName(DirectoryMetadataName) is { } directoryType && HasCreateTempSubdirectory(directoryType)
                ? RandomNameOrSubdirectorySuggestion
                : RandomNameSuggestion;

        /// <summary>Returns whether a type declares the static <c>CreateTempSubdirectory</c> replacement.</summary>
        /// <param name="directoryType">The resolved <c>System.IO.Directory</c> type.</param>
        /// <returns><see langword="true"/> when the isolated-directory factory is available.</returns>
        private static bool HasCreateTempSubdirectory(INamedTypeSymbol directoryType)
        {
            var members = directoryType.GetMembers(CreateTempSubdirectoryMethodName);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IMethodSymbol { IsStatic: true })
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Resolves and publishes the path type once.</summary>
        /// <returns>The path type, or null when it is absent.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol? ResolvePathType()
        {
            lock (_gate)
            {
                if (!_pathResolved)
                {
                    _pathType = compilation.GetTypeByMetadataName(PathMetadataName);
                    Volatile.Write(ref _pathResolved, true);
                }

                return _pathType;
            }
        }

        /// <summary>Resolves and publishes the replacement guidance once.</summary>
        /// <returns>The cached diagnostic suggestion.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string ResolveSuggestion()
        {
            lock (_gate)
            {
                var suggestion = _suggestion;
                if (suggestion is null)
                {
                    suggestion = BuildSuggestion(compilation);
                    Volatile.Write(ref _suggestion, suggestion);
                }

                return suggestion;
            }
        }
    }
}
