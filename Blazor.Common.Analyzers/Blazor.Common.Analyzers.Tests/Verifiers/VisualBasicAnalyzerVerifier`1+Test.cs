// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

namespace Blazor.Common.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run Visual Basic analyzer verification.
/// </content>
public static partial class VisualBasicAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    /// <summary>
    /// A Visual Basic analyzer test used to verify the analyzer under test.
    /// </summary>
    public class Test : VisualBasicAnalyzerTest<TAnalyzer, DefaultVerifier>;
}
