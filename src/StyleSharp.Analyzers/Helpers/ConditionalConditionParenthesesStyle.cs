// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The conditional-condition parentheses form SST2269 normalizes to.</summary>
internal enum ConditionalConditionParenthesesStyle
{
    /// <summary>Drop the parentheses when the condition is a single simple token.</summary>
    OmitWhenSingleToken,

    /// <summary>Keep parentheses around the condition.</summary>
    Include
}
