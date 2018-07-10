using Raven.Client.Documents.Operations.ETL.SQL;

namespace Raven.Server.Documents.ETL.Providers.SQL.RelationalWriters
{
    public class SimulateSqlEtl
    {
        /// <summary>
        /// Document Id to simulate SQL ETL on
        /// </summary>
        public string DocumentId;
        /// <summary>
        /// Perform Rolled Back Transaction
        /// </summary>
        public bool PerformRolledBackTransaction;
        /// <summary>
        /// Sql Replication Script
        /// </summary>
        public SqlEtlConfiguration Configuration;

        public SqlConnectionString Connection;
    }
}
