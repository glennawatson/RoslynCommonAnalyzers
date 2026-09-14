// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace RoslynCommon.Analyzers.CodeFixes;

/// <summary>Registers one diagnostic's batch edit through a code fix's own fix-all provider, the path fix-all takes.</summary>
internal static class BatchEditRegistration
{
    /// <summary>Registers the edits the provider's fix-all would make for one diagnostic.</summary>
    /// <typeparam name="TProvider">The code fix provider under test.</typeparam>
    /// <param name="editor">The document editor collecting the edits.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Register<TProvider>(DocumentEditor editor, Diagnostic diagnostic)
        where TProvider : CodeFixProvider, new() =>
        ((BatchEditFixAllProvider)new TProvider().GetFixAllProvider()!).RegisterBatchEdit(editor, diagnostic);
}
