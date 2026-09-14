// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>A reference to a member of the analyzed type, with the symbol it binds to.</summary>
/// <param name="Symbol">The symbol resolved at the reference site.</param>
/// <param name="Name">The reference name syntax.</param>
internal sealed record PrivateMemberReference(ISymbol Symbol, SimpleNameSyntax Name);
