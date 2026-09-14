// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>
/// Reads whether a property, indexer or event implements its accessors or only declares them. An accessor
/// with a block or an expression body supplies an implementation; a bodyless accessor is an auto-accessor
/// or an abstract declaration.
/// </summary>
internal static class AccessorBodies
{
    /// <summary>Returns whether any accessor in a list declares a body.</summary>
    /// <param name="accessors">The accessor list, if the member declares one.</param>
    /// <returns><see langword="true"/> when an accessor supplies an implementation.</returns>
    internal static bool AnyHasBody(AccessorListSyntax? accessors)
    {
        if (accessors is null)
        {
            return false;
        }

        var list = accessors.Accessors;
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Body is not null || list[i].ExpressionBody is not null)
            {
                return true;
            }
        }

        return false;
    }
}
