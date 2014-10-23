namespace TCode.r2rml4net.TeamCityReporter
{
    using System;

    using CommandLine;

    public class Program
    {
        private static int Main(string[] args)
        {
            var options = new Options();
            var parseSuccess = Parser.Default.ParseArguments(args, options);

            if (!parseSuccess)
            {
                Console.WriteLine("Invalid args");
                return 1;
            }

            new Reporter(options).ProcessResults();

            return 0;
        }
    }
}