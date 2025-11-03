
SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON

CREATE TABLE [dbo].[products_info_archive](
	[Id] [int] IDENTITY(1000,1) NOT NULL,
	[RegionName] [varchar](128) NULL,
	[GeographyName] [varchar](128) NULL,
	[MacroGeographyName] [varchar](128) NULL,
	[OfferingName] [varchar](256) NULL,
	[ProductSkuName] [varchar](256) NULL,
	[CurrentState] [varchar](32) NULL,
	[InsertTimeStamp] [datetime] NULL
) ON [PRIMARY]

CREATE INDEX idx_products_info_archive_region ON [dbo].[products_info_archive] ([OfferingName]);
CREATE INDEX idx_products_info_archive_geo ON [dbo].[products_info_archive] ([GeographyName] DESC);

