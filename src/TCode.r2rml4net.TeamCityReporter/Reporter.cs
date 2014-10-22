namespace TCode.r2rml4net.TeamCityReporter
{
    using System;

    using VDS.RDF;
    using VDS.RDF.Parsing;
    using VDS.RDF.Query;
    using VDS.RDF.Query.Builder;

    public class Reporter
    {
        static void Main(string[] args)
        {
            Console.WriteLine("##teamcity[testSuiteStarted name='{0}']", args[0]);

            var store = new TripleStore();
            store.LoadFromFile(args[0]);

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

                if (((IUriNode)result["outcome"]).Uri.ToString() == "http://www.w3.org/ns/earl#fail")
                {
                    Console.WriteLine("##teamcity[testFailed name='{0}']", result["test"]);
                }

                Console.WriteLine("##teamcity[testFinished name='{0}']", result["test"]);
            }

            Console.WriteLine("##teamcity[testSuiteFinished name='{0}']", args[0]);
        }
    }
}
