// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>An <see cref="Action{T1, T2}"/> whose first argument is passed by readonly reference.</summary>
/// <typeparam name="T1">The type of the argument passed by <c>in</c>, typically an analysis context struct.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <param name="arg1">The first argument, passed without a copy.</param>
/// <param name="arg2">The second argument.</param>
internal delegate void ActionIn<T1, in T2>(in T1 arg1, T2 arg2);
