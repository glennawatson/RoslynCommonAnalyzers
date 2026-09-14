// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A constant pattern and the effective options its validation result is cached under.</summary>
/// <param name="Pattern">The constant pattern.</param>
/// <param name="Options">The effective regex options.</param>
internal readonly record struct RegexValidationKey(string Pattern, int Options);
