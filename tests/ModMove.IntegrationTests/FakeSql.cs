using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using ModMove.Application.Common.Interfaces.Persistence;

namespace ModMove.IntegrationTests;

/// <summary>
/// Read side without a database (DECISIONS C2): every query returns the rows of <see cref="Result"/>, so a query
/// handler's own logic runs through real Dapper. The SQL text is not executed; columns must be named like the aliases.
/// The last SQL and its parameters are kept, so a test can check the columns selected and the ids bound.
/// </summary>
internal sealed class FakeSql : ISqlConnectionFactory
{
    public DataTable Result { get; set; } = new();

    /// <summary>Tables for handlers that run several queries in turn: each query takes the next one, then falls back to <see cref="Result"/>.</summary>
    public Queue<DataTable> Results { get; } = new();
    public object? ScalarResult { get; set; }
    public Func<string, IReadOnlyDictionary<string, object?>, DataTable?>? CustomQuery { get; set; }
    public string LastSql { get; private set; } = "";
    public IReadOnlyDictionary<string, object?> LastParameters { get; private set; } = new Dictionary<string, object?>();

    public IDbConnection CreateConnection() => new Connection(this);

    /// <summary>A one-row result with only the columns a test cares about; Dapper leaves the other properties at their default.</summary>
    public static DataTable Row(params (string Name, object Value)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, value) in columns)
            table.Columns.Add(name, value.GetType());
        table.Rows.Add(columns.Select(c => c.Value).ToArray());
        return table;
    }

    private sealed class Connection(FakeSql sql) : DbConnection
    {
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "8.0";
        public override ConnectionState State => ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Open() { }
        public override void Close() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new Command(sql);
    }

    private sealed class Command(FakeSql sql) : DbCommand
    {
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new Parameters();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            sql.LastSql = CommandText;
            sql.LastParameters = DbParameterCollection.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value);
            return 1;
        }

        public override object? ExecuteScalar()
        {
            sql.LastSql = CommandText;
            sql.LastParameters = DbParameterCollection.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value);
            if (sql.ScalarResult is not null)
                return sql.ScalarResult;
            if (sql.Result.Rows.Count > 0 && sql.Result.Columns.Count > 0)
                return sql.Result.Rows[0][0];
            return sql.Result.Rows.Count;
        }

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ExecuteScalar());

        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new Parameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            sql.LastSql = CommandText;
            sql.LastParameters = DbParameterCollection.Cast<DbParameter>().ToDictionary(p => p.ParameterName, p => p.Value);

            var statements = CommandText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (statements.Length > 1)
            {
                var ds = new DataSet();
                for (var i = 0; i < statements.Length; i++)
                {
                    DataTable source;
                    if (sql.Results.Count > 0)
                        source = sql.Results.Dequeue();
                    else if (sql.CustomQuery is not null && sql.CustomQuery(statements[i], sql.LastParameters) is { } custom)
                        source = custom;
                    else
                        source = sql.Result;

                    var table = source.Copy();
                    if (table.Columns.Count == 0)
                        table.Columns.Add("_empty", typeof(int));
                    table.TableName = $"T{i}";
                    ds.Tables.Add(table);
                }
                return ds.CreateDataReader();
            }

            if (sql.Results.Count > 0)
                return sql.Results.Dequeue().CreateDataReader();
            if (sql.CustomQuery is not null)
            {
                var custom = sql.CustomQuery(CommandText, sql.LastParameters);
                if (custom is not null)
                    return custom.CreateDataReader();
            }
            return sql.Result.CreateDataReader();
        }
    }

    private sealed class Parameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        [AllowNull] public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class Parameters : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];

        public override int Count => _items.Count;
        public override object SyncRoot => _items;
        public override int Add(object value) { _items.Add((DbParameter)value); return _items.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) Add(v); }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => _items.RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}
