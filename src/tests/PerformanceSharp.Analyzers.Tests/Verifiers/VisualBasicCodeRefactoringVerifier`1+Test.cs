// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.VisualBasic.Testing;

namespace PerformanceSharp.Analyzers.Tests;

/// <content>
/// Contains the nested <see cref="Test"/> type used to run Visual Basic code refactoring verification.
/// </content>
public static partial class VisualBasicCodeRefactoringVerifier<TCodeRefactoring>
    where TCodeRefactoring : CodeRefactoringProvider, new()
{
    /// <summary>
    /// A Visual Basic code refactoring test used to verify the refactoring under test.
    /// </summary>
    public class Test : VisualBasicCodeRefactoringTest<TCodeRefactoring, DefaultVerifier>;
}
