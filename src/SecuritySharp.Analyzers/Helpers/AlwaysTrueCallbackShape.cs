// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace SecuritySharp.Analyzers;

/// <summary>The shapes a callback or predicate value can take that let an always-true rule report it.</summary>
internal enum AlwaysTrueCallbackShape
{
    /// <summary>Not a reportable shape.</summary>
    None = 0,

    /// <summary>A lambda or anonymous method already known to always return true.</summary>
    AlwaysTrueLambda = 1,

    /// <summary>A method group whose referenced method still needs to be inspected.</summary>
    MethodGroup = 2,
}
