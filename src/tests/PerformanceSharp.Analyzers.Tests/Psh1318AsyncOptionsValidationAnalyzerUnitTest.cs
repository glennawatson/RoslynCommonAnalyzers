// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using VerifyAsyncOptions = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1318AsyncOptionsValidationAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for the asynchronous options-validation rule (PSH1318).</summary>
public class Psh1318AsyncOptionsValidationAnalyzerUnitTest
{
    /// <summary>
    /// Directives and stand-in options-validation interfaces, which live outside the base class
    /// library. Every test appends its own type to this.
    /// </summary>
    private const string OptionsApi = """
                                      #nullable enable
                                      using Microsoft.Extensions.Options;
                                      using System.Threading.Tasks;

                                      namespace Microsoft.Extensions.Options
                                      {
                                          public class ValidateOptionsResult
                                          {
                                              public static readonly ValidateOptionsResult Success = new ValidateOptionsResult();
                                          }

                                          public interface IValidateOptions<TOptions>
                                              where TOptions : class
                                          {
                                              ValidateOptionsResult Validate(string? name, TOptions options);
                                          }

                                          public interface IAsyncValidateOptions<TOptions>
                                              where TOptions : class
                                          {
                                              ValueTask<ValidateOptionsResult> ValidateAsync(string? name, TOptions options);
                                          }
                                      }

                                      public class MyOptions
                                      {
                                      }


                                      """;

    /// <summary>Verifies a validator that blocks on a task is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task BlockingValidatorReportedAsync() =>
        VerifyAsyncOptions.VerifyAnalyzerAsync(OptionsApi + """
            public class MyValidator : IValidateOptions<MyOptions>
            {
                public ValidateOptionsResult {|PSH1318:Validate|}(string? name, MyOptions options)
                {
                    Task.Delay(1).Wait();
                    return ValidateOptionsResult.Success;
                }
            }
            """);

    /// <summary>Verifies a validator that reads a task result is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task ResultReadReportedAsync() =>
        VerifyAsyncOptions.VerifyAnalyzerAsync(OptionsApi + """
            public class MyValidator : IValidateOptions<MyOptions>
            {
                public ValidateOptionsResult {|PSH1318:Validate|}(string? name, MyOptions options)
                {
                    var ready = Task.FromResult(true).Result;
                    return ready ? ValidateOptionsResult.Success : ValidateOptionsResult.Success;
                }
            }
            """);

    /// <summary>Verifies a validator that does no asynchronous work is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task SynchronousValidatorIsCleanAsync() =>
        VerifyAsyncOptions.VerifyAnalyzerAsync(OptionsApi + """
            public class MyValidator : IValidateOptions<MyOptions>
            {
                public ValidateOptionsResult Validate(string? name, MyOptions options)
                {
                    return ValidateOptionsResult.Success;
                }
            }
            """);

    /// <summary>Verifies a validator that already offers the asynchronous interface is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task AlreadyAsyncValidatorIsCleanAsync() =>
        VerifyAsyncOptions.VerifyAnalyzerAsync(OptionsApi + """
            public class MyValidator : IValidateOptions<MyOptions>, IAsyncValidateOptions<MyOptions>
            {
                public ValidateOptionsResult Validate(string? name, MyOptions options)
                {
                    Task.Delay(1).Wait();
                    return ValidateOptionsResult.Success;
                }

                public ValueTask<ValidateOptionsResult> ValidateAsync(string? name, MyOptions options)
                    => new ValueTask<ValidateOptionsResult>(ValidateOptionsResult.Success);
            }
            """);

    /// <summary>Verifies a blocking method on an unrelated type is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Test]
    public Task UnrelatedValidateIsCleanAsync() =>
        VerifyAsyncOptions.VerifyAnalyzerAsync(OptionsApi + """
            public class Checker
            {
                public bool Validate(string? name)
                {
                    Task.Delay(1).Wait();
                    return true;
                }
            }
            """);
}
