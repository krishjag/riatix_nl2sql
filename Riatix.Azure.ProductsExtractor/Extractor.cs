using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.ProductsExtractor
{
    public interface IExtractor
    {
        Task ExtractAsync(HttpClient client, CancellationToken cancellationToken = default);
    }

    public class Extractor : IExtractor
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<Extractor> _logger;
        private static readonly Regex DataRegex = new(
            "(?s)const\\s*data\\s*=\\s*\\[(.*?)\\];",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline,
            TimeSpan.FromSeconds(2));

        public Extractor(IConfiguration configuration, ILogger<Extractor> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task ExtractAsync(HttpClient client, CancellationToken cancellationToken = default)
        {
            string baseUrl = _configuration[StringConstants.az_base_url] ?? throw new ArgumentNullException(StringConstants.error_az_base_url_not_found);
            string resourcePath = _configuration[StringConstants.az_resource_path] ?? throw new ArgumentNullException(StringConstants.error_az_resource_path_not_found);

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                throw new ArgumentException(StringConstants.invalid_base_url, nameof(baseUrl));

            client.BaseAddress = baseUri;
            HttpResponseMessage response;

            response = await client.GetAsync(resourcePath, cancellationToken);
            response.EnsureSuccessStatusCode();

            string content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!await ExtractItemsAsync(content, cancellationToken))
                throw new ExtractionFailedException(StringConstants.error_response_content);
        }

        private async Task<bool> ExtractItemsAsync(string input, CancellationToken cancellationToken)
        {
            var match = DataRegex.Match(input);
            if (!match.Success || match.Groups.Count < 2)
                new ExtractionFailedException(StringConstants.error_no_data_array_found);

            _logger.LogInformation(StringConstants.extracted_data);

            if (!TryParseJsonArray($"[{match.Groups[1].Value}]", out var jsonArray)
                && !await IsValidSchemaAsync(jsonArray, cancellationToken))
                return false;

            var outputPath = _configuration[StringConstants.az_products_data_filename];
            await File.WriteAllTextAsync(outputPath!,
                jsonArray.ToString(Newtonsoft.Json.Formatting.Indented), cancellationToken);

            _logger.LogInformation(StringConstants.output_file_msg);
            return true;
        }

        private bool TryParseJsonArray(string json, out JArray jsonArray)
        {
            jsonArray = JArray.Parse(json);
            return true;
        }

        private async Task<bool> IsValidSchemaAsync(JArray array, CancellationToken cancellationToken)
        {
            var schemaPath = _configuration[StringConstants.az_products_data_schema_filename];

            var schema = JSchema.Parse(await File.ReadAllTextAsync(schemaPath!, cancellationToken));
            var isValid = array.IsValid(schema, out IList<ValidationError> errors);

            LogValidationResult(isValid, errors);
            return isValid;
        }

        private void LogValidationResult(bool isValid, IList<ValidationError> errors)
        {
            if (isValid)
            {
                _logger.LogInformation(StringConstants.json_valid);
                return;
            }

            _logger.LogWarning(StringConstants.json_invalid);
            foreach (var error in errors)
                _logger.LogWarning(StringConstants.error_schema_validation, error);
        }
    }
}
