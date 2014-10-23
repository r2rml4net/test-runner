namespace TCode.r2rml4net.TeamCityReporter
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    using VDS.RDF;
    using VDS.RDF.Parsing;
    using VDS.RDF.Query;
    using VDS.RDF.Query.Builder;

    public class Reporter
    {
        private readonly Options _options;
        private static readonly Regex TestRegex = new Regex(@"^http://www.w3.org/2001/sw/rdb2rdf/test-cases/#(?<type>\w+)TC(?<number>\d+?)(?<variant>[a-z]?)$");

        public Reporter(Options options)
        {
            this._options = options;
        }

        public void ProcessResults()
        {
            Console.WriteLine("##teamcity[testSuiteStarted name='{0}']", _options.Report);

            var store = new TripleStore();
            store.LoadFromFile(_options.Report);

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
                Console.WriteLine("##teamcity[testStarted name='{0}']", result["test"]);

                PrintTestOutput(result["test"].ToString());

                if (((IUriNode)result["outcome"]).Uri.ToString() == "http://www.w3.org/ns/earl#fail")
                {
                    Console.WriteLine("##teamcity[testFailed name='{0}']", result["test"]);
                }

                Console.WriteLine("##teamcity[testFinished name='{0}']", result["test"]);
            }

            Console.WriteLine("##teamcity[testSuiteFinished name='{0}']", _options.Report);
        }

        private void PrintTestOutput(string test)
        {
            try
            {
                var matchCollection = TestRegex.Matches(test);

                var testNum = int.Parse(matchCollection[0].Groups["number"].ToString());
                var testType = matchCollection[0].Groups["type"].ToString();
                var variant = matchCollection[0].Groups["variant"].ToString();

                var directory = Directory.GetDirectories(_options.CasesPath, string.Format("D{0:000}*", testNum)).Single();

                var fileBaseName = testType == "R2RML" ? "mapped" : "directGraph";
                var outFile = string.Format("{0}{1}-r2rml4net.*", fileBaseName, variant);

                var outFilePath = Directory.GetFiles(directory, outFile).SingleOrDefault();

                if (outFilePath != null)
                {
                    Console.WriteLine(File.ReadAllText(outFilePath));
                }
                else
                {
                    Console.WriteLine("Output file not found");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Cannot open file");
                Console.Error.WriteLine(ex.Message);
            }
        }
    }
}
