// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// The constructors an exception type is expected to declare (SST1488), as a flag set so one
/// diagnostic can name everything a type is missing and the code fix can read the same set back out
/// of the diagnostic's properties.
/// </summary>
[Flags]
internal enum StandardExceptionConstructors
{
    /// <summary>Nothing is missing.</summary>
    None = 0,

    /// <summary>The constructor that takes nothing.</summary>
    Parameterless = 1,

    /// <summary>The constructor that takes the message.</summary>
    Message = 2,

    /// <summary>The constructor that takes the message and the exception that caused it.</summary>
    MessageAndInner = 4,
}
