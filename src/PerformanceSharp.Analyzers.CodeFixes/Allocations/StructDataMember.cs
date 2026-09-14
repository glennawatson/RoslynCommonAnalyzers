// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>A struct instance data member the generated equality compares.</summary>
/// <param name="Type">The member's written type.</param>
/// <param name="Name">The member's name.</param>
internal readonly record struct StructDataMember(string Type, string Name);
