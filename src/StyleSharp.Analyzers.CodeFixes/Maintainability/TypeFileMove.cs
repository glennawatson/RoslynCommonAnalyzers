// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A type declaration and the file it moves into.</summary>
/// <param name="Type">The type declaration.</param>
/// <param name="FileName">The target file name.</param>
internal readonly record struct TypeFileMove(BaseTypeDeclarationSyntax Type, string FileName);
