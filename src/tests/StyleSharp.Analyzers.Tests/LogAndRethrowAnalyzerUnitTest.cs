// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyLogAndRethrow = StyleSharp.Analyzers.Tests.CSharpAnalyzerVerifier<StyleSharp.Analyzers.Sst2488LogAndRethrowAnalyzer>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2488 (a catch that logs the caught exception and then rethrows it).</summary>
public class LogAndRethrowAnalyzerUnitTest
{
    /// <summary>Verifies a catch that logs on a logger-typed receiver and rethrows is reported, across its shapes.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task LoggerReceiverShapesAreReportedAsync()
        => await VerifyLogAndRethrow.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public interface ILogger
            {
                void LogError(Exception error, string message);
                void LogError(string message);
                void LogDebug(string message);
                void Error(Exception error, string message);
            }

            public sealed class C
            {
                private readonly ILogger _logger = null!;

                public void WithException()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _logger.LogError(ex, "failed");
                        throw;
                    }
                }

                public void WithoutException()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _logger.LogError("failed");
                        throw;
                    }
                }

                public void BareWordName()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _logger.Error(ex, "failed");
                        throw;
                    }
                }

                public void MultipleLogs()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _logger.LogError(ex, "failed");
                        _logger.LogDebug("context");
                        throw;
                    }
                }

                public void NullConditional()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _logger?.LogError(ex, "failed");
                        throw;
                    }
                }

                private static void Work()
                {
                }
            }
            """);

    /// <summary>Verifies the exception-passed route across an implicit receiver, a static host, and inherited logger names.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ExceptionPassedRoutesAreReportedAsync()
        => await VerifyLogAndRethrow.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public interface ILog
            {
                void Error(Exception error, string message);
            }

            public interface ILogger
            {
                void Error(Exception error, string message);
            }

            public sealed class ConsoleSink : ILogger
            {
                public void Error(Exception error, string message) { }
            }

            public class BaseLogger
            {
                public void Error(Exception error, string message) { }
            }

            public sealed class AppTracer : BaseLogger
            {
            }

            public static class Telemetry
            {
                public static void LogError(Exception error, string message) { }
                public static void LogError(string first, string second) { }
            }

            public sealed class C
            {
                private readonly ILog _log = null!;
                private readonly ConsoleSink _sink = null!;
                private readonly AppTracer _tracer = null!;

                public void ImplicitReceiver()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        LogError(ex, "failed");
                        throw;
                    }
                }

                public void StaticNonLogger()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        Telemetry.LogError(ex, "failed");
                        throw;
                    }
                }

                public void ExceptionMember()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        Telemetry.LogError(ex.Message, "context");
                        throw;
                    }
                }

                public void Log4NetStyle()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _log.Error(ex, "failed");
                        throw;
                    }
                }

                public void InterfaceImplemented()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _sink.Error(ex, "failed");
                        throw;
                    }
                }

                public void BaseTypeLogger()
                {
                    try { Work(); }
                    {|SST2488:catch|} (Exception ex)
                    {
                        _tracer.Error(ex, "failed");
                        throw;
                    }
                }

                private void LogError(Exception error, string message) { }

                private static void Work() { }
            }
            """);

    /// <summary>Verifies the shapes a neighbouring rule owns, and genuine handling, are all left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task AdjacentShapesAreCleanAsync()
        => await VerifyLogAndRethrow.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public interface ILogger
            {
                void LogError(Exception error, string message);
            }

            public sealed class Audit : IDisposable
            {
                public void Error(Exception error, string message) { }

                public void Dispose() { }
            }

            public static class Telemetry
            {
                public static void LogError(Exception error, string message) { }
                public static void LogError(Exception error) { }
            }

            public sealed class C
            {
                private readonly ILogger _logger = null!;
                private readonly Audit _audit = null!;
                private int _count;

                public void RethrowOnly()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        throw;
                    }
                }

                public void ThrowVariable()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "failed");
                        throw ex;
                    }
                }

                public void LogThenSwallow()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "failed");
                    }
                }

                public void OtherHandling()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "failed");
                        Cleanup();
                        throw;
                    }
                }

                public void LocalDeclaration()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        var code = ex.HResult;
                        Console.WriteLine(code);
                        throw;
                    }
                }

                public void Assignment()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _count = ex.HResult;
                        throw;
                    }
                }

                public void NonLoggingCall()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _logger.ToString();
                        throw;
                    }
                }

                public void CalleeWithoutName()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        Run()();
                        throw;
                    }
                }

                public void BareWordNonLogger()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        _audit.Error(ex, "failed");
                        throw;
                    }
                }

                public void StrongNameWithoutException()
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        Telemetry.LogError(new Exception());
                        throw;
                    }
                }

                public void StrongNamePassedOther(Exception other)
                {
                    try { Work(); }
                    catch (Exception ex)
                    {
                        Telemetry.LogError(other);
                        throw;
                    }
                }

                private static Action Run() => () => { };

                private static void Cleanup() { }

                private static void Work() { }
            }
            """);

    /// <summary>Verifies a catch that names no exception is reported on the logger route but not on the exception route.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnnamedCatchesAsync()
        => await VerifyLogAndRethrow.VerifyAnalyzerAsync(
            """
            #nullable enable

            using System;

            public interface ILogger
            {
                void LogError(string message);
            }

            public static class Telemetry
            {
                public static void LogError(Exception error) { }
            }

            public sealed class C
            {
                private readonly ILogger _logger = null!;

                public void BareCatchOnLogger()
                {
                    try { Work(); }
                    {|SST2488:catch|}
                    {
                        _logger.LogError("failed");
                        throw;
                    }
                }

                public void UnnamedCatchNonLogger()
                {
                    try { Work(); }
                    catch (Exception)
                    {
                        Telemetry.LogError(new Exception());
                        throw;
                    }
                }

                private static void Work() { }
            }
            """);
}
