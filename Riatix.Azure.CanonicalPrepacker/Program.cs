using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Riatix.Azure.CanonicalPrepacker;

[MessagePackObject]
public class CanonicalMap
{
    [Key(0)] public string Version { get; set; } = $"v{DateTime.UtcNow:yyyy.MM.dd.HHmm}";
    [Key(1)] public Dictionary<string, List<string>> Map { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class Program
{
    public static int Main(string[] args)
    {
        var outputPath = args.Length > 0
        ? Path.GetFullPath(args[0])
        : Path.Combine(AppContext.BaseDirectory, "canonicalMap.bin");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var map = new CanonicalMap
        {
            Map = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Azure Kubernetes Service (AKS)"] = new()
{
"aks", "azure k8s", "azure kubernetes", "azure kubernetes service", "azure kube svc"
},
                ["API Management"] = new()
{
"api management", "apim", "azure apim", "azure api mgmt", "azure api management service",
"api management service", "api mgmt", "azure api manager", "azure management api",
"azure api gateway", "azure ap management", "azure apim service"
},

                ["App Service"] = new()
{
"app service", "app svc", "azure appsvc", "azure web app", "azure webapps", "web app",
"azure app services", "azure app service plan", "app service azure", "azure appserv",
"azure apps", "azure app hosting"
},

                ["Application Change Analysis"] = new()
{
"application change analysis", "app change analysis", "azure change analysis",
"azure application change analysis", "application change analytics",
"azure app change analysis", "change analysis", "change analysis for apps",
"azure application insight change", "app change analyzer"
},

                ["Application Gateway"] = new()
{
"application gateway", "azure app gateway", "app gateway", "appgw", "azure appgw",
"azure application gateway service", "azure gateway", "app gateway azure",
"application gw", "application load balancer", "azure lb for apps"
},

                ["Automation"] = new()
{
"automation", "azure automation", "azure automation account", "automation azure",
"azure runbook", "azure automation service", "automation service", "automation jobs",
"automation scripts", "runbook service", "automation runbook"
},

                ["Azure Active Directory B2C"] = new()
{
"azure ad b2c", "aad b2c", "ad b2c", "azure active directory b2c", "active directory b2c",
"azure b2c", "azure adb2c", "azure ad-b2c", "azure active directory business to consumer",
"azure ad consumer", "azure ad b2 customer", "azure ad b two c"
},

                ["Azure Advisor"] = new()
{
"azure advisor", "advisor", "azure advisr", "azure recommendations", "advisor azure",
"azure advisor service", "azure advisor tool", "azure advisor recommendation", "azure insights advisor"
},

                ["Azure AI Foundry"] = new()
{
"azure ai foundry", "ai foundry", "ai foundry azure", "azure aifoundry", "azure ai-foundry",
"azure foundry ai", "a i foundry", "ai foundry platform", "azure ai workspace", "azure foundry for ai"
},

                ["Azure AI Foundry Portal"] = new()
{
"azure ai foundry portal", "ai foundry portal", "foundry portal", "azure foundry portal",
"portal for ai foundry", "aifoundry portal", "azure ai foundry web portal",
"azure ai portal", "ai portal foundry"
},

                ["Azure AI Search"] = new()
{
"azure ai search", "ai search", "azure search", "azure cognitive search",
"cognitive search", "azure ai search service", "ai search azure",
"search ai azure", "azure ai-search", "azure aisearch", "azure search service",
"azure ai powered search", "azure semantic search"
},
                ["Azure Analysis Services"] = new()
{
"azure analysis services", "analysis services", "aas", "azure aas",
"azure analysis svc", "azure analysis srvc", "analysis service",
"azure analytical services", "analysis services azure",
"azure analysis", "microsoft azure analysis services"
},

                ["Azure App Configuration"] = new()
{
"azure app configuration", "app configuration", "app config", "azure app config",
"azure app configuration service", "app configuration azure", "appconfiguration",
"azure configuration service", "azure app settings", "app configuration store"
},

                ["Azure App Service Static Web Apps"] = new()
{
"azure static web apps", "static web apps", "static apps", "azure swa",
"swa", "app service static web apps", "azure static app", "azure app service static web",
"azure static websites", "azure app service static sites", "azure static webapp",
"azure static web app", "app service swa", "azure static sites"
},

                ["Azure Applied AI Services"] = new()
{
"azure applied ai services", "applied ai services", "azure applied ai",
"applied ai", "azure cognitive services", "azure ai services",
"applied intelligence services", "azure ai applied", "applied ai azure",
"applied artificial intelligence", "applied ai platform"
},

                ["Azure Arc Appliance"] = new()
{
"azure arc appliance", "arc appliance", "azure appliance", "arc-appliance",
"arc appliance azure", "azure arc-appl", "azure arc hardware appliance",
"arc management appliance", "arc hybrid appliance"
},

                ["Azure Arc enabled Kubernetes"] = new()
{
"azure arc enabled kubernetes", "arc enabled kubernetes", "arc kubernetes",
"arc k8s", "azure arc k8s", "arc enabled k8s", "azure arc-k8s",
"azure arc kubernetes cluster", "arc enabled clusters", "azure arc enabled k8s cluster"
},

                ["Azure Arc enabled System Center Virtual Machine Manager"] = new()
{
"azure arc enabled system center virtual machine manager",
"arc enabled scvmm", "azure arc scvmm", "arc scvmm",
"system center vmm arc", "arc enabled system center vmm",
"azure arc-enabled scvmm", "arc system center manager",
"azure arc system center virtual machine manager"
},

                ["Azure Arc enabled VMware vSphere"] = new()
{
"azure arc enabled vmware vsphere", "arc enabled vsphere", "arc vsphere",
"azure arc vsphere", "arc enabled vmware", "azure arc enabled vmware",
"arc vmware", "arc-enabled vmware vsphere", "arc-vmware", "azure arc-vmware"
},

                ["Azure Arc-enabled Servers"] = new()
{
"azure arc enabled servers", "arc enabled servers", "arc servers",
"azure arc servers", "arc-enabled servers", "azure arc server management",
"arc enabled machines", "arc servers azure", "azure arc for servers"
},

                ["Azure Arc-enabled SQL Server"] = new()
{
"azure arc enabled sql server", "arc enabled sql server", "arc sql server",
"azure arc sql", "azure arc sql server", "arc enabled sql", "arc-sql",
"azure arc for sql", "azure arc for sql server", "arc enabled db",
"arc sql db", "azure arc sql managed"
},
                ["Azure Automanage Machine Configuration"] = new()
{
"azure automanage machine configuration", "automanage machine configuration", "machine configuration",
"azure machine configuration", "automanage config", "automanage machine config",
"azure automanage config", "machine configuration azure", "automanage policy",
"azure automanage policy", "azure machine config"
},

                ["Azure Bastion"] = new()
{
"azure bastion", "bastion", "bastion host", "azure bastion host",
"bastion service", "azure bastion service", "bastion connection",
"azure bastion connection", "azure bastion vm", "bastion azure"
},

                ["Azure Blueprints"] = new()
{
"azure blueprints", "blueprints", "azure blueprint", "azure governance blueprint",
"blueprint service", "azure bp", "azure policy blueprint", "governance blueprint",
"blueprint templates", "azure blueprint templates"
},

                ["Azure Bot Service"] = new()
{
"azure bot service", "bot service", "azure bot", "bot framework service",
"microsoft bot service", "bot azure", "bot svc", "azure bots", "bot platform azure",
"azure bot framework"
},

                ["Azure Business Continuity Center"] = new()
{
"azure business continuity center", "business continuity center", "bcp center",
"business continuity azure", "azure bcc", "azure continuity center",
"continuity center", "azure disaster recovery center", "business continuity hub"
},

                ["Azure Carbon Optimization"] = new()
{
"azure carbon optimization", "carbon optimization", "carbon optimizer",
"azure carbon optimizer", "carbon management", "carbon reduction", "carbon optimize",
"carbon footprint optimization", "sustainability optimization", "azure sustainability"
},

                ["Azure Center for SAP Solutions"] = new()
{
"azure center for sap solutions", "sap center", "azure sap center",
"sap solutions center", "azure sap", "center for sap solutions",
"sap on azure", "azure sap services", "azure sap center solutions", "azure cfss"
},

                ["Azure Chaos Studio"] = new()
{
"azure chaos studio", "chaos studio", "chaos testing", "chaos engineering",
"azure chaos testing", "chaos studio azure", "chaos experiments", "azure chaos experiments"
},

                ["Azure Cloud HSM"] = new()
{
"azure cloud hsm", "cloud hsm", "hsm", "hardware security module", "azure hsm",
"azure dedicated hsm", "dedicated hsm", "azure cloud hardware security module",
"cloudhsm", "azure hsm service"
},

                ["Azure Communication Services"] = new()
{
"azure communication services", "communication services", "azure comms services",
"azure communications", "acs", "azure acs", "azure calling service", "azure chat service",
"azure sms service", "azure communication api", "communication service azure"
},

                ["Azure Communications Gateway"] = new()
{
"azure communications gateway", "communications gateway", "azure comm gateway",
"communication gateway", "azure comms gateway", "azure telephony gateway",
"azure pstn gateway", "azure voice gateway", "comms gateway azure"
},

                ["Azure Compute Fleet"] = new()
{
"azure compute fleet", "compute fleet", "azure fleet", "vm fleet",
"azure compute scale set", "compute scale set", "azure vm fleet",
"compute pool", "azure compute pool", "fleet manager", "azure fleet service"
},

                ["Azure Confidential Ledger"] = new()
{
"azure confidential ledger", "confidential ledger", "secure ledger",
"azure ledger", "confidential data ledger", "azure secure ledger",
"confidential blockchain", "ledger azure", "confidential log", "conf ledger"
},

                ["Azure Container Apps"] = new()
{
"azure container apps", "container apps", "container app", "azure containers",
"aca", "azure aca", "azure container service", "azure contaner servce",
"azure container apps service", "azure app containers", "azure container environment"
},

                ["Azure Container Storage"] = new()
{
"azure container storage", "container storage", "azure storage for containers",
"container volume storage", "azure container volumes", "container persistent storage",
"azure k8s storage", "azure kubernetes storage", "azure container volume service"
},

                ["Azure Cosmos DB"] = new()
{
"azure cosmos db", "cosmos db", "cosmos database", "cosmosdb",
"azure nosql", "azure documentdb", "documentdb", "cosmos nosql",
"azure cosmos", "azure cosmos database", "cosmos service", "cosmosdb azure"
},

                ["Azure Data Box"] = new()
{
"azure data box", "data box", "databox", "azure data transfer box",
"data box azure", "azure databox", "data box disk", "data box heavy",
"data box service", "data migration box"
},

                ["Azure Data Explorer"] = new()
{
"azure data explorer", "data explorer", "adx", "azure adx",
"azure kusto", "kusto", "kql explorer", "azure kql", "azure data query service",
"dataexplorer", "azure big data explorer"
},

                ["Azure Data Lake Storage Gen1"] = new()
{
"azure data lake storage gen1", "data lake gen1", "adl", "adls", "azure adls",
"azure data lake", "data lake storage", "azure data lake gen1",
"azure adls gen1", "adls gen 1", "data lake storage gen 1", "azure lake storage"
},

                ["Azure Data Manager for Energy"] = new()
{
"azure data manager for energy", "data manager for energy", "energy data manager",
"azure energy data manager", "azure edm", "energy manager azure",
"azure data energy", "energy data hub", "azure energy hub", "azure energy data platform"
},
                ["Azure Data Share"] = new()
{
"azure data share", "data share", "data sharing", "azure datashare",
"data share service", "data exchange", "data share azure",
"azure data exchange", "azure dataset sharing", "azure data distribution"
},

                ["Azure Database for MariaDB"] = new()
{
"azure database for mariadb", "mariadb", "azure mariadb", "mariadb azure",
"azure mariadb database", "mariadb service", "mariadb server",
"azure mariadb server", "azure mariadb managed", "mariadb cloud azure"
},

                ["Azure Database for MySQL"] = new()
{
"azure database for mysql", "mysql", "azure mysql", "mysql azure",
"azure mysql server", "mysql server", "azure mysql managed",
"mysql db azure", "azure database mysql", "azure mysql cloud"
},

                ["Azure Database for PostgreSQL"] = new()
{
"azure database for postgresql", "postgresql", "postgres", "azure postgres",
"azure postgresql", "postgresql azure", "azure postgresql server",
"azure postgres db", "postgresql server", "azure postgres database"
},

                ["Azure Database Migration Service"] = new()
{
"azure database migration service", "database migration service",
"dms", "azure dms", "db migration service", "database migration azure",
"azure db migration", "data migration service", "database migrator", "azure migration service"
},

                ["Azure Databricks"] = new()
{
"azure databricks", "databricks", "azure data bricks", "data bricks",
"azure spark", "spark azure", "azure databricks workspace",
"azure dbx", "databricks service", "databricks azure"
},

                ["Azure DDoS Protection"] = new()
{
"azure ddos protection", "ddos protection", "azure ddos", "ddos", "ddos service",
"azure ddos defense", "distributed denial of service protection",
"azure ddos safeguard", "ddos mitigation", "ddos protect azure"
},

                ["Azure Dedicated HSM"] = new()
{
"azure dedicated hsm", "dedicated hsm", "azure hsm", "hardware security module",
"azure hardware security module", "dedicated hardware security module",
"azure hsm service", "azure dedicated hardware", "dedicated hsm azure"
},

                ["Azure Defender for IoT"] = new()
{
"azure defender for iot", "defender for iot", "azure iot defender",
"iot defender", "azure security iot", "defender iot", "defender iot azure",
"azure iot security", "defender for internet of things"
},

                ["Azure Deployment Environments"] = new()
{
"azure deployment environments", "deployment environments", "deployment env",
"azure deploy env", "deployment service", "azure deployment service",
"deployment environment", "azure environments", "azure env deployment"
},

                ["Azure Device Registry"] = new()
{
"azure device registry", "device registry", "iot device registry",
"azure iot registry", "azure device reg", "device registration azure",
"device catalog azure", "iot registry", "azure registry for devices"
},

                ["Azure DevOps"] = new()
{
"azure devops", "devops", "azure dev ops", "ado", "azure ado",
"azure pipelines", "azure repos", "azure boards", "azure artifacts",
"devops service", "azure devops service", "devops portal", "devops azure"
},

                ["Azure DevTest Labs"] = new()
{
"azure devtest labs", "devtest labs", "devtest", "dev test labs",
"azure devtest", "azure lab services", "azure labs", "devtest lab",
"azure dev-test labs", "azure developer test labs"
},

                ["Azure Digital Twins"] = new()
{
"azure digital twins", "digital twins", "digital twin", "azure digital twin",
"digital twins service", "digital twin azure", "azure dt", "adt", "azure adt",
"digital twin platform", "azure digital modeling"
},

                ["Azure DNS"] = new()
{
"azure dns", "dns", "azure domain service", "azure name service",
"azure dns zone", "dns zone", "dns azure", "dns hosting", "domain name service azure"
},

                ["Azure Elastic SAN"] = new()
{
"azure elastic san", "elastic san", "azure san", "storage area network",
"azure elastic storage", "elastic storage azure", "azure san service",
"azure block storage", "azure elastic block", "elastic san azure"
},

                ["Azure Files"] = new()
{
"azure files", "file storage", "azure file share", "file share",
"azure smb storage", "file storage azure", "azure file services",
"azure file system", "azure fileshare", "azure file storage service"
},

                ["Azure Firewall"] = new()
{
"azure firewall", "firewall", "firewall azure", "azure network firewall",
"azure fw", "network firewall", "firewall service", "azure nfw", "azure perimeter firewall"
},

                ["Azure Firewall Manager"] = new()
{
"azure firewall manager", "firewall manager", "firewall mgmt", "firewall management",
"azure fw manager", "azure firewall mgmt", "firewall mgr", "azure firewall controller"
},

                ["Azure for Education"] = new()
{
"azure for education", "azure education", "microsoft azure for education",
"education program", "azure academic", "azure student", "azure academic access",
"azure students program", "azure for schools"
},

                ["Azure Front Door"] = new()
{
"azure front door", "front door", "afd", "azure afd", "frontdoor",
"front door service", "azure web accelerator", "front door azure",
"azure application front door", "azure fd"
},

                ["Azure Grafana Service"] = new()
{
"azure grafana service", "grafana service", "azure grafana", "managed grafana",
"azure managed grafana", "grafana azure", "grafana cloud azure", "grafana workspace"
},

                ["Azure Health AI Deidentification Service"] = new()
{
"azure health ai deidentification service", "health ai deidentification", "ai deidentification",
"azure ai deid", "azure health deid", "azure deidentification service",
"health ai service", "health ai deid", "health deidentification", "deidentify health data"
},

                ["Azure Health Bot"] = new()
{
"azure health bot", "health bot", "healthcare bot", "azure healthcare bot",
"azure medical bot", "health assistant", "azure health assistant", "health bot azure"
},

                ["Azure Health Data Services"] = new()
{
"azure health data services", "health data services", "azure health data",
"health data platform", "azure health platform", "azure healthcare data",
"azure health service", "health data api", "azure healthapi"
},

                ["Azure HPC Cache"] = new()
{
"azure hpc cache", "hpc cache", "high performance cache", "azure high performance cache",
"azure compute cache", "azure hpc storage", "azure hpc service", "azure caching for hpc"
},

                ["Azure Information Protection"] = new()
{
"azure information protection", "information protection", "aip", "azure aip",
"azure data protection", "information protection azure", "data classification",
"azure info protection", "microsoft aip", "information security azure"
},

                ["Azure IoT Operations"] = new()
{
"azure iot operations", "iot operations", "iot ops", "azure iot ops",
"azure iot management", "azure iot service operations", "iot operations azure",
"iot runtime", "iot orchestration"
},

                ["Azure Kubernetes Fleet Manager"] = new()
{
"azure kubernetes fleet manager", "kubernetes fleet manager", "aks fleet manager",
"fleet manager", "azure fleet manager", "azure k8s fleet manager",
"k8s fleet manager", "azure kubernetes fleet", "fleet manager azure"
},
                ["Azure Kubernetes Service enabled by Azure Arc"] = new()
{
"azure kubernetes service enabled by azure arc", "aks enabled by arc",
"azure arc enabled aks", "arc enabled aks", "azure arc kubernetes service",
"azure arc aks", "aks arc", "k8s enabled by arc", "azure arc managed aks",
"azure kubernetes arc", "arc-enabled kubernetes service"
},

                ["Azure Lab Services"] = new()
{
"azure lab services", "lab services", "azure labs", "azure lab",
"lab service", "azure classroom labs", "lab environment", "azure virtual lab",
"azure student labs", "azure lab svc"
},

                ["Azure Large Instance for Epic v1.1"] = new()
{
"azure large instance for epic v1.1", "large instance for epic", "epic v1.1 instance",
"azure epic instance", "azure large instance epic", "epic large instance",
"azure epic environment", "epic azure large instance"
},

                ["Azure Large Instances"] = new()
{
"azure large instances", "large instances", "large instance", "azure li",
"azure hana large instances", "hana large instances", "azure hli",
"azure large vms", "large vm instances", "large compute instance"
},

                ["Azure Lighthouse"] = new()
{
"azure lighthouse", "lighthouse", "azure management lighthouse",
"delegated resource management", "azure delegated management", "azure tenant management",
"lighthouse service", "azure customer management"
},

                ["Azure Load Balancer"] = new()
{
"azure load balancer", "load balancer", "azure lb", "lb", "network load balancer",
"azure network balancer", "azure loadbalancer", "load balancer azure",
"azure nlb", "azure public load balancer", "internal load balancer"
},

                ["Azure Load Testing"] = new()
{
"azure load testing", "load testing", "load test", "azure load test",
"azure performance testing", "azure load testing service",
"load testing azure", "azure lt", "performance test azure"
},

                ["Azure Local"] = new()
{
"azure local", "local azure", "azure local cloud", "azure local region",
"azure edge zone", "azure on premises", "azure on-prem", "azure local zone"
},

                ["Azure Managed Applications"] = new()
{
"azure managed applications", "managed applications", "managed app",
"azure managed app", "azure marketplace managed app",
"managed application service", "managed apps", "azure app management"
},

                ["Azure Managed Lustre (AMLFS)"] = new()
{
"azure managed lustre", "managed lustre", "amlfs", "azure amlfs",
"lustre filesystem", "lustre storage", "azure lustre", "azure hpc lustre",
"managed lustre azure", "azure managed lustre filesystem"
},

                ["Azure Maps"] = new()
{
"azure maps", "maps", "azure map service", "map service",
"azure location service", "maps azure", "azure mapping", "azure geolocation"
},

                ["Azure Migrate"] = new()
{
"azure migrate", "migrate", "migration service", "migration tool",
"azure migration", "azure migration center", "azure migration service",
"migration hub", "azure migrate hub"
},

                ["Azure Monitor"] = new()
{
"azure monitor", "monitor", "monitoring", "azure monitoring",
"azure observability", "metrics and logs", "azure insights",
"monitor azure", "monitor service", "azure monitoring service"
},

                ["Azure Monitor for SAP Solutions"] = new()
{
"azure monitor for sap solutions", "monitor for sap", "sap monitor",
"azure sap monitoring", "sap monitoring azure", "azure sap monitor",
"azure sap observability", "sap solution monitor"
},

                ["Azure Monitor Grafana"] = new()
{
"azure monitor grafana", "monitor grafana", "azure grafana monitor",
"azure managed grafana", "grafana integration", "azure grafana dashboards",
"azure monitor with grafana", "grafana for monitor"
},

                ["Azure NetApp Files"] = new()
{
"azure netapp files", "anf", "netapp files", "azure anf", "netapp azure",
"azure nas", "azure netapp storage", "netapp storage azure",
"azure netapp file service", "netapp filesystem"
},

                ["Azure Network Function Manager"] = new()
{
"azure network function manager", "network function manager", "nfm",
"azure nfm", "network functions manager", "network manager azure",
"azure function manager network", "telecom network manager"
},

                ["Azure Open Datasets"] = new()
{
"azure open datasets", "open datasets", "open data", "azure datasets",
"azure public datasets", "open data azure", "datasets azure",
"azure data catalog", "azure data sets"
},

                ["Azure Operator Service Manager"] = new()
{
"azure operator service manager", "operator service manager", "osm",
"azure osm", "telecom operator manager", "azure operator manager",
"operator manager", "operator platform azure"
},

                ["Azure Orbital Ground Station"] = new()
{
"azure orbital ground station", "orbital ground station", "orbital station",
"azure orbital", "azure ground station", "satellite ground station",
"azure satellite", "orbital azure", "azure satellite communication"
},

                ["Azure Payment HSM"] = new()
{
"azure payment hsm", "payment hsm", "azure hsm payment",
"azure financial hsm", "payment hardware security module",
"hsm payment", "azure finance hsm", "azure banking hsm"
},

                ["Azure Performance Diagnostics"] = new()
{
"azure performance diagnostics", "performance diagnostics", "azure perf diag",
"perf diagnostics", "performance analysis", "azure performance analysis",
"azure diagnostics", "performance troubleshoot azure"
},

                ["Azure Policy"] = new()
{
"azure policy", "policy", "azure governance policy", "resource policy",
"policy management", "policy azure", "azure policy service",
"azure compliance policy", "policy enforcement azure"
},

                ["Azure Private Link"] = new()
{
"azure private link", "private link", "private endpoint", "azure private endpoint",
"azure private network link", "azure private access", "private connectivity",
"private link service", "private link azure"
},

                ["Azure Quantum"] = new()
{
"azure quantum", "quantum", "quantum computing", "quantum service",
"quantum azure", "quantum workspace", "azure quantum service", "quantum computing azure"
},

                ["Azure Red Hat OpenShift (ARO)"] = new()
{
"azure red hat openshift", "aro", "azure aro", "red hat openshift",
"openshift", "azure openshift", "openshift azure", "azure rh openshift",
"redhat openshift", "azure redhat openshift"
},

                ["Azure Remote Rendering"] = new()
{
"azure remote rendering", "remote rendering", "rendering service",
"azure rendering", "3d rendering azure", "remote visualization", "azure 3d render"
},

                ["Azure Resource Graph"] = new()
{
"azure resource graph", "resource graph", "resource query",
"azure query", "resource explorer", "resource search", "azure resource search",
"azure inventory graph"
},

                ["Azure Resource Manager"] = new()
{
"azure resource manager", "arm", "azure arm", "resource manager",
"azure deployment manager", "resource management", "arm templates",
"azure rm", "resource manager azure"
},

                ["Azure Route Server"] = new()
{
"azure route server", "route server", "routing server", "azure routing",
"azure bgp route server", "bgp server", "azure network route server",
"route service", "route server azure"
},
                ["Azure Service Manager (RDFE)"] = new()
{
"azure service manager", "service manager", "rdf e", "rdfm", "azure rdf e",
"azure classic deployment", "classic service manager", "asm",
"azure asm", "azure classic model", "azure service management"
},

                ["Azure SignalR Service"] = new()
{
"azure signalr service", "signalr service", "signalr", "azure signalr",
"realtime service", "azure realtime", "signalr hub", "signalr azure",
"real-time service azure", "azure realtime messaging"
},

                ["Azure Signup Portal"] = new()
{
"azure signup portal", "signup portal", "azure registration portal",
"azure sign up", "azure signup", "azure portal signup",
"azure free account portal", "azure trial signup"
},

                ["Azure Sphere"] = new()
{
"azure sphere", "sphere", "iot sphere", "azure iot sphere",
"azure secure iot", "azure sphere os", "azure sphere device", "sphere azure"
},

                ["Azure Spring Apps"] = new()
{
"azure spring apps", "spring apps", "spring cloud", "azure spring cloud",
"azure spring service", "springboot azure", "springboot service",
"spring app service", "spring cloud azure", "azure springboot"
},

                ["Azure SQL Database"] = new()
{
"azure sql database", "sql database", "azure sql", "sql db", "azure sqldb",
"azure database", "azure relational db", "azure sql db service",
"azure sql service", "mssql azure", "azure managed sql"
},

                ["Azure SQL Managed Instance"] = new()
{
"azure sql managed instance", "sql managed instance", "managed instance",
"azure sql mi", "sql mi", "azure mi", "azure managed sql instance",
"azure sql server managed", "managed sql azure"
},

                ["Azure Stack"] = new()
{
"azure stack", "stack", "azure hybrid", "azure stack hub", "stack hub",
"azure private cloud", "azure onprem", "azure stack hybrid", "stack azure"
},

                ["Azure Stack Edge"] = new()
{
"azure stack edge", "stack edge", "azure edge", "azure edge appliance",
"azure edge device", "stackedge", "azure stack hardware", "edge device azure"
},

                ["Azure Storage Actions"] = new()
{
"azure storage actions", "storage actions", "storage automation",
"azure blob actions", "azure file actions", "storage task azure",
"azure storage triggers", "azure storage workflow"
},

                ["Azure Storage Mover"] = new()
{
"azure storage mover", "storage mover", "storage migration", "azure data mover",
"azure storage transfer", "azure mover", "azure storage migration",
"data mover azure", "storage mover service"
},

                ["Azure Stream Analytics"] = new()
{
"azure stream analytics", "stream analytics", "asa", "azure asa",
"stream processing", "streaming analytics", "real time analytics",
"azure streaming", "azure stream service", "azure stream processor"
},

                ["Azure Synapse Analytics"] = new()
{
"azure synapse analytics", "synapse", "azure synapse", "synapse workspace",
"azure dw", "data warehouse", "azure data warehouse", "azure synapse studio",
"synapse azure", "azure analytics service"
},

                ["Azure Update Manager"] = new()
{
"azure update manager", "update manager", "patch manager", "azure patching",
"update management", "azure update management", "update service azure",
"patch management azure"
},

                ["Azure Virtual Desktop"] = new()
{
"azure virtual desktop", "avd", "virtual desktop", "windows virtual desktop",
"wvd", "azure vdi", "azure desktop", "azure desktop virtualization",
"virtual desktop azure", "azure vd"
},

                ["Azure Virtual Network Manager"] = new()
{
"azure virtual network manager", "vnet manager", "network manager",
"azure vnet manager", "vnm", "azure network manager",
"azure virtual network mgmt", "network management azure"
},

                ["Azure VM Image Builder"] = new()
{
"azure vm image builder", "vm image builder", "image builder",
"azure image builder", "azure custom image", "vm builder",
"azure vm template", "image build service", "azure vm imaging"
},

                ["Azure VMware Solution"] = new()
{
"azure vmware solution", "avs", "vmware solution", "azure vmware",
"azure vsphere", "azure vmware cloud", "azure vmware service",
"azure vmware virtualization", "azure vmw solution"
},

                ["Azure Web Application Firewall"] = new()
{
"azure web application firewall", "waf", "azure waf", "web application firewall",
"application firewall", "azure waf policy", "azure waf service",
"web firewall", "azure waf config"
},

                ["Azure Web PubSub"] = new()
{
"azure web pubsub", "web pubsub", "pubsub", "pub sub", "pub/sub",
"websocket service", "azure websocket", "real-time messaging azure",
"web pub sub", "azure realtime pubsub"
},

                ["Backup"] = new()
{
"backup", "azure backup", "azure recovery", "backup vault",
"azure backup service", "backup azure", "recovery service", "azure data backup",
"vm backup", "backup storage azure"
},

                ["Bandwidth"] = new()
{
"bandwidth", "network bandwidth", "azure bandwidth", "data transfer",
"azure data transfer", "azure egress", "azure ingress", "azure throughput"
},

                ["Batch"] = new()
{
"batch", "azure batch", "batch compute", "azure batch service",
"batch processing", "batch jobs", "batch execution", "azure batch processing"
},

                ["BizTalk Services"] = new()
{
"biztalk services", "azure biztalk", "biztalk", "azure biztalk service",
"integration service", "azure biztalk integration", "azure biztalk server"
},

                ["Cloud Services"] = new()
{
"cloud services", "azure cloud services", "cloud service", "azure hosted service",
"cloud service classic", "azure role service", "azure web role", "azure worker role"
},

                ["Cloud Services Extended Support"] = new()
{
"cloud services extended support", "csex", "azure cloud services extended support",
"cloud services es", "extended support cloud services", "azure classic extended",
"extended cloud service", "cloud service extension"
},

                ["Cloud Shell"] = new()
{
"cloud shell", "azure cloud shell", "shell", "azure bash", "azure powershell",
"azure cli shell", "browser shell", "azure terminal", "cloudshell"
},

                ["Container Instances"] = new()
{
"container instances", "azure container instances", "aci", "azure aci",
"container instance", "azure container service", "azure container run",
"azure container compute", "container compute azure"
},

                ["Container Registry"] = new()
{
"container registry", "azure container registry", "acr", "azure acr",
"registry azure", "docker registry azure", "azure docker repo",
"container repo", "azure container repository"
},

                ["Content Delivery Network"] = new()
{
"content delivery network", "cdn", "azure cdn", "azure content delivery network",
"cdn service", "azure edge cdn", "azure frontdoor cdn", "content cache",
"content distribution", "cdn azure"
},
                ["Cost Management"] = new()
{
"cost management", "azure cost management", "cost analysis", "azure costs",
"cost optimization", "billing", "azure billing", "billing management",
"azure cost center", "cost insights", "azure spend analysis", "azure billing portal"
},

                ["Customer Lockbox for Microsoft Azure"] = new()
{
"customer lockbox for microsoft azure", "customer lockbox", "azure customer lockbox",
"lockbox", "lockbox azure", "customer data access approval", "microsoft azure lockbox"
},

                ["Data Factory"] = new()
{
"data factory", "azure data factory", "adf", "azure adf", "data pipeline",
"data orchestration", "dataflow azure", "data factory service", "azure etl",
"azure data pipelines"
},

                ["Defender TI"] = new()
{
"defender ti", "microsoft defender threat intelligence", "threat intelligence",
"azure defender ti", "defender threat intel", "threat intel", "defender ti service"
},

                ["Device Update for IoT Hub"] = new()
{
"device update for iot hub", "device update", "iot hub device update",
"azure device update", "azure iot device update", "iot firmware update",
"azure iot update", "device patching", "iot update service"
},

                ["Event Grid"] = new()
{
"event grid", "azure event grid", "eventing service", "azure eventing",
"event router", "eventgrid", "azure pubsub grid", "event distribution azure"
},

                ["Event Hubs"] = new()
{
"event hubs", "azure event hubs", "event hub", "event streaming", "azure event streaming",
"azure kafka", "event ingestion", "event stream azure", "azure eventhub", "event queue azure"
},

                ["ExpressRoute"] = new()
{
"expressroute", "azure expressroute", "express route", "private connection",
"dedicated circuit", "azure private connection", "azure direct connect",
"azure express route", "azure wan connection"
},

                ["Firmware Analysis"] = new()
{
"firmware analysis", "azure firmware analysis", "firmware scanning",
"firmware security", "firmware analyzer", "firmware check", "firmware validation"
},

                ["Fluid Relay Service"] = new()
{
"fluid relay service", "azure fluid relay", "fluid relay", "collaboration service",
"fluid framework", "azure real time collab", "fluid azure"
},

                ["Functions"] = new()
{
"functions", "azure functions", "function app", "faas", "serverless functions",
"azure faas", "azure function", "azure functions app", "functions azure", "function apps"
},

                ["HDInsight"] = new()
{
"hdinsight", "azure hdinsight", "hadoop", "spark cluster", "azure hadoop",
"azure spark", "hdinsight cluster", "hdinsight service", "big data azure"
},

                ["IoT Central"] = new()
{
"iot central", "azure iot central", "iot saas", "iot platform", "azure iot platform",
"iot management", "iot central app", "iot central azure", "azure iot saas"
},

                ["IoT Hub"] = new()
{
"iot hub", "azure iot hub", "iot messaging", "iot broker", "azure device hub",
"azure iot gateway", "iot connection hub", "iot service azure"
},

                ["IP Services"] = new()
{
"ip services", "azure ip services", "public ip", "ip management", "ip addresses",
"ip allocation", "ip routing", "azure ip manager", "ip networking", "azure ips"
},

                ["Key Vault"] = new()
{
"key vault", "azure key vault", "vault", "secret store", "secrets manager",
"azure secrets", "azure certificate vault", "azure kv", "azure key store", "azure keyvault"
},

                ["Logic Apps"] = new()
{
"logic apps", "azure logic apps", "logic app", "workflow automation",
"azure workflow", "azure automation workflow", "logic workflow",
"azure logic", "integration workflow", "azure automation logic"
},

                ["Managed DevOps Pools"] = new()
{
"managed devops pools", "azure managed devops pools", "devops pool",
"hosted agents", "azure hosted pools", "devops hosted agents",
"build pool", "azure devops managed pools"
},

                ["Microsoft Azure Attestation"] = new()
{
"microsoft azure attestation", "azure attestation", "maa", "attestation service",
"secure attestation", "azure confidential attestation", "attestation azure"
},

                ["Microsoft Azure classic portal"] = new()
{
"microsoft azure classic portal", "azure classic portal", "classic portal",
"old azure portal", "azure classic ui", "azure old portal", "azure legacy portal"
},

                ["Microsoft Azure Data Manager for Agriculture"] = new()
{
"microsoft azure data manager for agriculture", "data manager for agriculture",
"azure agriculture data manager", "agriculture data", "azure agri data",
"agri data manager", "agriculture data platform"
},

                ["Microsoft Azure Managed Instance for Apache Cassandra"] = new()
{
"microsoft azure managed instance for apache cassandra",
"managed instance for apache cassandra", "cassandra managed instance",
"azure cassandra", "azure cassandra managed", "apache cassandra azure",
"cassandra service", "azure cassandra service"
},

                ["Microsoft Azure Peering Service"] = new()
{
"microsoft azure peering service", "peering service", "azure peering",
"internet peering", "azure isp peering", "peering connectivity", "azure peer service"
},

                ["Microsoft Azure portal"] = new()
{
"microsoft azure portal", "azure portal", "portal", "azure web portal",
"azure management portal", "azure console", "azure dashboard", "portal.azure.com"
},

                ["Microsoft Defender for Cloud"] = new()
{
"microsoft defender for cloud", "defender for cloud", "azure defender",
"cloud defender", "defender cloud", "azure security center", "security center",
"azure cloud security", "defender cloud security"
},

                ["Microsoft Defender for DevOps"] = new()
{
"microsoft defender for devops", "defender for devops", "azure defender devops",
"devops defender", "devops security", "azure devops security", "defender pipelines"
},

                ["Microsoft Defender for Identity"] = new()
{
"microsoft defender for identity", "defender for identity", "azure defender identity",
"identity protection", "azure identity defender", "aad id protection", "identity security"
},

                ["Microsoft Dev Box"] = new()
{
"microsoft dev box", "dev box", "azure dev box", "developer box",
"dev workstation", "azure dev workstation", "developer environment azure",
"dev environment", "azure development vm"
},

                ["Microsoft Entra Domain Services"] = new()
{
"microsoft entra domain services", "entra domain services", "azure ad ds",
"aad ds", "domain services", "entra ds", "azure domain services",
"entra directory services", "azure managed domain"
},

                ["Microsoft Entra ID"] = new()
{
"microsoft entra id", "entra id", "entra identity", "azure ad", "aad",
"azure active directory", "azure directory", "entra", "entra identity service",
"azure entra"
},
                ["Microsoft Fabric"] = new()
{
"microsoft fabric", "fabric", "ms fabric", "azure fabric", "data fabric",
"microsoft data fabric", "fabric workspace", "fabric analytics",
"power bi fabric", "fabric service"
},

                ["Microsoft Graph"] = new()
{
"microsoft graph", "graph api", "ms graph", "graph", "graph azure",
"graph service", "microsoft graph service", "graph endpoint"
},

                ["Microsoft Sentinel"] = new()
{
"microsoft sentinel", "sentinel", "azure sentinel", "defender xdr",
"microsoft defender xdr", "siem", "soc platform", "azure security sentinel",
"microsoft xdr", "sentinel siem"
},

                ["Multi-Factor Authentication"] = new()
{
"multi-factor authentication", "mfa", "two factor", "2fa",
"azure mfa", "azure two factor", "microsoft mfa", "azure authentication",
"two-step verification", "mfa service"
},

                ["Network Security Perimeter"] = new()
{
"network security perimeter", "nsp", "azure nsp", "security perimeter",
"azure perimeter", "network boundary", "perimeter security", "azure perimeter service"
},

                ["Network Watcher"] = new()
{
"network watcher", "azure network watcher", "network diagnostics",
"azure netwatcher", "network monitoring", "azure packet capture",
"network insights", "azure connection monitor"
},

                ["Notification Hubs"] = new()
{
"notification hubs", "azure notification hubs", "notification hub",
"push notifications", "azure push service", "push hub", "mobile notifications",
"azure messaging hub"
},

                ["Nutanix on Azure"] = new()
{
"nutanix on azure", "azure nutanix", "nutanix", "nutanix cluster azure",
"azure nutanix cloud", "nutanix hybrid", "nutanix service azure",
"nutanix hci azure"
},

                ["Planned Maintenance"] = new()
{
"planned maintenance", "maintenance", "scheduled maintenance",
"azure maintenance", "maintenance window", "platform maintenance",
"update window", "maintenance schedule"
},

                ["Power BI"] = new()
{
"power bi", "powerbi", "microsoft power bi", "bi service", "azure bi",
"data visualization", "bi analytics", "bi dashboards", "powerbi service"
},

                ["Private MEC"] = new()
{
"private mec", "mobile edge computing", "azure mec", "private 5g",
"azure private 5g", "private edge", "azure mec solution", "mec service"
},

                ["Quota Management"] = new()
{
"quota management", "azure quota management", "quotas", "resource quota",
"azure limits", "usage quota", "quota settings", "azure quota service"
},

                ["Quota+ Usage blade"] = new()
{
"quota+ usage blade", "quota usage", "azure quota usage",
"quota and usage", "usage blade", "azure usage monitor", "usage insights"
},

                ["Redis Cache"] = new()
{
"redis cache", "azure redis cache", "cache", "azure cache",
"azure redis", "redis service", "azure cache for redis", "redis azure"
},

                ["Resource Move"] = new()
{
"resource move", "move resources", "azure resource move",
"subscription move", "resource migration", "azure resource migration",
"move azure resources", "move resource groups"
},

                ["Scheduler"] = new()
{
"scheduler", "azure scheduler", "job scheduler", "task scheduler",
"azure job scheduling", "scheduled tasks", "azure cron", "background jobs"
},

                ["Security Platform (Purview)"] = new()
{
"security platform purview", "microsoft purview", "purview",
"azure purview", "data governance", "compliance center",
"security compliance", "purview security platform", "purview compliance"
},

                ["Service Bus"] = new()
{
"service bus", "azure service bus", "message bus", "queue service",
"topic subscription", "messaging service", "event bus", "azure sb",
"asb", "service bus azure"
},

                ["Service Connector"] = new()
{
"service connector", "azure service connector", "connection manager",
"connector", "azure connector", "link services", "service linking",
"azure resource connector"
},

                ["Service Fabric"] = new()
{
"service fabric", "azure service fabric", "fabric cluster",
"sf", "azure sf", "microservices platform", "service fabric cluster",
"servicefabric", "fabric azure"
},

                ["Site Recovery"] = new()
{
"site recovery", "azure site recovery", "asr", "disaster recovery",
"azure dr", "azure recovery", "azure backup and recovery",
"failover site", "azure asr"
},

                ["SQL Managed Instance enabled by Azure Arc"] = new()
{
"sql managed instance enabled by azure arc", "arc enabled sql managed instance",
"arc sql mi", "sql mi arc", "azure arc sql mi", "azure sql mi arc",
"arc sql managed", "azure arc managed sql", "arc-enabled sql instance"
},

                ["SQL Server on Azure Virtual Machines"] = new()
{
"sql server on azure virtual machines", "azure sql vm", "sql vm",
"sql on vm", "azure sql server vm", "sql virtual machine",
"sql azure vm", "azure sql iaas", "sql server azure vm"
},

                ["Standby Pool"] = new()
{
"standby pool", "azure standby pool", "vm standby pool",
"hot standby", "reserved compute pool", "prewarmed vm",
"standby compute", "standby pool service"
},

                ["Storage"] = new()
{
"storage", "azure storage", "storage account", "blob storage",
"file storage", "table storage", "queue storage", "azure storage account",
"azure blob", "azure files", "azure tables", "azure storage service"
},

                ["Time Series Insights"] = new()
{
"time series insights", "tsi", "azure tsi", "time series", "azure time series",
"iot analytics", "time analytics", "time series data", "iot insights azure"
},

                ["Traffic Manager"] = new()
{
"traffic manager", "azure traffic manager", "traffic routing",
"dns traffic", "load balancing dns", "azure geo routing",
"global traffic", "traffic control", "tm azure"
},

                ["Trusted Hardware Identity Management"] = new()
{
"trusted hardware identity management", "thim", "trusted hardware",
"hardware identity", "azure hardware trust", "secure hardware identity",
"azure thim", "trusted platform identity"
},

                ["Universal Print"] = new()
{
"universal print", "azure universal print", "cloud printing",
"print service", "microsoft print", "azure print service", "up azure",
"universal printer"
},

                ["Virtual Machine Scale Sets"] = new()
{
"virtual machine scale sets", "vmss", "scale sets", "azure scale sets",
"azure vmss", "scaling vm", "auto scale vm", "scale set azure"
},

                ["Virtual Machines"] = new()
{
"virtual machines", "vms", "azure vm", "virtual machine", "azure virtual machine",
"azure iaas", "vm", "vm azure", "azure compute", "azure servers"
},

                ["Virtual Machines Licenses"] = new()
{
"virtual machines licenses", "vm licenses", "vm licensing",
"azure hybrid benefit", "azure vm licensing", "windows server license azure",
"azure license management", "vm license"
},

                ["Virtual Network"] = new()
{
"virtual network", "vnet", "azure vnet", "azure virtual network",
"virtual networking", "network vnet", "vnet azure", "azure network",
"private network azure"
},

                ["Virtual Network NAT"] = new()
{
"virtual network nat", "vnet nat", "nat gateway", "azure nat gateway",
"network address translation", "azure nat", "private nat",
"nat service", "azure vnet nat"
},

                ["Virtual WAN"] = new()
{
"virtual wan", "vwan", "azure vwan", "wan", "azure wide area network",
"azure wan", "global wan", "network hub azure", "azure global network"
},

                ["Visual Studio App Center"] = new()
{
"visual studio app center", "app center", "vs app center",
"mobile devops", "azure app center", "microsoft appcenter",
"app distribution", "app analytics"
},

                ["VMWatch"] = new()
{
"vmwatch", "vm monitor", "vm health", "azure vmwatch", "vm metrics",
"azure vm monitoring", "azure vm observer", "vm insight", "vm telemetry"
},

                ["VPN Gateway"] = new()
{
"vpn gateway", "azure vpn gateway", "vpn", "azure vpn", "gateway",
"vpn connection", "azure site-to-site vpn", "s2s vpn",
"point to site vpn", "azure vpn service", "vpn network"
}
            }

        };

        // Serialize with LZ4 compression
        var bytes = MessagePackSerializer.Serialize(map,
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray));

        File.WriteAllBytes(outputPath, bytes);

        // emit checksum for CI validation
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        var hashFile = Path.ChangeExtension(outputPath, ".sha256");
        File.WriteAllText(hashFile, Convert.ToHexString(hash));

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Canonical map packed: {outputPath}");
        Console.ResetColor();

        Console.WriteLine($"Entries: {map.Map.Count}");
        Console.WriteLine($"Version: {map.Version}");
        Console.WriteLine($"Binary Size: {bytes.Length / 1024.0:F1} KB");
        Console.WriteLine($"SHA256: {Convert.ToHexString(hash)}");
        return 0;
    }
}
