using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Collections.Generic;
using System.Data;


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

        public Tuple<bool, string, string> ExecuteReaderPolicy(string commandText, IDataParameter[] parametros)
        {
            var response = Tuple.Create<bool, string, string>(false, string.Empty, string.Empty);
            
            if (string.IsNullOrEmpty(commandText.Trim()))
            {
                throw new ArgumentException("Parameter expected", "string commandText");
            }
            IDbCommand command = null;
            object returnValue = null;
            
            try
            { 
                
                if (this.relConnection.State == ConnectionState.Closed)
                {
                    this.OpenRelConnection();
                }

                RetryPolicy retryPolicyComando = retPol;
                command = PrepareSQLCommandRel(commandText, CommandType.Text, parametros);
                command.CommandTimeout = Convert.ToInt32(timeoutCommand.TotalSeconds);

                (retryPolicyComando ?? RetryPolicy.NoRetry).ExecuteAction(() =>
                {
                    returnValue = command.ExecuteNonQuery();
                });

            }
            finally
            {
                if (this.relConnection != null && this.relConnection.State == ConnectionState.Open)
                {
                    this.relConnection.Close();
                    this.relConnection = null;
                }
                if (command != null)
                {
                    command.Dispose();
                    command = null;
                    if (returnValue != null)
                        response = new Tuple<bool, string, string>(true, string.Empty,string.Empty);
                }
            }

            return response;
        }

        public string ExecuteReader(string commandText, IDataParameter[] parametros)
        {
            string response = string.Empty;
            IEnumerable<Dictionary<string, object>> result;

            if (string.IsNullOrEmpty(commandText.Trim()))
            {
                throw new ArgumentException("No se recibio el parametro", "string commandText");
            }
            
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

                command = PrepareSQLCommandRel(commandText, CommandType.Text, parametros);
                command.CommandTimeout = Convert.ToInt32(timeoutCommand.TotalSeconds);

                (retryPolicyComando ?? RetryPolicy.NoRetry).ExecuteAction(() =>
                {

                    using (IDataReader rd = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        result = Serialize(rd);
                    }
                    response = Newtonsoft.Json.JsonConvert.SerializeObject(result);
                });

            }
            finally
            {
                if (this.relConnection != null && this.relConnection.State == ConnectionState.Open)
                {
                    this.relConnection.Close();
                    this.relConnection = null;
                }
                if (command != null)
                {
                    command.Dispose();
                    command = null;
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

        private IEnumerable<Dictionary<string, object>> Serialize(IDataReader reader)
        {
            var results = new List<Dictionary<string, object>>();
            var cols = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
                cols.Add(reader.GetName(i));

            while (reader.Read())
                results.Add(SerializeRow(cols, reader));

            return results;
        }
        private Dictionary<string, object> SerializeRow(IEnumerable<string> cols,
                                                        IDataReader reader)
        {
            var result = new Dictionary<string, object>();
            foreach (var col in cols)
                result.Add(col, reader[col]);
            return result;
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
