// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

namespace SecuritySharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run Visual Basic code fix verification.
/// </content>
public static partial class VisualBasicCodeFixVerifier<TAnalyzer, TCodeFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TCodeFix : CodeFixProvider, new()
{
    /// <summary>
    /// A Visual Basic code fix test used to verify the analyzer and code fix under test.
    /// </summary>
    public class Test : VisualBasicCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>;
}
