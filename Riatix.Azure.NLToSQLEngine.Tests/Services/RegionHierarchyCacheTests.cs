using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Riatix.Azure.NLToSQLEngine.Services;
using Xunit;

namespace Riatix.Azure.NLToSQLEngine.Tests.Services
{
    public class RegionHierarchyCacheTests
    {
        // Common sample data for tests
        private static readonly (string? Macro, string? Geography, string Region)[] SampleRows =
        {
            ("EMEA", "Europe", "West Europe"),
            ("EMEA", "Europe", "North Europe"),
            ("AMER", "US", "East US"),
            ("AMER", "US", "West US"),
            // Duplicate row to ensure sets de-duplicate
            ("AMER", "US", "East US"),
            // Row with null macro/geo to ensure maps ignore but parent still recorded
            (null, null, "Unknown Region")
        };

        private static RegionHierarchyCache CreateCacheWithRows(params (string? Macro, string? Geography, string Region)[] rows)
        {
            var connection = new FakeDbConnection(
                new[] { "MacroGeographyName", "GeographyName", "RegionName" },
                rows.Select(r => new object?[] { r.Macro, r.Geography, r.Region }).ToArray()
            );

            var scopeFactory = new FakeServiceScopeFactory(connection);
            return new RegionHierarchyCache(scopeFactory);
        }

        [Fact]
        public async Task PreWarmAsync_LoadsData_AndPopulatesLookups()
        {
            var cache = CreateCacheWithRows(SampleRows);
            await cache.PreWarmAsync();

            // Geographies -> Regions
            var euRegions = cache.GetRegionsForGeographies(new[] { "Europe" });
            Assert.True(euRegions.SetEquals(new[] { "West Europe", "North Europe" }));

            // Macros -> Regions
            var amerRegions = cache.GetRegionsForMacros(new[] { "AMER" });
            Assert.True(amerRegions.SetEquals(new[] { "East US", "West US" }));

            // Parents
            Assert.True(cache.TryGetParentGeography("East US", out var geo, out var macro));
            Assert.Equal("US", geo);
            Assert.Equal("AMER", macro);

            // Null macro/geo still recorded as parents
            Assert.True(cache.TryGetParentGeography("Unknown Region", out var geo2, out var macro2));
            Assert.Null(geo2);
            Assert.Null(macro2);
        }

        [Fact]
        public async Task PreWarmAsync_CalledMultipleTimes_LoadsOnlyOnce()
        {
            var connection = new FakeDbConnection(
                new[] { "MacroGeographyName", "GeographyName", "RegionName" },
                SampleRows.Select(r => new object?[] { r.Macro, r.Geography, r.Region }).ToArray()
            );
            var scopeFactory = new FakeServiceScopeFactory(connection);
            var cache = new RegionHierarchyCache(scopeFactory);

            await cache.PreWarmAsync();
            var firstExecutions = connection.CommandsExecuted;

            await cache.PreWarmAsync();
            var secondExecutions = connection.CommandsExecuted;

            Assert.Equal(1, firstExecutions);
            Assert.Equal(1, secondExecutions);
        }

        [Fact]
        public void FirstAccess_ThroughGetMethods_EnsuresLoaded()
        {
            var connection = new FakeDbConnection(
                new[] { "MacroGeographyName", "GeographyName", "RegionName" },
                SampleRows.Select(r => new object?[] { r.Macro, r.Geography, r.Region }).ToArray()
            );
            var scopeFactory = new FakeServiceScopeFactory(connection);
            var cache = new RegionHierarchyCache(scopeFactory);

            // Call a read method without pre-warm
            var res = cache.GetRegionsForGeographies(new[] { "Europe" });

            Assert.True(res.SetEquals(new[] { "West Europe", "North Europe" }));
            Assert.Equal(1, connection.CommandsExecuted);
        }

        [Fact]
        public void GetRegionsForGeographies_UnionsDistinct_AndIsCaseInsensitive()
        {
            var cache = CreateCacheWithRows(SampleRows);

            var regions = cache.GetRegionsForGeographies(new[] { "europe", "EUROPE" }); // same value in different casing
            Assert.True(regions.SetEquals(new[] { "West Europe", "North Europe" }));
        }

        [Fact]
        public void GetRegionsForMacros_UnionsDistinct_AndIsCaseInsensitive()
        {
            var cache = CreateCacheWithRows(SampleRows);

            var regions = cache.GetRegionsForMacros(new[] { "amer", "AMER" }); // duplicates in different casing
            Assert.True(regions.SetEquals(new[] { "East US", "West US" }));
        }

        [Fact]
        public void TryGetParentGeography_ReturnsFalse_ForUnknownRegion()
        {
            var cache = CreateCacheWithRows(SampleRows);

            var ok = cache.TryGetParentGeography("NotARealRegion", out var geo, out var macro);

            Assert.False(ok);
            Assert.Null(geo);
            Assert.Null(macro);
        }

        [Fact]
        public async Task PreWarmAsync_ConcurrentCalls_LoadOnlyOnce()
        {
            var connection = new FakeDbConnection(
                new[] { "MacroGeographyName", "GeographyName", "RegionName" },
                SampleRows.Select(r => new object?[] { r.Macro, r.Geography, r.Region }).ToArray()
            );
            var scopeFactory = new FakeServiceScopeFactory(connection);
            var cache = new RegionHierarchyCache(scopeFactory);

            var tasks = Enumerable.Range(0, 16).Select(_ => cache.PreWarmAsync()).ToArray();
            await Task.WhenAll(tasks);

            Assert.Equal(1, connection.CommandsExecuted);

            // Sanity check that data is usable
            var regions = cache.GetRegionsForMacros(new[] { "AMER" });
            Assert.True(regions.SetEquals(new[] { "East US", "West US" }));
        }

        // ------------------------
        // Minimal fakes for DI/DB
        // ------------------------

        private sealed class FakeServiceScopeFactory : IServiceScopeFactory
        {
            private readonly IDbConnection _connection;
            public FakeServiceScopeFactory(IDbConnection connection) => _connection = connection;
            public IServiceScope CreateScope() => new FakeScope(_connection);

            private sealed class FakeScope : IServiceScope
            {
                public IServiceProvider ServiceProvider { get; }
                public FakeScope(IDbConnection connection) => ServiceProvider = new FakeProvider(connection);
                public void Dispose() { }

                private sealed class FakeProvider : IServiceProvider
                {
                    private readonly IDbConnection _connection;
                    public FakeProvider(IDbConnection connection) => _connection = connection;
                    public object? GetService(Type serviceType)
                        => serviceType == typeof(IDbConnection) ? _connection : null;
                }
            }
        }

        private sealed class FakeDbConnection : IDbConnection
        {
            private ConnectionState _state = ConnectionState.Closed;
            private readonly string[] _columns;
            private readonly object?[][] _rows;
            public int CommandsExecuted { get; private set; }

            public FakeDbConnection(string[] columns, object?[][] rows)
            {
                _columns = columns;
                _rows = rows;
            }

            public string ConnectionString { get; set; } = string.Empty;
            public int ConnectionTimeout => 0;
            public string Database => "FakeDB";
            public ConnectionState State => _state;

            public IDbTransaction BeginTransaction() => throw new NotImplementedException();
            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();
            public void ChangeDatabase(string databaseName) { }
            public void Close() => _state = ConnectionState.Closed;
            public IDbCommand CreateCommand() => new FakeDbCommand(this, _columns, _rows, () => CommandsExecuted++);
            public void Open() => _state = ConnectionState.Open;
            public void Dispose() { }
        }

        private sealed class FakeDbCommand : IDbCommand
        {
            private readonly FakeDbConnection _connection;
            private readonly string[] _columns;
            private readonly object?[][] _rows;
            private readonly Action _onExecute;
            private readonly FakeDataParameterCollection _parameters = new();

            public FakeDbCommand(FakeDbConnection connection, string[] columns, object?[][] rows, Action onExecute)
            {
                _connection = connection;
                _columns = columns;
                _rows = rows;
                _onExecute = onExecute;
            }

            public string CommandText { get; set; } = string.Empty;
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; } = CommandType.Text;
            public IDbConnection Connection { get => _connection; set { } }
            public IDataParameterCollection Parameters => _parameters;
            public IDbTransaction? Transaction { get; set; }
            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel() { }
            public IDbDataParameter CreateParameter() => new FakeDataParameter();
            public void Dispose() { }
            public int ExecuteNonQuery() => throw new NotImplementedException();
            public IDataReader ExecuteReader()
            {
                _onExecute();
                return new FakeDataReader(_columns, _rows);
            }
            public IDataReader ExecuteReader(CommandBehavior behavior)
            {
                _onExecute();
                return new FakeDataReader(_columns, _rows);
            }
            public object? ExecuteScalar() => throw new NotImplementedException();
            public void Prepare() { }

            private sealed class FakeDataParameter : IDbDataParameter
            {
                public byte Precision { get; set; }
                public byte Scale { get; set; }
                public int Size { get; set; }
                public DbType DbType { get; set; }
                public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
                public bool IsNullable => true;
                public string ParameterName { get; set; } = string.Empty;
                public string SourceColumn { get; set; } = string.Empty;
                public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
                public object? Value { get; set; }
            }

            private sealed class FakeDataParameterCollection : List<IDbDataParameter>, IDataParameterCollection
            {
                public object? this[string parameterName]
                {
                    get => this.FirstOrDefault(p => p.ParameterName == parameterName);
                    set { /* no-op */ }
                }
                public bool Contains(string parameterName) => this.Any(p => p.ParameterName == parameterName);
                public int IndexOf(string parameterName) => this.FindIndex(p => p.ParameterName == parameterName);
                public void RemoveAt(string parameterName)
                {
                    var i = IndexOf(parameterName);
                    if (i >= 0) RemoveAt(i);
                }
            }
        }

        private sealed class FakeDataReader : IDataReader
        {
            private readonly string[] _columns;
            private readonly object?[][] _rows;
            private int _rowIndex = -1;
            private bool _isClosed;

            public FakeDataReader(string[] columns, object?[][] rows)
            {
                _columns = columns;
                _rows = rows;
            }

            public int FieldCount => _columns.Length;
            public object this[int i] => GetValue(i);
            public object this[string name] => GetValue(GetOrdinal(name));
            public int Depth => 0;
            public bool IsClosed => _isClosed;
            public int RecordsAffected => -1;

            public void Close() => _isClosed = true;
            public void Dispose() => Close();
            public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i));
            public byte GetByte(int i) => Convert.ToByte(GetValue(i));
            public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public char GetChar(int i) => Convert.ToChar(GetValue(i));
            public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
            public IDataReader GetData(int i) => throw new NotSupportedException();
            public string GetDataTypeName(int i) => "nvarchar";
            public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i));
            public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i));
            public double GetDouble(int i) => Convert.ToDouble(GetValue(i));
            public Type GetFieldType(int i) => typeof(string);
            public float GetFloat(int i) => Convert.ToSingle(GetValue(i));
            public Guid GetGuid(int i) => (Guid)GetValue(i);
            public short GetInt16(int i) => Convert.ToInt16(GetValue(i));
            public int GetInt32(int i) => Convert.ToInt32(GetValue(i));
            public long GetInt64(int i) => Convert.ToInt64(GetValue(i));
            public string GetName(int i) => _columns[i];
            public int GetOrdinal(string name) => Array.IndexOf(_columns, name);
            public DataTable GetSchemaTable() => throw new NotSupportedException();
            public string GetString(int i) => (string?)GetValue(i) ?? string.Empty;
            public object? GetValue(int i) => _rows[_rowIndex][i];
            public int GetValues(object[] values)
            {
                var len = Math.Min(values.Length, FieldCount);
                for (int i = 0; i < len; i++) values[i] = GetValue(i)!;
                return len;
            }
            public bool IsDBNull(int i) => GetValue(i) is null || GetValue(i) == DBNull.Value;
            public bool NextResult() => false;
            public bool Read()
            {
                _rowIndex++;
                return _rowIndex < _rows.Length;
            }
        }
    }

    internal static class SetEqualsExtensions
    {
        public static bool SetEquals<T>(this IEnumerable<T> source, IEnumerable<T> other, IEqualityComparer<T>? comparer = null)
        {
            var hs1 = new HashSet<T>(source, comparer ?? EqualityComparer<T>.Default);
            var hs2 = new HashSet<T>(other, comparer ?? EqualityComparer<T>.Default);
            return hs1.SetEquals(hs2);
        }
    }
}