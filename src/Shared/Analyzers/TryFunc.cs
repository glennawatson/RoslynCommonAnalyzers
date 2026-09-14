// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>The <c>Try</c> pattern as a delegate: reports success and returns the result through an <c>out</c> argument.</summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <param name="arg1">The first argument.</param>
/// <param name="arg2">The second argument.</param>
/// <param name="result">The result, not <see langword="null"/> when the call succeeds.</param>
/// <returns><see langword="true"/> when a result was produced.</returns>
internal delegate bool TryFunc<in T1, in T2, TResult>(T1 arg1, T2 arg2, [NotNullWhen(true)] out TResult? result);

/// <summary>The <c>Try</c> pattern as a delegate: reports success and returns the result through an <c>out</c> argument.</summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="T3">The type of the third argument.</typeparam>
/// <typeparam name="T4">The type of the fourth argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <param name="arg1">The first argument.</param>
/// <param name="arg2">The second argument.</param>
/// <param name="arg3">The third argument.</param>
/// <param name="arg4">The fourth argument.</param>
/// <param name="result">The result, not <see langword="null"/> when the call succeeds.</param>
/// <returns><see langword="true"/> when a result was produced.</returns>
internal delegate bool TryFunc<in T1, in T2, in T3, in T4, TResult>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, [NotNullWhen(true)] out TResult? result);
