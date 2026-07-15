// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    public static DisposableTypes? Create(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName("System.IDisposable") is not { } disposable)
        {
            return null;
        }

        return new DisposableTypes(
            disposable,
            compilation.GetTypeByMetadataName("System.IAsyncDisposable"),
            compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"));
    }

    /// <summary>Returns whether a created type is one a caller is expected to dispose.</summary>
    /// <param name="created">The type of the object that was created.</param>
    /// <returns><see langword="true"/> for a disposable reference type that is not a task.</returns>
    public bool IsOwnedDisposable(ITypeSymbol created)
        => !created.IsValueType && ImplementsDisposable(created) && !IsTask(created);

    /// <summary>Returns whether a type implements either disposal interface, or is one.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is disposable.</returns>
    public bool ImplementsDisposable(ITypeSymbol type)
        => Implements(type, Disposable) || (AsyncDisposable is not null && Implements(type, AsyncDisposable));

    /// <summary>Returns whether a type implements <see cref="System.IDisposable"/>, or is it.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is synchronously disposable.</returns>
    public bool ImplementsSyncDisposable(ITypeSymbol type) => Implements(type, Disposable);

    /// <summary>Returns whether a type implements <c>IAsyncDisposable</c>, or is it.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> when the type is asynchronously disposable.</returns>
    public bool ImplementsAsyncDisposable(ITypeSymbol type)
        => AsyncDisposable is not null && Implements(type, AsyncDisposable);

    /// <summary>Returns whether a type is, or implements, the supplied interface.</summary>
    /// <param name="type">The type to test.</param>
    /// <param name="interfaceType">The interface to look for.</param>
    /// <returns><see langword="true"/> when the type is or implements the interface.</returns>
    /// <remarks>
    /// A type's <c>AllInterfaces</c> does not include the type itself, so the interface has to be matched
    /// directly as well. It matters where a member is declared to hand back the interface rather than a
    /// concrete type — <c>IDisposable Start()</c> — which is the usual shape for a subscription.
    /// </remarks>
    public static bool Implements(ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, interfaceType))
        {
            return true;
        }

        var interfaces = type.AllInterfaces;
        for (var i = 0; i < interfaces.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(interfaces[i], interfaceType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether a type is a task, which implements the interface but is not meant to be disposed.</summary>
    /// <param name="type">The type to test.</param>
    /// <returns><see langword="true"/> for <c>Task</c> and anything deriving from it, including <c>Task&lt;T&gt;</c>.</returns>
    public bool IsTask(ITypeSymbol type)
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
