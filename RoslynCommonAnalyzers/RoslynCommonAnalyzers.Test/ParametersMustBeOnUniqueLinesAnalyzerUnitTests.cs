using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using VerifyCS = RoslynCommonAnalyzers.Test.CSharpCodeFixVerifier<
    RoslynCommonAnalyzers.RCGS0001BaseMethodDeclarationsParametersMustBeOnUniqueLinesAnalyzer,
    RoslynCommonAnalyzers.RCGS0001ParametersMustBeOnUniqueLinesCodeFixProvider>;

namespace RoslynCommonAnalyzers.Test
{
    [TestClass]
    public class RoslynCommonAnalyzersUnitTest
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task Empty()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task AllOnOneLine()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        public class MyTypeName
        {
            public void MyMethod(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
            {
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task DifferentLinesHalfSeparated()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        public class MyTypeName
        {
            public void MyMethod(int a, int b,
                int c, int d, int e, int f, int g, int h, int i, int j)
            {
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic("RCGS0001").WithSpan(13, 13, 16, 14));
        }

        [TestMethod]
        public async Task DifferentLinesSeparatedExceptFirst()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        public class MyTypeName
        {
            public void MyMethod(int a,
                int b,
                int c,
                int d,
                int e,
                int f,
                int g,
                int h,
                int i,
                int j)
            {
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test, VerifyCS.Diagnostic("RCGS0001").WithSpan(13, 13, 24, 14));
        }

        [TestMethod]
        public async Task DifferentLinesSeparated()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        public class MyTypeName
        {
            public void MyMethod(
                int a,
                int b,
                int c,
                int d,
                int e,
                int f,
                int g,
                int h,
                int i,
                int j)
            {
            }
        }
    }";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class {|#0:TypeName|}
        {   
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

            var expected = VerifyCS.Diagnostic("RoslynCommonAnalyzers").WithLocation(0).WithArguments("TypeName");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }
    }
}
