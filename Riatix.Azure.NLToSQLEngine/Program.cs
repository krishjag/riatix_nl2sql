using Microsoft.Data.SqlClient;
using Riatix.Azure.NLToSQLEngine;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Models.Examples;
using Riatix.Azure.NLToSQLEngine.QueryBuilders;
using Riatix.Azure.NLToSQLEngine.Services;
using Swashbuckle.AspNetCore.Filters;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
string sqlConnectionString = builder.Configuration["ConnectionStrings_SqlServer"]!.ToString();

builder.Services.AddTelemetryPipeline();

builder.Services.AddSingleton<ICanonicalMapLoader>(
    _ => new CanonicalMapLoader("Assets/canonicalMap.bin"));

builder.Services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(sqlConnectionString));

builder.Services.AddSingleton<IRegionHierarchyCache, RegionHierarchyCache>();
builder.Services.AddHostedService<RegionHierarchyPrewarmService>();

// Register azure service name normalizer: used by the query builders
builder.Services.AddSingleton<IServiceNameNormalizer, ConsensusServiceNameNormalizer>();

// Register HttpClient for the loader
builder.Services.AddHttpClient<ProductCategoryMapLoader>();

// Register the background warm-up service
builder.Services.AddHostedService<ProductCategoryMapWarmupService>();

// Register IProductCategoryMap using the warmed-up cache
builder.Services.AddSingleton<IProductCategoryMap>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<IProductCategoryMap>>();
    var map = ProductCategoryMapWarmupService.GetCachedMap();

    if (map == null)
    {
        logger.LogWarning("ProductCategoryMap cache was not yet ready, loading synchronously...");
        var loader = sp.GetRequiredService<ProductCategoryMapLoader>();
        var data = loader.LoadAsync().GetAwaiter().GetResult();
        map = new ProductCategoryMap(data);
    }

    logger.LogInformation(
        "ProductCategoryMap registered with {Count} categories.",
        map.GetAllCategories().Count
    );

    return map;
});


// Register query builders
builder.Services.AddScoped<IQueryBuilder, DifferenceQueryBuilder>();
builder.Services.AddScoped<IQueryBuilder, RankingQueryBuilder>();
builder.Services.AddScoped<IQueryBuilder, AggregationQueryBuilder>();
builder.Services.AddScoped<IQueryBuilder, ListQueryBuilder>();
builder.Services.AddScoped<IQueryBuilder, IntersectionQueryBuilder>();
builder.Services.AddScoped<IQueryBuilder, DoNothingQueryBuilder>();
builder.Services.AddScoped<ISqlGenerator, SqlGenerator>();

// Register SQL executor
builder.Services.AddSingleton<ISqlExecutor>(sp =>
    new SqlExecutor(sqlConnectionString));

builder.Services.AddSingleton<ISqlHelper>(sp => 
    new SqlHelper(sqlConnectionString));

// Register LLM factory
builder.Services.AddSingleton<ILLMProviderFactory, LLMProviderFactory>();

// Register default translator & summarizer (resolved from factory)
builder.Services.AddScoped<IIntentTranslator>(sp =>
{
    var factory = sp.GetRequiredService<ILLMProviderFactory>();
    var result = factory.GetIntentTranslator(); // default from config
    return result.Instance;
});

builder.Services.AddScoped<ISummaryGenerator>(sp =>
{
    var factory = sp.GetRequiredService<ILLMProviderFactory>();
    var result = factory.GetSummaryGenerator(); // default from config
    return result.Instance;
});

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.ExampleFilters(); //enable example providers
});
builder.Services.AddSwaggerExamplesFromAssemblyOf<QueryRequestExample>();


// Add CORS policy
var policyName = "AllowClient";
builder.Services.AddCors(options =>
{
    options.AddPolicy(policyName,
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:8080", "https://nl2sql.jagadishkrishnan.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });    
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(policyName);
app.UseAuthorization();
app.MapControllers();
app.Run();
