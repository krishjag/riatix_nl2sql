
namespace Riatix.Azure.ProductsExtractor
{
    [Serializable]
    internal class ExtractionFailedException : Exception
    {
        public ExtractionFailedException()
        {
        }

        public ExtractionFailedException(string? message) : base(message)
        {
        }

        public ExtractionFailedException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}