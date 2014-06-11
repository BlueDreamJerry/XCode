using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NewLife;
using NewLife.Collections;
using NewLife.Log;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    /// <summary>���ݿ�Ự����</summary>
    abstract partial class DbSession : DisposeBase, IDbSession
    {
        #region ���캯��
        /// <summary>������Դʱ���ع�δ�ύ���񣬲��ر����ݿ�����</summary>
        /// <param name="disposing"></param>
        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

            try
            {
                // ע�⣬û��Commit�����ݣ������ｫ�ᱻ�ع�
                //if (Trans != null) Rollback();
                // ��Ƕ�������У�Rollbackֻ�ܼ���Ƕ�ײ�������_Trans.Rollback�����������ϻع�
                if (_Trans != null && Opened) _Trans.Rollback();
                if (_Conn != null) Close();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                WriteLog("ִ��" + DbType.ToString() + "��Disposeʱ����" + ex.ToString());
            }
        }
        #endregion

        #region ����
        private IDatabase _Database;
        /// <summary>���ݿ�</summary>
        public IDatabase Database { get { return _Database; } set { _Database = value; } }

        /// <summary>�������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other</summary>
        private DatabaseType DbType { get { return Database.DbType; } }

        /// <summary>����</summary>
        private DbProviderFactory Factory { get { return Database.Factory; } }

        private String _ConnectionString;
        /// <summary>�����ַ������Ự�������棬�����޸ģ��޸Ĳ���Ӱ�����ݿ��е������ַ���</summary>
        public String ConnectionString { get { return _ConnectionString; } set { _ConnectionString = value; } }

        private DbConnection _Conn;
        /// <summary>�������Ӷ���</summary>
        public DbConnection Conn
        {
            get
            {
                if (_Conn == null)
                {
                    try
                    {
                        _Conn = Factory.CreateConnection();
                    }
                    catch (ObjectDisposedException) { this.Dispose(); throw; }
                    //_Conn.ConnectionString = Database.ConnectionString;
                    if (ConnectionString.IsNullOrWhiteSpace()) throw new XCodeException("[{0}]δָ�������ַ�����", Database == null ? "" : Database.ConnName);
                    _Conn.ConnectionString = ConnectionString;
                }
                return _Conn;
            }
            //set { _Conn = value; }
        }

        private Int32 _QueryTimes;
        /// <summary>��ѯ����</summary>
        public Int32 QueryTimes { get { return _QueryTimes; } set { _QueryTimes = value; } }

        private Int32 _ExecuteTimes;
        /// <summary>ִ�д���</summary>
        public Int32 ExecuteTimes { get { return _ExecuteTimes; } set { _ExecuteTimes = value; } }

        private Int32 _ThreadID = Thread.CurrentThread.ManagedThreadId;
        /// <summary>�̱߳�ţ�ÿ�����ݿ�ỰӦ��ֻ����һ���̣߳����������ڼ�����Ŀ��̲߳���</summary>
        public Int32 ThreadID { get { return _ThreadID; } set { _ThreadID = value; } }
        #endregion

        #region ��/�ر�
        private Boolean _IsAutoClose = true;
        /// <summary>�Ƿ��Զ��رա�
        /// ��������󣬸�������Ч��
        /// ���ύ��ع�����ʱ�����IsAutoCloseΪtrue������Զ��ر�
        /// </summary>
        public Boolean IsAutoClose { get { return _IsAutoClose; } set { _IsAutoClose = value; } }

        /// <summary>�����Ƿ��Ѿ���</summary>
        public Boolean Opened { get { return _Conn != null && _Conn.State != ConnectionState.Closed; } }

        /// <summary>��</summary>
        public virtual void Open()
        {
            if (DAL.Debug && ThreadID != Thread.CurrentThread.ManagedThreadId) DAL.WriteLog("���Ự���߳�{0}��������ǰ�߳�{1}�Ƿ�ʹ�øûỰ��");

            if (Conn != null && Conn.State == ConnectionState.Closed)
            {
                try
                {
                    Conn.Open();
                }
                catch (DbException)
                {
                    DAL.WriteLog("����Open����������ַ�����{0}", Conn.ConnectionString);
                    throw;
                }
            }
        }

        /// <summary>�ر�</summary>
        public virtual void Close()
        {
            if (_Conn != null && Conn.State != ConnectionState.Closed)
            {
                try { Conn.Close(); }
                catch (Exception ex)
                {
                    WriteLog("ִ��" + DbType.ToString() + "��Closeʱ����" + ex.ToString());
                }
            }
        }

        /// <summary>�Զ��رա�
        /// ��������󣬲��ر����ӡ�
        /// ���ύ��ع�����ʱ�����IsAutoCloseΪtrue������Զ��ر�
        /// </summary>
        public void AutoClose()
        {
            if (IsAutoClose && Trans == null && Opened) Close();
        }

        /// <summary>���ݿ���</summary>
        public String DatabaseName
        {
            get
            {
                return Conn == null ? null : Conn.Database;
            }
            set
            {
                if (DatabaseName == value) return;

                //XTrace.Log.Info("DatabaseName {0}=>{1}", DatabaseName, value);

                //if (Opened)
                //{
                //    //����Ѵ򿪣�������������л�
                //    Conn.ChangeDatabase(value);
                //}
                //else
                //{
                //DAL.WriteDebugLog("{0}=>{1}", Conn.Database, value);
                ////XTrace.DebugStack(3);

                //// ��ΪMSSQL��γ����������ַ�����������µı��������ַ���������ñ���ˣ�����ͳһ�ر����ӣ����ñ��������޸��ַ���
                //var b = Opened;
                //if (b) Close();

                ////���û�д򿪣���ı������ַ���
                //var builder = new DbConnectionStringBuilder();
                //builder.ConnectionString = ConnectionString;
                //var flag = false;
                //if (builder.ContainsKey("Database"))
                //{
                //    builder["Database"] = value;
                //    flag = true;
                //    ConnectionString = builder.ToString();
                //    Conn.ConnectionString = ConnectionString;
                //}
                //else if (builder.ContainsKey("Initial Catalog"))
                //{
                //    builder["Initial Catalog"] = value;
                //    flag = true;
                //}
                //if (flag)
                //{
                //    var connStr = builder.ToString();
                //    ConnectionString = connStr;
                //    Conn.ConnectionString = connStr;
                //}
                //if (b) Open();
                //}
            }
        }

        /// <summary>���쳣����ʱ�������ر����ݿ����ӣ����߷������ӵ����ӳء�</summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        protected virtual XDbException OnException(Exception ex)
        {
            if (Trans == null && Opened) Close(); // ǿ�ƹر����ݿ�
            if (ex != null)
                return new XDbSessionException(this, ex);
            else
                return new XDbSessionException(this);
        }

        /// <summary>���쳣����ʱ�������ر����ݿ����ӣ����߷������ӵ����ӳء�</summary>
        /// <param name="ex"></param>
        /// <param name="sql"></param>
        /// <returns></returns>
        protected virtual XSqlException OnException(Exception ex, String sql)
        {
            if (Trans == null && Opened) Close(); // ǿ�ƹر����ݿ�
            if (ex != null)
                return new XSqlException(sql, this, ex);
            else
                return new XSqlException(sql, this);
        }
        #endregion

        #region ����
        private DbTransaction _Trans;
        /// <summary>���ݿ�����</summary>
        protected DbTransaction Trans
        {
            get { return _Trans; }
            set { _Trans = value; }
        }

        /// <summary>������������ҽ��������������1ʱ�����ύ��ع���</summary>
        private Int32 TransactionCount = 0;

        /// <summary>��ʼ����</summary>
        /// <returns>ʣ�µ��������</returns>
        public Int32 BeginTransaction()
        {
            if (Disposed) throw new ObjectDisposedException(this.GetType().Name);

            if (TransactionCount < 0) TransactionCount = 0;
            TransactionCount++;
            if (TransactionCount > 1) return TransactionCount;

            try
            {
                if (!Opened) Open();
                Trans = Conn.BeginTransaction();
                TransactionCount = 1;
                return TransactionCount;
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }
        }

        /// <summary>�ύ����</summary>
        /// <returns>ʣ�µ��������</returns>
        public Int32 Commit()
        {
            TransactionCount--;
            if (TransactionCount > 0) return TransactionCount;

            if (Trans == null) throw new XDbSessionException(this, "��ǰ��δ��ʼ��������BeginTransaction������ʼ������");
            try
            {
                if (Trans.Connection != null) Trans.Commit();
                Trans = null;
                if (IsAutoClose) Close();
            }
            catch (DbException ex)
            {
                throw OnException(ex);
            }

            return TransactionCount;
        }

        /// <summary>�ع�����</summary>
        /// <param name="ignoreException">�Ƿ�����쳣</param>
        /// <returns>ʣ�µ��������</returns>
        public Int32 Rollback(Boolean ignoreException = true)
        {
            // ����ҪС�ģ��ڶ�������У�����ڲ�ع�����������ύ�����ڲ�Ļع������ύ
            TransactionCount--;
            if (TransactionCount > 0) return TransactionCount;

            var tr = Trans;
            if (tr == null) throw new XDbSessionException(this, "��ǰ��δ��ʼ��������BeginTransaction������ʼ������");
            Trans = null;
            try
            {
                if (tr.Connection != null) tr.Rollback();
                if (IsAutoClose) Close();
            }
            catch (DbException ex)
            {
                if (!ignoreException) throw OnException(ex);
            }

            return TransactionCount;
        }
        #endregion

        #region �������� ��ѯ/ִ��
        /// <summary>ִ��SQL��ѯ�����ؼ�¼��</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns></returns>
        public virtual DataSet Query(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            return Query(CreateCommand(sql, type, ps));
        }

        /// <summary>ִ��DbCommand�����ؼ�¼��</summary>
        /// <param name="cmd">DbCommand</param>
        /// <returns></returns>
        public virtual DataSet Query(DbCommand cmd)
        {
            QueryTimes++;
            WriteSQL(cmd);
            using (var da = Factory.CreateDataAdapter())
            {
                try
                {
                    if (!Opened) Open();
                    cmd.Connection = Conn;
                    if (Trans != null) cmd.Transaction = Trans;
                    da.SelectCommand = cmd;

                    var ds = new DataSet();
                    BeginTrace();
                    da.Fill(ds);
                    return ds;
                }
                catch (DbException ex)
                {
                    throw OnException(ex, cmd.CommandText);
                }
                finally
                {
                    EndTrace(cmd.CommandText);

                    AutoClose();
                    cmd.Parameters.Clear();
                }
            }
        }

        private static Regex reg_QueryCount = new Regex(@"^\s*select\s+\*\s+from\s+([\w\W]+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        /// <summary>ִ��SQL��ѯ�������ܼ�¼��</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns></returns>
        public virtual Int64 QueryCount(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            if (sql.Contains(" "))
            {
                var orderBy = DbBase.CheckOrderClause(ref sql);
                //sql = String.Format("Select Count(*) From {0}", CheckSimpleSQL(sql));
                //Match m = reg_QueryCount.Match(sql);
                var ms = reg_QueryCount.Matches(sql);
                if (ms != null && ms.Count > 0)
                {
                    sql = String.Format("Select Count(*) From {0}", ms[0].Groups[1].Value);
                }
                else
                {
                    sql = String.Format("Select Count(*) From {0}", DbBase.CheckSimpleSQL(sql));
                }
            }
            else
                sql = String.Format("Select Count(*) From {0}", Database.FormatName(sql));

            //return QueryCountInternal(sql);
            return ExecuteScalar<Int64>(sql, type, ps);
        }

        /// <summary>ִ��SQL��ѯ�������ܼ�¼��</summary>
        /// <param name="builder">��ѯ������</param>
        /// <returns>�ܼ�¼��</returns>
        public virtual Int64 QueryCount(SelectBuilder builder)
        {
            return ExecuteScalar<Int64>(builder.SelectCount().ToString(), CommandType.Text, builder.Parameters.ToArray());
        }

        /// <summary>���ٲ�ѯ�����¼��������ƫ��</summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public virtual Int64 QueryCountFast(String tableName) { return QueryCount(tableName); }

        /// <summary>ִ��SQL��䣬������Ӱ�������</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns></returns>
        public virtual Int32 Execute(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            return Execute(CreateCommand(sql, type, ps));
        }

        /// <summary>ִ��DbCommand��������Ӱ�������</summary>
        /// <param name="cmd">DbCommand</param>
        /// <returns></returns>
        public virtual Int32 Execute(DbCommand cmd)
        {
            ExecuteTimes++;
            WriteSQL(cmd);
            try
            {
                if (!Opened) Open();
                cmd.Connection = Conn;
                if (Trans != null) cmd.Transaction = Trans;

                BeginTrace();
                return cmd.ExecuteNonQuery();
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                EndTrace(cmd.CommandText);

                AutoClose();
                cmd.Parameters.Clear();
            }
        }

        /// <summary>ִ�в�����䲢���������е��Զ����</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns>�����е��Զ����</returns>
        public virtual Int64 InsertAndGetIdentity(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            return Execute(sql, type, ps);
        }

        /// <summary>ִ��SQL��䣬���ؽ���еĵ�һ�е�һ��</summary>
        /// <typeparam name="T">��������</typeparam>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns></returns>
        public virtual T ExecuteScalar<T>(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            return ExecuteScalar<T>(CreateCommand(sql, type, ps));
        }

        protected virtual T ExecuteScalar<T>(DbCommand cmd)
        {
            QueryTimes++;

            WriteSQL(cmd);
            try
            {
                BeginTrace();
                Object rs = cmd.ExecuteScalar();
                if (rs == null || rs == DBNull.Value) return default(T);
                if (rs is T) return (T)rs;
                return (T)Convert.ChangeType(rs, typeof(T));
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                EndTrace(cmd.CommandText);

                AutoClose();
                cmd.Parameters.Clear();
            }
        }

        /// <summary>
        /// ��ȡһ��DbCommand��
        /// ���������ӣ�������������
        /// �����Ѵ򿪡�
        /// ʹ����Ϻ󣬱������AutoClose��������ʹ���ڷ������������Զ��رյ�����¹ر�����
        /// </summary>
        /// <returns></returns>
        public virtual DbCommand CreateCommand()
        {
            var cmd = Factory.CreateCommand();
            if (!Opened) Open();
            cmd.Connection = Conn;
            if (Trans != null) cmd.Transaction = Trans;

            return cmd;
        }

        /// <summary>
        /// ��ȡһ��DbCommand��
        /// ���������ӣ�������������
        /// �����Ѵ򿪡�
        /// ʹ����Ϻ󣬱������AutoClose��������ʹ���ڷ������������Զ��رյ�����¹ر�����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="type">�������ͣ�Ĭ��SQL�ı�</param>
        /// <param name="ps">�������</param>
        /// <returns></returns>
        public virtual DbCommand CreateCommand(String sql, CommandType type = CommandType.Text, params DbParameter[] ps)
        {
            var cmd = CreateCommand();

            cmd.CommandType = type;
            cmd.CommandText = sql;
            if (ps != null && ps.Length > 0) cmd.Parameters.AddRange(ps);

            return cmd;
        }
        #endregion

        #region �ܹ�
        private DictionaryCache<String, DataTable> _schCache = new DictionaryCache<String, DataTable>(StringComparer.OrdinalIgnoreCase)
        {
            Expriod = 10,
            ClearExpriod = 10 * 60//,
            // �����첽�������޸ı�ṹ�󣬵�һ�λ�ȡ���Ǿɵ�
            //Asynchronous = true
        };

        /// <summary>��������Դ�ļܹ���Ϣ������10����</summary>
        /// <param name="collectionName">ָ��Ҫ���صļܹ������ơ�</param>
        /// <param name="restrictionValues">Ϊ����ļܹ�ָ��һ������ֵ��</param>
        /// <returns></returns>
        public virtual DataTable GetSchema(String collectionName, String[] restrictionValues)
        {
            // С��collectionNameΪ�գ���ʱ�г����мܹ�����
            var key = "" + collectionName;
            if (restrictionValues != null && restrictionValues.Length > 0) key += "_" + String.Join("_", restrictionValues);
            return _schCache.GetItem<String, String[]>(key, collectionName, restrictionValues, GetSchemaInternal);
        }

        DataTable GetSchemaInternal(String key, String collectionName, String[] restrictionValues)
        {
            QueryTimes++;
            // ������������񱣻�������Ҫ�¿�һ�����ӣ�����MSSQL���汨��SQLite�������������ݿ�δ����
            var isTrans = TransactionCount > 0;

            DbConnection conn = null;
            if (isTrans)
            {
                try
                {
                    conn = Factory.CreateConnection();
                    conn.ConnectionString = ConnectionString;
                    conn.Open();
                }
                catch (DbException ex)
                {
                    DAL.WriteLog("����GetSchema����������ַ�����{0}", conn.ConnectionString);
                    throw new XDbSessionException(this, "ȡ�����б��ܳ��������ַ��������⣬��鿴��־��", ex);
                }
            }
            else
            {
                if (!Opened) Open();
                conn = Conn;
            }

            try
            {
                DataTable dt;

                if (restrictionValues == null || restrictionValues.Length < 1)
                {
                    if (String.IsNullOrEmpty(collectionName))
                    {
                        WriteSQL("[" + Database.ConnName + "]GetSchema");
                        if (conn.State != ConnectionState.Closed) //ahuang 2013��06��25 �����ݿ������ַ�������
                            dt = conn.GetSchema();
                        else
                            dt = null;
                    }
                    else
                    {
                        WriteSQL("[" + Database.ConnName + "]GetSchema(\"" + collectionName + "\")");
                        if (conn.State != ConnectionState.Closed)
                            dt = conn.GetSchema(collectionName);
                        else
                            dt = null;
                    }
                }
                else
                {
                    var sb = new StringBuilder();
                    foreach (var item in restrictionValues)
                    {
                        sb.Append(", ");
                        if (item == null)
                            sb.Append("null");
                        else
                            sb.AppendFormat("\"{0}\"", item);
                    }
                    WriteSQL("[" + Database.ConnName + "]GetSchema(\"" + collectionName + "\"" + sb + ")");
                    if (conn.State != ConnectionState.Closed)
                        dt = conn.GetSchema(collectionName, restrictionValues);
                    else
                        dt = null;
                }

                return dt;
            }
            catch (DbException ex)
            {
                throw new XDbSessionException(this, "ȡ�����б��ܳ���", ex);
            }
            finally
            {
                if (isTrans)
                    conn.Close();
                else
                    AutoClose();
            }
        }
        #endregion

        #region Sql��־���
        [ThreadStatic]
        internal static Boolean? _ShowSQL;
        /// <summary>�Ƿ����SQL��䣬Ĭ��ΪXCode���Կ���XCode.Debug</summary>
        public static Boolean ShowSQL
        {
            get
            {
                if (_ShowSQL == null) return DAL.ShowSQL;
                return _ShowSQL.Value;
            }
            set { _ShowSQL = value; }
        }

        static TextFileLog logger;

        /// <summary>д��SQL���ı���</summary>
        /// <param name="sql"></param>
        /// <param name="ps"></param>
        public static void WriteSQL(String sql, params DbParameter[] ps)
        {
            if (!ShowSQL) return;

            if (ps != null && ps.Length > 0)
            {
                var sb = new StringBuilder(64);
                sb.Append(sql);
                sb.Append("[");
                for (int i = 0; i < ps.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var v = ps[i].Value;
                    var sv = "";
                    if (v is Byte[])
                    {
                        var bv = v as Byte[];
                        if (bv.Length > 8)
                            sv = String.Format("[{0}]0x{1}...", bv.Length, BitConverter.ToString(bv, 0, 8));
                        else
                            sv = String.Format("[{0}]0x{1}", bv.Length, BitConverter.ToString(bv));
                    }
                    else if (v is String)
                    {
                        sv = v as String;
                        if (sv.Length > 32) sv = String.Format("[{0}]{1}...", sv.Length, sv.Substring(0, 8));
                    }
                    else
                        sv = "" + v;
                    sb.AppendFormat("{1}:{0}={2}", ps[i].ParameterName, ps[i].DbType, sv);
                }
                sb.Append("]");
                sql = sb.ToString();
            }

            if (String.IsNullOrEmpty(DAL.SQLPath))
                WriteLog(sql);
            else
            {
                if (logger == null) logger = TextFileLog.Create(DAL.SQLPath);
                logger.WriteLine(sql);
            }
        }

        public static void WriteSQL(DbCommand cmd)
        {
            String sql = cmd.CommandText;
            if (cmd.CommandType != CommandType.Text) sql = String.Format("[{0}]{1}", cmd.CommandType, sql);

            DbParameter[] ps = null;
            if (cmd.Parameters != null)
            {
                var cps = cmd.Parameters;
                ps = new DbParameter[cps.Count];
                //cmd.Parameters.CopyTo(ps, 0);
                for (int i = 0; i < ps.Length; i++)
                {
                    ps[i] = cps[i];
                }
            }

            WriteSQL(sql, ps);
        }

        /// <summary>�����־</summary>
        /// <param name="msg"></param>
        public static void WriteLog(String msg) { DAL.WriteLog(msg); }

        /// <summary>�����־</summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void WriteLog(String format, params Object[] args) { DAL.WriteLog(format, args); }
        #endregion

        #region SQLʱ�����
        private Stopwatch _swSql;
        private static HashSet<String> _trace_sqls = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

        protected void BeginTrace()
        {
            if (DAL.TraceSQLTime <= 0) return;

            if (_swSql == null) _swSql = new Stopwatch();

            if (_swSql.IsRunning) _swSql.Stop();

            _swSql.Reset();
            _swSql.Start();
        }

        protected void EndTrace(String sql)
        {
            if (_swSql == null) return;

            _swSql.Stop();

            if (_swSql.ElapsedMilliseconds < DAL.TraceSQLTime) return;

            if (_trace_sqls.Contains(sql)) return;
            lock (_trace_sqls)
            {
                if (_trace_sqls.Contains(sql)) return;

                _trace_sqls.Add(sql);
            }

            XTrace.WriteLine("SQL��ʱ�ϳ��������Ż� {0:n}���� {1}", _swSql.ElapsedMilliseconds, sql);
        }
        #endregion
    }
}