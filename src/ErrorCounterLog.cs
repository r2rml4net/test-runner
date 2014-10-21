using System.IO;
using TCode.r2rml4net.Log;
using TCode.r2rml4net.Mapping;
using VDS.RDF;

namespace TCode.r2rml4net.TestCasesRunner
{
    class ErrorCounterLog : TextWriterLog
    {
        private int _errorsCount;

        public ErrorCounterLog(TextWriter writer) : base(writer)
        {
        }

        public int ErrorsCount
        {
            get { return _errorsCount; }
        }

        #region Implementation of ITriplesGenerationLog

        /////// <summary>
        /////// Logs an error of missing <see cref="T:TCode.r2rml4net.Mapping.ITriplesMap"/>'s <see cref="P:TCode.r2rml4net.Mapping.ITriplesMap.SubjectMap"/>
        /////// </summary>
        ////void ITriplesGenerationLog.LogMissingSubject(ITriplesMap triplesMap)
        ////{
        ////    LogMissingSubject(triplesMap);
        ////    _errorsCount++;
        ////}

        ////void ITriplesGenerationLog.LogQueryExecutionError(IQueryMap map, string errorMessage)
        ////{
        ////    LogQueryExecutionError(map, errorMessage);
        ////    _errorsCount++;
        ////}

        ////void ITriplesGenerationLog.LogInvalidTermMap(ITermMap termMap, string message)
        ////{
        ////    LogInvalidTermMap(termMap, message);
        ////    _errorsCount++;
        ////}

        ////void ITriplesGenerationLog.LogInvaldTriplesMap(ITriplesMap triplesMap, string message)
        ////{
        ////    LogInvaldTriplesMap(triplesMap, message);
        ////    _errorsCount++;
        ////}

        #endregion

        #region Implementation of IRDFTermGenerationLog

        ////void IRDFTermGenerationLog.LogTermGenerated(INode node)
        ////{
        ////}

        #endregion
    }
}
