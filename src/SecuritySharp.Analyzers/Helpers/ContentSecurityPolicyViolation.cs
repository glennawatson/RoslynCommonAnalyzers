// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>Identifies the effective directive and the capability its sources permit.</summary>
/// <param name="Directive">The normalized name of the directive providing the source list.</param>
/// <param name="Permission">The permitted capability described to the developer.</param>
internal readonly record struct ContentSecurityPolicyViolation(string Directive, ContentSecurityPolicyParser.Permission Permission);
