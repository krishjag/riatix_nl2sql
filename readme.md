[![Build and deploy Status](https://github.com/krishjag/riatix_nl2sql/actions/workflows/master_rixnl2sqlapi.yml/badge.svg)](https://github.com/krishjag/riatix_nl2sql/actions/workflows/master_rixnl2sqlapi.yml)

## Introduction

The **Riatix NL-to-SQL Engine** introduces an **alternate approach** to natural-language querying - one that focuses on **schema-aware intent translation** instead of direct SQL generation thus enabling natural language querying across enterprise data ecosystems without introducing new dependencies such as RAG pipelines, vector databases, or external semantic layers.

It is designed to work with your existing data estate, respecting your current security, governance, and authorization boundaries, while offering a seamless, explainable, and auditable bridge between natural language and structured data.

---

## Core Philosophy
The core belief behind the Riatix NL-to-SQL Engine is simple:

> Data should not have to move to become intelligent.

Instead of forcing organizations to adopt new data stores or indexing systems, the engine brings intelligence to where the data already lives. 
It directly translates user questions into optimized, parameterized SQL queries through deterministic intent parsing and schema-aware canonicalization based on a **two-step reasoning process**:

1. **Intent Translation** - The LLM converts a user's natural-language query into structured **Intent JSON** (intent, filters, parameters, clarifications).
2. **SQL Determinization** - The backend then maps that intent to deterministic, schema-safe SQL templates for execution and visualization.

This design emphasizes clarity and reliability by separating *semantic understanding* from *query construction*, resulting in:
- ✅ **Accuracy** - Contextually precise, schema-compliant queries.  
- 🔍 **Explainability** - Each step visible through the Intent JSON.  
- 🧩 **Extensibility** - Adaptable to any relational schema.
- 🔒 **Safety** - Deterministic SQL ensures governance and traceability.

The **Azure Product Matrix** serves as a **demonstration domain**, which shows how natural-language questions like  
> "Compare GA services between East US and West US"  
map cleanly to canonical intents - *List*, *Aggregation*, *Ranking*, *Difference*, or *Intersection* - and yield structured, explainable results.

> This project represents not a replacement for LLM-driven SQL generation, but an **alternative architecture** - one that grounds LLM reasoning in schema awareness for more trustworthy and auditable data interaction.

---

## Key Design Principles

#### 1. Deterministic, Explainable, and Auditable
- Every query is derived through a deterministic parsing pipeline that produces a transparent, structured JSON intent before SQL translation.  
- The resulting SQL and its execution context are fully traceable and auditable.  
- No hidden embeddings, no black-box retrieval.

#### 2. Native to Existing Data Infrastructure
- Can be extended to operate directly on existing databases such as SQL Server, PostgreSQL, Oracle or even document databases like Mongo, CosmodDB and others.  
- No need for data replication, vector stores, or proprietary schemas.

#### 3. Security and Governance Aligned
- Respects existing RBAC, ABAC, and data access policies.  
- Integrates seamlessly with enterprise authentication and authorization mechanisms.  
- Leverages your current security posture rather than reinventing it.

#### 4. Intent-Invariant Query Translation
- Uses a schema-teaching model that maps natural language to canonical intents such as list, compare, rank, and aggregate.  
- Intents remain invariant across datasets, allowing the same logical framework to apply universally within the enterprise data landscape.

#### 5. Schema-Aware Canonicalization
- Learns the structure, relationships, and synonyms within your existing schema. 

## Processing Pipeline
1. **Natural Language Input** -> interpreted by the LLM prompt.  
2. **Intent Response** -> structured JSON (`IntentResponse`) with filters, parameters, and clarifications.  
3. **SQL Builder** -> deterministic query generation per intent type.  
4. **Execution** -> SQL Server or Azure SQL executes the query.  
5. **Visualization** -> the Vue 3 [frontend](https://github.com/krishjag/riatix_nl2sql_ui) renders data in AG Grid tables and charts.  
6. **Telemetry & Logging** -> every query is correlated via `CorrelationId` for traceability.

---

## Core Intent Invariants

The engine uses **five canonical intents** to reflect the most common analytical patterns - listing, aggregating, ranking, comparing, and intersecting.  
They form the **intent invariant model** - minimal, orthogonal, and complete.

| **Intent** | **Canonical Cue(s)** | **Interpretation Logic** | **Example** |
|-------------|----------------------|---------------------------|--------------|
| **List** | Enumerative verbs: "list", "show", "display", "give me", "what are" - with no quantitative or comparative words. | Enumerate entities or pairwise mappings (e.g., Regions - SKUs). Returns raw rows without grouping or ranking. | "Show regions by VM SKUs in Europe." |
| **Aggregation** | Quantitative cues: "count", "number of", "grouped by", "how many", "breakdown by". | Summarize or count entities using `GROUP BY` and `COUNT DISTINCT`. | "Show count of VM SKUs grouped by region." |
| **Ranking** | Comparative cues: "top", "most", "highest", "largest", "fewest", "best". | Produce ordered or Top-N lists of aggregates. | "Top 5 regions by VM SKUs." |
| **Difference** | Contrastive cues: "compare", "between", "vs", "only in", "exclusive to", "but not in", "missing from". | Express set difference semantics - symmetric (A Δ B) or directional (A - B). | "Compare Europe and Asia Pacific." |
| **Intersection** | Overlap cues: "common", "shared", "available in both", "overlapping", "present across all". | Express set intersection semantics - entities available in multiple scopes simultaneously. | "Show GA services common to East US and West Europe." |

> 💡 Each intent maps deterministically to a unique SQL pattern and never overlaps another. If a query does not fit any of these five intents, it is rejected for clarification.

---

## Architecture Overview

### Layered Design

| **Layer** | **Responsibility** | **Technology Stack** |
|------------|--------------------|----------------------|
| **Intent Parser** | Translates natural language into typed intent JSON. | LLM (prompt-based, system prompt tuned for Azure products availability domain). |
| **SQL Builder Layer** | Converts intent objects to SQL text. | C# `IQueryBuilder` implementations (`ListQueryBuilder`, `AggregationQueryBuilder`, `RankingQueryBuilder`, `DifferenceQueryBuilder`, `IntersectionQueryBuilder`). |
| **Execution Layer** | Executes SQL against `dbo.products_info`. | .NET 9+/SQL Server/Azure SQL. |
| **Visualization Layer** | Displays results interactively. | Vue 3 + Vite + Tailwind + AG Grid + ECharts. |
| **Telemetry Layer** | Logs queries, execution times, and correlation IDs. | Serilog / App Insights / Custom Telemetry API. |

---

## Query Builders

Each intent is handled by a dedicated builder that implements `IQueryBuilder`.

| **Builder** | **Intent** | **SQL Pattern** | **Purpose** |
|--------------|-------------|-----------------|--------------|
| `ListQueryBuilder` | list | `SELECT DISTINCT ... WHERE ...` | Enumerations or pairwise mappings without aggregation. |
| `AggregationQueryBuilder` | aggregation | `SELECT ... COUNT(DISTINCT ...) GROUP BY ...` | Quantitative summaries and breakdowns. |
| `RankingQueryBuilder` | ranking | `SELECT TOP N ... ORDER BY COUNT(...) DESC` | Ordered leaderboards and top-N results. |
| `DifferenceQueryBuilder` | difference | Symmetric (A Δ B) or Directional (A - B) set difference. | Comparative analysis of availability across scopes. |
| `IntersectionQueryBuilder` | intersection | `SELECT ... FROM A INTERSECT SELECT ... FROM B` | Identifies services or SKUs common to multiple scopes (regions/geographies/macros). |

---

## Example End-to-End Flow

### Input
> "Show GA services available in East US but not in West US."

### Intent Response from LLM
```json
{
  "intent": "difference",
  "filters": { "CurrentState": "GA" },
  "parameters": {
    "DifferenceMode": "directional",
    "DifferenceSource": {
      "ScopeType": "RegionName",
      "ScopeValue": "East US"
    },
    "DifferenceTarget": {
      "ScopeType": "RegionName",
      "ScopeValue": "West US"
    }
  },
  "clarifications": [
    "Detected directional difference (A - B)."
  ]
}
```

### Generated SQL
```sql
SELECT DISTINCT OfferingName, ProductSkuName
FROM dbo.products_info
WHERE RegionName = 'East US' AND CurrentState = 'GA'
EXCEPT
SELECT DISTINCT OfferingName, ProductSkuName
FROM dbo.products_info
WHERE RegionName = 'West US' AND CurrentState = 'GA';
```

### Rendered Output (UI)
| OfferingName | ProductSkuName | East US | West US |
|---------------|----------------|----------|-------------|
| API Management | Premium_v2 | ✅ Yes | ❌ No |
| Event Hubs | Basic | ✅ Yes | ✅ Yes |
| App Service | Isolated_v2 | ❌ No | ✅ Yes |

---

## Example - Intersection Intent

### Input
> "Show GA services common to East US and West Europe."

### Intent Response from LLM
```json
{
  "intent": "intersection",
  "filters": {
    "CurrentState": "GA",
    "RegionName": ["East US", "West Europe"]
  },
  "parameters": {},
  "clarifications": [
    "Detected intersection (common elements) across East US and West Europe."
  ]
}
```

### Generated SQL
```sql
SELECT DISTINCT OfferingName, ProductSkuName
FROM dbo.products_info
WHERE RegionName = 'East US' AND CurrentState = 'GA'
INTERSECT
SELECT DISTINCT OfferingName, ProductSkuName
FROM dbo.products_info
WHERE RegionName = 'West Europe' AND CurrentState = 'GA';
```

### Rendered Output (UI)
| OfferingName | ProductSkuName | Common in Both |
|---------------|----------------|----------------|
| API Management | Premium_v2 | ✅ Yes |
| Event Hubs | Basic | ✅ Yes |
| App Service | Isolated_v2 | ✅ Yes |

---

## Extensibility & Future Work

| **Area** | **Goal** | **Example Enhancement** |
|-----------|-----------|--------------------------|
| **New Intents** | Add `Leaderboard`, `Trend`, `Anomaly` patterns. | "Show monthly trend of GA services." |
| **Other Domain** | Extend to datasets for different domains. | Schema mapping layer for cloud resources. |
| **Analytics Layer** | Time-series analysis and cost insights. | Integrate usage and pricing metrics. |

---

## Visualization Layer

- **Frontend Stack:** Vue 3 + Vite + Tailwind CSS  
- **Data Grid:** AG Grid (Alpine Theme, Dark Mode supported)  
- **Charts:** ECharts + Recharts for trend and composition visuals  
- **UX Features:**
  - Geography filters, status legends, and region matrix views.  
  - Dynamic switch between tabular and graphical modes.  
  - Responsive layout with smooth animations (Framer Motion).

---

## Telemetry & Observability

| **Component** | **Responsibility** |
|----------------|--------------------|
| **QueryLogService** | Persists user query, intent JSON, generated SQL, model metadata, and response time. |
| **CorrelationId** | Ensures end-to-end traceability from UI -> API -> Database. |
| **Metrics** | Average response time, cache hit ratio, LLM accuracy score. |
| **Persistence Options** | SQL log table / JSON file / In-memory queue via Dependency Injection. |

---

## 🏗️ Build & Run Instructions

### **Step 1 - Prerequisites**
- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download)
- SQL Server or Azure SQL instance  
- API Access keys for your preferred LLM providers: OpenAI, Grok, or Anthropic

---

### **Step 2 - Set Environment Variables**

Before running the API, define the following environment variables required for authentication and database connectivity:

| Variable | Description |
|-----------|--------------|
| `OpenAI_ApiKey` | API key for OpenAI models (used by `LLMProviderFactory`). |
| `Grok_ApiKey` | API key for Grok provider. |
| `Anthropic_ApiKey` | API key for Anthropic models. |
| `ConnectionStrings_SqlServer` | Connection string to the target SQL Server / Azure SQL database. |

#### Windows PowerShell example
```powershell
setx OpenAI_ApiKey "your_openai_key"
setx Grok_ApiKey "your_grok_key"
setx Anthropic_ApiKey "your_anthropic_key"
setx ConnectionStrings_SqlServer "Server=localhost;Database=yourSQLServerDB;User Id=yourID;Password=yourPassword;"
```

#### Linux / macOS example
```bash
export OpenAI_ApiKey="your_openai_key"
export Grok_ApiKey="your_grok_key"
export Anthropic_ApiKey="your_anthropic_key"
export ConnectionStrings_SqlServer="Server=localhost;Database=yourSQLServerDB;User Id=yourID;Password=yourPassword;"
```

---

### **Step 3 - Build the Project**

From the repository root directory, build the solution:

```bash
dotnet build Riatix.Azure.NLToSQLEngine.csproj
```

This restores dependencies and compiles the Web API project.

---

### **Step 4 - Run the Web API**

Start the API locally:

```bash
dotnet run Riatix.Azure.NLToSQLEngine.csproj
```

By default, the API runs on the URL defined in  
`properties/launchSettings.json` -> `applicationUrl`:

```
"http://localhost:5294"
```

You can verify the service using:

- **Swagger UI:** [http://localhost:5294/swagger](http://localhost:5294/swagger)  
- **Health Check Endpoint:** [http://localhost:5294/api/query/providers](http://localhost:5294/api/query/providers)
- Connect the [frontend](https://github.com/krishjag/riatix_nl2sql_ui) to the Web API for real-time natural-language query translation, SQL generation, and visualization.

---

✅ After completing these steps, the **Riatix Azure NL->SQL Engine** will be fully operational - ready to interpret natural-language questions, translate them into structured intents, and generate deterministic SQL queries.

---

## Contributing

Contributions are welcome via PRs or feature branches.

### Guidelines
- Follow the existing intent-builder pattern.  
- Update the system prompt and `IntentResponse` schema when adding a new intent.  
- Include unit tests (`xUnit`) for each builder and other modules where applicable.  
- Ensure new intents adhere to the invariant model or justify expansion.

---

## License

[MIT License](https://github.com/krishjag/riatix_nl2sql?tab=MIT-1-ov-file)

---

> **Maintainer:** [Jagadish Krishnan](https://github.com/krishjag)
