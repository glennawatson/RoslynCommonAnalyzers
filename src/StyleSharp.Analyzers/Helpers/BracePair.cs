// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The opening and closing braces a brace-bearing node carries.</summary>
/// <param name="Open">The opening brace, or a default token when the node carries none.</param>
/// <param name="Close">The closing brace, or a default token when the node carries none.</param>
internal readonly record struct BracePair(SyntaxToken Open, SyntaxToken Close);
