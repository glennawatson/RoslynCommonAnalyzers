// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>A compiled declaration whose attributes a scan test inspects.</summary>
/// <param name="Model">The semantic model for the declaration's tree.</param>
/// <param name="Compilation">The compilation that declares the marker types.</param>
/// <param name="AttributeLists">The declaration's attribute lists.</param>
internal readonly record struct ScannedDeclaration(SemanticModel Model, CSharpCompilation Compilation, SyntaxList<AttributeListSyntax> AttributeLists);
