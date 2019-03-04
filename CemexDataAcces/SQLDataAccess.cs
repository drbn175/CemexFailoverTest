using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CemexDataAcces
{
    public class SQLDataAccess : IDisposable
    {

        #region ClassMembers
        protected bool IsDisposed = false;

        protected ReliableSqlConnection relConnection;

        private string stringConnection;

        private TimeSpan timeoutCommand;

        const int retryCount = 4;
        const int minBackoffDelayMilliseconds = 2000;
        const int maxBackoffDelayMilliseconds = 8000;
        const int deltaBackoffMilliseconds = 2000;

        #endregion

        #region Constructor

        public SQLDataAccess(string stringConnection, TimeSpan timeoutCommand)
        {
            if (string.IsNullOrEmpty(stringConnection.Trim()))
            {
                throw new ArgumentException("No se recibio el parametro", "string databaseConnectionString");
            }

            this.stringConnection = stringConnection;
            this.timeoutCommand = timeoutCommand;
            GenerateConnection();
        }
        #endregion

        #region Methods
        protected IDbCommand PrepareSQLCommandRel(string sqlTextCommand, CommandType commandType, IDataParameter[] parameters)
        {
            IDbCommand command = relConnection.CreateCommand();
            command.CommandText = sqlTextCommand;
            command.CommandType = commandType;

            if (parameters != null)
            {
                foreach (IDataParameter paremetro in parameters)
                {
                    command.Parameters.Add(paremetro);
                }
            }

            return command;
        }

        
        public string EjecutaReaderPolicy(string textoComando, IDataParameter[] parametros)
        {
            string resp = string.Empty;

            if (string.IsNullOrEmpty(textoComando.Trim()))
            {
                throw new ArgumentException("No se recibio el parametro", "string commandText");
            }

            IDataReader reader = null;
            IDbCommand command = null;

            try
            {
                var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
                var retPol = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(retryStrategy);
                
                this.relConnection = new ReliableSqlConnection(this.stringConnection, retPol, retPol);

                if (this.relConnection.State == ConnectionState.Closed)
                {
                    this.OpenRelConnection();
                }

                RetryPolicy retryPolicyComando = retPol;

                command = PrepareSQLCommandRel(textoComando, CommandType.Text, parametros);
                command.CommandTimeout = Convert.ToInt32(timeoutCommand.TotalSeconds);

                (retryPolicyComando ?? RetryPolicy.NoRetry).ExecuteAction(() =>
                {
                    
                    using (IDataReader rd = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        while (rd.Read())
                        {
                            resp += rd[2].ToString();
                        }

                    }
                });

            }
            catch(Exception ex)
            {
                if (this.relConnection != null && this.relConnection.State == ConnectionState.Open)
                {
                    this.relConnection.Close();
                    this.relConnection = null;
                }

                throw;
            }
            finally
            {
                if (command != null)
                {
                    command.Dispose();
                    command = null;
                }
            }
            return resp;
        }

        private void GenerateConnection()
        {
            var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            var retPol = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(retryStrategy);
            RetryPolicy retryPolicyComando = retPol;

            (retryPolicyComando ?? RetryPolicy.NoRetry).ExecuteAction(() =>
            {
                this.relConnection = new ReliableSqlConnection(this.stringConnection, retPol, retPol);
            });
        }

        public void OpenRelConnection()
        {

            var retryStrategy = new Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
            var retPol = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(retryStrategy);
            RetryPolicy retryPolicyConexion = retPol;

            (retryPolicyConexion ?? RetryPolicy.NoRetry).ExecuteAction(() =>
            {
                this.relConnection.Open();
            });
        }

        public void CloseConnection()
        {
            if (this.relConnection != null && this.relConnection.State == ConnectionState.Open)
            {
                this.relConnection.Close();
                this.relConnection = null;
            }
        }
        #endregion

        #region Implementations
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            this.CloseConnection();
            GC.SuppressFinalize(this);
            IsDisposed = true;
        }
        #endregion
    }
}
