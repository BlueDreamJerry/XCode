using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Text;
using System.Threading;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Threading;
using XCode.Code;
using XCode.Exceptions;
using System.ComponentModel;
using NewLife;

namespace XCode.DataAccessLayer
{
    /// <summary>���ݷ��ʲ�</summary>
    /// <remarks>
    /// ��Ҫ����ѡ��ͬ�����ݿ⣬��ͬ�����ݿ�Ĳ����������
    /// ÿһ�����ݿ������ַ�������ӦΨһ��һ��DALʵ����
    /// ���ݿ������ַ�������д�������ļ��У�Ȼ����Createʱָ�����֣�
    /// Ҳ����ֱ�Ӱ������ַ�����ΪAddConnStr�Ĳ������롣
    /// ÿһ�����ݿ����������ָ�����������ڹ����棬�ձ�����*��ƥ�����л���
    /// </remarks>
    public partial class DAL
    {
        #region ��������
        /// <summary>���캯��</summary>
        /// <param name="connName">������</param>
        private DAL(String connName)
        {
            _ConnName = connName;

            //if (!ConnStrs.ContainsKey(connName)) throw new XCodeException("����ʹ�����ݿ�ǰ����[" + connName + "]�����ַ���");
            if (!ConnStrs.ContainsKey(connName))
            {
                var dbpath = ".";
                if (Runtime.IsWeb)
                {
                    if (!Environment.CurrentDirectory.Contains("iisexpress") ||
                        !Environment.CurrentDirectory.Contains("Web"))
                        dbpath = "..\\Data";
                    else
                        dbpath = "~\\App_Data";
                }
                var connstr = "Data Source={0}\\{1}.db".F(dbpath, connName);
                WriteLog("�Զ�Ϊ[{0}]���������ַ�����{1}", connName, connstr);
                AddConnStr(connName, connstr, null, "SQLite");
            }

            _ConnStr = ConnStrs[connName].ConnectionString;
            if (String.IsNullOrEmpty(_ConnStr)) throw new XCodeException("����ʹ�����ݿ�ǰ����[" + connName + "]�����ַ���");
        }

        private static Dictionary<String, DAL> _dals = new Dictionary<String, DAL>(StringComparer.OrdinalIgnoreCase);
        /// <summary>����һ�����ݷ��ʲ����</summary>
        /// <param name="connName">������</param>
        /// <returns>��Ӧ��ָ�����ӵ�ȫ��Ψһ�����ݷ��ʲ����</returns>
        public static DAL Create(String connName)
        {
            if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException("connName");

            // �����Ҫ�޸�һ��DAL�������ַ�������Ӧ���޸���������޸�DALʵ����ConnStr����
            DAL dal = null;
            if (_dals.TryGetValue(connName, out dal)) return dal;
            lock (_dals)
            {
                if (_dals.TryGetValue(connName, out dal)) return dal;

                dal = new DAL(connName);
                // ����connName����Ϊ�����ڴ����������Զ�ʶ����ConnName
                _dals.Add(dal.ConnName, dal);
            }

            return dal;
        }

        private static Object _connStrs_lock = new Object();
        private static Dictionary<String, ConnectionStringSettings> _connStrs;
        private static Dictionary<String, Type> _connTypes = new Dictionary<String, Type>(StringComparer.OrdinalIgnoreCase);
        /// <summary>�����ַ�������</summary>
        /// <remarks>
        /// �����Ҫ�޸�һ��DAL�������ַ�������Ӧ���޸���������޸�DALʵ����<see cref="ConnStr"/>����
        /// </remarks>
        public static Dictionary<String, ConnectionStringSettings> ConnStrs
        {
            get
            {
                if (_connStrs != null) return _connStrs;
                lock (_connStrs_lock)
                {
                    if (_connStrs != null) return _connStrs;
                    var cs = new Dictionary<String, ConnectionStringSettings>(StringComparer.OrdinalIgnoreCase);

                    // ��ȡ�����ļ�
                    var css = ConfigurationManager.ConnectionStrings;
                    if (css != null && css.Count > 0)
                    {
                        foreach (ConnectionStringSettings set in css)
                        {
                            if (set.ConnectionString.IsNullOrWhiteSpace()) continue;
                            if (set.Name == "LocalSqlServer") continue;
                            if (set.Name == "LocalMySqlServer") continue;

                            var type = DbFactory.GetProviderType(set.ConnectionString, set.ProviderName);
                            if (type == null) XTrace.WriteLine("�޷�ʶ��{0}���ṩ��{1}��", set.Name, set.ProviderName);

                            cs.Add(set.Name, set);
                            _connTypes.Add(set.Name, type);
                        }
                    }
                    _connStrs = cs;
                }
                return _connStrs;
            }
        }

        /// <summary>��������ַ���</summary>
        /// <param name="connName">������</param>
        /// <param name="connStr">�����ַ���</param>
        /// <param name="type">ʵ����IDatabase�ӿڵ����ݿ�����</param>
        /// <param name="provider">���ݿ��ṩ�ߣ����û��ָ�����ݿ����ͣ������ṩ���ж�ʹ����һ����������</param>
        public static void AddConnStr(String connName, String connStr, Type type, String provider)
        {
            if (String.IsNullOrEmpty(connName)) throw new ArgumentNullException("connName");

            if (type == null) type = DbFactory.GetProviderType(connStr, provider);
            if (type == null) throw new XCodeException("�޷�ʶ��{0}���ṩ��{1}��", connName, provider);

            // ��������߸���ǰ�����ù��˵�
            var set = new ConnectionStringSettings(connName, connStr, provider);
            ConnStrs[connName] = set;
            _connTypes[connName] = type;
        }

        /// <summary>��ȡ������ע���������</summary>
        /// <returns></returns>
        public static IEnumerable<String> GetNames() { return ConnStrs.Keys; }
        #endregion

        #region ����
        private String _ConnName;
        /// <summary>������</summary>
        public String ConnName { get { return _ConnName; } }

        private Type _ProviderType;
        /// <summary>ʵ����IDatabase�ӿڵ����ݿ�����</summary>
        private Type ProviderType
        {
            get
            {
                if (_ProviderType == null && _connTypes.ContainsKey(ConnName)) _ProviderType = _connTypes[ConnName];
                return _ProviderType;
            }
        }

        /// <summary>���ݿ�����</summary>
        public DatabaseType DbType
        {
            get
            {
                var db = DbFactory.GetDefault(ProviderType);
                if (db == null) return DatabaseType.Other;
                return db.DbType;
            }
        }

        private String _ConnStr;
        /// <summary>�����ַ���</summary>
        /// <remarks>
        /// �޸������ַ����������<see cref="Db"/>
        /// </remarks>
        public String ConnStr
        {
            get { return _ConnStr; }
            set
            {
                if (_ConnStr != value)
                {
                    _ConnStr = value;
                    _ProviderType = null;
                    _Db = null;

                    AddConnStr(ConnName, _ConnStr, null, null);
                }
            }
        }

        private IDatabase _Db;
        /// <summary>���ݿ⡣�������ݿ�����ڴ�ͳһ����ǿ�ҽ��鲻Ҫֱ��ʹ�ø����ԣ��ڲ�ͬ�汾��IDatabase�����нϴ�ı�</summary>
        public IDatabase Db
        {
            get
            {
                if (_Db != null) return _Db;
                lock (this)
                {
                    if (_Db != null) return _Db;

                    var type = ProviderType;
                    if (type == null) throw new XCodeException("�޷�ʶ��{0}�������ṩ�ߣ�", ConnName);

                    //_Db = type.CreateInstance() as IDatabase;
                    //if (!String.IsNullOrEmpty(ConnName)) _Db.ConnName = ConnName;
                    //if (!String.IsNullOrEmpty(ConnStr)) _Db.ConnectionString = DecodeConnStr(ConnStr);
                    //!!! ���������£��������������ַ���Ϊ127/master�����Ӵ��󣬷ǳ��п�������Ϊ�����̳߳�ͻ��A�̴߳�����ʵ����δ���ü���ֵ�����ַ������ͱ�B�߳�ʹ����
                    var db = type.CreateInstance() as IDatabase;
                    if (!String.IsNullOrEmpty(ConnName)) db.ConnName = ConnName;
                    if (!String.IsNullOrEmpty(ConnStr)) db.ConnectionString = DecodeConnStr(ConnStr);

                    //Interlocked.CompareExchange<IDatabase>(ref _Db, db, null);
                    _Db = db;

                    return _Db;
                }
            }
        }

        /// <summary>���ݿ�Ự</summary>
        public IDbSession Session { get { return Db.CreateSession(); } }
        #endregion

        #region �����ַ����������
        /// <summary>�����ַ�������</summary>
        /// <remarks>����=>UTF8�ֽ�=>Base64</remarks>
        /// <param name="connstr"></param>
        /// <returns></returns>
        public static String EncodeConnStr(String connstr)
        {
            if (String.IsNullOrEmpty(connstr)) return connstr;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(connstr));
        }

        /// <summary>�����ַ�������</summary>
        /// <remarks>Base64=>UTF8�ֽ�=>����</remarks>
        /// <param name="connstr"></param>
        /// <returns></returns>
        static String DecodeConnStr(String connstr)
        {
            if (String.IsNullOrEmpty(connstr)) return connstr;

            // ��������κη�Base64�����ַ���ֱ�ӷ���
            foreach (Char c in connstr)
            {
                if (!(c >= 'a' && c <= 'z' ||
                    c >= 'A' && c <= 'Z' ||
                    c >= '0' && c <= '9' ||
                    c == '+' || c == '/' || c == '=')) return connstr;
            }

            Byte[] bts = null;
            try
            {
                // ����Base64���룬�������ʧ�ܣ����ƾ��������ַ�����ֱ�ӷ���
                bts = Convert.FromBase64String(connstr);
            }
            catch { return connstr; }

            return Encoding.UTF8.GetString(bts);
        }
        #endregion

        #region ���򹤳�
        private List<IDataTable> _Tables;
        /// <summary>ȡ�����б����ͼ�Ĺ�����Ϣ���첽�����ӳ�1�룩����Ϊnull���������</summary>
        /// <remarks>
        /// ��������ڻ��棬���ȡ�󷵻أ�����ʹ���̳߳��̻߳�ȡ�������̷߳��ػ��档
        /// </remarks>
        /// <returns></returns>
        public List<IDataTable> Tables
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
            set
            {
                //��Ϊnull���������
                _Tables = null;
            }
        }

        private List<IDataTable> GetTables()
        {
            CheckBeforeUseDatabase();
            return Db.CreateMetaData().GetTables();
        }

        /// <summary>����ģ��</summary>
        /// <returns></returns>
        public String Export()
        {
            var list = Tables;

            if (list == null || list.Count < 1) return null;

            return Export(list);
        }

        /// <summary>����ģ��</summary>
        /// <param name="tables"></param>
        /// <returns></returns>
        public static String Export(IEnumerable<IDataTable> tables)
        {
            return ModelHelper.ToXml(tables);
        }

        /// <summary>����ģ��</summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static List<IDataTable> Import(String xml)
        {
            if (String.IsNullOrEmpty(xml)) return null;

            return ModelHelper.FromXml(xml, CreateTable);
        }
        #endregion

        #region ���򹤳�
        Int32 _hasCheck;
        /// <summary>ʹ�����ݿ�֮ǰ����ܹ�</summary>
        /// <remarks>�����������ܵ�һ���߳����ڼ���ܹ�������߳��Ѿ���ʼʹ�����ݿ���</remarks>
        void CheckBeforeUseDatabase()
        {
            if (_hasCheck > 0 || Interlocked.CompareExchange(ref _hasCheck, 1, 0) > 0) return;

            try
            {
                SetTables();
            }
            catch (Exception ex)
            {
                if (Debug) WriteLog(ex.ToString());
            }
        }

        /// <summary>���򹤳̡�������в��õ�ǰ���ӵ�ʵ��������ݱ�ܹ�</summary>
        private void SetTables()
        {
            if (!Setting.Current.Negative.Enable || NegativeExclude.Contains(ConnName)) return;

            // NegativeCheckOnly����Ϊtrueʱ��ʹ���첽��ʽ��飬��Ϊ�ϼ�����˼�ǲ���������ݿ�ܹ�
            if (!Setting.Current.Negative.CheckOnly)
                CheckTables();
            else
                ThreadPoolX.QueueUserWorkItem(CheckTables);
        }

        internal List<String> HasCheckTables = new List<String>();
        /// <summary>����Ƿ��Ѵ��ڣ���������������</summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        internal Boolean CheckAndAdd(String tableName)
        {
            var tbs = HasCheckTables;
            if (tbs.Contains(tableName)) return true;
            lock (tbs)
            {
                if (tbs.Contains(tableName)) return true;

                tbs.Add(tableName);
            }

            return false;
        }

        /// <summary>������ݱ�ܹ������ܷ��򹤳����ÿ������ƣ������δ����������ı�</summary>
        public void CheckTables()
        {
            WriteLog("��ʼ�������[{0}/{1}]�����ݿ�ܹ�����", ConnName, DbType);

            var sw = new Stopwatch();
            sw.Start();

            try
            {
                var list = EntityFactory.GetTables(ConnName);
                if (list != null && list.Count > 0)
                {
                    // �Ƴ������ѳ�ʼ����
                    list.RemoveAll(dt => CheckAndAdd(dt.TableName));
                    //// ȫ����Ϊ�ѳ�ʼ����
                    //foreach (var item in list)
                    //{
                    //    if (!HasCheckTables.Contains(item.TableName)) HasCheckTables.Add(item.TableName);
                    //}

                    // ���˵����ų��ı���
                    if (NegativeExclude.Count > 0)
                    {
                        for (int i = list.Count - 1; i >= 0; i--)
                        {
                            if (NegativeExclude.Contains(list[i].TableName)) list.RemoveAt(i);
                        }
                    }
                    // ���˵���ͼ
                    list.RemoveAll(dt => dt.IsView);
                    if (list != null && list.Count > 0)
                    {
                        WriteLog(ConnName + "������ܹ���ʵ�������" + list.Count);

                        SetTables(null, list.ToArray());
                    }
                }
            }
            finally
            {
                sw.Stop();

                WriteLog("�������[{0}/{1}]�����ݿ�ܹ���ʱ{2:n0}ms", ConnName, DbType, sw.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>�ڵ�ǰ�����ϼ��ָ�����ݱ�ļܹ�</summary>
        /// <param name="tables"></param>
        [Obsolete("=>SetTables(set, tables)")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetTables(params IDataTable[] tables) { SetTables(null, tables); }

        /// <summary>�ڵ�ǰ�����ϼ��ָ�����ݱ�ļܹ�</summary>
        /// <param name="set"></param>
        /// <param name="tables"></param>
        public void SetTables(NegativeSetting set, params IDataTable[] tables)
        {
            if (set == null)
            {
                set = new NegativeSetting();
                set.CheckOnly = Setting.Current.Negative.CheckOnly;
                set.NoDelete = Setting.Current.Negative.NoDelete;
            }
            //if (set.CheckOnly && DAL.Debug) WriteLog("XCode.Negative.CheckOnly����ΪTrue��ֻ�Ǽ�鲻�����ݿ���в���");
            //if (set.NoDelete && DAL.Debug) WriteLog("XCode.Negative.NoDelete����ΪTrue������ɾ�����ݱ�����ֶ�");
            Db.CreateMetaData().SetTables(set, tables);
        }
        #endregion

        #region �������ݲ���ʵ��
        private EntityAssembly _Assembly;
        /// <summary>��������ģ�Ͷ�̬�����ĳ��򼯡������棬���Ҫ���£��������<see cref="EntityAssembly.Create(string, string, System.Collections.Generic.List&lt;XCode.DataAccessLayer.IDataTable&gt;)"/></summary>
        public EntityAssembly Assembly
        {
            get
            {
                return _Assembly ?? (_Assembly = EntityAssembly.CreateWithCache(ConnName, Tables));
            }
            set { _Assembly = value; }
        }

        /// <summary>����ʵ������ӿ�</summary>
        /// <remarks>��Ϊֻ������ʵ�����������ֻ��Ҫһ��ʵ������</remarks>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public IEntityOperate CreateOperate(String tableName)
        {
            var asm = Assembly;
            if (asm == null) return null;
            var type = asm.GetType(tableName);
            if (type == null)
                return null;
            else
                return EntityFactory.CreateOperate(type);
        }
        #endregion
    }
}