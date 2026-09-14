// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace StyleSharp.Analyzers;

/// <summary>The token a member-level diagnostic is reported at, and the member name its message shows.</summary>
/// <param name="Token">The token to report at.</param>
/// <param name="Name">The member name as shown in the message.</param>
internal readonly record struct MemberNameToken(SyntaxToken Token, string Name);
