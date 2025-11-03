using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;

namespace Riatix.Azure.NLToSQLEngine.QueryBuilders
{
    public class DoNothingQueryBuilder : BaseQueryBuilder
    {
        public DoNothingQueryBuilder(IServiceNameNormalizer normalizer, IRegionHierarchyCache regionHierarchyCache)
            : base(normalizer, regionHierarchyCache)
        {
        }
        public override bool CanHandle(IntentResponse intent) => true; // fallback for anything not handled earlier

        public override string BuildQuery(IntentResponse intentResponse)
        {
            var unknownIntent = string.IsNullOrWhiteSpace(intentResponse.Intent)
                ? "unknown"
                : intentResponse.Intent;

            return $@"
                SELECT 
                    'No SQL generated. The intent ""{unknownIntent}"" is not supported yet.' AS Message
            ";
        }
    }
}
