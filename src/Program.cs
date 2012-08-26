using System;
using System.Configuration;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseSchemaReader;
using TCode.r2rml4net.Log;
using TCode.r2rml4net.Mapping;
using TCode.r2rml4net.Mapping.DirectMapping;
using TCode.r2rml4net.RDB.DatabaseSchemaReader;
using TCode.r2rml4net.TriplesGeneration;
using VDS.RDF;
using VDS.RDF.Writing;
using System.Data;

namespace TCode.r2rml4net.TestCasesRunner
{
    class Program
    {
        static readonly IColumnTypeMapper ColumnTypeMapper = new MSSQLServerColumTypeMapper();
        private static StreamWriter _r2RMLLogWriter, _directLogWriter;
        private static TextWriterLog _log;

        static void Main()
        {
            using (_r2RMLLogWriter = new StreamWriter("r2rml.log"))
            {
                using (_directLogWriter = new StreamWriter("direct.log"))
                {
                    string masterConnection = ConfigurationManager.ConnectionStrings["SqlServer2008Master"].ConnectionString;

                    string testDir = ConfigurationManager.AppSettings["testDir"];
                    foreach (var testCase in Directory.EnumerateDirectories(testDir, "D*"))
                    {
                        Console.Out.WriteLine("Test case {0}: ", testCase);

                        var dbProviderFactory = DbProviderFactories.GetFactory(ConfigurationManager.ConnectionStrings["SqlServer2008Master"].ProviderName);
                        using (IDbConnection connection = dbProviderFactory.CreateConnection())
                        {
                            if (connection != null)
                            {
                                connection.ConnectionString = masterConnection;
                                connection.Open();
                                try
                                {
                                    ExecuteCommand(connection, "if db_id('r2rml4net_tests') is not null DROP DATABASE r2rml4net_tests");
                                    ExecuteCommand(connection, "CREATE DATABASE r2rml4net_tests");

                                    connection.ChangeDatabase("r2rml4net_tests");

                                    ExecuteTests(connection, testCase);
                                }
                                catch (SqlException ex)
                                {
                                    Console.WriteLine("FAIL");
                                    Console.WriteLine(ex.Message);
                                }
                                finally
                                {
                                    connection.ChangeDatabase("master");
                                    ExecuteCommand(connection, "ALTER DATABASE r2rml4net_tests SET SINGLE_USER WITH ROLLBACK IMMEDIATE");
                                    ExecuteCommand(connection, "ALTER DATABASE r2rml4net_tests SET MULTI_USER");
                                }
                            }
                        }

                        Console.WriteLine();
                    }
                }
            }
        }

        private static void ExecuteCommand(IDbConnection connection, string commandText)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                command.ExecuteNonQuery();
            }
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

            _log = new TextWriterLog(_directLogWriter);
            ExecuteDirectMappingTest(connection, directOutput);

            _log = new TextWriterLog(_r2RMLLogWriter);
            foreach (var mappingFile in Directory.EnumerateFiles(testCaseDirectory, "r2rml*"))
            {
                string outputDatasetPath = Regex.Replace(mappingFile, @"r2rml([a-z]*)\.ttl$", "mapped$1-r2rml4net.nq");
                ExecuteR2RMLTest(connection, mappingFile, outputDatasetPath);
            }
        }

        private static void ExecuteR2RMLTest(IDbConnection connection, string inputMappingPath, string outputDatasetPath)
        {
            Console.Out.Write("R2RML: ");

            _r2RMLLogWriter.WriteLine();
            _r2RMLLogWriter.WriteLine(inputMappingPath);
            var mappings = new Func<IR2RML>(() => R2RMLLoader.Load(File.OpenRead(inputMappingPath)));
            GenerateTriples(mappings, outputDatasetPath, connection, new RDFTermGenerator());
        }

        private static void ExecuteDirectMappingTest(IDbConnection connection, string directMappingOutputPath)
        {
            Console.Out.Write("DIRECT: ");

            _directLogWriter.WriteLine();
            _directLogWriter.WriteLine(directMappingOutputPath);
            var connectionString = ConfigurationManager.ConnectionStrings["SqlServer2008Test"];
            using (var databaseReader = new DatabaseReader(connectionString.ConnectionString, connectionString.ProviderName))
            {
                var r2RMLConfiguration = new R2RMLConfiguration(new Uri("http://example.com/base/"));
                var metadataProvider = new DatabaseSchemaAdapter(databaseReader, ColumnTypeMapper);
                var mappingGenerator = new R2RMLMappingGenerator(metadataProvider, r2RMLConfiguration, new MappingOptions
                {
                    IgnoreDataErrors = false,
                    IgnoreMappingErrors = false
                });

                var mappings = new Func<IR2RML>(mappingGenerator.GenerateMappings);
                GenerateTriples(mappings, directMappingOutputPath, connection, new RDFTermGenerator(new MappingOptions{PreserveDuplicateRows = true}));
            }
        }

        private static void GenerateTriples(Func<IR2RML> createMappings, string outPath, IDbConnection connection, IRDFTermGenerator termGen)
        {
            File.Delete(outPath);
            ITripleStore store;
            IR2RMLProcessor processor;
            try
            {
                processor = new W3CR2RMLProcessor(connection, termGen, new MappingOptions
                    {
                        IgnoreDataErrors = false,
                        IgnoreMappingErrors = false
                    })
                    {
                        Log = _log
                    };
                store = processor.GenerateTriples(createMappings());
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FAIL! {0}", ex.Message);
                return;
            }

            if (processor.Success)
            {
                store.SaveToFile(outPath, new NQuadsWriter());
                Console.Out.WriteLine("SUCCESS! {0} triples generated", store.Triples.Count());
            }
            else
            {
                Console.Out.WriteLine("SUCCESS! No dataset generated");
            }
        }
    }
}
