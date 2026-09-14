// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>An argument and its position in the argument list.</summary>
/// <param name="Argument">The argument, or <see langword="null"/> when the shape does not match.</param>
/// <param name="Index">The argument's zero-based position, or -1 when the shape does not match.</param>
internal readonly record struct PositionedArgument(ArgumentSyntax? Argument, int Index);
