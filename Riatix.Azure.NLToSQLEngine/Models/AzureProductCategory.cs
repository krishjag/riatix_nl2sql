namespace Riatix.Azure.NLToSQLEngine.Models
{
    public class AzureProductCategory
    {
        public string Slug { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<AzureProduct> Products { get; set; } = new();
    }

    public class AzureProduct
    {
        public string Slug { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
