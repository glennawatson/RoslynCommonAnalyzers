// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The type declaration a fix rewrites and the diagnostic property value it applies.</summary>
/// <param name="Declaration">The type declaration.</param>
/// <param name="Value">The diagnostic property value.</param>
internal readonly record struct TypeDeclarationFix(TypeDeclarationSyntax Declaration, string Value);
