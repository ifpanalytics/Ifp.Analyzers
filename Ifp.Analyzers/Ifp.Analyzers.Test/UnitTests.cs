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
        public void EmptyCodeBlockPassesWithoutErrors()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SimplePropertyGetsTransformed()
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
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value"),
                Severity = DiagnosticSeverity.Info,
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
        public void FieldInitializerIsPreserved()
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
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value"),
                Severity = DiagnosticSeverity.Info,
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
            readonly string value2 = ""InitValue"";

            TypeName(string value)
            {
                this.Value = value;
            }

        public string Value { get; } = ""InitValue"";
    }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void MultiplePropertiesPerClassGetTranformed()
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
            readonly string value, value2=""InitValue"";

            TypeName(string value)
            {
                this.value=value;
                this.value2=value;
            }

            public string Value { get { return this.value; } }
            public string Value2 { get { return this.value2; } }
        }
    }";
            var expected1 = new DiagnosticResult
            {
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value"),
                Severity = DiagnosticSeverity.Info,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 27)
                        }
            };
            var expected2 = new DiagnosticResult
            {
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value2"),
                Severity = DiagnosticSeverity.Info,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 27)
                        }
            };

            VerifyCSharpDiagnostic(test, new DiagnosticResult[] { expected1, expected2 });

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
                this.Value = value;
                this.Value2 = value;
            }

        public string Value { get; } = ""InitValue"";
        public string Value2 { get; } = ""InitValue"";
    }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void MultiplePropertiesPerClassWithFieldInitilizerAndUnusedFieldsGetTranformed()
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
            readonly string value, value2, value3=""InitValue"";

            TypeName(string value)
            {
                this.value=value;
                this.value2=value;
            }

            public string Value { get { return this.value; } }
            public string Value2 { get { return this.value2; } }
        }
    }";
            var expected1 = new DiagnosticResult
            {
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value"),
                Severity = DiagnosticSeverity.Info,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 21, 27)
                        }
            };
            var expected2 = new DiagnosticResult
            {
                Id = "IFP0001",
                Message = String.Format("Property '{0}' can be convertered to getter-only auto-property", "Value2"),
                Severity = DiagnosticSeverity.Info,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 22, 27)
                        }
            };

            VerifyCSharpDiagnostic(test, new DiagnosticResult[] { expected1, expected2 });

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
            readonly string value3=""InitValue"";

            TypeName(string value)
            {
                this.Value = value;
                this.Value2 = value;
            }

        public string Value { get; } = ""InitValue"";
        public string Value2 { get; } = ""InitValue"";
    }
    }";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider() => new IfpAnalyzersCodeFixProvider();

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new IfpAnalyzersAnalyzer();
    }
}