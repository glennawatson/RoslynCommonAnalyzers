// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The empty-argument-parentheses form SST2268 normalizes to.</summary>
internal enum ObjectCreationParenthesesStyle
{
    /// <summary><c>new T { ... }</c> — no empty parentheses.</summary>
    Omit,

    /// <summary><c>new T() { ... }</c> — keep the empty parentheses.</summary>
    Include
}
