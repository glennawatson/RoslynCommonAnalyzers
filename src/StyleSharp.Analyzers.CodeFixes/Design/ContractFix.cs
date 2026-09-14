// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The type declaration a non-generic contract is added to, and the contract to add.</summary>
/// <param name="Declaration">The type declaration.</param>
/// <param name="Contract">The contract to add.</param>
/// <param name="Argument">The fully-qualified generic type argument.</param>
internal readonly record struct ContractFix(TypeDeclarationSyntax Declaration, string Contract, string Argument);
