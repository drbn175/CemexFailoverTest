using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Collections.Generic;
using System.Configuration;
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
        
        Incremental retryStrategy;
        RetryPolicy retPol;
        TimeSpan timeoutCommand;
        protected ReliableSqlConnection relConnection;
        string stringConnection;
        private readonly int retryCount;
        private readonly int initialInterval;
        private readonly int increment;
        protected bool IsDisposed = false;
        #endregion

        #region Constructor

        public SQLDataAccess(string connectionString, int retryCount, int initialInterval, int increment, int databaseTimeout)
        {
            try
            {
                if (string.IsNullOrEmpty(connectionString))
                    throw new ArgumentException("String connection expected");
                
                this.retryCount = retryCount;
                this.initialInterval = initialInterval;
                this.increment = increment;
                timeoutCommand = TimeSpan.FromSeconds(databaseTimeout);
                stringConnection = connectionString;

                retryStrategy = new Incremental(retryCount, TimeSpan.FromSeconds(this.initialInterval), TimeSpan.FromSeconds(this.increment));
                retPol = new RetryPolicy<SqlDatabaseTransientErrorDetectionStrategy>(retryStrategy);
                GenerateConnection();
            }
            catch(Exception ex)
            {
                throw ex;
            }
            
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

        public Tuple<bool, string, string> ExecuteReaderPolicy(string textoComando, IDataParameter[] parametros)
        {
            var response = Tuple.Create<bool, string, string>(false, string.Empty, string.Empty);
            
            if (string.IsNullOrEmpty(textoComando.Trim()))
            {
                throw new ArgumentException("Parameter expected", "string commandText");
            }

            //IDataReader reader = null;
            IDbCommand command = null;
            string respSQL = string.Empty;

            try
            { 
                //this.relConnection = new ReliableSqlConnection(this.stringConnection, retPol, retPol);

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
                            respSQL += rd[2].ToString();
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

                return new Tuple<bool, string, string>(false, ex.Message, ex.StackTrace);
                throw;
            }
            finally
            {
                if (command != null)
                {
                    command.Dispose();
                    command = null;
                    if (!string.IsNullOrEmpty(respSQL))
                        response = new Tuple<bool, string, string>(true, string.Empty,string.Empty);
                }
            }

            return response;
        }



        private void GenerateConnection()
        {
            RetryPolicy retryPolicyComando = retPol;

            (retryPolicyComando ?? RetryPolicy.NoRetry).ExecuteAction(() =>
            {
                this.relConnection = new ReliableSqlConnection(this.stringConnection, retPol, retPol);
            });
        }

        public void OpenRelConnection()
        {
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
