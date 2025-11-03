using Microsoft.Data.SqlClient;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class SqlHelper : ISqlHelper
    {
        SqlConnectionStringBuilder _connectionString;

        public SqlHelper(string _sonnectionString)
        {
            _connectionString = new SqlConnectionStringBuilder(_sonnectionString);
        }

        public Dictionary<string, object> GetAzureRegionMatrixDataAction()
        {
            string query = @"
                            DECLARE @productsTS DATETIME
                            DECLARE @productsArchiveTS DATETIME

                            SELECT @productsTS = MAX(InsertTimeStamp) FROM [dbo].[products_info]
                            SELECT @productsArchiveTS = MAX(InsertTimeStamp) FROM [dbo].[products_info_archive]

                            SELECT CASE WHEN COUNT(*) > 1 THEN 'fetch' ELSE 'nofetch' END AS DataAction FROM
                            (
                            SELECT
                                   [RegionName]
                                  ,[GeographyName]
                                  ,[MacroGeographyName]
                                  ,[OfferingName]
                                  ,[ProductSkuName]
                                  ,[CurrentState]      
                            FROM 
                                [dbo].[products_info]
                            WHERE
                                InsertTimeStamp = @productsTS
                            EXCEPT
                            SELECT
                                   [RegionName]
                                  ,[GeographyName]
                                  ,[MacroGeographyName]
                                  ,[OfferingName]
                                  ,[ProductSkuName]
                                  ,[CurrentState]      
                            FROM 
                                [dbo].[products_info_archive]
                            WHERE
                                InsertTimeStamp = @productsArchiveTS
                            )a
                ";

            using (var connection = new SqlConnection(_connectionString.ToString()))
            using (var command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    var row = new Dictionary<string, object>();
                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = (reader.IsDBNull(i) ? null : reader.GetValue(i))!;
                        }
                    }
                    return row;
                }
            }
        }

        public List<Dictionary<string, object>> GetAzureRegionMatrix()
        {
            string query = @"
                                SELECT 
	                                GeographyName,
	                                RegionName,
	                                OfferingName as Products,
	                                ProductSkuName as [Product SKU],
	                                CurrentState as [Status]
                                FROM
	                                dbo.products_info                                
                            ";


            using (var connection = new SqlConnection(_connectionString.ToString()))
            using (var command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    var table = new List<Dictionary<string, object>>();
                    while (reader.Read())
                    {
                        var row = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = (reader.IsDBNull(i) ? null : reader.GetValue(i))!;
                        }

                        table.Add(row);
                    }

                    return table;
                }
            }
        }

        public Dictionary<string, string> GetAzureRegionMatrixDataCurrency()
        {
            string query = @"
                            select max(insertTimeStamp) DataCurrency from products_info
                            ";

            using (var connection = new SqlConnection(_connectionString.ToString()))
            using (var command = new SqlCommand(query, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    var row = new Dictionary<string, string>();
                    while (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var dataCurrency = DateTime.MinValue;
                            if (DateTime.TryParse((reader.IsDBNull(i) ? null : reader.GetValue(i))!.ToString()!, out dataCurrency))
                                row[reader.GetName(i)] = dataCurrency.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        }
                    }
                    return row;
                }
            }
        }
    }
}
