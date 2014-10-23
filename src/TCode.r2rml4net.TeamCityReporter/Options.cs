namespace TCode.r2rml4net.TeamCityReporter
{
    using CommandLine;

    public class Options
    {
        [Option('r', "report", Required = true)]
        public string Report { get; set; }

        [Option('c', "casesPath", Required = true)]
        public string CasesPath { get; set; }
    }
}