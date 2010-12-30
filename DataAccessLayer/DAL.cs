using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Serialization;
using XCode.Cache;
using XCode.Code;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// ���ݷ��ʲ㡣
    /// <remarks>
    /// ��Ҫ����ѡ��ͬ�����ݿ⣬��ͬ�����ݿ�Ĳ����������
    /// ÿһ�����ݿ������ַ�������ӦΨһ��һ��DALʵ����
    /// ���ݿ������ַ�������д�������ļ��У�Ȼ����Createʱָ�����֣�
    /// Ҳ����ֱ�Ӱ������ַ�����ΪAddConnStr�Ĳ������롣
    /// ÿһ��DALʵ������Ϊÿһ���̳߳�ʼ��һ��DataBaseʵ����
    /// ÿһ�����ݿ����������ָ�����������ڹ����棬�ձ�����*��ƥ�����л���
    /// </remarks>
    /// </summary>
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
            if (String.IsNullOrEmpty(connName)) return new DAL(null);

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

                            Type type = GetTypeFromConn(set.ConnectionString, set.ProviderName);

                            cs.Add(set.Name, set);
                            _connTypes.Add(set.Name, type);

#if DEBUG
                            NewLife.Log.XTrace.WriteLine("�������ݿ�����{0}��{1}", set.Name, set.ConnectionString);
#endif
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

            if (ConnStrs.ContainsKey(connName)) return;
            lock (ConnStrs)
            {
                if (ConnStrs.ContainsKey(connName)) return;

                if (type == null) type = GetTypeFromConn(connStr, provider);

                ConnectionStringSettings set = new ConnectionStringSettings(connName, connStr, provider);
                ConnStrs.Add(connName, set);
                _connTypes.Add(connName, type);
#if DEBUG
                NewLife.Log.XTrace.WriteLine("�������ݿ�����{0}��{1}", set.Name, set.ConnectionString);
#endif
            }
        }

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
                    type = typeof(SqlServer2005);
                else if (provider.Contains("sql2005"))
                    type = typeof(SqlServer2005);
                else if (provider.Contains("sql2000"))
                    type = typeof(SqlServer);
                else if (provider.Contains("sql"))
                    type = typeof(SqlServer);
                else
                {
                    if (provider.Contains(",")) // ���г������ƣ����س���
                        type = Assembly.Load(provider.Substring(0, provider.IndexOf(","))).GetType(provider.Substring(provider.IndexOf(",") + 1, provider.Length), true, false);
                    else // û�г������ƣ���ʹ�ñ�����
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
        /// <remarks>�ı����ݷ��ʲ����ݿ�ʵ���Ͽ���ǰ���ӣ��������κ����ݿ����֮ǰ�ı�</remarks>
        /// </summary>
        public Type DALType
        {
            get
            {
                if (_DALType == null && _connTypes.ContainsKey(ConnName)) _DALType = _connTypes[ConnName];
                return _DALType;
            }
            set	// ����ⲿ��Ҫ�ı����ݷ��ʲ����ݿ�ʵ��
            {
                IDatabase idb;
                //if (HttpContext.Current == null)
                idb = _DBs != null && _DBs.ContainsKey(ConnName) ? _DBs[ConnName] : null;
                //else
                //    idb = HttpContext.Current.Items[ConnName + "_DB"] as IDataBase;
                if (idb != null)
                {
                    idb.Dispose();
                    idb = null;
                }
                _DALType = value;
            }
        }

        /// <summary>
        /// ���ݿ�����
        /// </summary>
        public DatabaseType DbType
        {
            get { return DB.DbType; }
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

        /// <summary>
        /// ThreadStatic ָʾ��̬�ֶε�ֵ����ÿ���̶߳���Ψһ�ġ�
        /// </summary>
        [ThreadStatic]
        private static IDictionary<String, IDatabase> _DBs;
        /// <summary>
        /// DAL����
        /// <remarks>
        /// ����ʹ���̼߳���������󼶻��棬��֤�������ݿ�����̰߳�ȫ��
        /// ʹ���ⲿ���ݿ�������ʹ�����������½���
        /// </remarks>
        /// </summary>
        public IDatabase DB
        {
            get
            {
                if (String.IsNullOrEmpty(ConnStr)) throw new XCodeException("����ʹ�����ݿ�ǰ����[" + ConnName + "]�����ַ���");

                //if (HttpContext.Current == null) // ��Web����ʹ���̼߳�����
                return CreateForNotWeb();
                //else
                //    return CreateForWeb();
            }
        }

        private static Dictionary<String, Boolean> IsSql2005 = new Dictionary<String, Boolean>();

        private IDatabase CreateForNotWeb()
        {
            if (_DBs == null) _DBs = new Dictionary<String, IDatabase>();

            IDatabase _DB;
            if (_DBs.TryGetValue(ConnName, out _DB)) return _DB;
            lock (_DBs)
            {
                if (_DBs.TryGetValue(ConnName, out _DB)) return _DB;

                //// ����������ȡ�ó��򼯣��ٴ���ʵ������Ϊ�˷�ֹ�ڱ����򼯴����ⲿDAL���ʵ��������
                ////�����Ȩ
                //if (!License.Check()) return null;

                if (DALType == typeof(Access))
                    _DB = new Access();
                else if (DALType == typeof(SqlServer))
                    _DB = new SqlServer();
                else if (DALType == typeof(SqlServer2005))
                    _DB = new SqlServer2005();
                else if (DALType == typeof(Oracle))
                    _DB = new Oracle();
                else if (DALType == typeof(MySql))
                    _DB = new MySql();
                else if (DALType == typeof(SQLite))
                    _DB = new SQLite();
                else
                    _DB = DALType.Assembly.CreateInstance(DALType.FullName, false, BindingFlags.Default, null, new Object[] { ConnStr }, null, null) as IDatabase;

                _DB.ConnectionString = ConnStr;

                //����Ƿ�SqlServer2005
                //_DB = CheckSql2005(_DB);

                if (!IsSql2005.ContainsKey(ConnName))
                {
                    lock (IsSql2005)
                    {
                        if (!IsSql2005.ContainsKey(ConnName))
                        {
                            IsSql2005.Add(ConnName, CheckSql2005(_DB));
                        }
                    }
                }

                if (DALType != typeof(SqlServer2005) && IsSql2005.ContainsKey(ConnName) && IsSql2005[ConnName])
                {
                    _DALType = typeof(SqlServer2005);
                    _DB.Dispose();
                    _DB = new SqlServer2005();
                    _DB.ConnectionString = ConnStr;
                }

                _DBs.Add(ConnName, _DB);

                if (Database.Debug) Database.WriteLog("����DB��NotWeb����{0}", _DB.ID);

                return _DB;
            }
        }

        //private IDataBase CreateForWeb()
        //{
        //    String key = ConnName + "_DB";
        //    IDataBase d;

        //    if (HttpContext.Current.Items[key] != null && HttpContext.Current.Items[key] is IDataBase)
        //        d = HttpContext.Current.Items[key] as IDataBase;
        //    else
        //    {
        //        //�����Ȩ
        //        if (!License.Check()) return null;

        //        if (DALType == typeof(Access))
        //            d = new Access();
        //        else if (DALType == typeof(SqlServer))
        //            d = new SqlServer();
        //        else if (DALType == typeof(Oracle))
        //            d = new Oracle();
        //        else if (DALType == typeof(MySql))
        //            d = new MySql();
        //        else if (DALType == typeof(SQLite))
        //            d = new SQLite();
        //        else
        //            d = DALType.Assembly.CreateInstance(DALType.FullName, false, BindingFlags.Default, null, new Object[] { ConnStr }, null, null) as IDataBase;

        //        d.ConnectionString = ConnStr;

        //        if (DataBase.Debug) DataBase.WriteLog("����DB��Web����{0}", d.ID);

        //        HttpContext.Current.Items.Add(key, d);
        //    }
        //    //����Ƿ�SqlServer2005
        //    //_DB = CheckSql2005(_DB);

        //    if (!IsSql2005.ContainsKey(ConnName))
        //    {
        //        lock (IsSql2005)
        //        {
        //            if (!IsSql2005.ContainsKey(ConnName))
        //            {
        //                IsSql2005.Add(ConnName, CheckSql2005(d));
        //            }
        //        }
        //    }

        //    if (IsSql2005.ContainsKey(ConnName) && IsSql2005[ConnName])
        //    {
        //        _DALType = typeof(SqlServer2005);
        //        d.Dispose();
        //        d = new SqlServer2005();
        //        d.ConnectionString = ConnStr;
        //    }

        //    return d;
        //}

        //private IDataBase CheckSql2005(IDataBase db)
        //{
        //    //����Ƿ�SqlServer2005
        //    if (db.DbType != DatabaseType.SqlServer) return db;

        //    //ȡ���ݿ�汾
        //    DataSet ds = db.Query("Select @@Version");
        //    if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows != null && ds.Tables[0].Rows.Count > 0)
        //    {
        //        String ver = ds.Tables[0].Rows[0][0].ToString();
        //        if (!String.IsNullOrEmpty(ver) && ver.StartsWith("Microsoft SQL Server 2005"))
        //        {
        //            _DALType = typeof(SqlServer2005);
        //            db.Dispose();
        //            db = new SqlServer2005(ConnStr);
        //        }
        //    }
        //    return db;
        //}

        private Boolean CheckSql2005(IDatabase db)
        {
            //����Ƿ�SqlServer2005
            if (db.DbType != DatabaseType.SqlServer) return false;

            //�л���master��
            Database d = db as Database;
            String dbname = d.DatabaseName;
            //���ָ�������ݿ��������Ҳ���master�����л���master
            if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
            {
                d.DatabaseName = "master";
            }

            //ȡ���ݿ�汾
            Boolean b = false;
            //DataSet ds = db.Query("Select @@Version");
            //if (ds.Tables != null && ds.Tables.Count > 0 && ds.Tables[0].Rows != null && ds.Tables[0].Rows.Count > 0)
            //{
            //    String ver = ds.Tables[0].Rows[0][0].ToString();
            //    if (!String.IsNullOrEmpty(ver) && ver.StartsWith("Microsoft SQL Server 2005"))
            //    {
            //        b = true;
            //    }
            //}
            String ver = db.ServerVersion;
            b = !ver.StartsWith("08");

            if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
            {
                d.DatabaseName = dbname;
            }

            return b;
        }

        /// <summary>
        /// �Ƿ����DBʵ����
        /// ���ֱ��ʹ��DB�����ж��Ƿ���ڣ������ᴴ��һ��ʵ����
        /// </summary>
        private Boolean ExistDB
        {
            get
            {
                //if (HttpContext.Current == null || HttpContext.Current.Items == null)
                //{
                if (_DBs != null && !_DBs.ContainsKey(ConnName)) return true;
                return false;
                //}
                //else
                //{
                //    String key = ConnName + "_DB";
                //    if (HttpContext.Current.Items[key] != null && HttpContext.Current.Items[key] is IDataBase) return true;
                //    return false;
                //}
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

                String s = DB.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
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

                String s = DB.PageSplit(builder, startRowIndex, maximumRows, keyColumn);
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
            DataSet ds = DB.Query(sql);
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
            Int32 rs = DB.QueryCount(sql);
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

        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <param name="tableNames">�������ı�ı���</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String[] tableNames)
        {
            return SelectCount(PageSplit(sql, startRowIndex, maximumRows, keyColumn), tableNames);
        }

        /// <summary>
        /// ִ��SQL��ѯ�������ܼ�¼��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <param name="tableName">�������ı�ı���</param>
        /// <returns></returns>
        public Int32 SelectCount(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn, String tableName)
        {
            return SelectCount(sql, startRowIndex, maximumRows, keyColumn, new String[] { tableName });
        }

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
            return DB.Execute(sql);
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
        public Int32 InsertAndGetIdentity(String sql, String[] tableNames)
        {
            // �Ƴ����к���Ӱ����йصĻ���
            if (EnableCache) XCache.Remove(tableNames);
            Interlocked.Increment(ref _ExecuteTimes);
            return DB.InsertAndGetIdentity(sql);
        }

        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="tableName">��Ӱ��ı�ı���</param>
        /// <returns>�����е��Զ����</returns>
        public Int32 InsertAndGetIdentity(String sql, String tableName)
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
            DataSet ds = DB.Query(cmd);
            if (EnableCache) XCache.Add(cacheKey, ds, tableNames);
            DB.AutoClose();
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
            Int32 ret = DB.Execute(cmd);
            DB.AutoClose();
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
            List<XTable> list = DB.GetTables();
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
            return DB.BeginTransaction();
        }

        /// <summary>
        /// �ύ����
        /// </summary>
        /// <returns></returns>
        public Int32 Commit()
        {
            return DB.Commit();
        }

        /// <summary>
        /// �ع�����
        /// </summary>
        /// <returns></returns>
        public Int32 Rollback()
        {
            return DB.Rollback();
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
    }
}