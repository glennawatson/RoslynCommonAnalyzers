// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.Operations;

namespace SecuritySharp.Analyzers;

/// <summary>
/// Flags a constant <c>UnixFileMode</c> that grants write access to group or other, supplied to a
/// filesystem permission API (SES1308). The rule reports the mode argument of
/// <c>File.SetUnixFileMode</c> and <c>Directory.CreateDirectory</c>, and the value assigned to
/// <c>FileInfo.UnixFileMode</c>, <c>DirectoryInfo.UnixFileMode</c>, or
/// <c>FileStreamOptions.UnixCreateMode</c>, when that value folds to a compile-time constant whose
/// group-write (0o020) or other-write (0o002) bit is set -- whether written as a single member
/// (<c>UnixFileMode.OtherWrite</c>), an OR-combination, or a broad 0o777-style combo. A group- or
/// world-writable file lets other local users tamper with it (CWE-732). The rule is gated on
/// <c>System.IO.UnixFileMode</c> resolving (.NET 7+), and the types are resolved once, on first demand.
/// A target framework without it (netstandard2.0, .NET Framework) gets no diagnostic.
/// The clean path is a syntactic screen -- a member call named <c>SetUnixFileMode</c>
/// or <c>CreateDirectory</c> with at least two arguments, or an assignment to a member named
/// <c>UnixCreateMode</c>/<c>UnixFileMode</c> -- before any type resolution or binding runs. Only the local shape is
/// inspected: a mode first stored in a variable and passed later is not tracked, so the
/// constant/non-constant decision is made at the call or assignment site with no data-flow analysis.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Ses1308OverPermissiveUnixFileModeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The filesystem method name that sets a path's Unix permission mode.</summary>
    private const string SetUnixFileModeMethodName = "SetUnixFileMode";

    /// <summary>The filesystem method name whose overload takes a create-time Unix mode.</summary>
    private const string CreateDirectoryMethodName = "CreateDirectory";

    /// <summary>The <c>FileStreamOptions</c> property name that sets a create-time Unix mode.</summary>
    private const string UnixCreateModePropertyName = "UnixCreateMode";

    /// <summary>The <c>FileInfo</c>/<c>DirectoryInfo</c> property name that sets the Unix permission mode.</summary>
    private const string UnixFileModePropertyName = "UnixFileMode";

    /// <summary>The POSIX group-write permission bit (<c>S_IWGRP</c>, 0o020).</summary>
    private const int GroupWriteBit = 16;

    /// <summary>The POSIX other-write permission bit (<c>S_IWOTH</c>, 0o002).</summary>
    private const int OtherWriteBit = 2;

    /// <summary>The mask matching a mode that grants write access to group or other.</summary>
    private const int GroupOrOtherWriteMask = GroupWriteBit | OtherWriteBit;

    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(SecurityRules.OverPermissiveUnixFileMode);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new PermissionTypes(start.Compilation);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeInvocation(nodeContext, types), SyntaxKind.InvocationExpression);
            start.RegisterSyntaxNodeAction(nodeContext => AnalyzeAssignment(nodeContext, types), SyntaxKind.SimpleAssignmentExpression);
        });
    }

    /// <summary>Reports SES1308 for a filesystem permission call whose mode grants group or other write.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The permission types resolved on first demand.</param>
    private static void AnalyzeInvocation(in SyntaxNodeAnalysisContext context, PermissionTypes types)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Reject unrelated names and arities before either metadata resolution or binding.
        if (!IsModeInvocationShape(invocation))
        {
            return;
        }

        // Resolve the target before materializing operations for all arguments. Invalid calls and
        // same-named methods on unrelated types do not need an operation tree.
        if (types.GetModeType() is not { } modeType
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsSinkContainer(method.ContainingType, types.GetSinkContainers())
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation)
        {
            return;
        }

        var arguments = operation.Arguments;
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (!IsUnixFileMode(argument.Parameter?.Type, modeType) || !GrantsGroupOrOtherWrite(argument.Value.ConstantValue))
            {
                continue;
            }

            var modeSyntax = argument.Value.Syntax;
            context.ReportDiagnostic(DiagnosticHelper.Create(
                SecurityRules.OverPermissiveUnixFileMode,
                modeSyntax.SyntaxTree,
                modeSyntax.Span,
                $"{operation.TargetMethod.ContainingType.Name}.{operation.TargetMethod.Name}"));
            return;
        }
    }

    /// <summary>Reports SES1308 for an assignment of a group- or world-writable mode to a permission property.</summary>
    /// <param name="context">The syntax node analysis context.</param>
    /// <param name="types">The permission types resolved on first demand.</param>
    private static void AnalyzeAssignment(in SyntaxNodeAnalysisContext context, PermissionTypes types)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        // This includes both member access and the bare member name in an object initializer.
        if (GetAssignedMemberName(assignment.Left) is not (UnixCreateModePropertyName or UnixFileModePropertyName))
        {
            return;
        }

        if (types.GetModeType() is not { } modeType
            || context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property
            || !IsUnixFileMode(property.Type, modeType)
            || !IsSinkContainer(property.ContainingType, types.GetSinkContainers())
            || !GrantsGroupOrOtherWrite(context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken)))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(
            SecurityRules.OverPermissiveUnixFileMode,
            assignment.Right.SyntaxTree,
            assignment.Right.Span,
            $"{property.ContainingType.Name}.{property.Name}"));
    }

    /// <summary>Returns whether a call can supply a Unix mode to a filesystem API, before binding.</summary>
    /// <param name="invocation">The invocation to screen.</param>
    /// <returns><see langword="true"/> for a permission method name with at least two arguments.</returns>
    private static bool IsModeInvocationShape(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: SetUnixFileModeMethodName or CreateDirectoryMethodName }
            && invocation.ArgumentList.Arguments.Count >= 2;

    /// <summary>Returns the simple name of the member on the left of an assignment.</summary>
    /// <param name="left">The assignment's left-hand side.</param>
    /// <returns>The member's simple name, or <see langword="null"/> when the target is not a member reference.</returns>
    private static string? GetAssignedMemberName(ExpressionSyntax left) => left switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Returns whether a constant mode value has the group-write or other-write bit set.</summary>
    /// <param name="constant">The folded constant value of the mode expression.</param>
    /// <returns><see langword="true"/> when the mode grants write access to group or other.</returns>
    private static bool GrantsGroupOrOtherWrite(Optional<object?> constant) =>
        constant is { HasValue: true, Value: int mode } && (mode & GroupOrOtherWriteMask) != 0;

    /// <summary>Returns whether a type is <c>UnixFileMode</c>, unwrapping a nullable value type first.</summary>
    /// <param name="type">The candidate parameter or property type.</param>
    /// <param name="unixFileMode">The resolved <c>UnixFileMode</c> type.</param>
    /// <returns><see langword="true"/> when the type is <c>UnixFileMode</c> or <c>UnixFileMode?</c>.</returns>
    private static bool IsUnixFileMode(ITypeSymbol? type, INamedTypeSymbol unixFileMode)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] })
        {
            type = underlying;
        }

        return SymbolEqualityComparer.Default.Equals(type, unixFileMode);
    }

    /// <summary>Returns whether a member's containing type is one of the gated filesystem sink types.</summary>
    /// <param name="containingType">The bound member's containing type.</param>
    /// <param name="sinkContainers">The resolved filesystem sink container types.</param>
    /// <returns><see langword="true"/> when the container is a gated filesystem type.</returns>
    private static bool IsSinkContainer(INamedTypeSymbol containingType, INamedTypeSymbol?[] sinkContainers)
    {
        for (var i = 0; i < sinkContainers.Length; i++)
        {
            if (sinkContainers[i] is { } container && SymbolEqualityComparer.Default.Equals(container, containingType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves permission metadata once, only after a syntax candidate needs it.</summary>
    /// <param name="compilation">The compilation whose permission types are cached.</param>
    private sealed class PermissionTypes(Compilation compilation)
    {
        /// <summary>The metadata name of the file-permission enum whose write bits are guarded.</summary>
        private const string UnixFileModeMetadataName = "System.IO.UnixFileMode";

        /// <summary>
        /// The metadata names of the BCL filesystem types whose permission sinks are guarded. The
        /// <c>UnixFileMode</c> property that <c>FileInfo</c> and <c>DirectoryInfo</c> expose is declared on
        /// their shared base <c>FileSystemInfo</c>, so that base is the gated container for both.
        /// </summary>
        private static readonly string[] SinkContainerMetadataNames =
        [
            "System.IO.File",
            "System.IO.Directory",
            "System.IO.FileSystemInfo",
            "System.IO.FileStreamOptions"
        ];

        /// <summary>Serializes the initial metadata lookups across callbacks.</summary>
        private readonly object _gate = new();

        /// <summary>The cached mode type, including a missing type.</summary>
        private INamedTypeSymbol?[]? _modeType;

        /// <summary>The cached sink containers, resolved only after a member binds.</summary>
        private INamedTypeSymbol?[]? _sinkContainers;

        /// <summary>Gets the mode type on first demand.</summary>
        /// <returns>The mode type, or null when the framework does not provide it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol? GetModeType() => (Volatile.Read(ref _modeType) ?? ResolveModeType())[0];

        /// <summary>Gets the sink containers on first demand.</summary>
        /// <returns>The cached sink types, including missing containers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public INamedTypeSymbol?[] GetSinkContainers() => Volatile.Read(ref _sinkContainers) ?? ResolveSinkContainers();

        /// <summary>Resolves the filesystem sink container types present in the compilation.</summary>
        /// <param name="compilation">The compilation to probe.</param>
        /// <returns>An array whose slots hold each resolved sink container type, or <see langword="null"/> for the absent ones.</returns>
        private static INamedTypeSymbol?[] CollectSinkContainers(Compilation compilation)
        {
            var containers = new INamedTypeSymbol?[SinkContainerMetadataNames.Length];
            for (var i = 0; i < SinkContainerMetadataNames.Length; i++)
            {
                containers[i] = compilation.GetTypeByMetadataName(SinkContainerMetadataNames[i]);
            }

            return containers;
        }

        /// <summary>Publishes the mode type once for the compilation.</summary>
        /// <returns>The cached mode type slot.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol?[] ResolveModeType()
        {
            lock (_gate)
            {
                var resolved = _modeType;
                if (resolved is null)
                {
                    resolved = [compilation.GetTypeByMetadataName(UnixFileModeMetadataName)];
                    Volatile.Write(ref _modeType, resolved);
                }

                return resolved;
            }
        }

        /// <summary>Publishes the sink containers once for the compilation.</summary>
        /// <returns>The cached sink types.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private INamedTypeSymbol?[] ResolveSinkContainers()
        {
            lock (_gate)
            {
                var resolved = _sinkContainers;
                if (resolved is null)
                {
                    resolved = CollectSinkContainers(compilation);
                    Volatile.Write(ref _sinkContainers, resolved);
                }

                return resolved;
            }
        }
    }
}
