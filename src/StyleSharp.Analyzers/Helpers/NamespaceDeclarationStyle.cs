// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The namespace declaration form SST2237 normalizes to.</summary>
/// <remarks>
/// One rule holds both directions on purpose. Two rules — one demanding each form — can both be
/// switched on, and then no file satisfies either; a single setting cannot contradict itself.
/// </remarks>
internal enum NamespaceDeclarationStyle
{
    /// <summary><c>namespace N;</c>.</summary>
    FileScoped,

    /// <summary><c>namespace N { ... }</c>.</summary>
    BlockScoped
}
