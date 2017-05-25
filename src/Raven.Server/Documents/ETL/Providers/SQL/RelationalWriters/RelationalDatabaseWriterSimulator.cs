using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;

namespace Raven.Server.Documents.ETL.Providers.SQL.RelationalWriters
{
    public class RelationalDatabaseWriterSimulator : RelationalDatabaseWriterBase
    {
        private readonly SqlDestination _configuration;
        private readonly DbProviderFactory _providerFactory;
        private readonly DbCommandBuilder _commandBuilder;

        public RelationalDatabaseWriterSimulator(SqlEtlConnection connection, SqlDestination configuration) 
            : base(connection)
        {
            _configuration = configuration;
            _providerFactory = DbProviderFactories.GetFactory(connection.FactoryName);
            _commandBuilder = _providerFactory.CreateCommandBuilder();
        }

        public IEnumerable<string> SimulateExecuteCommandText(SqlTableWithRecords records, CancellationToken token)
        {
            if (records.InsertOnlyMode == false)
            {
                // first, delete all the rows that might already exist there
                foreach (var deleteQuery in GenerateDeleteItemsCommandText(records.TableName, records.DocumentIdColumn, _configuration.ParameterizeDeletes,
                    records.Deletes, token))
                {
                    yield return deleteQuery;
                }
            }

            foreach (var insertQuery in GenerteInsertItemCommandText(records.TableName, records.DocumentIdColumn, records.Inserts, token))
            {
                yield return insertQuery;
            }
        }

        private IEnumerable<string> GenerteInsertItemCommandText(string tableName, string pkName, List<ToSqlItem> dataForTable, CancellationToken token)
        {
            foreach (var itemToReplicate in dataForTable)
            {
                token.ThrowIfCancellationRequested();
                
                var sb = new StringBuilder("INSERT INTO ")
                        .Append(GetTableNameString(tableName))
                        .Append(" (")
                        .Append(_commandBuilder.QuoteIdentifier(pkName))
                        .Append(", ");
                foreach (var column in itemToReplicate.Columns)
                {
                    if (column.Id == pkName)
                        continue;
                    sb.Append(_commandBuilder.QuoteIdentifier(column.Id)).Append(", ");
                }
                sb.Length = sb.Length - 2;


                sb.Append(") VALUES (")
                    .Append("'")
                    .Append(itemToReplicate.DocumentId)
                    .Append("'")
                    .Append(", ");

                foreach (var column in itemToReplicate.Columns)
                {
                    if (column.Id == pkName)
                        continue;
                     DbParameter param = new SqlParameter();
                     RelationalDatabaseWriter.SetParamValue(param, column, null);
                     sb.Append("'").Append(param.Value).Append("'").Append(", ");
                }
                sb.Length = sb.Length - 2;
                sb.Append(")");
                if (IsSqlServerFactoryType && _configuration.ForceQueryRecompile)
                {
                    sb.Append(" OPTION(RECOMPILE)");
                }

                sb.Append(";");

                yield return sb.ToString();
            }
        }

        private IEnumerable<string> GenerateDeleteItemsCommandText(string tableName, string pkName, bool parameterize, List<ToSqlItem> toSqlItems, CancellationToken token)
        {
            const int maxParams = 1000;

            token.ThrowIfCancellationRequested();

            for (int i = 0; i < toSqlItems.Count; i += maxParams)
            {

                var sb = new StringBuilder("DELETE FROM ")
                    .Append(GetTableNameString(tableName))
                    .Append(" WHERE ")
                    .Append(_commandBuilder.QuoteIdentifier(pkName))
                    .Append(" IN (");

                for (int j = i; j < Math.Min(i + maxParams, toSqlItems.Count); j++)
                {
                    if (i != j)
                        sb.Append(", ");
                    if (parameterize)
                    {
                        sb.Append(toSqlItems[j].DocumentId);
                    }
                    else
                    {
                        sb.Append("'").Append(RelationalDatabaseWriter.SanitizeSqlValue(toSqlItems[j].DocumentId)).Append("'");
                    }

                }
                sb.Append(")");

                if (IsSqlServerFactoryType && _configuration.ForceQueryRecompile)
                {
                    sb.Append(" OPTION(RECOMPILE)");
                }

                sb.Append(";");
                yield return sb.ToString();
            }
        }

        private string GetTableNameString(string tableName)
        {
            if (_configuration.QuoteTables)
            {
                return string.Join(".", tableName.Split('.').Select(x => _commandBuilder.QuoteIdentifier(x)).ToArray());
            }
            else
            {
                return tableName;
            }
        }
    }
}