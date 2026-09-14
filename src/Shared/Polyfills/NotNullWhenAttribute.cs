// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET
using System.Diagnostics;

namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Specifies that when a method returns the given <see cref="ReturnValue"/>, the
/// associated <c>out</c> parameter is not <see langword="null"/>. The netstandard2.0
/// analyzer assemblies only see Roslyn's internal copy of this attribute, so this
/// same-assembly copy makes it usable for nullable-flow annotations on <c>Try</c>
/// helpers.
/// </summary>
[ExcludeFromCodeCoverage]
[DebuggerNonUserCode]
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class NotNullWhenAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="NotNullWhenAttribute"/> class.</summary>
    /// <param name="returnValue">The return value for which the out parameter is guaranteed non-null.</param>
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

    /// <summary>Gets a value indicating whether the associated out parameter is non-null when the method returns this value.</summary>
    public bool ReturnValue { get; }
}

#else
using System.Diagnostics.CodeAnalysis;

[assembly: TypeForwardedTo(typeof(NotNullWhenAttribute))]
#endif
