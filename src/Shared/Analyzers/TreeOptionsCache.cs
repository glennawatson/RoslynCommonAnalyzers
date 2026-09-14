// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace RoslynCommon.Analyzers;

/// <summary>
/// Reads a rule's resolved settings for one syntax tree at most once per compilation. Every tree can carry
/// its own <c>.editorconfig</c> section, so settings are cached per tree rather than per compilation.
/// </summary>
/// <remarks>
/// A race between two callbacks on the same tree parses the options twice and keeps the first result; the
/// settings are immutable, so either copy is correct.
/// </remarks>
internal static class TreeOptionsCache
{
    /// <summary>Returns the cached settings for the node's tree, reading them on first demand.</summary>
    /// <typeparam name="TOptions">The resolved settings type.</typeparam>
    /// <param name="cache">The per-compilation cache.</param>
    /// <param name="context">The syntax node context whose tree and options are read.</param>
    /// <returns>The resolved settings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TOptions GetOrRead<TOptions>(ConcurrentDictionary<SyntaxTree, TOptions> cache, in SyntaxNodeAnalysisContext context)
        where TOptions : struct, ITreeOptions<TOptions> =>
        GetOrRead(cache, context.Node.SyntaxTree, context.Options);

    /// <summary>Returns the cached settings for a tree, reading them on first demand.</summary>
    /// <typeparam name="TOptions">The resolved settings type.</typeparam>
    /// <param name="cache">The per-compilation cache.</param>
    /// <param name="tree">The tree whose settings are wanted.</param>
    /// <param name="options">The analyzer options supplying each tree's configuration.</param>
    /// <returns>The resolved settings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TOptions GetOrRead<TOptions>(ConcurrentDictionary<SyntaxTree, TOptions> cache, SyntaxTree tree, AnalyzerOptions options)
        where TOptions : struct, ITreeOptions<TOptions> =>
        cache.TryGetValue(tree, out var resolved) ? resolved : Read(cache, tree, options);

    /// <summary>Parses and caches the settings for a tree seen for the first time.</summary>
    /// <typeparam name="TOptions">The resolved settings type.</typeparam>
    /// <param name="cache">The per-compilation cache.</param>
    /// <param name="tree">The tree whose settings are wanted.</param>
    /// <param name="options">The analyzer options supplying each tree's configuration.</param>
    /// <returns>The resolved settings.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static TOptions Read<TOptions>(ConcurrentDictionary<SyntaxTree, TOptions> cache, SyntaxTree tree, AnalyzerOptions options)
        where TOptions : struct, ITreeOptions<TOptions>
    {
        var resolved = default(TOptions).ReadFrom(options.AnalyzerConfigOptionsProvider.GetOptions(tree));
        _ = cache.TryAdd(tree, resolved);
        return resolved;
    }
}
