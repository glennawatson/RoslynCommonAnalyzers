// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace StyleSharp.Analyzers;

/// <summary>
/// The disposal types of one compilation, resolved once and shared by the rules that reason about
/// ownership of an <see cref="System.IDisposable"/> (or <c>IAsyncDisposable</c>): who must dispose a
/// created value, whether a type advertises disposal at all, and whether a value is a
/// <see cref="System.Threading.Tasks.Task"/> that implements the interface but must not be disposed.
/// </summary>
/// <param name="Disposable">The <see cref="System.IDisposable"/> interface, always present.</param>
/// <param name="AsyncDisposable">The <c>IAsyncDisposable</c> interface, when the target framework has one.</param>
/// <param name="Task">The <see cref="System.Threading.Tasks.Task"/> type, which is disposable but must not be disposed.</param>
internal readonly record struct DisposableTypes(
    INamedTypeSymbol Disposable,
    INamedTypeSymbol? AsyncDisposable,
    INamedTypeSymbol? Task)
{
    /// <summary>Resolves the disposal types for a compilation, or nothing when the framework has no <see cref="System.IDisposable"/>.</summary>
    /// <param name="compilation">The compilation to resolve against.</param>
    /// <returns>The resolved types, or <see langword="null"/> when disposal cannot be reasoned about at all.</returns>
    internal static DisposableTypes? Create(Compilation compilation) => compilation.GetTypeByMetadataName("System.IDisposable") is not { } disposable
        ? null
        : new DisposableTypes(
            disposable,
            compilation.GetTypeByMetadataName("System.IAsyncDisposable"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"));

    /// <summary>Returns whether a created type is one a caller is expected to dispose.</summary>
    /// <param name="created">The type of the object that was created.</param>
    /// <returns><see langword="true"/> for a disposable reference type that is not a task.</returns>
    internal bool IsOwnedDisposable(ITypeSymbol created) =>
        !created.IsValueType && ImplementsDisposable(created) && !IsTask(created);

    /// <summary>Returns whether a type implements either disposal interface, or is one.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is disposable.</returns>
    internal bool ImplementsDisposable(ITypeSymbol type) =>
        TypeRelations.IsOrImplements(type, Disposable) || (AsyncDisposable is not null && TypeRelations.IsOrImplements(type, AsyncDisposable));

    /// <summary>Returns whether a type implements <see cref="System.IDisposable"/>, or is it.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is synchronously disposable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ImplementsSyncDisposable(ITypeSymbol type) => TypeRelations.IsOrImplements(type, Disposable);

    /// <summary>Returns whether a type implements <c>IAsyncDisposable</c>, or is it.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is asynchronously disposable.</returns>
    internal bool ImplementsAsyncDisposable(ITypeSymbol type) =>
        AsyncDisposable is not null && TypeRelations.IsOrImplements(type, AsyncDisposable);

    /// <summary>Returns whether a type is a task, which implements the interface but is not meant to be disposed.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for <c>Task</c> and anything deriving from it, including <c>Task&lt;T&gt;</c>.</returns>
    internal bool IsTask(ITypeSymbol type)
    {
        if (Task is null)
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, Task))
            {
                return true;
            }
        }

        return false;
    }
}
