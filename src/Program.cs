using System;
using System.Configuration;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using DatabaseSchemaReader;
using TCode.r2rml4net.Mapping;
using TCode.r2rml4net.Mapping.DirectMapping;
using TCode.r2rml4net.RDB.DatabaseSchemaReader;
using TCode.r2rml4net.TriplesGeneration;
using VDS.RDF;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Writing.Formatting;
using System.Data;

namespace TCode.r2rml4net.TestCasesRunner
{
    class Program
    {
        static readonly IColumnTypeMapper ColumnTypeMapper = new MSSQLServerColumTypeMapper();
        private static StreamWriter _r2rmlLog, _directLog;

        static void Main(string[] args)
        {
            _r2rmlLog = new StreamWriter("r2rml.log");
            _directLog = new StreamWriter("direct.log");

            string masterConnection = ConfigurationManager.ConnectionStrings["SqlServer2008Master"].ConnectionString;

            string testDir = ConfigurationManager.AppSettings["testDir"];
            foreach (var testCase in Directory.EnumerateDirectories(testDir, "D*"))
            {
                Console.Out.WriteLine("Test case {0}: ", testCase);

                var dbProviderFactory = DbProviderFactories.GetFactory(ConfigurationManager.ConnectionStrings["SqlServer2008Master"].ProviderName);
                using (DbConnection connection = dbProviderFactory.CreateConnection())
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

                Console.WriteLine();

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

        private static void ExecuteR2RMLTest(DbConnection connection, string inputMappingPath, string outputDatasetPath)
        {
            Console.Out.Write("R2RML: ");

            var countHandler = new CountHandler();

            _r2rmlLog.WriteLine();
            _r2rmlLog.WriteLine(inputMappingPath);
            ErrorCounterLog log = new ErrorCounterLog(_r2rmlLog);
            try
            {
                using (var directStream = File.Create(outputDatasetPath))
                {
                    using (var streamWriter = new StreamWriter(directStream))
                    {
                        IRdfHandler writer = new ChainedHandler(new IRdfHandler[]
                                {
                                    countHandler, 
                                    new WriteThroughHandler(new NQuadsFormatter(), streamWriter)
                                });
                        IRDFTermGenerator termGen = new RDFTermGenerator
                        {
                            Log = log
                        };
                        IR2RMLProcessor procesor = new W3CR2RMLProcessor(connection, termGen)
                            {
                                Log = log
                            };
                        var mappings = R2RMLLoader.Load(File.OpenRead(inputMappingPath));
                        procesor.GenerateTriples(mappings, writer);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FAIL! {0}", ex.Message);
                return;
            }

            ReportResult(outputDatasetPath, countHandler, log);
        }

        private static void ExecuteDirectMappingTest(DbConnection connection, string directMappingOutputPath)
        {
            Console.Out.Write("DIRECT: ");

            var countHandler = new CountHandler();

            _directLog.WriteLine();
            _directLog.WriteLine(directMappingOutputPath);
            var log = new ErrorCounterLog(_directLog);
            try
            {
                using (var directStream = File.Create(directMappingOutputPath))
                {
                    using (var streamWriter = new StreamWriter(directStream))
                    {
                        var connectionString = ConfigurationManager.ConnectionStrings["SqlServer2008Test"];
                        using (var databaseReader = new DatabaseReader(connectionString.ConnectionString, connectionString.ProviderName))
                        {
                            var r2RMLConfiguration = new R2RMLConfiguration(new Uri("http://example.com/base/"));
                            var metadataProvider = new DatabaseSchemaAdapter(databaseReader, ColumnTypeMapper);
                            var mappingGenerator = new R2RMLMappingGenerator(metadataProvider, r2RMLConfiguration);

                            var mappings = mappingGenerator.GenerateMappings();

                            IRdfHandler writer = new ChainedHandler(new IRdfHandler[]
                                {
                                    countHandler, 
                                    new WriteThroughHandler(new TurtleW3CFormatter(), streamWriter)
                                });
                            IRDFTermGenerator termGen = new RDFTermGenerator(true)
                                {
                                    Log = log
                                };
                            IR2RMLProcessor procesor = new W3CR2RMLProcessor(connection, termGen)
                                {
                                    Log = log
                                };
                            procesor.GenerateTriples(mappings, writer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FAIL! {0}", ex.Message);
                File.Delete(directMappingOutputPath);
                return;
            }

            ReportResult(directMappingOutputPath, countHandler, log);
        }

        private static void ReportResult(string outputPath, CountHandler countHandler, ErrorCounterLog log)
        {
            if (log.ErrorsCount == 0)
            {
                Console.Out.WriteLine("SUCCESS! {0} triples generated", countHandler.Count);
            }
            else
            {
                Console.Out.WriteLine("SUCCESS! No output file produced");
                File.Delete(outputPath);
            }
        }
    }
}
