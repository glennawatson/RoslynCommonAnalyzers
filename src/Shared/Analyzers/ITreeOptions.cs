// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>A rule's settings, read from one syntax tree's editorconfig options and cached per tree by <see cref="TreeOptionsCache"/>.</summary>
/// <typeparam name="TSelf">The settings type itself.</typeparam>
/// <remarks>
/// Implemented by a record struct, so the cache reads through a constrained call on <c>default(TSelf)</c> rather
/// than through a callback.
/// </remarks>
internal interface ITreeOptions<TSelf>
    where TSelf : struct, ITreeOptions<TSelf>
{
    /// <summary>Reads the settings from one tree's options.</summary>
    /// <param name="options">The analyzer config options for the tree.</param>
    /// <returns>The resolved settings.</returns>
    TSelf ReadFrom(AnalyzerConfigOptions options);
}
