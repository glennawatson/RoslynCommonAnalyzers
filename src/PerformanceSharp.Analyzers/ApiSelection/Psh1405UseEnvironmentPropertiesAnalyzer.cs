// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace PerformanceSharp.Analyzers;

/// <summary>
/// Flags call chains that reach current-process or current-thread state the expensive way
/// (PSH1405): <c>Process.GetCurrentProcess().Id</c>, <c>Process.GetCurrentProcess().MainModule.FileName</c>,
/// and <c>Thread.CurrentThread.ManagedThreadId</c> allocate or indirect where
/// <c>Environment.ProcessId</c>, <c>Environment.ProcessPath</c>, and
/// <c>Environment.CurrentManagedThreadId</c> read the runtime state directly. After a chain's syntax matches,
/// the rule checks that its replacement <c>System.Environment</c> property exists in the referenced framework,
/// so nothing is suggested where the replacement cannot compile.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Psh1405UseEnvironmentPropertiesAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The Environment property replacing <c>Process.GetCurrentProcess().Id</c>.</summary>
    internal const string ProcessIdPropertyName = "ProcessId";

    /// <summary>The Environment property replacing <c>Process.GetCurrentProcess().MainModule.FileName</c>.</summary>
    internal const string ProcessPathPropertyName = "ProcessPath";

    /// <summary>The Environment property replacing <c>Thread.CurrentThread.ManagedThreadId</c>.</summary>
    internal const string CurrentManagedThreadIdPropertyName = "CurrentManagedThreadId";

    /// <summary>The name of the current-process factory method.</summary>
    private const string GetCurrentProcessMethodName = "GetCurrentProcess";

    /// <summary>The trailing member of the process-id chain.</summary>
    private const string IdPropertyName = "Id";

    /// <summary>The middle member of the process-path chain.</summary>
    private const string MainModulePropertyName = "MainModule";

    /// <summary>The trailing member of the process-path chain.</summary>
    private const string FileNamePropertyName = "FileName";

    /// <summary>The static thread property the thread-id chain starts from.</summary>
    private const string CurrentThreadPropertyName = "CurrentThread";

    /// <summary>The trailing member of the thread-id chain.</summary>
    private const string ManagedThreadIdPropertyName = "ManagedThreadId";

    /// <summary>The message argument for the process-id chain.</summary>
    private const string ProcessIdMessageArg = "Environment.ProcessId";

    /// <summary>The message argument for the process-path chain.</summary>
    private const string ProcessPathMessageArg = "Environment.ProcessPath";

    /// <summary>The message argument for the thread-id chain.</summary>
    private const string CurrentManagedThreadIdMessageArg = "Environment.CurrentManagedThreadId";

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(ApiSelectionRules.UseEnvironmentProperties);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var markers = new Markers(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeMemberAccess(nodeContext, markers), SyntaxKind.SimpleMemberAccessExpression);
        });
    }

    /// <summary>Matches a reported chain shape (syntax only) and maps it to the replacement Environment property.</summary>
    /// <param name="access">The member access to inspect; a match covers the whole chain.</param>
    /// <param name="propertyName">The Environment property name when the shape matches.</param>
    /// <returns><see langword="true"/> when the member access is one of the three replaced chains.</returns>
    internal static bool TryGetReplacementPropertyName(MemberAccessExpressionSyntax access, out string propertyName)
    {
        var name = access.Name.Identifier.ValueText;
        if (name == IdPropertyName
            && access.Expression is InvocationExpressionSyntax idReceiver
            && IsGetCurrentProcessShape(idReceiver))
        {
            propertyName = ProcessIdPropertyName;
            return true;
        }

        if (name == FileNamePropertyName
            && access.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: MainModulePropertyName } mainModuleAccess
            && mainModuleAccess.Expression is InvocationExpressionSyntax fileNameReceiver
            && IsGetCurrentProcessShape(fileNameReceiver))
        {
            propertyName = ProcessPathPropertyName;
            return true;
        }

        if (name == ManagedThreadIdPropertyName
            && access.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: CurrentThreadPropertyName })
        {
            propertyName = CurrentManagedThreadIdPropertyName;
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

    /// <summary>Reports PSH1405 for a chain whose replacement Environment property exists.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="markers">The compilation's deferred framework gate.</param>
    private static void AnalyzeMemberAccess(in SyntaxNodeAnalysisContext context, Markers markers)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        if (!TryGetReplacementPropertyName(access, out var propertyName))
        {
            return;
        }

        var gate = markers.Get();

        var (enabled, messageArg) = propertyName switch
        {
            ProcessIdPropertyName => (gate.ReportProcessId, ProcessIdMessageArg),
            ProcessPathPropertyName => (gate.ReportProcessPath, ProcessPathMessageArg),
            _ => (gate.ReportCurrentManagedThreadId, CurrentManagedThreadIdMessageArg)
        };

        if (!enabled || !IsBoundChain(context, access, propertyName, gate))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            ApiSelectionRules.UseEnvironmentProperties,
            access.SyntaxTree,
            access.Span,
            messageArg));
    }

    /// <summary>Returns whether a syntax-matched chain binds to the real Process/Thread members.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="access">The syntax-matched chain.</param>
    /// <param name="propertyName">The replacement property the shape mapped to.</param>
    /// <param name="gate">The per-compilation gate state.</param>
    /// <returns><see langword="true"/> when the chain starts from the current process or thread.</returns>
    private static bool IsBoundChain(in SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax access, string propertyName, EnvironmentGate gate)
    {
        if (propertyName == CurrentManagedThreadIdPropertyName)
        {
            return context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is IPropertySymbol { IsStatic: true, Name: CurrentThreadPropertyName } currentThread
                && SymbolEqualityComparer.Default.Equals(currentThread.ContainingType, gate.ThreadType);
        }

        // The casts are safe: TryGetReplacementPropertyName validated the chain's syntax shape.
        var factoryInvocation = propertyName == ProcessIdPropertyName
            ? (InvocationExpressionSyntax)access.Expression
            : (InvocationExpressionSyntax)((MemberAccessExpressionSyntax)access.Expression).Expression;

        return context.SemanticModel.GetSymbolInfo(factoryInvocation, context.CancellationToken).Symbol
                is IMethodSymbol { IsStatic: true, Parameters.Length: 0, Name: GetCurrentProcessMethodName } factory
            && SymbolEqualityComparer.Default.Equals(factory.ContainingType, gate.ProcessType);
    }

    /// <summary>Returns whether an invocation has the argument-free <c>GetCurrentProcess()</c> syntax shape.</summary>
    /// <param name="invocation">The invocation to inspect.</param>
    /// <returns><see langword="true"/> when the syntax-only factory shape matches (member access or using-static call).</returns>
    private static bool IsGetCurrentProcessShape(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: GetCurrentProcessMethodName }
                or IdentifierNameSyntax { Identifier.ValueText: GetCurrentProcessMethodName };

    /// <summary>Captures the framework gate state resolved after a chain passes the syntax filter.</summary>
    /// <param name="ProcessType">The process type the reported chains start from, when it exists.</param>
    /// <param name="ThreadType">The thread type the reported chains start from, when it exists.</param>
    /// <param name="ReportProcessId">Whether <c>Environment.ProcessId</c> exists, enabling the process-id chain.</param>
    /// <param name="ReportProcessPath">Whether <c>Environment.ProcessPath</c> exists, enabling the process-path chain.</param>
    /// <param name="ReportCurrentManagedThreadId">Whether <c>Environment.CurrentManagedThreadId</c> exists, enabling the thread-id chain.</param>
    private readonly record struct EnvironmentGate(
        INamedTypeSymbol? ProcessType,
        INamedTypeSymbol? ThreadType,
        bool ReportProcessId,
        bool ReportProcessPath,
        bool ReportCurrentManagedThreadId);

    /// <summary>Resolves the framework gate once per compilation, on first demand.</summary>
    /// <param name="compilation">The compilation whose replacement properties are probed.</param>
    private sealed class Markers(Compilation compilation)
    {
        /// <summary>The metadata name of the environment type probed for the replacement properties.</summary>
        private const string EnvironmentMetadataName = "System.Environment";

        /// <summary>The metadata name of the process type the reported chains start from.</summary>
        private const string ProcessMetadataName = "System.Diagnostics.Process";

        /// <summary>The metadata name of the thread type the reported chains start from.</summary>
        private const string ThreadMetadataName = "System.Threading.Thread";

        /// <summary>The single cached gate, including a disabled gate when the environment type is absent.</summary>
        private EnvironmentGate[]? _resolved;

        /// <summary>Gets the framework gate after a chain passes the syntax filter.</summary>
        /// <returns>The cached framework gate.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EnvironmentGate Get() => (_resolved ??= [Resolve(compilation)])[0];

        /// <summary>Resolves the types and replacement properties required by the reported chains.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>The available replacements, or a disabled gate when the environment type is absent.</returns>
        private static EnvironmentGate Resolve(Compilation compilation)
        {
            if (compilation.GetTypeByMetadataName(EnvironmentMetadataName) is not { } environmentType)
            {
                return default;
            }

            var processType = compilation.GetTypeByMetadataName(ProcessMetadataName);
            var threadType = compilation.GetTypeByMetadataName(ThreadMetadataName);
            return new(
                processType,
                threadType,
                processType is not null && HasStaticProperty(environmentType, ProcessIdPropertyName),
                processType is not null && HasStaticProperty(environmentType, ProcessPathPropertyName),
                threadType is not null && HasStaticProperty(environmentType, CurrentManagedThreadIdPropertyName));
        }

        /// <summary>Returns whether a type exposes a static property with the given name.</summary>
        /// <param name="type">The type to probe.</param>
        /// <param name="name">The property name to look for.</param>
        /// <returns><see langword="true"/> when the probed property exists.</returns>
        private static bool HasStaticProperty(INamedTypeSymbol type, string name)
        {
            var members = type.GetMembers(name);
            for (var i = 0; i < members.Length; i++)
            {
                if (members[i] is IPropertySymbol { IsStatic: true })
                {
                    return true;
                }
            }

            return false;
        }
    }
}
