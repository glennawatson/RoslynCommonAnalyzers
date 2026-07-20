// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis.Testing;

using Verify = PerformanceSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    PerformanceSharp.Analyzers.Psh1419PreferBuiltInTimeZoneAnalyzer,
    PerformanceSharp.Analyzers.Psh1419PreferBuiltInTimeZoneCodeFixProvider>;

namespace PerformanceSharp.Analyzers.Tests;

/// <summary>Tests for <see cref="Psh1419PreferBuiltInTimeZoneAnalyzer"/> (PSH1419 prefer the built-in time-zone API).</summary>
public class PreferBuiltInTimeZoneAnalyzerUnitTest
{
    /// <summary>A minimal <c>TimeZoneConverter.TZConvert</c> surface under the real namespace, so the metadata-name probe resolves the source type.</summary>
    private const string TimeZoneConverterStub = """


        namespace TimeZoneConverter
        {
            public static class TZConvert
            {
                public static System.TimeZoneInfo GetTimeZoneInfo(string id) => System.TimeZoneInfo.Utc;

                public static string IanaToWindows(string ianaId) => ianaId;

                public static string WindowsToIana(string windowsId) => windowsId;
            }
        }
        """;

    /// <summary>Verifies a converter time-zone lookup is reported and rewritten to the built-in call.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task GetTimeZoneInfoIsFlaggedAndFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.TimeZoneInfo M(string id) => {|PSH1419:TimeZoneConverter.TZConvert.GetTimeZoneInfo(id)|};
                              }
                              """ + TimeZoneConverterStub;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.TimeZoneInfo M(string id) => System.TimeZoneInfo.FindSystemTimeZoneById(id);
                                   }
                                   """ + TimeZoneConverterStub;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies every converter time-zone lookup in a document is rewritten by the batch fix.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task MultipleGetTimeZoneInfoCallsAreFixedAsync()
    {
        const string Source = """
                              public class C
                              {
                                  public System.TimeZoneInfo First(string a) => {|PSH1419:TimeZoneConverter.TZConvert.GetTimeZoneInfo(a)|};

                                  public System.TimeZoneInfo Second(string b) => {|PSH1419:TimeZoneConverter.TZConvert.GetTimeZoneInfo(b)|};
                              }
                              """ + TimeZoneConverterStub;
        const string FixedSource = """
                                   public class C
                                   {
                                       public System.TimeZoneInfo First(string a) => System.TimeZoneInfo.FindSystemTimeZoneById(a);

                                       public System.TimeZoneInfo Second(string b) => System.TimeZoneInfo.FindSystemTimeZoneById(b);
                                   }
                                   """ + TimeZoneConverterStub;
        await VerifyAsync(Source, FixedSource);
    }

    /// <summary>Verifies the IANA-to-Windows id conversion is reported but left for a human to convert.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IanaToWindowsIsFlaggedWithoutFixAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public string M(string id) => {|PSH1419:TimeZoneConverter.TZConvert.IanaToWindows(id)|};
            }
            """ + TimeZoneConverterStub);

    /// <summary>Verifies the Windows-to-IANA id conversion is reported but left for a human to convert.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task WindowsToIanaIsFlaggedWithoutFixAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public string M(string id) => {|PSH1419:TimeZoneConverter.TZConvert.WindowsToIana(id)|};
            }
            """ + TimeZoneConverterStub);

    /// <summary>Verifies the id-conversion calls are silent on a framework without the conversion helpers.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task IdConversionMethodsAreCleanBeforeNet6Async()
    {
        const string Source = """
                              public class C
                              {
                                  public string ToWindows(string id) => TimeZoneConverter.TZConvert.IanaToWindows(id);

                                  public string ToIana(string id) => TimeZoneConverter.TZConvert.WindowsToIana(id);
                              }
                              """ + TimeZoneConverterStub;

        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.NetStandard.NetStandard20,
            TestCode = Source,
            FixedCode = Source,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Verifies a same-named static method of the user's own is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserStaticGetTimeZoneInfoIsCleanAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public static System.TimeZoneInfo GetTimeZoneInfo(string id) => System.TimeZoneInfo.Utc;

                public System.TimeZoneInfo M(string id) => GetTimeZoneInfo(id);
            }
            """ + TimeZoneConverterStub);

    /// <summary>Verifies a same-named instance method of the user's own is never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UserInstanceGetTimeZoneInfoIsCleanAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public System.TimeZoneInfo GetTimeZoneInfo(string id) => System.TimeZoneInfo.Utc;

                public System.TimeZoneInfo M(string id) => this.GetTimeZoneInfo(id);
            }
            """ + TimeZoneConverterStub);

    /// <summary>Verifies invocations that name no converter method are ignored on the clean path.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UnrelatedInvocationsAreCleanAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public void M(System.Func<int>[] fns)
                {
                    System.Console.WriteLine("x");
                    var first = fns[0]();
                    System.Action act = () => { };
                    act();
                }
            }
            """ + TimeZoneConverterStub);

    /// <summary>Verifies the rule stays silent in a project that does not reference the converter package.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RuleIsSilentWithoutPackageAsync()
        => await VerifyCleanAsync("""
            public class C
            {
                public static System.TimeZoneInfo GetTimeZoneInfo(string id) => System.TimeZoneInfo.Utc;

                public System.TimeZoneInfo M(string id) => GetTimeZoneInfo(id);
            }
            """);

    /// <summary>Runs a verification against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source with diagnostic markup.</param>
    /// <param name="fixedSource">The expected fixed source.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyAsync(string source, string fixedSource)
    {
        var test = new Verify.Test
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net90,
            TestCode = source,
            FixedCode = fixedSource,
        };

        await test.RunAsync(CancellationToken.None);
    }

    /// <summary>Runs a verification whose source is unchanged by any fix, against the .NET 9 reference assemblies.</summary>
    /// <param name="source">The source, which may carry report-only markup.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    private static async Task VerifyCleanAsync(string source) => await VerifyAsync(source, source);
}
