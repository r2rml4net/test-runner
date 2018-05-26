using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Builder;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TCode.r2rml4net.TestsCases
{
    public class TestCases
    {
        private readonly ITestOutputHelper _output;
        private static readonly Regex TestRegex = new Regex(@"^http://www.w3.org/2001/sw/rdb2rdf/test-cases/#(?<type>\w+)TC(?<number>\d+?)(?<variant>[a-z]?)$");
        private const string CasesPath = @"..\..\..\..\paket-files\r2rml4net\test-cases";

        public TestCases(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [TestHarnessReportSource(@"..\..\..\..\test harness\earl-r2rml4net-dm.ttl")]
        public void DirectMapping(TestCaseResult testCase)
        {
            _output.WriteLine(testCase.Id);

            if (!testCase.Success)
            {
                PrintTestOutput(testCase.Id);
            }

            Assert.True(testCase.Success);
        }

        [Theory]
        [TestHarnessReportSource(@"..\..\..\..\test harness\earl-r2rml4net-r2rml.ttl")]
        public void R2RML(TestCaseResult testCase)
        {
            _output.WriteLine(testCase.Id);

            if (!testCase.Success)
            {
                PrintTestOutput(testCase.Id);
            }

            Assert.True(testCase.Success);
        }

        private void PrintTestOutput(string test)
        {
            var matchCollection = TestRegex.Matches(test);

            var testNum = int.Parse(matchCollection[0].Groups["number"].ToString());
            var testType = matchCollection[0].Groups["type"].ToString();
            var variant = matchCollection[0].Groups["variant"].ToString();

            var directory = Directory.GetDirectories(CasesPath, string.Format("D{0:000}*", testNum)).Single();

            var fileBaseName = testType == "R2RML" ? "mapped" : "directGraph";
            var expectedFile = string.Format("{0}{1}.*", fileBaseName, variant);
            var actualFile = string.Format("{0}{1}-r2rml4net.*", fileBaseName, variant);

            var expectedFilePath = Directory.GetFiles(directory, expectedFile).SingleOrDefault();
            _output.WriteLine("Expected result:");
            _output.WriteLine(expectedFilePath == null ? "no file" : File.ReadAllText(expectedFilePath));

            _output.WriteLine(string.Empty);

            var actualFilePath = Directory.GetFiles(directory, actualFile).SingleOrDefault();
            _output.WriteLine("Actual result:");
            _output.WriteLine(actualFilePath == null ? "no file" : File.ReadAllText(actualFilePath));
        }
    }

    public class TestCaseResult
    {
        public bool Success { get; }
        public string Id { get; }

        public TestCaseResult(bool success, string id)
        {
            Success = success;
            Id = id;
        }

        public override string ToString()
        {
            return Id;
        }
    }

    public class TestHarnessReportSource : DataAttribute
    {
        public string Report { get; }

        public TestHarnessReportSource(string report)
        {
            Report = report;
        }

        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            var store = new TripleStore();
            store.LoadFromFile(Report);

            var queryProcessor = new LeviathanQueryProcessor(store);

            var query = QueryBuilder.Select("test", "outcome")
                .Where(tpb => tpb.Subject("assertion").PredicateUri(new Uri(RdfSpecsHelper.RdfType)).Object<IUriNode>("earl:Assertion")
                    .Subject("assertion").PredicateUri("earl:result").Object("result")
                    .Subject("result").PredicateUri("earl:outcome").Object("outcome")
                    .Subject("assertion").PredicateUri("earl:test").Object("test"));
            query.Prefixes.AddNamespace("earl", new Uri("http://www.w3.org/ns/earl#"));
            var results = (SparqlResultSet)queryProcessor.ProcessQuery(query.BuildQuery());

            foreach (var result in results)
            {
                var success = ((IUriNode)result["outcome"]).Uri.ToString() == "http://www.w3.org/ns/earl#pass";

                yield return new object[]
                {
                    new TestCaseResult(success, result["test"].ToString())
                };
            }
        }
    }
}
