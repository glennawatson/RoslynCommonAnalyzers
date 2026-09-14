// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>A <see cref="Func{T1, T2, TResult}"/> whose first argument is passed by readonly reference and whose second is passed by reference.</summary>
/// <typeparam name="T1">The type of the argument passed by <c>in</c>, typically a syntax token.</typeparam>
/// <typeparam name="T2">The type of the argument passed by <c>ref</c>, typically caller state threaded through a walk.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <param name="arg1">The first argument, passed without a copy.</param>
/// <param name="arg2">The second argument, which the callee may overwrite.</param>
/// <returns>The result.</returns>
internal delegate TResult FuncInRef<T1, T2, out TResult>(in T1 arg1, ref T2 arg2);
