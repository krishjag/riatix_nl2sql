using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Riatix.Azure.NLToSQLEngine;
using Riatix.Azure.NLToSQLEngine.Controllers;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry;
using Riatix.Azure.NLToSQLEngine.Infrastructure.Telemetry.Queue;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Controllers
{
    public class QueryControllerTests
    {
        [Fact]
        public async Task Ask_Success_ReturnsOk_WithResponse_AndEnqueuesLog_UsesHeaders()
        {
            // Arrange
            var factory = new Mock<ILLMProviderFactory>();
            var translator = new Mock<IIntentTranslator>();
            var summarizer = new Mock<ISummaryGenerator>();
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var request = new QueryRequest { UserQuery = "list products", Model = "OpenAI: gpt-4o" };
            var intent = new IntentResponse
            {
                Intent = "list",
                Clarifications = new List<string> { "c1" }
            };

            translator.Setup(t => t.TranslateAsync(request.UserQuery)).ReturnsAsync(intent);
            factory.Setup(f => f.GetIntentTranslator(request.Model!))
                .Returns(new LLMProviderResult<IIntentTranslator>(translator.Object, "OpenAI", "gpt-4o"));
            factory.Setup(f => f.GetSummaryGenerator(request.Model!))
                .Returns(new LLMProviderResult<ISummaryGenerator>(summarizer.Object, "OpenAI", "gpt-4o"));

            sqlGenerator.Setup(g => g.Generate(It.IsAny<IntentResponse>())).Returns("SELECT 1");
            var resultSets = new List<List<Dictionary<string, object>>>
            {
                new()
                {
                    new() { ["col"] = 1 }
                }
            };
            executor.Setup(e => e.Execute("SELECT 1")).Returns(resultSets);

            QueryLog? capturedLog = null;
            queue.Setup(q => q.EnqueueAsync(It.IsAny<QueryLog>()))
                 .Callback<QueryLog>(l => capturedLog = l)
                 .Returns(ValueTask.CompletedTask);

            var controller = new QueryController(factory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "trace-xyz";
            httpContext.Request.Headers["X-NL2SQL-Request-ID"] = "abc-123";
            httpContext.Request.Headers["X-Forwarded-For"] = "1.2.3.4, 8.8.8.8";
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "jdoe") }, "mock"));
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var action = await controller.Ask(request);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var response = Assert.IsType<QueryResponse>(ok.Value);

            Assert.Equal("SELECT 1", response.Sql);
            Assert.Equal(new[] { "c1" }, response.Clarifications);
            Assert.Equal("list", response.Intent);
            Assert.Equal("No summary generated.", response.NaturalLanguageSummary);
            Assert.Same(resultSets, response.ResultSets);
            Assert.Equal("OpenAI", response.Provider);
            Assert.Equal("gpt-4o", response.ProviderModel);
            Assert.Equal("OpenAI", response.SummaryProvider);
            Assert.Equal("gpt-4o", response.SummaryProviderModel);

            Assert.NotNull(capturedLog);
            Assert.Equal("abc-123", capturedLog!.CorrelationId);
            Assert.Equal("1.2.3.4", capturedLog.ClientIp);
            Assert.Equal("jdoe", capturedLog.UserId);
            Assert.Equal(request.UserQuery, capturedLog.UserQuery);
            Assert.Equal(request.Model, capturedLog.Model);
            Assert.Equal("list", capturedLog.TranslatedIntent);
            Assert.Equal("SELECT 1", capturedLog.SqlQuery);
            Assert.Equal("No summary generated.", capturedLog.ResponseSummary);
            Assert.True(capturedLog.ResponseTimeMs >= 0);

            // Validate serialized intent captured in log contains the enriched fields
            var loggedIntent = JsonSerializer.Deserialize<IntentResponse>(capturedLog.IntentResponse!);
            Assert.NotNull(loggedIntent);
            Assert.Equal(request.UserQuery, loggedIntent!.NonLLMMetaData.RawUserQuery);
            Assert.Equal("abc-123", loggedIntent.NonLLMMetaData.CorrelationId);
        }

        [Fact]
        public async Task Ask_TranslatorThrows_Returns500_AndEnqueuesFailureLog_UsesCorrelationHeader()
        {
            // Arrange
            var factory = new Mock<ILLMProviderFactory>();
            var translator = new Mock<IIntentTranslator>();
            var summarizer = new Mock<ISummaryGenerator>();
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var request = new QueryRequest { UserQuery = "query", Model = "OpenAI: gpt-4o" };

            translator.Setup(t => t.TranslateAsync(request.UserQuery)).ThrowsAsync(new InvalidOperationException("boom"));
            factory.Setup(f => f.GetIntentTranslator(request.Model!))
                .Returns(new LLMProviderResult<IIntentTranslator>(translator.Object, "OpenAI", "gpt-4o"));
            factory.Setup(f => f.GetSummaryGenerator(request.Model!))
                .Returns(new LLMProviderResult<ISummaryGenerator>(summarizer.Object, "OpenAI", "gpt-4o"));

            QueryLog? capturedLog = null;
            queue.Setup(q => q.EnqueueAsync(It.IsAny<QueryLog>()))
                 .Callback<QueryLog>(l => capturedLog = l)
                 .Returns(ValueTask.CompletedTask);

            var controller = new QueryController(factory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);
            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "trace-should-not-be-used";
            httpContext.Request.Headers["X-NL2SQL-Request-ID"] = "cid-1";
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var action = await controller.Ask(request);

            // Assert
            var error = Assert.IsType<ObjectResult>(action.Result);
            Assert.Equal(500, error.StatusCode);

            // Extract anonymous object properties via reflection
            var value = error.Value!;
            var errProp = value.GetType().GetProperty("error")!;
            var cidProp = value.GetType().GetProperty("correlationId")!;
            Assert.Equal("boom", errProp.GetValue(value));
            Assert.Equal("cid-1", cidProp.GetValue(value));

            Assert.NotNull(capturedLog);
            Assert.Equal("cid-1", capturedLog!.CorrelationId);
            Assert.Equal("Error: boom", capturedLog.ResponseSummary);
            Assert.Null(capturedLog.TranslatedIntent);
            Assert.Null(capturedLog.IntentResponse);
            Assert.Null(capturedLog.SqlQuery);
        }

        [Fact]
        public void GetProviders_ResolvesFactoryFromRequestServices_ReturnsOk()
        {
            // Arrange
            var fieldFactory = new Mock<ILLMProviderFactory>(); // field not used by this endpoint
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var requestFactory = new Mock<ILLMProviderFactory>();
            var providers = new List<LLMProviderFactory.ProviderInfo>
            {
                new("OpenAI", "gpt-4o", "gpt-4o"),
                new("Grok", "grok-2", "grok-2")
            };
            requestFactory.Setup(f => f.GetAvailableProviders()).Returns(providers);

            var controller = new QueryController(fieldFactory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);
            var services = new ServiceCollection()
                .AddSingleton(requestFactory.Object)
                .BuildServiceProvider();

            var httpContext = new DefaultHttpContext { RequestServices = services };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var action = controller.GetProviders();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(action.Result);
            var returned = Assert.IsAssignableFrom<IEnumerable<LLMProviderFactory.ProviderInfo>>(ok.Value);
            Assert.Equal(providers, returned.ToList());
        }

        [Fact]
        public void GetAzureRegionMatrixAsJson_ReturnsSerializedJson()
        {
            // Arrange
            var factory = new Mock<ILLMProviderFactory>();
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var matrix = new List<Dictionary<string, object>>
            {
                new() { ["RegionName"] = "East US", ["Count"] = 5 }
            };
            sqlHelper.Setup(s => s.GetAzureRegionMatrix()).Returns(matrix);

            var controller = new QueryController(factory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);

            var expected = JsonSerializer.Serialize(matrix, new JsonSerializerOptions { WriteIndented = false });

            // Act
            var action = controller.GetAzureRegionMatrixAsJson();

            // Assert
            Assert.Null(action.Result); // implicit string return
            Assert.Equal(expected, action.Value);
        }

        [Fact]
        public void GetAzureRegionMatrixDataActionAsJson_ReturnsSerializedJson()
        {
            // Arrange
            var factory = new Mock<ILLMProviderFactory>();
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var data = new Dictionary<string, object>
            {
                ["Region"] = "West Europe",
                ["Products"] = 12
            };
            sqlHelper.Setup(s => s.GetAzureRegionMatrixDataAction()).Returns(data);

            var controller = new QueryController(factory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);

            var expected = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });

            // Act
            var action = controller.GetAzureRegionMatrixDataActionAsJson();

            // Assert
            Assert.Null(action.Result); // implicit string return
            Assert.Equal(expected, action.Value);
        }

        [Fact]
        public async Task Ask_NoCorrelationHeader_UsesTraceIdentifier_AndNoForwardedHeader_UsesRemoteIp_AndAnonymousUser()
        {
            // Arrange
            var factory = new Mock<ILLMProviderFactory>();
            var translator = new Mock<IIntentTranslator>();
            var summarizer = new Mock<ISummaryGenerator>();
            var sqlGenerator = new Mock<ISqlGenerator>();
            var executor = new Mock<ISqlExecutor>();
            var sqlHelper = new Mock<ISqlHelper>();
            var queue = new Mock<IQueryLogQueue>();
            var logger = new Mock<ILogger<QueryController>>();

            var request = new QueryRequest { UserQuery = "hello", Model = "OpenAI: gpt-4o" };
            translator.Setup(t => t.TranslateAsync(request.UserQuery))
                .ReturnsAsync(new IntentResponse { Intent = "list" });

            factory.Setup(f => f.GetIntentTranslator(request.Model!))
                .Returns(new LLMProviderResult<IIntentTranslator>(translator.Object, "OpenAI", "gpt-4o"));
            factory.Setup(f => f.GetSummaryGenerator(request.Model!))
                .Returns(new LLMProviderResult<ISummaryGenerator>(summarizer.Object, "OpenAI", "gpt-4o"));

            sqlGenerator.Setup(g => g.Generate(It.IsAny<IntentResponse>())).Returns("SELECT 1");
            executor.Setup(e => e.Execute("SELECT 1")).Returns(new List<List<Dictionary<string, object>>>());

            QueryLog? capturedLog = null;
            queue.Setup(q => q.EnqueueAsync(It.IsAny<QueryLog>()))
                 .Callback<QueryLog>(l => capturedLog = l)
                 .Returns(ValueTask.CompletedTask);

            var controller = new QueryController(factory.Object, sqlGenerator.Object, executor.Object, sqlHelper.Object, queue.Object, logger.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.TraceIdentifier = "trace-123";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("5.6.7.8");
            // No headers set; No authenticated user
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            // Act
            var result = await controller.Ask(request);

            // Assert
            Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(capturedLog);
            Assert.Equal("trace-123", capturedLog!.CorrelationId);
            Assert.Equal("5.6.7.8", capturedLog.ClientIp);
            Assert.Equal("anonymous", capturedLog.UserId);
        }
    }
}