using System.Collections.Concurrent;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services
{
    public class ConsensusServiceNameNormalizerTests
    {
        private sealed class FakeLoader : ICanonicalMapLoader
        {
            private readonly Dictionary<string, List<string>> _map;
            private readonly int _delayMs;
            public int LoadCalls { get; private set; }

            public FakeLoader(Dictionary<string, List<string>> map, int delayMs = 0)
            {
                _map = map;
                _delayMs = delayMs;
            }

            public Dictionary<string, List<string>> Load()
            {
                LoadCalls++; // Changed from Interlocked.Increment(ref LoadCalls);
                if (_delayMs > 0)
                {
                    Thread.Sleep(_delayMs);
                }
                // Return a new dictionary instance to ensure callers cannot mutate ours.
                return _map.ToDictionary(k => k.Key, v => v.Value.ToList());
            }
        }

        private static ConsensusServiceNameNormalizer CreateSut(
            out FakeLoader loader,
            Dictionary<string, List<string>>? map = null,
            double? threshold = null,
            int loadDelayMs = 0)
        {
            map ??= new Dictionary<string, List<string>>
            {
                // Canonical: aliases
                ["Azure AI Studio"] = new List<string> { "ai studio" },
                ["Azure Kubernetes Service"] = new List<string> { "aks" },
                ["Azure Cognitive Search"] = new List<string> { "cognitive search" },
                ["Azure Database for MySQL Flexible Server"] = new List<string> { "mysql flexible server" },
            };

            loader = new FakeLoader(map, loadDelayMs);
            var sut = new ConsensusServiceNameNormalizer(loader);
            if (threshold.HasValue)
                sut.ConfidenceThreshold = threshold.Value;
            return sut;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \t")]
        public void Normalize_ReturnsInput_WhenNullOrWhitespace(string? input)
        {
            var sut = CreateSut(out _);
            // It should return the input as-is (even null).
            var result = sut.Normalize(input!);
            Assert.Equal(input, result);
        }

        [Fact]
        public void Normalize_ReturnsInput_WhenCanonicalMapEmpty()
        {
            var sut = CreateSut(out _, new Dictionary<string, List<string>>());
            var result = sut.Normalize("ai studio");
            Assert.Equal("ai studio", result);
        }

        [Fact]
        public void Normalize_ExactAliasMatch_ReturnsCanonical()
        {
            var sut = CreateSut(out _);
            var result = sut.Normalize("ai studio");
            Assert.Equal("Azure AI Studio", result);
        }

        [Fact]
        public void Normalize_ExactCanonicalKey_ReturnsCanonical()
        {
            var sut = CreateSut(out _);
            var result = sut.Normalize("Azure Kubernetes Service");
            Assert.Equal("Azure Kubernetes Service", result);
        }

        [Theory]
        [InlineData("Search, Cognitive - Azure!")]
        [InlineData("cognitive Azure; search")]
        [InlineData("azure cognitive search")]
        [InlineData("search cognitive azure")]
        public void Normalize_IgnoresCasePunctuationAndTokenOrder_ReturnsCanonical(string input)
        {
            var sut = CreateSut(out _);
            var result = sut.Normalize(input);
            Assert.Equal("Azure Cognitive Search", result);
        }

        [Fact]
        public void Normalize_BelowThreshold_ReturnsOriginal()
        {
            // Use a very strict threshold and an input that is not an exact alias/canonical match.
            var sut = CreateSut(out _, threshold: 1.0);
            var result = sut.Normalize("azure k8s service"); // not equal to "aks" or canonical
            Assert.Equal("azure k8s service", result);
        }

        [Fact]
        public void Normalize_UsesAliasesAndCanonicalKey_AsCandidates()
        {
            var map = new Dictionary<string, List<string>>
            {
                ["Azure Database for MySQL Flexible Server"] = new List<string> { "mysql flexible server" },
                ["Azure Redis Cache"] = new List<string>(), // no aliases
            };

            var sut = CreateSut(out _, map);

            // Alias-only match
            var r1 = sut.Normalize("mysql flexible server");
            Assert.Equal("Azure Database for MySQL Flexible Server", r1);

            // Canonical-key-only match (token/order differences)
            var r2 = sut.Normalize("cache redis azure"); // matches canonical "Azure Redis Cache"
            Assert.Equal("Azure Redis Cache", r2);
        }

        [Fact]
        public void Normalize_CachesResult_PerInputKey()
        {
            var sut = CreateSut(out var loader);
            // First call populates cache and triggers Load once.
            var r1 = sut.Normalize("ai studio");
            Assert.Equal("Azure AI Studio", r1);

            // Change threshold to something impossible to reach unless exact 1.0 and ensure cache still returns prior result.
            sut.ConfidenceThreshold = 1.0;

            var r2 = sut.Normalize("ai studio");
            Assert.Equal("Azure AI Studio", r2);

            // Loader should have been called only once due to Lazy load.
            Assert.Equal(1, loader.LoadCalls);
        }

        [Fact]
        public void Normalize_CachePreservesNonMatch_IfThresholdChangesLater()
        {
            // Start with strict threshold causing a non-match.
            var sut = CreateSut(out _ , threshold: 1.0);

            var input = "azure k8s service";
            var r1 = sut.Normalize(input);
            Assert.Equal(input, r1); // no match stored

            // Lower threshold to allow a match if recomputed, but cache should return the same input.
            sut.ConfidenceThreshold = 0.0;
            var r2 = sut.Normalize(input);
            Assert.Equal(input, r2);
        }

        [Fact]
        public void Normalize_MultipleInputs_OnlyOneLoad_FromLoader()
        {
            var sut = CreateSut(out var loader);

            var inputs = new[] { "ai studio", "aks", "search, cognitive - azure!", "random service" };
            foreach (var i in inputs)
                _ = sut.Normalize(i);

            Assert.Equal(1, loader.LoadCalls);
        }

        [Fact]
        public async Task Normalize_IsThreadSafe_AndConsistent_UnderConcurrency()
        {
            var sut = CreateSut(out var loader, loadDelayMs: 30); // simulate slower load

            var inputs = new[]
            {
                "ai studio",
                "AI STUDIO",
                "search, cognitive - azure!",
                "azure cognitive search",
                "aks",
                "AKS",
                "azure k8s service", // will remain original
            };

            var bag = new ConcurrentBag<(string input, string output)>();

            await Parallel.ForEachAsync(inputs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (i, _) =>
            {
                // spread calls a bit
                await Task.Delay(Random.Shared.Next(1, 10));
                var r = sut.Normalize(i);
                bag.Add((i, r));
            });

            // Validate outputs
            Assert.Contains(bag, x => x.input == "ai studio" && x.output == "Azure AI Studio");
            Assert.Contains(bag, x => x.input == "AI STUDIO" && x.output == "Azure AI Studio");
            Assert.Contains(bag, x => x.input == "search, cognitive - azure!" && x.output == "Azure Cognitive Search");
            Assert.Contains(bag, x => x.input == "azure cognitive search" && x.output == "Azure Cognitive Search");
            Assert.Contains(bag, x => x.input == "aks" && x.output == "Azure Kubernetes Service");
            Assert.Contains(bag, x => x.input == "AKS" && x.output == "Azure Kubernetes Service");
            Assert.Contains(bag, x => x.input == "azure k8s service" && x.output == "Azure Kubernetes Service");

            // Loader should still be called once due to Lazy+thread-safety of Lazy<T>.
            Assert.Equal(1, loader.LoadCalls);
        }

        [Fact]
        public void Normalize_PrefersBestScore_AmongCompetingCanonicalCandidates()
        {
            // Craft two canonicals that share similar tokens; ensure the truly matching one wins.
            var map = new Dictionary<string, List<string>>
            {
                ["Azure AI Studio"] = new List<string> { "ai studio" },
                ["Azure AI Services"] = new List<string> { "ai services" }
            };

            var sut = CreateSut(out _, map);

            var result = sut.Normalize("ai studio");
            Assert.Equal("Azure AI Studio", result);
        }
    }
}