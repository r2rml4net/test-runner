using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DatabaseSchemaReader;
using TCode.r2rml4net.Mapping;
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

        static void Main(string[] args)
        {
            string masterConnection = ConfigurationManager.ConnectionStrings["SqlServer2008Master"].ConnectionString;

            string testDir = ConfigurationManager.AppSettings["testDir"];
            foreach (var testCase in Directory.EnumerateDirectories(testDir, "D*").Skip(1).Take(1))
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

            try
            {
                using (var directStream = File.Create(outputDatasetPath))
                {
                    using (var streamWriter = new StreamWriter(directStream))
                    {
                        IRdfHandler writer = new ChainedHandler(new IRdfHandler[]
                                {
                                    countHandler, 
                                    new WriteThroughHandler(new TurtleW3CFormatter(), streamWriter)
                                });
                        IR2RMLProcessor procesor = new W3CR2RMLProcessor(connection, writer);
                        var mappings = R2RMLLoader.Load(File.OpenRead(inputMappingPath));
                        procesor.GenerateTriples(mappings);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FAIL! {0}", ex.Message);
                return;
            }

            Console.Out.WriteLine("SUCCESS! {0} triples generated", countHandler.Count);
        }

        private static void ExecuteDirectMappingTest(DbConnection connection, string directMappingOutputPath)
        {
            Console.Out.Write("DIRECT: ");

            var countHandler = new CountHandler();

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
                            var mappingGenerator = new DefaultR2RMLMappingGenerator(metadataProvider, r2RMLConfiguration);

                            var mappings = mappingGenerator.GenerateMappings();

                            IRdfHandler writer = new ChainedHandler(new IRdfHandler[]
                                {
                                    countHandler, 
                                    new WriteThroughHandler(new TurtleW3CFormatter(), streamWriter)
                                });
                            IR2RMLProcessor procesor = new W3CR2RMLProcessor(connection, writer);
                            procesor.GenerateTriples(mappings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine("FAIL! {0}", ex.Message);
                return;
            }

            Console.Out.WriteLine("SUCCESS! {0} triples generated", countHandler.Count);
        }
    }
}
