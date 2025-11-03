using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.ProductsExtractor
{
    public interface ILoader
    {
        Task LoadAndSaveAsync(CancellationToken cancellationToken = default);
    }

    public interface IAdapter
    {
        Task SaveDataAsync(List<ProductInfo> productInfos, CancellationToken cancellationToken = default);
    }

    public class Loader : ILoader
    {
        private readonly ILogger<Loader> _logger;
        private readonly IConfiguration _configuration;
        private readonly IAdapter _adapter;
        private readonly MacroGeographyResolver _macroGeographyResolver;
        public Loader(ILogger<Loader> logger, IConfiguration configuration, IAdapter adapter, MacroGeographyResolver macroGeographyResolver)
        {
            _logger = logger;
            _configuration = configuration;
            _adapter = adapter;
            _macroGeographyResolver = macroGeographyResolver;
        }

        public async Task LoadAndSaveAsync(CancellationToken cancellationToken = default)
        {
            var productInfos = new List<ProductInfo>();
            var filePath = _configuration[StringConstants.az_products_data_filename];
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new ArgumentNullException(StringConstants.error_extracted_data_file_path_not_found);            

            string jsonContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            var jsonArray = JArray.Parse(jsonContent);

            foreach (var item in jsonArray)
            {                
                var geographyName = item["GeographyName"]?.ToString() ?? string.Empty;
                var productInfo = new ProductInfo
                {
                    RegionName = item["RegionName"]?.ToString() ?? string.Empty,
                    GeographyName = geographyName,
                    OfferingName = item["OfferingName"]?.ToString() ?? string.Empty,
                    ProductSkuName = item["ProductSkuName"]?.ToString() ?? string.Empty,
                    CurrentState = item["CurrentState"]?.ToString() ?? string.Empty,
                    MacroGeographyName = _macroGeographyResolver.GeographyData.GetMacroGeography(geographyName) ?? string.Empty,
                };
                productInfos.Add(productInfo);
            }

            await _adapter.SaveDataAsync(productInfos, cancellationToken);
        }
    }
}
