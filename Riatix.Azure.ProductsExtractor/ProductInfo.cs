namespace Riatix.Azure.ProductsExtractor
{
    public class ProductInfo
    {
        public required string RegionName { get; set; }
        public required string MacroGeographyName { get; set; }
        public required string GeographyName { get; set; }
        public required string OfferingName { get; set; }
        public required string ProductSkuName { get; set; }
        public required string CurrentState { get; set; }
    }
}