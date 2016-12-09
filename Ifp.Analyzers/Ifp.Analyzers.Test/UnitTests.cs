using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using Ifp.Analyzers;

namespace Ifp.Analyzers.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {

        [TestMethod]
        public void TestMethod1()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void TestMethod2()
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
        class TypeName
        {   
            readonly string _value;

            TypeName(string value)
            {
                _value=value;
            }

            public string Value { get { return _value; } }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "Ifp0001",
                Message = String.Format("Property '{0}' can be convertered to getter only property", "Value"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 27)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TypeName
        {   

            TypeName(string value)
            {
            Value = value;
            }

        public string Value { get; }
    }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void TestMethod3()
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
        class TypeName
        {   
            readonly string value, value2 = ""InitValue"";

            TypeName(string value)
            {
                this.value=value;
            }

            public string Value { get { return this.value; } }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "Ifp0001",
                Message = String.Format("Property '{0}' can be convertered to getter only property", "Value"),
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 20, 27)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        TypeName(string value)
        {   
            Value=value;
        }

        public string Value { get; }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new IfpAnalyzersCodeFixProvider();

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new IfpAnalyzersAnalyzer();
    }
}