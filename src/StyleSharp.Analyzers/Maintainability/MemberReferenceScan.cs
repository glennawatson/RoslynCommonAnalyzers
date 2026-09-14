// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The inputs a scan for references to a type's members reads.</summary>
/// <param name="Usage">The type usage receiving each reference.</param>
/// <param name="Model">The semantic model.</param>
/// <param name="MemberNames">The names of every member the type declares.</param>
/// <param name="CancellationToken">A token that cancels binding.</param>
internal readonly record struct MemberReferenceScan(PrivateTypeUsage Usage, SemanticModel Model, HashSet<string> MemberNames, CancellationToken CancellationToken);
