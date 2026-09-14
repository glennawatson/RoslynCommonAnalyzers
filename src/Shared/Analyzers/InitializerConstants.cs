// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace RoslynCommon.Analyzers;

/// <summary>
/// Decides whether an array initializer holds only compile-time constants, shared by the rules that treat such
/// an initializer as fixed data: a performance rule hoisting it, and security rules flagging a fixed nonce or salt.
/// </summary>
internal static class InitializerConstants
{
    /// <summary>Returns whether every element of an initializer binds to a compile-time constant.</summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="initializer">The initializer whose elements to verify.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> when every element has a constant value, including for an empty initializer.</returns>
    internal static bool AreAllConstant(SemanticModel model, InitializerExpressionSyntax initializer, CancellationToken cancellationToken)
    {
        var expressions = initializer.Expressions;
        for (var i = 0; i < expressions.Count; i++)
        {
            if (!model.GetConstantValue(expressions[i], cancellationToken).HasValue)
            {
                return false;
            }
        }

        return true;
    }
}
