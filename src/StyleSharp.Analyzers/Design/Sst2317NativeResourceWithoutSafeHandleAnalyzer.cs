// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reports a disposable type that owns a native resource in a raw <c>IntPtr</c> field, releases it only
/// on the disposal path, and has no finalizer (SST2317). The resource then leaks whenever
/// <c>Dispose</c> is not called, and the fix is not a hand-written finalizer — it is a
/// <c>SafeHandle</c>, whose critical finalization, ref-counted release, and marshalling close the
/// use-after-free window.
/// </summary>
/// <remarks>
/// The prepass is ordered so a normal type exits early: the type must implement <c>IDisposable</c>, it
/// must have an instance field of a pointer-ish type, and it must have no finalizer. Only a type that
/// passes all three has its disposal path examined for the field being handed to a call — the proof
/// that the handle is an owned resource rather than an opaque cookie. The rule stays silent unless
/// <c>System.Runtime.InteropServices.SafeHandle</c> resolves, so the suggestion always compiles.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sst2317NativeResourceWithoutSafeHandleAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArrays.Of(DesignRules.NativeResourceWithoutSafeHandle);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(static start =>
        {
            if (DisposableTypes.Create(start.Compilation) is not { } types
                || start.Compilation.GetTypeByMetadataName("System.Runtime.InteropServices.SafeHandle") is null)
            {
                return;
            }

            start.RegisterSymbolAction(symbolContext => Analyze(symbolContext, types), SymbolKind.NamedType);
        });
    }

    /// <summary>Analyzes one named type for an owned native handle with no finalizer.</summary>
    /// <param name="context">The symbol analysis context.</param>
    /// <param name="types">The disposal types resolved for this compilation.</param>
    private static void Analyze(SymbolAnalysisContext context, in DisposableTypes types)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || !types.ImplementsSyncDisposable(type)
            || HasFinalizer(type))
        {
            return;
        }

        var members = type.GetMembers();
        var field = FindOwnedNativeField(members);
        if (field is null
            || !IsReleasedOnDisposalPath(members, field.Name, context.CancellationToken)
            || field.Locations is not [var location, ..])
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
    private static bool IsNativePointer(ITypeSymbol type)
        => type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr
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
                DescendantTraversalHelper.VisitDescendants<IdentifierNameSyntax, FieldArgumentScan>(references[j].GetSyntax(cancellationToken), ref scan, VisitFieldArgument);
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
}
