using Riatix.Azure.NLToSQLEngine.Models;
using System.Text.Json.Nodes;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class SqlGenerator : ISqlGenerator
    {
        private readonly IEnumerable<IQueryBuilder> _builders;        

        public SqlGenerator(IEnumerable<IQueryBuilder> builders)
        {
            _builders = builders;            
        }

        public string Generate(IntentResponse intentResponse)
        {
            //intentResponse = NormalizeIntent(intentResponse);

            var builder = _builders.FirstOrDefault(b => b.CanHandle(intentResponse));
            if (builder == null)
                throw new System.NotSupportedException($"No SQL builder found for intent '{intentResponse.Intent}'.");

            return builder.BuildQuery(intentResponse);
        }        
    }
}
