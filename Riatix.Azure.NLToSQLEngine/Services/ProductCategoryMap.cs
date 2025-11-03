using Microsoft.Extensions.Logging;
using SimMetrics.Net.Metric;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Riatix.Azure.NLToSQLEngine.Services
{
    public class ProductCategoryMap : IProductCategoryMap
    {
        private readonly Dictionary<string, List<string>> _categoryMap;
        private readonly ILogger<ProductCategoryMap>? _logger;

        public ProductCategoryMap(Dictionary<string, List<string>> categoryMap, ILogger<ProductCategoryMap>? logger = null)
        {
            _categoryMap = categoryMap;
            _logger = logger;
        }

        public IReadOnlyList<string> GetOfferingsForCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return Array.Empty<string>();

            var key = categoryName.Trim();
            if (_categoryMap.TryGetValue(key, out var offerings))
                return offerings;

            // fallback with case-insensitive check
            var found = _categoryMap
                .FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));

            return found.Value ?? Array.Empty<string>().ToList();
        }

        public bool ContainsCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return false;

            return _categoryMap.ContainsKey(categoryName.Trim()) ||
                   _categoryMap.Keys.Any(k => k.Equals(categoryName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyDictionary<string, List<string>> GetAllCategories() => _categoryMap;
    }


    /// <summary>
    /// Loads and builds the product category - offerings map.
    /// Tries to download from the official Microsoft Azure pricing API (with retries).
    /// Falls back to a local fallback JSON if download fails.
    /// Merges an optional supplemental category file to produce the final map.
    /// </summary>
    public class ProductCategoryMapLoader
    {
        private readonly string _assetDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");
        private readonly string _primaryFile = "azure_product_categories.json";
        private readonly string _fallbackFile = "fallback_azure_product_categories.json";
        private readonly string _supplementalFile = "supplemental_azure_product_categories.json";
        private readonly HttpClient _httpClient;

        private string AzureCategoryUrl = "https://azure.microsoft.com/api/v2/pricing/categories/calculator/";

        public ProductCategoryMapLoader(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// Loads the product category map from remote or local sources.
        /// </summary>
        public async Task<Dictionary<string, List<string>>> LoadAsync()
        {
            EnsureAssetDirectory();

            string primaryPath = Path.Combine(_assetDirectory, _primaryFile);
            string fallbackPath = Path.Combine(_assetDirectory, _fallbackFile);
            string supplementalPath = Path.Combine(_assetDirectory, _supplementalFile);

            Dictionary<string, List<string>> finalMap = new(StringComparer.OrdinalIgnoreCase);

            // Step 1 Attempt remote download and save
            bool success = await TryDownloadWithRetryAsync(primaryPath, retries: 3);

            string loadPath = success && File.Exists(primaryPath)
                ? primaryPath
                : fallbackPath;

            // Step 2 Load base map (primary or fallback)
            if (!File.Exists(loadPath))
                throw new FileNotFoundException($"Neither {primaryPath} nor {fallbackPath} could be found.");

            var baseData = await File.ReadAllTextAsync(loadPath);
            var baseMap = ProcessCategory(baseData);

            foreach (var kvp in baseMap)
                finalMap[kvp.Key] = kvp.Value;

            // Step 3 Merge supplemental categories (if available)
            if (File.Exists(supplementalPath))
            {
                var supplementalData = await File.ReadAllTextAsync(supplementalPath);
                var supplementalMap = ProcessCategory(supplementalData);

                foreach (var kvp in supplementalMap)
                {
                    if (!finalMap.ContainsKey(kvp.Key))
                        finalMap[kvp.Key] = kvp.Value;
                    else
                        finalMap[kvp.Key].AddRange(kvp.Value
                            .Where(v => !finalMap[kvp.Key].Contains(v, StringComparer.OrdinalIgnoreCase)));
                }
            }

            return finalMap;
        }

        // --- Helpers ---

        private void EnsureAssetDirectory()
        {
            if (!Directory.Exists(_assetDirectory))
                Directory.CreateDirectory(_assetDirectory);
        }

        private async Task<bool> TryDownloadWithRetryAsync(string savePath, int retries)
        {
            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(AzureCategoryUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        await File.WriteAllTextAsync(savePath, content);
                        return true;
                    }
                }
                catch
                {
                    // wait before retrying
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
            }
            return false;
        }

        /// <summary>
        /// Parses Azure calculator JSON into category - offerings map.
        /// </summary>
        private static Dictionary<string, List<string>> ProcessCategory(string json)
        {
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var categoryElement = JsonDocument.Parse(json).RootElement;                        

            foreach(var category in  categoryElement.EnumerateArray())
            {
                var categoryName = category.GetProperty("displayName").GetString();
                var offerings = new List<string>();
                foreach (var product in category.GetProperty("products").EnumerateArray())
                {
                    if (product.TryGetProperty("displayName", out var offeringProp))
                    {
                        var offeringName = offeringProp.GetString();
                        if (!string.IsNullOrWhiteSpace(offeringName))
                            offerings.Add(offeringName.Trim());
                    }
                }

                if (offerings.Count > 0 && !String.IsNullOrEmpty(categoryName))
                    map[categoryName.Trim()] = offerings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }            

            return map;
        }
    }
}
