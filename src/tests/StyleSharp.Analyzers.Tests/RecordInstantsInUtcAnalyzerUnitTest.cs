// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using VerifyUtc = StyleSharp.Analyzers.Tests.CSharpCodeFixVerifier<
    StyleSharp.Analyzers.Sst2011RecordInstantsInUtcAnalyzer,
    StyleSharp.Analyzers.Sst2011RecordInstantsInUtcCodeFixProvider>;

namespace StyleSharp.Analyzers.Tests;

/// <summary>Unit tests for SST2011 (record instants in UTC) and its fix.</summary>
public class RecordInstantsInUtcAnalyzerUnitTest
{
    /// <summary>Verifies every recording position is reported and rewritten to the UTC clock.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordedLocalInstantsAreRewrittenAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private DateTime _initialized = {|SST2011:DateTime.Now|};

                                  private DateTime _assigned;

                                  public DateTime Created { get; set; } = {|SST2011:DateTime.Now|};

                                  public DateTimeOffset Stamp => {|SST2011:DateTimeOffset.Now|};

                                  public void Touch() => _assigned = {|SST2011:DateTime.Now|};

                                  public DateTime Read()
                                  {
                                      return {|SST2011:DateTime.Now|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private DateTime _initialized = DateTime.UtcNow;

                                       private DateTime _assigned;

                                       public DateTime Created { get; set; } = DateTime.UtcNow;

                                       public DateTimeOffset Stamp => DateTimeOffset.UtcNow;

                                       public void Touch() => _assigned = DateTime.UtcNow;

                                       public DateTime Read()
                                       {
                                           return DateTime.UtcNow;
                                       }
                                   }
                                   """;

        await VerifyUtc.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a recorded DateTime.Today — local midnight — is reported and rewritten to the UTC date.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task RecordedTodayIsRewrittenToTheUtcDateAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private DateTime _initialized = {|SST2011:DateTime.Today|};

                                  private DateTime _assigned;

                                  public DateTime Started { get; set; } = {|SST2011:DateTime.Today|};

                                  public DateTime Day => {|SST2011:DateTime.Today|};

                                  public void Touch() => _assigned = {|SST2011:DateTime.Today|};

                                  public DateTime Read()
                                  {
                                      return {|SST2011:DateTime.Today|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private DateTime _initialized = DateTime.UtcNow.Date;

                                       private DateTime _assigned;

                                       public DateTime Started { get; set; } = DateTime.UtcNow.Date;

                                       public DateTime Day => DateTime.UtcNow.Date;

                                       public void Touch() => _assigned = DateTime.UtcNow.Date;

                                       public DateTime Read()
                                       {
                                           return DateTime.UtcNow.Date;
                                       }
                                   }
                                   """;

        await VerifyUtc.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies a recorded DateTimeOffset.Now.DateTime — the local time with the offset thrown away — is reported and rewritten to the UTC one.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>UtcDateTime, not DateTime: the ticks agree once the clock is UtcNow, but only UtcDateTime carries DateTimeKind.Utc.</remarks>
    [Test]
    public async Task RecordedLocalDateTimeOffOfAnOffsetIsRewrittenToUtcDateTimeAsync()
    {
        const string Source = """
                              using System;

                              public class C
                              {
                                  private DateTime _initialized = {|SST2011:DateTimeOffset.Now.DateTime|};

                                  public DateTime Stamp => {|SST2011:DateTimeOffset.Now.DateTime|};

                                  public DateTime Read()
                                  {
                                      return {|SST2011:DateTimeOffset.Now.DateTime|};
                                  }
                              }
                              """;
        const string FixedSource = """
                                   using System;

                                   public class C
                                   {
                                       private DateTime _initialized = DateTimeOffset.UtcNow.UtcDateTime;

                                       public DateTime Stamp => DateTimeOffset.UtcNow.UtcDateTime;

                                       public DateTime Read()
                                       {
                                           return DateTimeOffset.UtcNow.UtcDateTime;
                                       }
                                   }
                                   """;

        await VerifyUtc.VerifyCodeFixAsync(Source, FixedSource);
    }

    /// <summary>Verifies the reads off an offset that already carry the UTC instant are never reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UtcReadsOffAnOffsetAreCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private DateTime _fromLocalClock = DateTimeOffset.Now.UtcDateTime;

                private DateTime _fromUtcClock = DateTimeOffset.UtcNow.DateTime;

                private DateTime _utcDate = DateTime.UtcNow.Date;

                public DateTime Read() => _fromLocalClock;
            }
            """);

    /// <summary>Verifies a Today that is consulted rather than recorded is left alone, as a consulted Now is.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsultingTodayIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public string Display() => DateTime.Today.ToString("d");

                public DayOfWeek Weekday() => DateTime.Today.DayOfWeek;

                public void Local()
                {
                    var today = DateTime.Today;
                    System.Console.WriteLine(today);
                }
            }
            """);

    /// <summary>Verifies a Today of the user's own is not mistaken for the framework clock.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task TodayOnAnotherTypeIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            namespace Fakes
            {
                public static class DateTime
                {
                    public static int Today => 1;
                }
            }

            public class C
            {
                private int _value = Fakes.DateTime.Today;

                public int Read() => _value;
            }
            """);

    /// <summary>Verifies a local clock read that never escapes the expression is left alone.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConsultingTheLocalClockIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                public string Display() => DateTime.Now.ToString("t");

                public int Hour() => DateTime.Now.Hour;

                public bool Afternoon() => DateTime.Now.Hour > 12;

                public void Local()
                {
                    var now = DateTime.Now;
                    System.Console.WriteLine(now);
                }
            }
            """);

    /// <summary>Verifies the UTC clock is never reported: it is what the rule asks for.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task UtcClockIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private DateTime _created = DateTime.UtcNow;

                public DateTimeOffset Stamp => DateTimeOffset.UtcNow;

                public DateTime Read() => _created;
            }
            """);

    /// <summary>Verifies a constructed DateTime is SST1451's business, not this rule's.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Test]
    public async Task ConstructedDateTimeIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            using System;

            public class C
            {
                private DateTime _epoch = new DateTime(2024, 1, 1);

                public DateTime Read() => _epoch;
            }
            """);

    /// <summary>Verifies a 'Now' on a type of the user's own that is also called DateTime is not reported.</summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    /// <remarks>The spelling matches, so the rule binds — and the bind is what saves it.</remarks>
    [Test]
    public async Task ClockShapedMemberOfAnotherTypeIsCleanAsync()
        => await VerifyUtc.VerifyAnalyzerAsync(
            """
            namespace Fakes
            {
                public static class DateTime
                {
                    public static int Now => 1;
                }
            }

            public class C
            {
                private int _value = Fakes.DateTime.Now;

                public int Read() => _value;
            }
            """);
}
