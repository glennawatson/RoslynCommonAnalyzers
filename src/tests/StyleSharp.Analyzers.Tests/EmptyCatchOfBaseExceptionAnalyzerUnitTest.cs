// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyEmptyCatch = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    StyleSharp.Analyzers.ExceptionHandlingAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST1429 (a catch of the base exception that swallows it).</summary>
public class EmptyCatchOfBaseExceptionAnalyzerUnitTest
{
    /// <summary>The editorconfig body that opts into reporting a catch whose body only returns a constant.</summary>
    private const string CheckConstantReturningCatchConfig = """
                                                             root = true
                                                             [*.cs]
                                                             stylesharp.check_constant_returning_catch = true

                                                             """;

    /// <summary>The source whose catch clauses only hand back a constant.</summary>
    private const string ConstantReturningCatchSource = """
                                                        using System;

                                                        public class C
                                                        {
                                                            public int Parse(string text)
                                                            {
                                                                try
                                                                {
                                                                    return int.Parse(text);
                                                                }
                                                                {|SST1429:catch|} (Exception)
                                                                {
                                                                    return 0;
                                                                }
                                                            }

                                                            public string Read(string path)
                                                            {
                                                                try
                                                                {
                                                                    return path;
                                                                }
                                                                {|SST1429:catch|}
                                                                {
                                                                    return default;
                                                                }
                                                            }

                                                            public void Run(Action action)
                                                            {
                                                                try
                                                                {
                                                                    action();
                                                                }
                                                                {|SST1429:catch|} (Exception)
                                                                {
                                                                    return;
                                                                }
                                                            }
                                                        }
                                                        """;

    /// <summary>Verifies an empty <c>catch (Exception)</c> and a bare empty <c>catch</c> are reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task EmptyBaseCatchesReportedAsync()
        => await VerifyEmptyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void Typed()
                {
                    try
                    {
                    }
                    {|SST1429:catch|} (Exception)
                    {
                    }
                }

                public void Bare()
                {
                    try
                    {
                    }
                    {|SST1429:catch|}
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a handled catch, a narrow catch, and a filtered catch are not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task HandledOrNarrowCatchesAreCleanAsync()
        => await VerifyEmptyCatch.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public void M()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine(ex);
                    }

                    try
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    try
                    {
                    }
                    catch (Exception) when (System.Environment.HasShutdownStarted)
                    {
                    }
                }
            }
            """);

    /// <summary>Verifies a catch that only returns a constant is reported once the option is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantReturningCatchesReportedWhenCheckedAsync()
    {
        var test = new VerifyEmptyCatch.Test { TestCode = ConstantReturningCatchSource };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", CheckConstantReturningCatchConfig));

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies the same catches are left alone by default, since the option is opt-in.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstantReturningCatchesAreCleanByDefaultAsync()
        => await VerifyEmptyCatch.VerifyAnalyzerAsync(
            ConstantReturningCatchSource.Replace("{|SST1429:catch|}", "catch", StringComparison.Ordinal));

    /// <summary>Verifies a catch that does more than return a constant stays clean even when the option is enabled.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task CatchesThatDoMoreThanReturnAConstantAreCleanAsync()
    {
        var test = new VerifyEmptyCatch.Test
        {
            TestCode = """
                       using System;

                       public class C
                       {
                           public int Logged(string text)
                           {
                               try
                               {
                                   return int.Parse(text);
                               }
                               catch (Exception ex)
                               {
                                   Console.WriteLine(ex);
                                   return 0;
                               }
                           }

                           public string Computed(string text)
                           {
                               try
                               {
                                   return text;
                               }
                               catch (Exception ex)
                               {
                                   return ex.Message;
                               }
                           }

                           public int Rethrown(string text)
                           {
                               try
                               {
                                   return int.Parse(text);
                               }
                               catch (Exception)
                               {
                                   throw;
                               }
                           }
                       }
                       """
        };
        test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", CheckConstantReturningCatchConfig));

        await test.RunAsync(CancellationToken.None);
    }
}
