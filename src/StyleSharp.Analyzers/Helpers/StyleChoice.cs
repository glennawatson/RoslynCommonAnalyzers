// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>One configurable style alternative and the lowercase option value that selects it.</summary>
/// <typeparam name="TStyle">The style enum.</typeparam>
/// <param name="Text">The lowercase option value.</param>
/// <param name="Style">The style the value selects.</param>
internal readonly record struct StyleChoice<TStyle>(string Text, TStyle Style);
