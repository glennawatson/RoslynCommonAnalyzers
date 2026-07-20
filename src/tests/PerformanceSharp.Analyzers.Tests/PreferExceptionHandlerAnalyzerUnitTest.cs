// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using AnalyzeExceptionHandler = PerformanceSharp.Analyzers.Tests.CSharpAnalyzerVerifier<
    PerformanceSharp.Analyzers.Psh1505PreferExceptionHandlerAnalyzer>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Unit tests for PSH1505 (prefer IExceptionHandler over a legacy MVC exception filter).</summary>
public class PreferExceptionHandlerAnalyzerUnitTest
{
    /// <summary>The ASP.NET Core marker plus filter interface stubs, so the rule is gated on and can bind.</summary>
    private const string AspNetCoreStubs = """

                                           namespace Microsoft.AspNetCore.Diagnostics
                                           {
                                               public interface IExceptionHandler { }
                                           }

                                           namespace Microsoft.AspNetCore.Mvc.Filters
                                           {
                                               public interface IExceptionFilter { }
                                               public interface IAsyncExceptionFilter { }
                                           }
                                           """;

    /// <summary>The filter interface stubs without the modern IExceptionHandler marker, so the rule stays inert.</summary>
    private const string FilterStubsWithoutMarker = """

                                                    namespace Microsoft.AspNetCore.Mvc.Filters
                                                    {
                                                        public interface IExceptionFilter { }
                                                        public interface IAsyncExceptionFilter { }
                                                    }
                                                    """;

    /// <summary>Verifies a class implementing the synchronous MVC exception filter is reported (PSH1505).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task SyncExceptionFilterReportedAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Mvc.Filters;

                       public class {|PSH1505:LegacyFilter|} : IExceptionFilter
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a class implementing the asynchronous MVC exception filter is reported (PSH1505).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task AsyncExceptionFilterReportedAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Mvc.Filters;

                       public class {|PSH1505:LegacyAsyncFilter|} : IAsyncExceptionFilter
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a fully qualified filter interface in the base list is reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task FullyQualifiedFilterReportedAsync()
        => VerifyAsync("""
                       public class {|PSH1505:LegacyFilter|} : Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a class implementing both filter interfaces is reported once.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task ImplementsBothFiltersReportedOnceAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Mvc.Filters;

                       public class {|PSH1505:LegacyFilter|} : IExceptionFilter, IAsyncExceptionFilter
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a filter carried alongside another interface and a base class is still reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task FilterAmongOtherBaseTypesReportedAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Mvc.Filters;

                       public class BaseFilter { }

                       public interface IMarker { }

                       public class {|PSH1505:LegacyFilter|} : BaseFilter, IMarker, IExceptionFilter
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a same-named filter interface in another namespace is not reported (binding gate).</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task LookAlikeInterfaceNotReportedAsync()
        => VerifyAsync("""
                       public class NotAFilter : Other.IExceptionFilter
                       {
                       }

                       namespace Other
                       {
                           public interface IExceptionFilter { }
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies the rule stays silent when the modern IExceptionHandler marker is absent.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task SilentWhenMarkerAbsentAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Mvc.Filters;

                       public class LegacyFilter : IExceptionFilter
                       {
                       }
                       """ + FilterStubsWithoutMarker);

    /// <summary>Verifies a class implementing the modern IExceptionHandler is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task ModernExceptionHandlerNotReportedAsync()
        => VerifyAsync("""
                       using Microsoft.AspNetCore.Diagnostics;

                       public class ModernHandler : IExceptionHandler
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Verifies a class with no base list is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public Task ClassWithoutBaseListNotReportedAsync()
        => VerifyAsync("""
                       public class Plain
                       {
                       }
                       """ + AspNetCoreStubs);

    /// <summary>Runs an analyzer-only verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source)
    {
        var test = new AnalyzeExceptionHandler.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source
        };

        await test.RunAsync(CancellationToken.None);
    }
}
