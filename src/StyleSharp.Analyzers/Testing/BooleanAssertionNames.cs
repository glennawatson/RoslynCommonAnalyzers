// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A test framework's equality assertion and the boolean assertions that replace it.</summary>
/// <param name="Equality">The equality assertion method name.</param>
/// <param name="True">The assertion that checks for <see langword="true"/>.</param>
/// <param name="False">The assertion that checks for <see langword="false"/>.</param>
internal readonly record struct BooleanAssertionNames(string Equality, string True, string False);
