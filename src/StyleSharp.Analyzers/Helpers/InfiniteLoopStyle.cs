// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The infinite-loop form SST2267 normalizes to.</summary>
internal enum InfiniteLoopStyle
{
    /// <summary><c>while (true)</c>.</summary>
    While,

    /// <summary><c>for (;;)</c>.</summary>
    For
}
