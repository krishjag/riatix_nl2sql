using System;
using System.Collections.Generic;
using Riatix.Azure.NLToSQLEngine.Models;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services
{
    public class SqlGeneratorTests
    {
        private sealed class FakeQueryBuilder : IQueryBuilder
        {
            private readonly Func<IntentResponse, bool> _canHandleFunc;
            private readonly string _name;

            public FakeQueryBuilder(string name, Func<IntentResponse, bool> canHandleFunc, string? buildQueryResult = null)
            {
                _name = name;
                _canHandleFunc = canHandleFunc;
                BuildQueryResult = buildQueryResult ?? $"-- query from {_name}";
            }

            public string BuildQueryResult { get; set; }
            public IntentResponse? LastBuildQueryInput { get; private set; }
            public bool BuildQueryCalled => LastBuildQueryInput is not null;

            public bool CanHandle(IntentResponse intent) => _canHandleFunc(intent);

            public string BuildQuery(IntentResponse response)
            {
                LastBuildQueryInput = response;
                return BuildQueryResult;
            }

            public override string ToString() => _name;
        }

        private static IntentResponse CreateIntent(string intent = "list") => new IntentResponse
        {
            Intent = intent,
            Filters = new Filters(),
            Parameters = new Parameters()
        };

        [Fact]
        public void Generate_WhenNoBuilderCanHandle_ThrowsNotSupportedException()
        {
            var builders = new IQueryBuilder[]
            {
                new FakeQueryBuilder("A", _ => false),
                new FakeQueryBuilder("B", _ => false)
            };
            var sut = new SqlGenerator(builders);
            var intent = CreateIntent("list");

            var ex = Assert.Throws<NotSupportedException>(() => sut.Generate(intent));
            Assert.Equal("No SQL builder found for intent 'list'.", ex.Message);
        }

        [Fact]
        public void Generate_DelegatesToMatchingBuilder_AndReturnsQuery()
        {
            var accepting = new FakeQueryBuilder("Accepting", _ => true, "SELECT 1;");
            var sut = new SqlGenerator(new[] { accepting });
            var intent = CreateIntent("list");

            var sql = sut.Generate(intent);

            Assert.Equal("SELECT 1;", sql);
            Assert.Same(intent, accepting.LastBuildQueryInput);
        }

        [Fact]
        public void Generate_ChoosesFirstBuilderThatCanHandle_WhenMultipleMatch()
        {
            var first = new FakeQueryBuilder("First", _ => true, "SELECT 'first';");
            var second = new FakeQueryBuilder("Second", _ => true, "SELECT 'second';");
            var sut = new SqlGenerator(new IQueryBuilder[] { first, second });
            var intent = CreateIntent("list");

            var sql = sut.Generate(intent);

            Assert.Equal("SELECT 'first';", sql);
            Assert.True(first.BuildQueryCalled);
            Assert.False(second.BuildQueryCalled);
        }

        [Fact]
        public void Generate_SkipsBuildersThatCannotHandle_UsesFirstThatCan()
        {
            var no = new FakeQueryBuilder("No", _ => false, "SELECT 'no';");
            var yes = new FakeQueryBuilder("Yes", _ => true, "SELECT 'yes';");
            var sut = new SqlGenerator(new IQueryBuilder[] { no, yes });
            var intent = CreateIntent("list");

            var sql = sut.Generate(intent);

            Assert.Equal("SELECT 'yes';", sql);
            Assert.False(no.BuildQueryCalled);
            Assert.True(yes.BuildQueryCalled);
        }

        [Fact]
        public void Generate_Throws_WhenBuildersCollectionIsEmpty()
        {
            var sut = new SqlGenerator(Array.Empty<IQueryBuilder>());
            var intent = CreateIntent("list");

            var ex = Assert.Throws<NotSupportedException>(() => sut.Generate(intent));
            Assert.Equal("No SQL builder found for intent 'list'.", ex.Message);
        }
    }
}