// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Reserved to be used by the compiler for tracking metadata. Enables <c>init</c>
/// accessors (and therefore records) on the netstandard2.0 analyzer assemblies so
/// the library can use records instead of hand-written <c>IEquatable&lt;T&gt;</c>.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
internal static class IsExternalInit;

#else
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(IsExternalInit))]
#endif
