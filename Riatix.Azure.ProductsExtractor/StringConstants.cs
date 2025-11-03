using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Riatix.Azure.ProductsExtractor
{
    internal static class StringConstants
    {
        internal static readonly string az_base_url = "az_base_url";
        internal static readonly string appsettings_filename = "appsettings.json";
        internal static readonly string error_az_base_url_not_found = "BaseUrl is not configured";
        internal static readonly string az_resource_path = "az_resource_path";
        internal static readonly string error_az_resource_path_not_found = "ResourcePath is not configured";
        internal static readonly string error_response_content = "Failed to extract items from the response content.";
        internal static readonly string extracted_data = "Extracted the Azure products availability data";
        internal static readonly string log_file_path = "logs/app.log";
        internal static readonly string log_file_msg = "Log file created at";
        internal static readonly string json_valid = "JSON is valid";
        internal static readonly string json_invalid = "JSON is invalid";

        internal static readonly string az_products_data_filename = "az_products_data_filename";
        internal static readonly string az_products_data_schema_filename = "az_products_data_schema_filename";
        internal static readonly string output_file_msg = "Saved the data to output.json in the executing directory.";
        internal static readonly string error_sql_connection_string_not_found = "SQL ConnectionString is not configured";
        internal static readonly string sql_insert_query = "SQL_Insert_Query";
        internal static readonly string error_sql_insert_query_not_found = "SQL Insert Query is not configured";
        internal static readonly string sql_insert_msg = "Data inserted into SQL Database successfully";
        internal static readonly string sql_insert_error_msg = "Error inserting data into SQL Database";

        internal static readonly string db_hostname_key = "SQLDB_HostName";
        internal static readonly string db_name_key = "SQLDB_DatabaseName";
        internal static readonly string db_userid_key = "SQLDB_UserID";
        internal static readonly string db_pwd_key = "SQLDB_Password";
        internal static readonly string error_extracted_data_file_path_not_found = "The extracted data file not found";
        internal static readonly string invalid_base_url = "Invalid base URL";
        internal static readonly string error_no_data_array_found = "No data array found in input.";
        internal static readonly string error_schema_validation = "Schema validation error: {Error}";
        internal static readonly string apply_sql_file = "Applying SQL file: {File}";
    }
}
