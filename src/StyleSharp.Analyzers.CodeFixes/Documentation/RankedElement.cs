// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A documentation element with a conventional rank, and where it sits among the comment's content.</summary>
/// <param name="Rank">The element's rank in the conventional order.</param>
/// <param name="Position">The element's index in the comment's content.</param>
/// <param name="Node">The element.</param>
internal readonly record struct RankedElement(int Rank, int Position, XmlNodeSyntax Node);
