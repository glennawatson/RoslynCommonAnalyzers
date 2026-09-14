// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>A lookup key that materializes a string from a span.</summary>
/// <param name="Source">The span expression the string is built from.</param>
/// <param name="Description">The materializing call as shown in the diagnostic.</param>
internal readonly record struct KeyMaterialization(ExpressionSyntax Source, string Description);
