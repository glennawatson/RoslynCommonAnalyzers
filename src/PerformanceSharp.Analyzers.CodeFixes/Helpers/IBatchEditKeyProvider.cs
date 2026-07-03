// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PerformanceSharp.Analyzers;

/// <summary>Provides a resolved edit key for batch fixes whose diagnostic span is smaller than the edited node.</summary>
internal interface IBatchEditKeyProvider
{
    /// <summary>Gets the syntax span that will be edited for one diagnostic.</summary>
    /// <param name="root">The syntax root.</param>
    /// <param name="diagnostic">The diagnostic to inspect.</param>
    /// <param name="span">The syntax span that identifies the edit target.</param>
    /// <returns><see langword="true"/> when an edit target can be resolved.</returns>
    bool TryGetBatchEditSpan(SyntaxNode root, Diagnostic diagnostic, out TextSpan span);
}
