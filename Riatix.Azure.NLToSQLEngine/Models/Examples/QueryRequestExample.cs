using Swashbuckle.AspNetCore.Filters;

namespace Riatix.Azure.NLToSQLEngine.Models.Examples
{
    public class QueryRequestExample : IExamplesProvider<QueryRequest>
    {
        public QueryRequest GetExamples()
        {
            return new QueryRequest
            {
                UserQuery = "Compare GA services between East US and Japan East",
                Model = "Grok: grok-code-fast-1"
            };
        }
    }
}
