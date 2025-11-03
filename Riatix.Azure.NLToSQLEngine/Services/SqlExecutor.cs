using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class SqlExecutor: ISqlExecutor
    {
        private readonly string _connectionString;

        public SqlExecutor(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<List<Dictionary<string, object>>> Execute(string sql)
        {
            var allResults = new List<List<Dictionary<string, object>>>();

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    do
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

                        allResults.Add(table);
                    }
                    while (reader.NextResult()); // advance to next result set
                }
            }

            return allResults;
        }        
    }
}
