// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2325AsyncValidatesAfterAwaitAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2325 (an async method's argument guard stranded after its first await).</summary>
public class Sst2325AsyncValidatesAfterAwaitAnalyzerUnitTest
{
    /// <summary>Verifies a null check placed after the first await is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NullCheckAfterAwaitIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name)
                {
                    await Task.Yield();
                    if (name is null)
                    {
                        {|SST2325:throw new ArgumentNullException(nameof(name));|}
                    }
                }
            }
            """);

    /// <summary>Verifies a single-line, qualified-type range check after the first await is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task SingleLineRangeCheckAfterAwaitIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(int count)
                {
                    await Task.Delay(1);
                    if (count < 0)
                        {|SST2325:throw new System.ArgumentOutOfRangeException(nameof(count));|}
                }
            }
            """);

    /// <summary>Verifies an argument check reached through a local declaration's await is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardAfterLocalDeclarationAwaitIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> LoadAsync(string path)
                {
                    var length = await Task.FromResult(0);
                    if (path is null)
                    {
                        {|SST2325:throw new ArgumentException("path required", nameof(path));|}
                    }

                    return length;
                }
            }
            """);

    /// <summary>Verifies an argument check reached through an assignment's await is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardAfterAssignmentAwaitIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> LoadAsync(string path)
                {
                    int length;
                    length = await Task.FromResult(0);
                    if (path is null)
                    {
                        {|SST2325:throw new ArgumentNullException(nameof(path));|}
                    }

                    return length;
                }
            }
            """);

    /// <summary>Verifies a runtime throw-helper guard after the first await is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ThrowHelperAfterAwaitIsReportedAsync()
        => await VerifyNet80AnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name)
                {
                    await Task.Yield();
                    {|SST2325:ArgumentNullException.ThrowIfNull(name)|};
                }
            }
            """);

    /// <summary>Verifies only the guard after the await is reported when one precedes it too.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task OnlyTheGuardAfterAwaitIsReportedAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name, string path)
                {
                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }

                    await Task.Yield();
                    if (path is null)
                    {
                        {|SST2325:throw new ArgumentNullException(nameof(path));|}
                    }
                }
            }
            """);

    /// <summary>Verifies validation that precedes the first await is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ValidationBeforeAwaitIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name)
                {
                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }

                    await Task.Yield();
                }
            }
            """);

    /// <summary>Verifies a synchronous method that returns a task, validating up front, is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task NonAsyncMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public Task SaveAsync(string name)
                {
                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }

                    return Task.CompletedTask;
                }
            }
            """);

    /// <summary>Verifies a guard after the await that checks something other than a parameter is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardNotCheckingAParameterIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string value)
                {
                    var copy = value;
                    await Task.Yield();
                    if (copy is null)
                    {
                        throw new ArgumentNullException(nameof(copy));
                    }
                }
            }
            """);

    /// <summary>Verifies a guard nested inside a lambda after the await is clean, being that lambda's business.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GuardInsideNestedLambdaIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name)
                {
                    await Task.Yield();
                    Action validate = () =>
                    {
                        if (name is null)
                        {
                            throw new ArgumentNullException(nameof(name));
                        }
                    };

                    validate();
                }
            }
            """);

    /// <summary>Verifies a conditional await is not treated as the first-await boundary.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConditionalAwaitIsNotABoundaryIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task SaveAsync(string name, bool flag)
                {
                    if (flag)
                    {
                        await Task.Yield();
                    }

                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }
                }
            }
            """);

    /// <summary>Verifies an async void method is left to the rules that already cover that shape.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AsyncVoidIsIgnoredAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async void FireAndForget(string name)
                {
                    await Task.Yield();
                    if (name is null)
                    {
                        throw new ArgumentNullException(nameof(name));
                    }
                }
            }
            """);

    /// <summary>Verifies a parameterless async method with a post-await throw is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ParameterlessMethodIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System;
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task RunAsync()
                {
                    await Task.Yield();
                    if (DateTime.UtcNow.Year < 0)
                    {
                        throw new ArgumentException("never");
                    }
                }
            }
            """);

    /// <summary>Verifies an expression-bodied async method is clean.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExpressionBodiedAsyncIsCleanAsync()
        => await Verify.VerifyAnalyzerAsync(
            """
            using System.Threading.Tasks;

            public sealed class C
            {
                public async Task<int> LengthAsync(string text)
                    => await Task.FromResult(text.Length);
            }
            """);

    /// <summary>Runs an analyzer verification against the .NET 8 reference assemblies, where the throw-helpers exist.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyNet80AnalyzerAsync(string source)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = source,
        };

        await test.RunAsync(CancellationToken.None);
    }
}
