using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Reflection;
using XCode.Cache;
using XCode.Code;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// ���ݷ��ʲ㡣
    /// </summary>
    /// <remarks>
    /// ��Ҫ����ѡ��ͬ�����ݿ⣬��ͬ�����ݿ�Ĳ����������
    /// ÿһ�����ݿ������ַ�������ӦΨһ��һ��DALʵ����
    /// ���ݿ������ַ�������д�������ļ��У�Ȼ����Createʱָ�����֣�
    /// Ҳ����ֱ�Ӱ������ַ�����ΪAddConnStr�Ĳ������롣
    /// ÿһ��DALʵ������Ϊÿһ���̳߳�ʼ��һ��DataBaseʵ����
    /// ÿһ�����ݿ����������ָ�����������ڹ����棬�ձ�����*��ƥ�����л���
    /// </remarks>
    public class DAL
    {
        #region ��������
        /// <summary>
        /// ���캯��
        /// </summary>
        /// <param name="connName">������</param>
        private DAL(String connName)
        {
            _ConnName = connName;

            if (!ConnStrs.ContainsKey(connName)) throw new XCodeException("����ʹ�����ݿ�ǰ����[" + connName + "]�����ַ���");

            ConnStr = ConnStrs[connName].ConnectionString;

            DatabaseSchema.Check(this);
        }

        private static Dictionary<String, DAL> _dals = new Dictionary<String, DAL>();
        /// <summary>
        /// ����һ�����ݷ��ʲ������null��Ϊ�����ɻ�õ�ǰĬ�϶���
        /// </summary>
        /// <param name="connName">���������������ַ���</param>
        /// <returns>��Ӧ��ָ�����ӵ�ȫ��Ψһ�����ݷ��ʲ����</returns>
        public static DAL Create(String connName)
        {
            //��connNameΪnullʱ��_dals���沢û�а���null���������Ҫ��ǰ����
            //if (String.IsNullOrEmpty(connName)) return new DAL(null);
            if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException("connName");

            DAL dal = null;
            if (_dals.TryGetValue(connName, out dal)) return dal;
            lock (_dals)
            {
                if (_dals.TryGetValue(connName, out dal)) return dal;

                ////������ݿ������������Ȩ��
                //if (License.DbConnectCount != _dals.Count + 1)
                //    License.DbConnectCount = _dals.Count + 1;

                dal = new DAL(connName);
                // ����connName����Ϊ�����ڴ����������Զ�ʶ����ConnName
                _dals.Add(dal.ConnName, dal);
            }

            return dal;
        }

        private static Object _connStrs_lock = new Object();
        private static Dictionary<String, ConnectionStringSettings> _connStrs;
        private static Dictionary<String, Type> _connTypes = new Dictionary<String, Type>();
        /// <summary>
        /// �����ַ�������
        /// </summary>
        public static Dictionary<String, ConnectionStringSettings> ConnStrs
        {
            get
            {
                if (_connStrs != null) return _connStrs;
                lock (_connStrs_lock)
                {
                    if (_connStrs != null) return _connStrs;
                    Dictionary<String, ConnectionStringSettings> cs = new Dictionary<String, ConnectionStringSettings>();

                    //��ȡ�����ļ�
                    ConnectionStringSettingsCollection css = ConfigurationManager.ConnectionStrings;
                    if (css != null && css.Count > 0)
                    {
                        foreach (ConnectionStringSettings set in css)
                        {
                            if (set.Name == "LocalSqlServer") continue;
                            if (set.Name == "LocalMySqlServer") continue;
                            if (String.IsNullOrEmpty(set.ConnectionString)) continue;

                            Type type = GetTypeFromConn(set.ConnectionString, set.ProviderName);
                            if (type == null) throw new XCodeException("�޷�ʶ����ṩ��" + set.ProviderName + "��");

                            cs.Add(set.Name, set);
                            _connTypes.Add(set.Name, type);
                        }
                    }
                    _connStrs = cs;
                }
                return _connStrs;
            }
        }

        /// <summary>
        /// ��������ַ���
        /// </summary>
        /// <param name="connName"></param>
        /// <param name="connStr"></param>
        /// <param name="type"></param>
        /// <param name="provider"></param>
        public static void AddConnStr(String connName, String connStr, Type type, String provider)
        {
            if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException("connName");

            // ConnStrs���󲻿���Ϊnull��������û��Ԫ��
            if (ConnStrs.ContainsKey(connName)) return;
            lock (ConnStrs)
            {
                if (ConnStrs.ContainsKey(connName)) return;

                if (type == null) type = GetTypeFromConn(connStr, provider);
                if (type == null) throw new XCodeException("�޷�ʶ����ṩ��" + provider + "��");

                ConnectionStringSettings set = new ConnectionStringSettings(connName, connStr, provider);
                ConnStrs.Add(connName, set);
                _connTypes.Add(connName, type);
                //#if DEBUG
                //                NewLife.Log.XTrace.WriteLine("�������ݿ�����{0}��{1}", set.Name, set.ConnectionString);
                //#endif
            }
        }

        /// <summary>
        /// ���ṩ�ߺ������ַ����²����ݿ⴦����
        /// </summary>
        /// <param name="connStr"></param>
        /// <param name="provider"></param>
        /// <returns></returns>
        private static Type GetTypeFromConn(String connStr, String provider)
        {
            Type type = null;
            if (!String.IsNullOrEmpty(provider))
            {
                provider = provider.ToLower();
                if (provider.Contains("sqlclient"))
                    type = typeof(SqlServer);
                else if (provider.Contains("oracleclient"))
                    type = typeof(Oracle);
                else if (provider.Contains("microsoft.jet.oledb"))
                    type = typeof(Access);
                else if (provider.Contains("access"))
                    type = typeof(Access);
                else if (provider.Contains("mysql"))
                    type = typeof(MySql);
                else if (provider.Contains("sqlite"))
                    type = typeof(SQLite);
                else if (provider.Contains("sql2008"))
                    type = typeof(SqlServer);
                else if (provider.Contains("sql2005"))
                    type = typeof(SqlServer);
                else if (provider.Contains("sql2000"))
                    type = typeof(SqlServer);
                else if (provider.Contains("sql"))
                    type = typeof(SqlServer);
                else
                {
                    //if (provider.Contains(",")) // ���г������ƣ����س���
                    //    type = Assembly.Load(provider.Substring(0, provider.IndexOf(","))).GetType(provider.Substring(provider.IndexOf(",") + 1), true, false);
                    //else // û�г������ƣ���ʹ�ñ�����
                    type = Type.GetType(provider, true, true);
                }
            }
            else
            {
                // ��������
                String str = connStr.ToLower();
                if (str.Contains("mssql") || str.Contains("sqloledb"))
                    type = typeof(SqlServer);
                else if (str.Contains("oracle"))
                    type = typeof(Oracle);
                else if (str.Contains("microsoft.jet.oledb"))
                    type = typeof(Access);
                else if (str.Contains("sql"))
                    type = typeof(SqlServer);
                else
                    type = typeof(Access);
            }
            return type;
        }
        #endregion

        #region ��̬����
        [ThreadStatic]
        private static DAL _Default;
        /// <summary>
        /// ��ǰ���ݷ��ʶ���
        /// </summary>
        public static DAL Default
        {
            get
            {
                if (_Default == null && ConnStrs != null && ConnStrs.Count > 0)
                {
                    String name = null;
                    foreach (String item in ConnStrs.Keys)
                    {
                        if (!String.IsNullOrEmpty(item))
                        {
                            name = item;
                            break;
                        }
                    }
                    if (!String.IsNullOrEmpty(name)) _Default = Create(name);
                }

                return _Default;
            }
        }
        #endregion

        #region ����
        private String _ConnName;
        /// <summary>
        /// ��������ֻ������Ҫ���ã�����������һ��DAL����
        /// </summary>
        public String ConnName
        {
            get { return _ConnName; }
        }

        private Type _DALType;
        /// <summary>
        /// ���ݷ��ʲ�������͡�
        /// </summary>
        /// <remarks>�ı����ݷ��ʲ����ݿ�ʵ���Ͽ���ǰ���ӣ��������κ����ݿ����֮ǰ�ı�</remarks>
        public Type DALType
        {
            get
            {
                if (_DALType == null && _connTypes.ContainsKey(ConnName)) _DALType = _connTypes[ConnName];
                return _DALType;
            }
            // ��������δʹ�ã�ע�ͣ�
            //set	// ����ⲿ��Ҫ�ı����ݷ��ʲ����ݿ�ʵ��
            //{
            //    IDbSession idb;
            //    //if (HttpContext.Current == null)
            //    idb = _Sessions != null && _Sessions.ContainsKey(ConnName) ? _Sessions[ConnName] : null;
            //    //else
            //    //    idb = HttpContext.Current.Items[ConnName + "_DB"] as IDataBase;
            //    if (idb != null)
            //    {
            //        idb.Dispose();
            //        idb = null;
            //    }
            //    _DALType = value;
            //}
        }

        /// <summary>
        /// ���ݿ�����
        /// </summary>
        public DatabaseType DbType
        {
            get { return Db.DbType; }
        }

        private String _ConnStr;
        /// <summary>
        /// Ĭ�������ַ�������һ��ConnectionString����
        /// </summary>
        public String ConnStr
        {
            get { return _ConnStr; }
            private set { _ConnStr = value; }
        }

        private IDatabase _Db;
        /// <summary>
        /// ���ݿ�
        /// </summary>
        public IDatabase Db
        {
            get
            {
                if (_Db != null) return _Db;

                Type type = DALType;
                if (type != null)
                {
                    _Db = TypeX.CreateInstance(type) as IDatabase;
                    _Db.ConnName = ConnName;
                    _Db.ConnectionString = ConnStr;
                }

                return _Db;
            }
        }

        /// <summary>
        /// ���ݿ�Ự
        /// </summary>
        /// <remarks>
        /// ����ʹ���̼߳���������󼶻��棬��֤�������ݿ�����̰߳�ȫ��
        /// ʹ���ⲿ���ݿ�������ʹ�����������½���
        /// </remarks>
        [Obsolete("���Ϊʹ��Session���ԣ�")]
        public IDbSession DB
        {
            get
            {
                return Session;
            }
        }

        /// <summary>
        /// ���ݿ�Ự
        /// </summary>
        /// <remarks>
        /// ����ʹ���̼߳���������󼶻��棬��֤�������ݿ�����̰߳�ȫ��
        /// ʹ���ⲿ���ݿ�������ʹ�����������½���
        /// </remarks>
        public IDbSession Session
        {
            get
            {
                if (String.IsNullOrEmpty(ConnStr)) throw new XCodeException("����ʹ�����ݿ�ǰ����[" + ConnName + "]�����ַ���");

                return Db.CreateSession();
            }
        }
        #endregion

        #region ʹ�û��������ݲ�������
        #region ����
        private Boolean _EnableCache = true;
        /// <summary>
        /// �Ƿ����û��档
        /// <remarks>��Ϊfalse����ջ���</remarks>
        /// </summary>
        public Boolean EnableCache
        {
            get { return _EnableCache; }
            set
            {
                _EnableCache = value;
                if (!_EnableCache) XCache.RemoveAll();
            }
        }

        /// <summary>
        /// �������
        /// </summary>
        public Int32 CacheCount
        {
            get
            {
                return XCache.Count;
            }
        }

        [ThreadStatic]
        private static Int32 _QueryTimes;
        /// <summary>
        /// ��ѯ����
        /// </summary>
        public static Int32 QueryTimes
        {
            //get { return DB != null ? DB.QueryTimes : 0; }
            get { return _QueryTimes; }
        }

        [ThreadStatic]
        private static Int32 _ExecuteTimes;
        /// <summary>
        /// ִ�д���
        /// </summary>
        public static Int32 ExecuteTimes
        {
            //get { return DB != null ? DB.ExecuteTimes : 0; }
            get { return _ExecuteTimes; }
        }
        #endregion

        private static Dictionary<String, String> _PageSplitCache = new Dictionary<String, String>();
        /// <summary>
        /// ������������ͨ��ѯSQL��ʽ��Ϊ��ҳSQL��
        /// </summary>
        /// <remarks>
        /// ��Ϊ��Ҫ�̳���д��ԭ�����������в������㻺���ҳSQL��
        /// ���������������档
        /// </remarks>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��ҳSQL</returns>
        public String PageSplit(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            String cacheKey = String.Format("{0}_{1}_{2}_{3}_", sql, startRowIndex, maximumRows, ConnName);
            if (!String.IsNullOrEmpty(keyColumn)) cacheKey += keyColumn;

            String rs = String.Empty;
            if (_PageSplitCache.TryGetValue(cacheKey, out rs)) return rs;
            lock (_PageSplitCache)
            {
                if (_PageSplitCache.TryGetValue(cacheKey, out rs)) return rs;

                String s = Db.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
                _PageSplitCache.Add(cacheKey, s);
                return s;
            }
        }

        /// <summary>
        /// ������������ͨ��ѯSQL��ʽ��Ϊ��ҳSQL��
        /// </summary>
        /// <remarks>
        /// ��Ϊ��Ҫ�̳���д��ԭ�����������в������㻺���ҳSQL��
        /// ���������������档
        /// </remarks>
        /// <param name="builder">��ѯ������</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��ҳSQL</returns>
        public String PageSplit(SelectBuilder builder, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            String cacheKey = String.Format("{0}_{1}_{2}_{3}_", builder.ToString(), startRowIndex, maximumRows, ConnName);
            if (!String.IsNullOrEmpty(keyColumn)) cacheKey += keyColumn;

            String rs = String.Empty;
            if (_PageSplitCache.TryGetValue(cacheKey, out rs)) return rs;
            lock (_PageSplitCache)
            {
                if (_PageSplitCache.TryGetValue(cacheKey, out rs)) return rs;

                String s = Db.PageSplit(builder, startRowIndex, maximumRows, keyColumn);
                _PageSplitCache.Add(cacheKey, s);
                return s;
            }
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ؼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableNames">�������ı�ı���</param>
        /// <returns></returns>
        public DataSet Select(String sql, String[] tableNames)
        {
            String cacheKey = sql + "_" + ConnName;
            if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            DataSet ds = Session.Query(sql);
            if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            return ds;
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ؼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableName">�������ı�ı���</param>
        /// <returns></returns>
        public DataSet Select(String sql, String tableName)
        {
            return Select(sql, new String[] { tableName });
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ط�ҳ��¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <param name="tableNames">�������ı�ı���</param>
        /// <returns></returns>
        public DataSet Select(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String[] tableNames)
        {
            //String cacheKey = sql + "_" + startRowIndex + "_" + maximumRows + "_" + ConnName;
            //if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            //Interlocked.Increment(ref _QueryTimes);
            //DataSet ds = DB.Query(PageSplit(sql, startRowIndex, maximumRows, keyColumn));
            //if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            //return ds;

            return Select(PageSplit(sql, startRowIndex, maximumRows, keyColumn), tableNames);
        }

        /// <summary>
        /// ִ��SQL��ѯ�����ط�ҳ��¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <param name="tableName">�������ı�ı���</param>
        /// <returns></returns>
        public DataSet Select(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String tableName)
        {
            return Select(sql, startRowIndex, maximumRows, keyColumn, new String[] { tableName });
        }

        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableNames">�������ı�ı���</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, String[] tableNames)
        {
            String cacheKey = sql + "_SelectCount" + "_" + ConnName;
            if (EnableCache && XCache.IntContain(cacheKey)) return XCache.IntItem(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            Int32 rs = Session.QueryCount(sql);
            if (EnableCache) XCache.Add(cacheKey, rs, tableNames);
            return rs;
        }

        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableName">�������ı�ı���</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, String tableName)
        {
            return SelectCount(sql, new String[] { tableName });
        }

        ///// <summary>
        ///// ִ��SQL��ѯ�������ܼ�¼��
        ///// </summary>
        ///// <param name="sql">SQL���</param>
        ///// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        ///// <param name="maximumRows">��󷵻�����</param>
        ///// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        ///// <param name="tableNames">�������ı�ı���</param>
        ///// <returns></returns>
        //public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String[] tableNames)
        //{
        //    return SelectCount(PageSplit(sql, startRowIndex, maximumRows, keyColumn), tableNames);
        //}

        ///// <summary>
        ///// ִ��SQL��ѯ�������ܼ�¼��
        ///// </summary>
        ///// <param name="sql">SQL���</param>
        ///// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        ///// <param name="maximumRows">��󷵻�����</param>
        ///// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        ///// <param name="tableName">�������ı�ı���</param>
        ///// <returns></returns>
        //public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String tableName)
        //{
        //    return SelectCount(sql, startRowIndex, maximumRows, keyColumn, new String[] { tableName });
        //}

        /// <summary>
        /// ִ��SQL��䣬������Ӱ�������
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableNames">��Ӱ��ı�ı���</param>
        /// <returns></returns>
        public Int32 Execute(String sql, String[] tableNames)
        {
            // �Ƴ����к���Ӱ����йصĻ���
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            return Session.Execute(sql);
        }

        /// <summary>
        /// ִ��SQL��䣬������Ӱ�������
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableName">��Ӱ��ı�ı���</param>
        /// <returns></returns>
        public Int32 Execute(String sql, String tableName)
        {
            return Execute(sql, new String[] { tableName });
        }

        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="tableNames">��Ӱ��ı�ı���</param>
        /// <returns>�����е��Զ����</returns>
        public Int64 InsertAndGetIdentity(String sql, String[] tableNames)
        {
            // �Ƴ����к���Ӱ����йصĻ���
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            return Session.InsertAndGetIdentity(sql);
        }

        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableName">��Ӱ��ı�ı���</param>
        /// <returns>�����е��Զ����</returns>
        public Int64 InsertAndGetIdentity(String sql, String tableName)
        {
            return InsertAndGetIdentity(sql, new String[] { tableName });
        }

        /// <summary>
        /// ִ��CMD�����ؼ�¼��
        /// </summary>
        /// <param name="cmd">CMD</param>
        /// <param name="tableNames">�������ı�ı���</param>
        /// <returns></returns>
        public DataSet Select(DbCommand cmd, String[] tableNames)
        {
            String cacheKey = cmd.CommandText + "_" + ConnName;
            if (EnableCache && XCache.Contain(cacheKey)) return XCache.Item(cacheKey);
            Interlocked.Increment(ref _QueryTimes);
            DataSet ds = Session.Query(cmd);
            if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            Session.AutoClose();
            return ds;
        }

        /// <summary>
        /// ִ��CMD��������Ӱ�������
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tableNames"></param>
        /// <returns></returns>
        public Int32 Execute(DbCommand cmd, String[] tableNames)
        {
            // �Ƴ����к���Ӱ����йصĻ���
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            Int32 ret = Session.Execute(cmd);
            Session.AutoClose();
            return ret;
        }

        ///// <summary>
        ///// ��ȡһ��DbCommand��
        ///// ���������ӣ�������������
        ///// �����Ѵ򿪡�
        ///// ʹ����Ϻ󣬱������AutoClose��������ʹ���ڷ������������Զ��رյ�����¹ر����ӡ�
        ///// �����Ȳ����ѣ������벻Ҫʹ�ø÷��������Կ�����Select(cmd)��Execute(cmd)�����档
        ///// �Ƿ�ʹ�û�ʹ����Դʧȥ���ơ�����Σ�գ�
        ///// </summary>
        ///// <returns></returns>
        //private DbCommand PrepareCommand()
        //{
        //    return DB.PrepareCommand();
        //}

        private List<XTable> _Tables;
        /// <summary>
        /// ȡ�����б����ͼ�Ĺ�����Ϣ
        /// </summary>
        /// <remarks>��������ڻ��棬���ȡ�󷵻أ�����ʹ���̳߳��̻߳�ȡ�������̷߳��ػ���</remarks>
        /// <returns></returns>
        public List<XTable> Tables
        {
            get
            {
                // ��������ڻ��棬���ȡ�󷵻أ�����ʹ���̳߳��̻߳�ȡ�������̷߳��ػ���
                if (_Tables == null)
                    _Tables = GetTables();
                else
                    ThreadPool.QueueUserWorkItem(delegate(Object state) { _Tables = GetTables(); });

                return _Tables;
            }
        }

        private List<XTable> GetTables()
        {
            List<XTable> list = Db.CreateMetaData().GetTables();
            if (list != null && list.Count > 0) list.Sort(delegate(XTable item1, XTable item2) { return item1.Name.CompareTo(item2.Name); });
            return list;
        }
        #endregion

        #region ����
        /// <summary>
        /// ��ʼ����
        /// ����һ����ʼ��������ڲ�����ɺ��ύ����ʧ��ʱ�ع���������ܻ������Դʧȥ���ơ�����Σ�գ�
        /// </summary>
        /// <returns></returns>
        public Int32 BeginTransaction()
        {
            return Session.BeginTransaction();
        }

        /// <summary>
        /// �ύ����
        /// </summary>
        /// <returns></returns>
        public Int32 Commit()
        {
            return Session.Commit();
        }

        /// <summary>
        /// �ع�����
        /// </summary>
        /// <returns></returns>
        public Int32 Rollback()
        {
            return Session.Rollback();
        }
        #endregion

        #region ���뵼��
        /// <summary>
        /// �����ܹ���Ϣ
        /// </summary>
        /// <returns></returns>
        public String Export()
        {
            IList<XTable> list = Tables;

            if (list == null || list.Count < 1) return null;

            XmlSerializer serializer = new XmlSerializer(typeof(XTable[]));
            using (StringWriter sw = new StringWriter())
            {
                serializer.Serialize(sw, list);
                return sw.ToString();
            }
        }

        /// <summary>
        /// ����ܹ���Ϣ
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static XTable[] Import(String xml)
        {
            if (String.IsNullOrEmpty(xml)) return null;

            XmlSerializer serializer = new XmlSerializer(typeof(XTable[]));
            using (StringReader sr = new StringReader(xml))
            {
                return serializer.Deserialize(sr) as XTable[];
            }
        }
        #endregion

        #region �������ݲ���ʵ��
        /// <summary>
        /// ����ʵ������ӿ�
        /// </summary>
        /// <remarks>��Ϊֻ������ʵ�����������ֻ��Ҫһ��ʵ������</remarks>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public IEntityOperate CreateOperate(String tableName)
        {
            Assembly asm = EntityAssembly.Create(this);
            Type type = asm.GetType(tableName);
            if (type == null)
            {
                Type[] ts = asm.GetTypes();
                foreach (Type item in ts)
                {
                    if (item.Name == tableName)
                    {
                        type = item;
                        break;
                    }
                }

                if (type == null) return null;
            }

            return EntityFactory.CreateOperate(type);
        }
        #endregion

        #region Sql��־���
        private static Boolean? _Debug;
        /// <summary>
        /// �Ƿ����
        /// </summary>
        public static Boolean Debug
        {
            get
            {
                if (_Debug != null) return _Debug.Value;

                _Debug = Config.GetConfig<Boolean>("XCode.Debug", Config.GetConfig<Boolean>("OrmDebug"));

                return _Debug.Value;
            }
            set { _Debug = value; }
        }

        /// <summary>
        /// �����־
        /// </summary>
        /// <param name="msg"></param>
        public static void WriteLog(String msg)
        {
            XTrace.WriteLine(msg);
        }

        /// <summary>
        /// �����־
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void WriteLog(String format, params Object[] args)
        {
            XTrace.WriteLine(format, args);
        }
        #endregion
    }
}