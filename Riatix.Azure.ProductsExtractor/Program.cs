using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Riatix.Azure.ProductsExtractor
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(StringConstants.log_file_path, rollingInterval: RollingInterval.Day)
            .CreateLogger();

            var host = Host.CreateDefaultBuilder(args)
             .ConfigureAppConfiguration((hostingContext, config) =>
             {
                 config.AddJsonFile(StringConstants.appsettings_filename, optional: true, reloadOnChange: true);
                 config.AddEnvironmentVariables();
             })
             .ConfigureServices((context, services) =>
             {
                 services.AddTransient<IExtractor, Extractor>();
                 services.AddTransient<ILoader, Loader>();
                 services.AddTransient<IAdapter, SqlDBHelper>();
             })
             .ConfigureServices(services =>
             {
                 services.AddSingleton<MacroGeographyResolver>(sp =>
                 {
                     var env = sp.GetRequiredService<IHostEnvironment>();
                     var filePath = Path.Combine(env.ContentRootPath, "Data", "GeoRegionMappings.json");
                     return new MacroGeographyResolver(filePath);
                 });
             })
             .UseSerilog()
             .Build();

            var extractorService = host.Services.GetRequiredService<IExtractor>();
            var loaderService = host.Services.GetRequiredService<ILoader>();
            using var httpClient = new HttpClient();
            await extractorService.ExtractAsync(httpClient, CancellationToken.None);
            await loaderService.LoadAndSaveAsync(CancellationToken.None);
        }
    }
}
