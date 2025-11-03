SET ANSI_NULLS ON
SET QUOTED_IDENTIFIER ON


CREATE TABLE [dbo].[query_logs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [nvarchar](100) NULL,
	[UserQuery] [nvarchar](max) NOT NULL,
	[Model] [nvarchar](200) NOT NULL,
	[TranslatedIntent] [nvarchar](max) NULL,
	[SqlQuery] [nvarchar](max) NULL,
	[ResponseSummary] [nvarchar](max) NULL,
	[ResponseTimeMs] [int] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CorrelationId] [nvarchar](100) NULL,
	[ClientIp] [nvarchar](64) NULL,
	[IntentResponse] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

ALTER TABLE [dbo].[query_logs] ADD  CONSTRAINT [DF_query_logs_CreatedAt]  DEFAULT (sysutcdatetime()) FOR [CreatedAt]


CREATE INDEX idx_query_logs_CorrelationId ON [dbo].[query_logs] ([CorrelationId]);
CREATE INDEX idx_query_logs_CreatedAt ON [dbo].[query_logs] ([CreatedAt] DESC);
CREATE INDEX idx_query_logs_Model ON [dbo].[query_logs] ([Model]);
