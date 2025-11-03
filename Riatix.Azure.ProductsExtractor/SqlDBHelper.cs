using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.ProductsExtractor
{
    public class SqlDBHelper : IAdapter
    {
        private readonly ILogger<SqlDBHelper> _logger;
        private readonly IConfiguration _configuration;
        public SqlDBHelper(ILogger<SqlDBHelper> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        string GetSqlDatabaseConnectionString()
        {
            string connectionString = _configuration["ConnectionStrings_SqlServer"] ?? 
                                        throw new ArgumentNullException("ConnectionStrings_SqlServer is not set in configuration.");

            var sqlConnectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

            return sqlConnectionStringBuilder.ToString();
        }

        public async Task SaveDataAsync(List<ProductInfo> productInfos, CancellationToken cancellationToken = default)
        {
            string insertCommand = @"INSERT INTO [dbo].[products_info]
                                           ([RegionName]
                                           ,[GeographyName]
                                           ,[MacroGeographyName]
                                           ,[OfferingName]
                                           ,[ProductSkuName]
                                           ,[CurrentState]
                                           ,[InsertTimeStamp])
                                     VALUES
                                           (@RegionName
                                           ,@GeographyName
                                           ,@MacroGeographyName       
                                           ,@OfferingName
                                           ,@ProductSkuName
                                           ,@CurrentState
                                           ,@InsertTimeStamp)";

            using (SqlConnection connection = new SqlConnection(GetSqlDatabaseConnectionString()))
            {
                if (connection.State != ConnectionState.Open)
                {
                    int attempts = 0;
                    do
                    {
                        try
                        {
                            await connection.OpenAsync(cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            attempts++;
                            _logger.LogInformation($"Attempt {attempts} to connect to database failed. Retrying in 5 second...");
                            _logger.LogInformation(ex.Message);
                            if (ex.InnerException != null)
                            {
                                _logger.LogInformation(ex.InnerException.Message);
                            }
                            await Task.Delay(5000, cancellationToken);
                        }
                    }
                    while (attempts < 5 && connection.State != ConnectionState.Open);
                }

                // Ensure SQL objects (tables/procs) exist by executing any .sql scripts under SqlObjects
                await EnsureSqlObjectsExistAsync(connection, cancellationToken);

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    string cmdText = @"INSERT INTO 
                                            [dbo].[products_info_archive]
                                        SELECT  [RegionName]
                                                   ,[GeographyName]
                                                   ,[MacroGeographyName]
                                                   ,[OfferingName]
                                                   ,[ProductSkuName]
                                                   ,[CurrentState]
                                                   ,[InsertTimeStamp]
                                        FROM 
                                            [dbo].[products_info]
                                       GO
                                       DELETE FROM [dbo].[products_info]";

                    using (SqlCommand cmdArchive = new SqlCommand(cmdText, connection, transaction))
                    {
                        await cmdArchive.ExecuteNonQueryAsync(cancellationToken);
                    }

                    using (SqlCommand command = new SqlCommand())
                    {
                        command.CommandText = insertCommand;
                        command.CommandType = CommandType.Text;
                        command.Connection = connection;
                        command.Transaction = transaction;

                        try
                        {
                            int recordCounter = 0;
                            var insertTimeStamp = DateTimeOffset.Now;

                            foreach (var product in productInfos)
                            {
                                recordCounter++;
                                command.Parameters.Clear();
                                command.Parameters.AddWithValue("@RegionName", product.RegionName);
                                command.Parameters.AddWithValue("@GeographyName", product.GeographyName);
                                command.Parameters.AddWithValue("@MacroGeographyName", product.MacroGeographyName);
                                command.Parameters.AddWithValue("@OfferingName", product.OfferingName);
                                command.Parameters.AddWithValue("@ProductSkuName", product.ProductSkuName);
                                command.Parameters.AddWithValue("@CurrentState", product.CurrentState);
                                command.Parameters.AddWithValue("@InsertTimeStamp", insertTimeStamp);
                                await command.ExecuteNonQueryAsync(cancellationToken);

                                Console.Write($"\rRecords processed: {recordCounter}/{productInfos.Count}");
                            }
                            _logger.LogInformation($"{recordCounter} of {productInfos.Count} records added");
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            _logger.LogInformation(ex.Message);
                            _logger.LogInformation("Transaction rolled back.");
                        }
                    }
                }
            }
        }

        private async Task EnsureSqlObjectsExistAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            // Look for SqlObjects folder in a few likely locations
            var candidates = new[]
            {                
                Path.Combine(Directory.GetCurrentDirectory(), "SqlObjects"),                
            };

            string? sqlObjectsPath = candidates.FirstOrDefault(Directory.Exists);
            var sqlFiles = Directory.GetFiles(sqlObjectsPath!, "*.sql").OrderBy(f => f);

            foreach (var file in sqlFiles)
            {
                _logger.LogInformation(StringConstants.apply_sql_file, file);
                string sql;

                sql = await File.ReadAllTextAsync(file, cancellationToken);

                var match = Regex.Match(sql, @"(?i)CREATE\s+TABLE\s+(?:\[\s*(?<schema>[^\]]+)\s*\]\.)?\[\s*(?<table>[^\]]+)\s*\]");

                if (!match.Success)
                    continue;

                string tblSchema = match.Groups["schema"].Value;
                string tblName = match.Groups["table"].Value;

                _logger.LogInformation($"Schema: {tblSchema}");
                _logger.LogInformation($"Table: {tblName}");

                using var existsCmd = connection.CreateCommand();
                existsCmd.CommandText = @"select 
	                                                COUNT(CASE WHEN 1 = 1 THEN '1' ELSE '0' END) AS [Exists]
                                                from 
	                                                INFORMATION_SCHEMA.TABLES 
                                                where 
	                                                TABLE_SCHEMA = @tblSchema
	                                                and 
	                                                TABLE_NAME = @tblName";
                existsCmd.Parameters.AddWithValue("@tblSchema", tblSchema);
                existsCmd.Parameters.AddWithValue("@tblName", tblName);
                var exists = Convert.ToBoolean(await existsCmd.ExecuteScalarAsync());

                if (exists)
                {
                    _logger.LogInformation($"Table {tblSchema}.{tblName} already exists; skipping.");
                    continue;
                }

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}