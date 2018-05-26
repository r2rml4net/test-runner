using System;
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
using NLog;
using SqlLocalDb;

namespace TCode.r2rml4net.TestCasesRunner
{
    class Program
    {
        static readonly IColumnTypeMapper ColumnTypeMapper = new MSSQLServerColumTypeMapper();
        private static readonly Logger LogTo = LogManager.GetCurrentClassLogger();

        static int Main(params string[] args)
        {
            if (!args.Any())
            {
                LogTo.Fatal("Missing test cases folder parameter");
                return 1;
            }

            string testDir = args[0];
            foreach (var testCase in Directory.EnumerateDirectories(testDir, "D*"))
            {
                LogTo.Info("Test case {0}", testCase);

                using (var database = new LocalDatabase())
                {
                    using (var connection = database.GetConnection())
                    {
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

        private static void ExecuteTests(DbConnection connection, string testCaseDirectory)
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
            LogTo.Debug("R2RML: ");

            var mappings = new Func<IR2RML>(() => R2RMLLoader.Load(File.OpenRead(inputMappingPath)));

            using (new MappingScope(new MappingOptions().IgnoringDataErrors(false).IgnoringMappingErrors(false)))
            {
                GenerateTriples(mappings, outputDatasetPath, connection, new RDFTermGenerator());
            }
        }

        private static void ExecuteDirectMappingTest(DbConnection connection, string directMappingOutputPath)
        {
            LogTo.Debug("DIRECT: ");

            using (var databaseReader = new DatabaseReader(connection))
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
                LogTo.Debug("SUCCESS! {0} triples generated", store.Triples.Count());
            }
            else
            {
                LogTo.Debug("SUCCESS! No dataset generated");
            }
        }
    }
}
