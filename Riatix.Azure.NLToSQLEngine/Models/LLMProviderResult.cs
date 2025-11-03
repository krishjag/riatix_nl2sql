namespace Riatix.Azure.NLToSQLEngine.Models
{
    public class LLMProviderResult<T>
    {
        public string ProviderName { get; }
        public string Model { get; }
        public T Instance { get; }

        public LLMProviderResult(T instance, string providerName, string model)
        {
            Instance = instance;
            ProviderName = providerName;
            Model = model;
        }
    }
}
