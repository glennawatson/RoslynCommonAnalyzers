// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeScript = SecuritySharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    SecuritySharp.Analyzers.Ses1306DynamicScriptCompilationAnalyzer>;

namespace SecuritySharp.Analyzers.Tests;

/// <summary>Unit tests for SES1306 (do not compile or execute non-constant C# via the scripting API).</summary>
public class DynamicScriptCompilationAnalyzerUnitTest
{
    /// <summary>An inline stand-in for the C# scripting entry-point class, supplying the gated well-known type.</summary>
    private const string ScriptingStub = """
        namespace Microsoft.CodeAnalysis.CSharp.Scripting
        {
            public static class CSharpScript
            {
                public static System.Threading.Tasks.Task<T> EvaluateAsync<T>(string code) => default!;

                public static System.Threading.Tasks.Task RunAsync(string code) => default!;

                public static object Create(string code) => default!;
            }
        }
        """;

    /// <summary>Verifies a runtime variable passed to <c>EvaluateAsync</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantVariableToEvaluateAsyncReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M(string userInput)
                {
                    await CSharpScript.EvaluateAsync<int>({|SES1306:userInput|});
                }
            }
            """);

    /// <summary>Verifies a concatenation that folds in a variable passed to <c>RunAsync</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConcatenationWithVariableToRunAsyncReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M(string userInput)
                {
                    await CSharpScript.RunAsync({|SES1306:"Console.WriteLine(" + userInput + ");"|});
                }
            }
            """);

    /// <summary>Verifies a runtime variable passed to <c>Create</c> is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonConstantToCreateReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public void M(string userInput)
                {
                    CSharpScript.Create({|SES1306:userInput|});
                }
            }
            """);

    /// <summary>Verifies the code argument passed by name is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NamedCodeArgumentReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M(string userInput)
                {
                    await CSharpScript.EvaluateAsync<int>(code: {|SES1306:userInput|});
                }
            }
            """);

    /// <summary>Verifies an interpolated string that folds in a variable is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task InterpolationWithVariableReportedAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M(int seed)
                {
                    await CSharpScript.EvaluateAsync<int>({|SES1306:$"{seed} + 1"|});
                }
            }
            """);

    /// <summary>Verifies a constant string literal script is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantLiteralIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M()
                {
                    await CSharpScript.EvaluateAsync<int>("1 + 1");
                }
            }
            """);

    /// <summary>Verifies a <c>const</c> reference script is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstReferenceIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                private const string Template = "1 + 1";

                public async System.Threading.Tasks.Task M()
                {
                    await CSharpScript.EvaluateAsync<int>(Template);
                }
            }
            """);

    /// <summary>Verifies a concatenation of two constants (folded to a constant) is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantConcatenationIsCleanAsync()
        => await VerifyAsync(
            """
            using Microsoft.CodeAnalysis.CSharp.Scripting;

            public class C
            {
                public async System.Threading.Tasks.Task M()
                {
                    await CSharpScript.RunAsync("1 + " + "1");
                }
            }
            """);

    /// <summary>Verifies a same-named method on an unrelated type is not reported (only CSharpScript is gated).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedEvaluateAsyncIsCleanAsync()
        => await VerifyAsync(
            """
            public static class Calculator
            {
                public static System.Threading.Tasks.Task<int> EvaluateAsync<T>(string code) => default!;
            }

            public class C
            {
                public async System.Threading.Tasks.Task M(string userInput)
                {
                    await Calculator.EvaluateAsync<int>(userInput);
                }
            }
            """);

    /// <summary>Verifies the rule stays silent when the scripting class is not the gated well-known type.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SilentWhenScriptingUnavailableAsync()
    {
        const string Source = """
                              namespace MyScripting
                              {
                                  public static class CSharpScript
                                  {
                                      public static System.Threading.Tasks.Task<T> EvaluateAsync<T>(string code) => default!;
                                  }
                              }

                              public class C
                              {
                                  public async System.Threading.Tasks.Task M(string userInput)
                                  {
                                      await MyScripting.CSharpScript.EvaluateAsync<int>(userInput);
                                  }
                              }
                              """;

        var test = new AnalyzeScript.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = Source
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs an analyzer-only verification against .NET 9, appending the inline scripting stub to the source.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        // The stub follows the source so the source's own using directives still precede all namespaces.
        var test = new AnalyzeScript.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source + "\n\n" + ScriptingStub
        };

        await test.RunAsync(CancellationToken.None);
    }
}
