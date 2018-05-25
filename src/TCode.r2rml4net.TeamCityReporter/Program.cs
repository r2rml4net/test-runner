namespace TCode.r2rml4net.TeamCityReporter
{
    using System;

    using CommandLine;

    public class Program
    {
        private static int Main(string[] args)
        {
            var parseSuccess = Parser.Default.ParseArguments<Options>(args);

            if (parseSuccess.Tag == ParserResultType.NotParsed)
            {
                Console.WriteLine("Invalid args");
                return 1;
            }

            parseSuccess.WithParsed(options =>
            {
                new Reporter(options).ProcessResults();
            });

            return 0;
        }
    }
}