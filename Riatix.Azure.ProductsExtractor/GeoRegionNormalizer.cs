using System.Text.Json;

namespace Riatix.Azure.ProductsExtractor
{
    public class GeographyDataModel
    {
        Dictionary<string, List<string>> _macroGeography = new Dictionary<string, List<string>>();
        Dictionary<string, string> _mapOfMacroGeography = new Dictionary<string, string>();
        public GeographyDataModel()
        {
            
        }
        public Dictionary<string, List<string>> MacroGeography { 
            get { return _macroGeography; } 
            set
            {
                _macroGeography = value;
                // Initialize the map of macro geography for quick lookup
                foreach (var macro in value)
                {
                    foreach (var geo in macro.Value)
                    {
                        _mapOfMacroGeography[geo] = macro.Key;
                    }
                }
            } 
        }        

        public string GetMacroGeography(string geography)
        {
            if (_mapOfMacroGeography.TryGetValue(geography, out var macro))
            {
                return macro;
            }
            return "Unknown";
        }

    }

    public class MacroGeographyResolver
    {
        private string filePath;
        public GeographyDataModel GeographyData { get; private set; }

        public MacroGeographyResolver(string filePath)
        {
            this.filePath = filePath;            

            this.GeographyData = JsonSerializer.Deserialize<GeographyDataModel>(new StreamReader(filePath).BaseStream)!;
        }
    }
}