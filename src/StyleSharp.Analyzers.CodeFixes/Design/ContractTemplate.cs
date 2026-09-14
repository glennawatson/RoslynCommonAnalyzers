// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The base type and member texts that implement a non-generic contract.</summary>
/// <param name="BaseType">The base type name, or <see langword="null"/> when the contract adds no base type.</param>
/// <param name="Members">The member declaration texts, or <see langword="null"/> for an unknown contract.</param>
internal readonly record struct ContractTemplate(string? BaseType, string[]? Members);
