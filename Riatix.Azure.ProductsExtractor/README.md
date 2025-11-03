# Riatix.Azure.ProductsExtractor

## Project overview
`Riatix.Azure.ProductsExtractor` downloads an Azure products availability page, extracts a JavaScript `data` array, validates it against a JSON schema, writes the output JSON file, and loads the results into SQL Server. Before inserting, it applies any `*.sql` files in the `SqlObjects` folder to ensure required DB objects exist.

Key files:
- `Extractor.cs` - fetches the page, extracts `const data = [...]`, validates schema, writes output JSON.
- `Loader.cs` - reads output JSON and maps entries to `ProductInfo`.
- `SqlDBHelper.cs` - applies SQL scripts from `SqlObjects` and inserts records.
- `SqlObjects/` - contains `*.sql` files (applied in alphabetical order).
- `appsettings.json` - default configuration (overridable via environment variables).

## Prerequisites
- .NET 9 SDK
- Microsoft SQL Server reachable from the host
- (Optional) Docker if running in a container

## Configuration
The app reads values from `appsettings.json` and environment variables. Required keys:

- `ConnectionStrings_SqlServer` - SQL Server connection string [Environment Variables].

- `az_base_url` - Base URL for extraction.
- `az_resource_path` - Resource path to fetch (joined with `az_base_url`).
- `az_products_data_filename` - Output JSON file path (e.g., `data/output.json`).
- `az_products_data_schema_filename` - JSON schema file path (e.g., `az_products_data_schema.json`).

Example `appsettings.json`:
```json
{
  "az_base_url": "https://azure.microsoft.com/en-us",
  "az_resource_path": "/explore/global-infrastructure/products-by-region/table",
  "az_products_data_schema_filename": "az_products_data_schema.json",
  "az_products_data_filename": "az_products_data.json"
}
```

Notes:
- Environment variables override config (use `ConnectionStrings__SqlServer` style if needed).
- Place DB initialization scripts in the `SqlObjects` directory under the project root. Files are executed in alphabetical order and batches are split on `GO` markers.

## Build (local)
From repository root:
```sh
dotnet restore
dotnet build -c Release
```

## Run (local)
Run the extractor (extract + load):
```sh
dotnet run --project Riatix.Azure.ProductsExtractor -c Release
```

## Docker
Build the image:
```sh
docker build -t riatix/products-extractor ./Riatix.Azure.ProductsExtractor
```

Run container (example; pass config via env vars and mount `SqlObjects` if needed):
```sh
docker run -e ConnectionStrings_SqlServer="Server=host.docker.internal,1433;Database=yourDB;User Id=yourSQLID;Password=yourPassword;" \    
  riatix/products-extractor
```

## Database schema and initialization
- The app will read `*.sql` from `SqlObjects/` and execute each file before inserting data.


## Logging & output
- Default log file: `logs/app.log` (path configured via `StringConstants`).
- Extracted JSON path is controlled by `az_products_data_filename`. Ensure the parent directory is writable or exists.

## Troubleshooting
- SQL connection issues: verify `ConnectionStrings_SqlServer` and network connectivity.
- Extraction fails: verify `az_base_url` + `az_resource_path` and that the page contains `const data = [...]`.
- Schema validation fails: check `az_products_data_schema_filename` and schema compatibility.
