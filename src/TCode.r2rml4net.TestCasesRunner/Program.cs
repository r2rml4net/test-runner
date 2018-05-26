using System.Configuration;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseSchemaReader;
using TCode.r2rml4net.Mapping.Fluent;
using TCode.r2rml4net.RDB.DatabaseSchemaReader;
using TCode.r2rml4net.TriplesGeneration;
using VDS.RDF;
using VDS.RDF.Writing;
using System.Data;
using SqlLocalDb;

namespace TCode.r2rml4net.TestCasesRunner
{
    using System;

    using Anotar.NLog;

    class Program
    {
        static readonly IColumnTypeMapper ColumnTypeMapper = new MSSQLServerColumTypeMapper();

        static int Main(params string[] args)
        {
            if (!args.Any())
            {
                LogTo.Fatal("Missing test cases folder parameter");
                return 1;
            }

            using (var database = new LocalDatabase())
            {
                string testDir = args[0];
                foreach (var testCase in Directory.EnumerateDirectories(testDir, "D*"))
                {
                    LogTo.Info("Test case {0}: ", testCase);

                    using (IDbConnection connection = database.GetConnection())
                    {
                        connection.Open();
                        try
                        {
                            ExecuteTests(connection, testCase);
                        }
                        catch (SqlException ex)
                        {
                            LogTo.Fatal("FAIL: {0}", ex.Message);
                            return 1;
                        }
                    }
                }
            }

            return 0;
        }

        private static void ExecuteTests(IDbConnection connection, string testCaseDirectory)
        {
            string sqlScript = Path.Combine(testCaseDirectory, "create.sql");
            string directOutput = Path.Combine(testCaseDirectory, "directGraph-r2rml4net.ttl");

            using (var command = connection.CreateCommand())
            {
                command.CommandText = File.ReadAllText(sqlScript);
                command.ExecuteNonQuery();
            }

            ExecuteDirectMappingTest(connection, directOutput);

            foreach (var mappingFile in Directory.EnumerateFiles(testCaseDirectory, "r2rml*"))
            {
                string outputDatasetPath = Regex.Replace(mappingFile, @"r2rml([a-z]*)\.ttl$", "mapped$1-r2rml4net.nq");
                ExecuteR2RMLTest(connection, mappingFile, outputDatasetPath);
            }
        }

        private static void ExecuteR2RMLTest(IDbConnection connection, string inputMappingPath, string outputDatasetPath)
        {
            LogTo.Info("R2RML: ");

            var mappings = new Func<IR2RML>(() => R2RMLLoader.Load(File.OpenRead(inputMappingPath)));

            using (new MappingScope(new MappingOptions().IgnoringDataErrors(false).IgnoringMappingErrors(false)))
            {
                GenerateTriples(mappings, outputDatasetPath, connection, new RDFTermGenerator());
            }
        }

        private static void ExecuteDirectMappingTest(IDbConnection connection, string directMappingOutputPath)
        {
            LogTo.Info("DIRECT: ");

            var connectionString = ConfigurationManager.ConnectionStrings["SqlServer2008Test"];
            using (var databaseReader = new DatabaseReader(connectionString.ConnectionString, connectionString.ProviderName))
            {
                var metadataProvider = new DatabaseSchemaAdapter(databaseReader, ColumnTypeMapper);
                var mappingGenerator = new DirectR2RMLMapping(metadataProvider, new Uri("http://example.com/base/"));
               
                using (new MappingScope(new MappingOptions().IgnoringDataErrors(false).IgnoringMappingErrors(false).WithDuplicateRowsPreserved(true)))
                {
                    Func<IR2RML> mappings = () => mappingGenerator;
                    GenerateTriples(mappings, directMappingOutputPath, connection, new RDFTermGenerator());
                }
            }
        }

        private static void GenerateTriples(Func<IR2RML> createMappings, string outPath, IDbConnection connection, IRDFTermGenerator termGen)
        {
            File.Delete(outPath);
            ITripleStore store;
            IR2RMLProcessor processor;
            try
            {
                processor = new W3CR2RMLProcessor(connection, termGen);
                store = processor.GenerateTriples(createMappings());
            }
            catch (Exception ex)
            {
                LogTo.Error("FAIL! {0}", ex.Message);
                return;
            }

            if (processor.Success)
            {
                store.SaveToFile(outPath, new NQuadsWriter());
                LogTo.Info("SUCCESS! {0} triples generated", store.Triples.Count());
            }
            else
            {
                LogTo.Info("SUCCESS! No dataset generated");
            }
        }
    }
}
