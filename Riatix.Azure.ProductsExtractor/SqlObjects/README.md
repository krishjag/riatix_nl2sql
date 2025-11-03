Create two tables in your Microsoft SQL database to store product information and product categories.
If using other database systems, adjust the SQL syntax accordingly. And also adjust the SqlDBHelper class.
```sql

/* Create Products table */
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[products_info](
	[Id] [int] IDENTITY(1000,1) NOT NULL,
	[RegionName] [varchar](128) NULL,
	[GeographyName] [varchar](128) NULL,
	[MacroGeographyName] [varchar](128) NULL,
	[OfferingName] [varchar](256) NULL,
	[ProductSkuName] [varchar](256) NULL,
	[CurrentState] [varchar](32) NULL,
	[InsertTimeStamp] [datetime] NULL
) ON [PRIMARY]
GO

/* Create Products Archive table */

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[products_info_archive](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RegionName] [varchar](128) NULL,
	[GeographyName] [varchar](128) NULL,
	[MacroGeographyName] [varchar](128) NULL,
	[OfferingName] [varchar](256) NULL,
	[ProductSkuName] [varchar](256) NULL,
	[CurrentState] [varchar](32) NULL,
	[InsertTimeStamp] [datetime] NULL
) ON [PRIMARY]

GO
```