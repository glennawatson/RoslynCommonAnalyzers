// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a disposable type that owns a native resource in a raw <c>IntPtr</c> field, releases it only
/// on the disposal path, and has no finalizer (SST2317). The resource then leaks whenever
/// <c>Dispose</c> is not called, and the fix is not a hand-written finalizer — it is a
/// <c>SafeHandle</c>, whose critical finalization, ref-counted release, and marshalling close the
/// use-after-free window.
/// </summary>
/// <remarks>
/// The prepass is ordered so a normal type exits before metadata resolution: it must have no finalizer
/// and must have an instance field of a pointer-ish type passed to a call on the disposal path — the
/// proof that the handle is an owned resource rather than an opaque cookie. Only then are the disposal
/// types resolved to check that the type implements <c>IDisposable</c>. The rule stays silent unless
/// <c>System.Runtime.InteropServices.SafeHandle</c> resolves, so the suggestion always compiles.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2317NativeResourceWithoutSafeHandleAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The descriptors this analyzer reports, built once rather than on every access.</summary>
    private static readonly ImmutableArray<DiagnosticDescriptor> SupportedDiagnosticsValue = ImmutableArrays.Of(DesignRules.NativeResourceWithoutSafeHandle);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SupportedDiagnosticsValue;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            var types = new NativeResourceTypes(start.Compilation);
            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, types), SymbolKind.NamedType);
        });
    }

    /// <summary>Analyzes one named type for an owned native handle with no finalizer.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The disposal and safe-handle types resolved on first demand.</param>
    private static void Analyze(in SymbolAnalysisContext context, NativeResourceTypes types)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || HasFinalizer(type))
        {
            return;
        }

        var members = type.GetMembers();
        var field = FindOwnedNativeField(members);
        if (field is null
            || !IsReleasedOnDisposalPath(members, field.Name, context.CancellationToken)
            || field.Locations is not [var location, ..]
            || types.Get() is not { } resolved
            || !resolved.ImplementsSyncDisposable(type))
        {
            return;
        }

        context.ReportDiagnostic(DiagnosticHelper.Create(DesignRules.NativeResourceWithoutSafeHandle, location, type.Name, field.Name));
    }

    /// <summary>Returns whether a type declares a finalizer.</summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns><see langword="true"/> when the type has a destructor.</returns>
    private static bool HasFinalizer(INamedTypeSymbol type)
    {
        var members = type.GetMembers();
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IMethodSymbol { MethodKind: MethodKind.Destructor })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds an instance field whose type is a raw native pointer.</summary>
    /// <param name="members">The type's members.</param>
    /// <returns>The native field, or <see langword="null"/>.</returns>
    private static IFieldSymbol? FindOwnedNativeField(ImmutableArray<ISymbol> members)
    {
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is IFieldSymbol { IsStatic: false, IsConst: false } field && IsNativePointer(field.Type))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Returns whether a type is a raw native pointer that a SafeHandle would replace.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><see langword="true"/> for <c>IntPtr</c>, <c>UIntPtr</c>, <c>nint</c>, <c>nuint</c>, or a pointer type.</returns>
    private static bool IsNativePointer(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr
            || type.TypeKind == TypeKind.Pointer;

    /// <summary>Returns whether the native field is handed to a call inside a disposal method.</summary>
    /// <param name="members">The type's members.</param>
    /// <param name="fieldName">The native field name.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when the field is released on the disposal path.</returns>
    /// <remarks>
    /// The scan is syntactic and name-based: the field, the disposal method, and the release call all
    /// belong to the same type, so a name match on the field cannot collide with an unrelated symbol.
    /// </remarks>
    private static bool IsReleasedOnDisposalPath(ImmutableArray<ISymbol> members, string fieldName, CancellationToken cancellationToken)
    {
        for (var i = 0; i < members.Length; i++)
        {
            if (members[i] is not IMethodSymbol { Name: "Dispose" or "DisposeAsync" } method)
            {
                continue;
            }

            var references = method.DeclaringSyntaxReferences;
            for (var j = 0; j < references.Length; j++)
            {
                var scan = new FieldArgumentScan(fieldName);
                _ = DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, FieldArgumentScan>(references[j].GetSyntax(cancellationToken), ref scan, VisitFieldArgument);
                if (scan.Found)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Records the field being handed to a call, stopping the walk once found.</summary>
    /// <param name="identifier">The identifier being visited.</param>
    /// <param name="state">The scan state.</param>
    /// <returns><see langword="false"/> once the field is found as a call argument.</returns>
    private static bool VisitFieldArgument(IdentifierNameSyntax identifier, ref FieldArgumentScan state)
    {
        if (identifier.Identifier.ValueText != state.FieldName || !IsInvocationArgument(identifier))
        {
            return true;
        }

        state.Found = true;
        return false;
    }

    /// <summary>Returns whether an identifier is passed as an argument to an invocation.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns><see langword="true"/> when it is an argument of a call, directly or through <c>this.</c>.</returns>
    private static bool IsInvocationArgument(IdentifierNameSyntax identifier)
    {
        var expression = identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier
            ? (ExpressionSyntax)access
            : identifier;

        return expression.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax } };
    }

    /// <summary>The state threaded through the disposal-path field scan.</summary>
    /// <param name="FieldName">The native field name.</param>
    private record struct FieldArgumentScan(string FieldName)
    {
        /// <summary>Gets or sets a value indicating whether the field was found as a call argument.</summary>
        public bool Found { get; set; }
    }

    /// <summary>Resolves framework types once per compilation, only for an owned native field.</summary>
    /// <param name="compilation">The compilation being analyzed.</param>
    private sealed class NativeResourceTypes(Compilation compilation)
    {
        /// <summary>The cached disposal types, including an unavailable framework or safe handle.</summary>
        private DisposableTypes?[]? _resolved;

        /// <summary>Gets the disposal types when the framework also provides a safe handle.</summary>
        /// <returns>The disposal types, or <see langword="null"/> when the required types are unavailable.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DisposableTypes? Get() => (_resolved ??= [Resolve(compilation)])[0];

        /// <summary>Resolves the disposal types and verifies safe-handle support.</summary>
        /// <param name="compilation">The compilation being analyzed.</param>
        /// <returns>The disposal types, or <see langword="null"/> when the required types are unavailable.</returns>
        private static DisposableTypes? Resolve(Compilation compilation)
        {
            var types = DisposableTypes.Create(compilation);
            return types is not null && compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle") is not null
                ? types
                : null;
        }
    }
}
