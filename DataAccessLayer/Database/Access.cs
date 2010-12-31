using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using ADODB;
using ADOX;
using DAO;
using NewLife.Log;
using XCode.Common;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// Access���ݿ�
    /// </summary>
    internal class AccessSession : DbSession
    {
        #region ����
        ///// <summary>
        ///// �������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other
        ///// </summary>
        //public override DatabaseType DbType
        //{
        //    get { return DatabaseType.Access; }
        //}

        ///// <summary>����</summary>
        //public override DbProviderFactory Factory
        //{
        //    get
        //    {

        //        return OleDbFactory.Instance;
        //    }
        //}

        /// <summary>�����ַ���</summary>
        public override string ConnectionString
        {
            get
            {
                return base.ConnectionString;
            }
            set
            {
                try
                {
                    OleDbConnectionStringBuilder csb = new OleDbConnectionStringBuilder(value);
                    // ���Ǿ���·��
                    if (!String.IsNullOrEmpty(csb.DataSource) && csb.DataSource.Length > 1 && csb.DataSource.Substring(1, 1) != ":")
                    {
                        String mdbPath = csb.DataSource;
                        if (mdbPath.StartsWith("~/") || mdbPath.StartsWith("~\\"))
                        {
                            mdbPath = mdbPath.Replace("/", "\\").Replace("~\\", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\");
                        }
                        else if (mdbPath.StartsWith("./") || mdbPath.StartsWith(".\\"))
                        {
                            mdbPath = mdbPath.Replace("/", "\\").Replace(".\\", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\\");
                        }
                        else
                        {
                            mdbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, mdbPath.Replace("/", "\\"));
                        }
                        csb.DataSource = mdbPath;
                        FileName = mdbPath;
                        value = csb.ConnectionString;
                    }
                }
                catch (DbException ex)
                {
                    throw new XDbException(this, "����OLEDB�����ַ���ʱ����", ex);
                }
                base.ConnectionString = value;
            }
        }

        private String _FileName;
        /// <summary>�ļ�</summary>
        public String FileName
        {
            get { return _FileName; }
            private set { _FileName = value; }
        }
        #endregion

        #region ����ʹ�����ӳ�
        /// <summary>
        /// �򿪡�����д��Ϊ�˽������ݿ�
        /// </summary>
        public override void Open()
        {
            if (!Supported) return;

            if (!File.Exists(FileName)) CreateDatabase();

            //try
            //{
            base.Open();
            //}
            //catch (InvalidOperationException ex)
            //{
            //    if (ex.Message.Contains("Microsoft.Jet.OLEDB.4.0"))
            //        throw new InvalidOperationException("64λϵͳ��֧��OLEDB����ѱ���ƽ̨��Ϊx86��", ex);

            //    throw;
            //}
        }
        #endregion

        #region �������� ��ѯ/ִ��
        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns>�����е��Զ����</returns>
        public override Int32 InsertAndGetIdentity(String sql)
        {
            ExecuteTimes++;
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                Int32 rs = cmd.ExecuteNonQuery();
                if (rs > 0)
                {
                    cmd.CommandText = "Select @@Identity";
                    rs = Int32.Parse(cmd.ExecuteScalar().ToString());
                }
                AutoClose();
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
        }
        #endregion

        #region ����
        //static TypeX oledbSchema;
        ///// <summary>
        ///// �����ء����������ʹ��OleDb�����GetOleDbSchemaTable
        ///// </summary>
        ///// <param name="collectionName"></param>
        ///// <param name="restrictionValues"></param>
        ///// <returns></returns>
        //public override DataTable GetSchema(string collectionName, string[] restrictionValues)
        //{
        //    if (oledbSchema == null) oledbSchema = TypeX.Create(typeof(OleDbSchemaGuid));

        //    if (String.IsNullOrEmpty(collectionName))
        //    {
        //        DataTable dt = base.GetSchema(collectionName, restrictionValues);
        //        foreach (FieldInfoX item in oledbSchema.Fields)
        //        {
        //            DataRow dr = dt.NewRow();
        //            dr[0] = item.Field.Name;
        //            dt.Rows.Add(dr);
        //        }
        //        return dt;
        //    }

        //    if (oledbSchema.Fields != null && oledbSchema.Fields.Count > 0)
        //    {
        //        foreach (FieldInfoX item in oledbSchema.Fields)
        //        {
        //            if (!String.Equals(item.Field.Name, collectionName, StringComparison.OrdinalIgnoreCase)) continue;

        //            Guid guid = (Guid)item.GetValue();
        //            if (guid != Guid.Empty)
        //            {
        //                Object[] pms = null;
        //                if (restrictionValues != null)
        //                {
        //                    pms = new Object[restrictionValues.Length];
        //                    for (int i = 0; i < restrictionValues.Length; i++)
        //                    {
        //                        pms[i] = restrictionValues[i];
        //                    }
        //                }
        //                //return (Conn as OleDbConnection).GetOleDbSchemaTable(guid, pms);
        //                return GetOleDbSchemaTable(guid, pms);
        //            }
        //        }
        //    }

        //    return base.GetSchema(collectionName, restrictionValues);
        //}

        //private DataTable GetOleDbSchemaTable(Guid schema, object[] restrictions)
        //{
        //    if (!Opened) Open();

        //    try
        //    {
        //        return (Conn as OleDbConnection).GetOleDbSchemaTable(schema, restrictions);
        //    }
        //    //catch (Exception ex)
        //    //{
        //    //    if (Debug) WriteLog(ex.ToString());
        //    //    return null;
        //    //}
        //    catch (DbException ex)
        //    {
        //        throw new XDbException(this, "ȡ�����б��ܳ���", ex);
        //    }
        //    finally
        //    {
        //        AutoClose();
        //    }
        //}

        public override List<XTable> GetTables()
        {
            DataTable dt = GetSchema("Tables", null);
            if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return null;

            // Ĭ���г������ֶ�
            DataRow[] rows = dt.Select(String.Format("{0}='Table' Or {0}='View'", "TABLE_TYPE"));
            return GetTables(rows);
        }

        protected override List<XField> GetFields(XTable xt)
        {
            List<XField> list = base.GetFields(xt);
            if (list == null || list.Count < 1) return null;

            Dictionary<String, XField> dic = new Dictionary<String, XField>();
            foreach (XField xf in list)
            {
                dic.Add(xf.Name, xf);
            }

            try
            {
                using (ADOTabe table = new ADOTabe(ConnectionString, FileName, xt.Name))
                {
                    if (table.Supported && table.Columns != null)
                    {
                        foreach (ADOColumn item in table.Columns)
                        {
                            if (!dic.ContainsKey(item.Name)) continue;

                            dic[item.Name].Identity = item.AutoIncrement;
                            if (!dic[item.Name].Identity) dic[item.Name].Nullable = item.Nullable;
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        protected override void FixField(XField field, DataRow dr)
        {
            base.FixField(field, dr);

            // �ֶα�ʶ
            Int64 flag = GetDataRowValue<Int64>(dr, "COLUMN_FLAGS");

            Boolean? isLong = null;

            Int32 id = 0;
            if (Int32.TryParse(GetDataRowValue<String>(dr, "DATA_TYPE"), out id))
            {
                DataRow[] drs = FindDataType(id, isLong);
                if (drs != null && drs.Length > 0)
                {
                    String typeName = GetDataRowValue<String>(drs[0], "TypeName");
                    field.RawType = typeName;

                    if (TryGetDataRowValue<String>(drs[0], "DataType", out typeName)) field.DataType = Type.GetType(typeName);

                    // ������ע����
                    if (field.DataType == typeof(String) && drs.Length > 1)
                    {
                        isLong = (flag & 0x80) == 0x80;
                        drs = FindDataType(id, isLong);
                        if (drs != null && drs.Length > 0)
                        {
                            typeName = GetDataRowValue<String>(drs[0], "TypeName");
                            field.RawType = typeName;
                        }
                    }
                }
            }

            //// ��������
            //if (field.DataType == typeof(Int32))
            //{
            //    //field.Identity = (flag & 0x20) != 0x20;
            //}
        }

        protected override Dictionary<DataRow, String> GetPrimaryKeys(string tableName)
        {
            Dictionary<DataRow, String> pks = base.GetPrimaryKeys(tableName);
            if (pks == null || pks.Count < 1) return null;
            if (pks.Count == 1) return pks;

            // �����������������
            List<DataRow> list = new List<DataRow>();
            foreach (DataRow item in pks.Keys)
            {
                if (!GetDataRowValue<Boolean>(item, "PRIMARY_KEY")) list.Add(item);
            }
            if (list.Count == pks.Count) return pks;

            foreach (DataRow item in list)
            {
                pks.Remove(item);
            }
            return pks;
        }
        #endregion

        #region ���ݶ���
        /// <summary>
        /// �������ݶ���ģʽ
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public override object SetSchema(DDLSchema schema, object[] values)
        {
            Object obj = null;
            switch (schema)
            {
                case DDLSchema.CreateDatabase:
                    CreateDatabase();
                    return null;
                case DDLSchema.DropDatabase:
                    //���ȹر����ݿ�
                    Close();

                    OleDbConnection.ReleaseObjectPool();
                    GC.Collect();

                    if (File.Exists(FileName)) File.Delete(FileName);
                    return null;
                case DDLSchema.DatabaseExist:
                    return File.Exists(FileName);
                case DDLSchema.CreateTable:
                    obj = base.SetSchema(DDLSchema.CreateTable, values);
                    XTable table = values[0] as XTable;
                    if (!String.IsNullOrEmpty(table.Description)) AddTableDescription(table.Name, table.Description);
                    foreach (XField item in table.Fields)
                    {
                        if (!String.IsNullOrEmpty(item.Description)) AddColumnDescription(table.Name, item.Name, item.Description);
                    }
                    return obj;
                case DDLSchema.DropTable:
                    break;
                case DDLSchema.TableExist:
                    DataTable dt = GetSchema("Tables", new String[] { null, null, (String)values[0], "TABLE" });
                    if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return false;
                    return true;
                case DDLSchema.AddTableDescription:
                    return AddTableDescription((String)values[0], (String)values[1]);
                case DDLSchema.DropTableDescription:
                    return DropTableDescription((String)values[0]);
                case DDLSchema.AddColumn:
                    obj = base.SetSchema(DDLSchema.AddColumn, values);
                    AddColumnDescription((String)values[0], ((XField)values[1]).Name, ((XField)values[1]).Description);
                    return obj;
                case DDLSchema.AlterColumn:
                    break;
                case DDLSchema.DropColumn:
                    break;
                case DDLSchema.AddColumnDescription:
                    return AddColumnDescription((String)values[0], (String)values[1], (String)values[2]);
                case DDLSchema.DropColumnDescription:
                    return DropColumnDescription((String)values[0], (String)values[1]);
                case DDLSchema.AddDefault:
                    return AddDefault((String)values[0], (String)values[1], (String)values[2]);
                case DDLSchema.DropDefault:
                    return DropDefault((String)values[0], (String)values[1]);
                default:
                    break;
            }
            return base.SetSchema(schema, values);
        }

        #region �������ݿ�
        private void CreateDatabase()
        {
            FileSource.ReleaseFile("Database.mdb", FileName, true);
        }
        #endregion

        #region ����ֶα�ע
        public Boolean AddTableDescription(String tablename, String description)
        {
            try
            {
                using (ADOTabe table = new ADOTabe(ConnectionString, FileName, tablename))
                {
                    table.Description = description;
                    return true;
                }
            }
            catch { return false; }
        }

        public Boolean DropTableDescription(String tablename)
        {
            return AddTableDescription(tablename, null);
        }

        public Boolean AddColumnDescription(String tablename, String columnname, String description)
        {
            try
            {
                using (ADOTabe table = new ADOTabe(ConnectionString, FileName, tablename))
                {
                    if (table.Supported && table.Columns != null)
                    {
                        foreach (ADOColumn item in table.Columns)
                        {
                            if (item.Name == columnname)
                            {
                                item.Description = description;
                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        public Boolean DropColumnDescription(String tablename, String columnname)
        {
            return AddColumnDescription(tablename, columnname, null);
        }
        #endregion

        #region Ĭ��ֵ
        public virtual Boolean AddDefault(String tablename, String columnname, String value)
        {
            try
            {
                using (ADOTabe table = new ADOTabe(ConnectionString, FileName, tablename))
                {
                    if (table.Supported && table.Columns != null)
                    {
                        foreach (ADOColumn item in table.Columns)
                        {
                            if (item.Name == columnname)
                            {
                                item.Default = value;
                                return true;
                            }
                        }
                    }
                    return false;
                }
            }
            catch { return false; }
        }

        public virtual Boolean DropDefault(String tablename, String columnname)
        {
            return AddDefault(tablename, columnname, null);
        }
        #endregion
        #endregion

        #region ��������
        //public override Type FieldTypeToClassType(String typeName)
        //{
        //    Int32 id = 0;
        //    if (Int32.TryParse(typeName, out id))
        //    {
        //        DataRow[] drs = FindDataType(id, null);
        //        if (drs != null && drs.Length > 0)
        //        {
        //            if (!TryGetDataRowValue<String>(drs[0], "DataType", out typeName)) return null;
        //            return Type.GetType(typeName);
        //        }
        //    }

        //    return base.FieldTypeToClassType(typeName);
        //}

        DataRow[] FindDataType(Int32 typeID, Boolean? isLong)
        {
            DataTable dt = DataTypes;
            if (dt == null) return null;

            DataRow[] drs = null;
            if (isLong == null)
            {
                drs = dt.Select(String.Format("NativeDataType={0}", typeID));
                if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0}", typeID));
            }
            else
            {
                drs = dt.Select(String.Format("NativeDataType={0} And IsLong={1}", typeID, isLong.Value));
                if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0} And IsLong={1}", typeID, isLong.Value));
            }
            return drs;
        }
        #endregion

        #region ����
        //static Access()
        //{
        //    Module module = typeof(Object).Module;

        //    PortableExecutableKinds kind;
        //    ImageFileMachine machine;
        //    module.GetPEKind(out kind, out machine);

        //    if (machine != ImageFileMachine.I386) throw new NotSupportedException("64λƽ̨��֧��OLEDB������");

        //    //AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        //}

        //static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        //{
        //    try
        //    {
        //        Assembly asm = null;
        //        if (args.Name.StartsWith("Interop.DAO,")) asm = FileSource.GetAssembly("Interop.DAO.dll");
        //        if (args.Name.StartsWith("Interop.ADODB,")) asm = FileSource.GetAssembly("Interop.ADODB.dll");
        //        if (args.Name.StartsWith("Interop.ADOX,")) asm = FileSource.GetAssembly("Interop.ADOX.dll");

        //        if (asm != null)
        //        {
        //            FileSource.ReleaseFile("Interop.DAO.dll", null, false);
        //            FileSource.ReleaseFile("Interop.ADODB.dll", null, false);
        //            FileSource.ReleaseFile("Interop.ADOX.dll", null, false);

        //            return asm;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        XTrace.WriteLine(ex.ToString());
        //    }

        //    throw new Exception("δ�ܼ��س���" + args.Name);
        //}
        #endregion

        #region ƽ̨���
        private static Boolean? _Supported;
        /// <summary>
        /// �Ƿ�֧��
        /// </summary>
        private static Boolean Supported
        {
            get
            {
                if (_Supported != null) return _Supported.Value;

                Module module = typeof(Object).Module;

                PortableExecutableKinds kind;
                ImageFileMachine machine;
                module.GetPEKind(out kind, out machine);

                if (machine != ImageFileMachine.I386) throw new NotSupportedException("64λƽ̨��֧��OLEDB������");

                _Supported = true;

                return true;
            }
        }
        #endregion
    }

    class Access : Database
    {
        #region ����
        private Access() { }

        public static Access Instance = new Access();
        #endregion

        #region ����
        /// <summary>
        /// �������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other
        /// </summary>
        public override DatabaseType DbType
        {
            get { return DatabaseType.Access; }
        }

        /// <summary>����</summary>
        public override DbProviderFactory Factory
        {
            get { return OleDbFactory.Instance; }
        }
        #endregion

        #region ����
        public override IDbSession CreateSession()
        {
            return new AccessSession();
        }
        #endregion

        #region ���ݿ�����
        /// <summary>
        /// ��ǰʱ�亯��
        /// </summary>
        public override String DateTimeNow { get { return "now()"; } }

        /// <summary>
        /// ��Сʱ��
        /// </summary>
        public override DateTime DateTimeMin { get { return DateTime.MinValue; } }

        /// <summary>
        /// ��ʽ��ʱ��ΪSQL�ַ���
        /// </summary>
        /// <param name="dateTime">ʱ��ֵ</param>
        /// <returns></returns>
        public override String FormatDateTime(DateTime dateTime)
        {
            return String.Format("#{0:yyyy-MM-dd HH:mm:ss}#", dateTime);
        }

        /// <summary>
        /// ��ʽ���ؼ���
        /// </summary>
        /// <param name="keyWord">�ؼ���</param>
        /// <returns></returns>
        public override String FormatKeyWord(String keyWord)
        {
            if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");

            if (keyWord.StartsWith("[") && keyWord.EndsWith("]")) return keyWord;

            return String.Format("[{0}]", keyWord);
            //return keyWord;
        }
        #endregion
    }

    #region OleDb���ӳ�
    ///// <summary>
    ///// Access���ݿ����ӳء�
    ///// ÿ�������ַ���һ�����ӳء�
    ///// һ��ʱ��󣬹ر�δʹ�õĶ������ӣ�
    ///// </summary>
    //internal class AccessPool : IDisposable
    //{
    //    #region ���ӳصĴ���������
    //    /// <summary>
    //    /// �����ַ���
    //    /// </summary>
    //    private String ConnectionString;
    //    /// <summary>
    //    /// ˽�й��캯������ֹ�ⲿ����ʵ����
    //    /// </summary>
    //    /// <param name="connStr">�����ַ���</param>
    //    private AccessPool(String connStr)
    //    {
    //        ConnectionString = connStr;
    //    }

    //    private Boolean Disposed = false;
    //    /// <summary>
    //    /// �ͷ���������
    //    /// </summary>
    //    public void Dispose()
    //    {
    //        if (Disposed) return;
    //        lock (this)
    //        {
    //            if (Disposed) return;
    //            foreach (OleDbConnection conn in FreeList)
    //            {
    //                try
    //                {
    //                    if (conn != null && conn.State != ConnectionState.Closed) conn.Close();
    //                }
    //                catch (Exception ex)
    //                {
    //                    Trace.WriteLine("��AccessPool���ӳ����ͷ���������ʱ����" + ex.ToString());
    //                }
    //            }
    //            FreeList.Clear();
    //            foreach (OleDbConnection conn in UsedList)
    //            {
    //                try
    //                {
    //                    if (conn != null && conn.State != ConnectionState.Closed) conn.Close();
    //                }
    //                catch (Exception ex)
    //                {
    //                    Trace.WriteLine("��AccessPool���ӳ����ͷ���������ʱ����" + ex.ToString());
    //                }
    //            }
    //            UsedList.Clear();
    //            //˫��
    //            if (Pools.ContainsKey(ConnectionString))
    //            {
    //                lock (Pools)
    //                {
    //                    if (Pools.ContainsKey(ConnectionString)) Pools.Remove(ConnectionString);
    //                }
    //            }
    //            Disposed = true;
    //        }
    //    }

    //    ~AccessPool()
    //    {
    //        // ��������ÿ�����ӳض����Dispose��Dispose���ֿ�����������
    //        Dispose();
    //    }
    //    #endregion

    //    #region ��/�� ����
    //    /// <summary>
    //    /// �����б�
    //    /// </summary>
    //    private List<OleDbConnection> FreeList = new List<OleDbConnection>();
    //    /// <summary>
    //    /// ʹ���б�
    //    /// </summary>
    //    private List<OleDbConnection> UsedList = new List<OleDbConnection>();
    //    /// <summary>
    //    /// ���ش�С
    //    /// </summary>
    //    public Int32 MaxPoolSize = 100;
    //    /// <summary>
    //    /// ��С�ش�С
    //    /// </summary>
    //    public Int32 MinPoolSize = 0;

    //    /// <summary>
    //    /// ȡ����
    //    /// </summary>
    //    /// <returns></returns>
    //    private OleDbConnection Open()
    //    {
    //        // ���̳߳�ͻ���������´�����ͬһʱ��ֻ����һ���߳̽���
    //        lock (this)
    //        {
    //            if (UsedList.Count >= MaxPoolSize) throw new XException("���ӳص�����������������ƣ��޷��ṩ����");
    //            OleDbConnection conn;
    //            // �����Ƿ������ӣ����û�У���Ҫ���ϴ���
    //            if (FreeList.Count < 1)
    //            {
    //                Trace.WriteLine("�½�����");
    //                conn = new OleDbConnection(ConnectionString);
    //                conn.Open();
    //                // ֱ�ӽ���ʹ���б�
    //                UsedList.Add(conn);
    //                return conn;
    //            }
    //            // �ӿ����б���ȡ��һ������
    //            conn = FreeList[0];
    //            // ��һ�������뿪�����б�
    //            FreeList.RemoveAt(0);
    //            // �����ӽ���ʹ���б�
    //            UsedList.Add(conn);
    //            // ��������Ƿ��Ѿ��򿪣����û�򿪣����
    //            if (conn.State == ConnectionState.Closed) conn.Open();
    //            return conn;
    //        }
    //    }

    //    /// <summary>
    //    /// ��������
    //    /// </summary>
    //    /// <param name="conn">���Ӷ���</param>
    //    private void Close(OleDbConnection conn)
    //    {
    //        if (conn == null || UsedList == null || UsedList.Count < 1) return;
    //        lock (this)
    //        {
    //            if (UsedList == null || UsedList.Count < 1) return;
    //            // ����ļ�飬ԭ������lock���棬�ڸ߲����Ļ����±����Ǹ������ܵ��쳣�������Ժ�һ��ҪDouble Lock
    //            // Double LockҲ���ǣ����->����->�ټ��->ִ��
    //            // �������Ӷ����Ƿ����Ա����ӳء�����ϢӦ�������ʱ�ھ���ʾ���԰��������߿�����������
    //            if (!UsedList.Contains(conn)) throw new XException("������AccessPool���ӳص����ӣ��������Ա����ӳأ�");
    //            // �뿪ʹ���б�
    //            UsedList.Remove(conn);
    //            // �ص������б�
    //            FreeList.Add(conn);
    //        }
    //    }
    //    #endregion

    //    #region �������
    //    /// <summary>
    //    /// ������ӳء��ر�δʹ�����ӣ���ֹ�򿪹������Ӷ��ֲ��ر�
    //    /// </summary>
    //    /// <returns>�Ƿ�ر������ӣ������߽��Դ�Ϊ�����������Ƿ�ͣ�ö�ʱ��</returns>
    //    private Boolean Check()
    //    {
    //        if (FreeList.Count < 1 || FreeList.Count + UsedList.Count <= MinPoolSize) return false;
    //        lock (this)
    //        {
    //            if (FreeList.Count < 1 || FreeList.Count + UsedList.Count <= MinPoolSize) return false;
    //            Trace.WriteLine("ɾ������");
    //            try
    //            {
    //                // �ر����п������ӣ���������С�ش�С
    //                while (FreeList.Count > 0 && FreeList.Count + UsedList.Count > MinPoolSize)
    //                {
    //                    OleDbConnection conn = FreeList[0];
    //                    FreeList.RemoveAt(0);
    //                    conn.Close();
    //                    conn.Dispose();
    //                }
    //            }
    //            catch (Exception ex)
    //            {
    //                Trace.WriteLine("���AccessPool���ӳ�ʱ����" + ex.ToString());
    //            }
    //            return true;
    //        }
    //    }
    //    #endregion

    //    #region �����ӳ��� ��/�� ����
    //    /// <summary>
    //    /// ���ӳؼ��ϡ������ַ�����Ϊ������ÿ�������ַ�����Ӧһ�����ӳء�
    //    /// </summary>
    //    private static Dictionary<String, AccessPool> Pools = new Dictionary<string, AccessPool>();

    //    /// <summary>
    //    /// �������
    //    /// </summary>
    //    /// <param name="connStr">�����ַ���</param>
    //    /// <returns></returns>
    //    public static OleDbConnection Open(String connStr)
    //    {
    //        if (String.IsNullOrEmpty(connStr)) return null;
    //        // ����Ƿ���������ַ���ΪconnStr�����ӳ�
    //        if (!Pools.ContainsKey(connStr))
    //        {
    //            lock (Pools)
    //            {
    //                if (!Pools.ContainsKey(connStr))
    //                {
    //                    Pools.Add(connStr, new AccessPool(connStr));
    //                    // �����ڿ�ʼ10���ÿ��10����һ�����ӳأ�ɾ��һ����ʹ�õ�����
    //                    CreateAndStartTimer();
    //                }
    //            }
    //        }
    //        return Pools[connStr].Open();
    //    }

    //    /// <summary>
    //    /// �����ӷ������ӳ�
    //    /// </summary>
    //    /// <param name="connStr">�����ַ���</param>
    //    /// <param name="conn">����</param>
    //    public static void Close(String connStr, OleDbConnection conn)
    //    {
    //        if (String.IsNullOrEmpty(connStr)) return;
    //        if (conn == null) return;
    //        if (!Pools.ContainsKey(connStr)) return;
    //        Pools[connStr].Close(conn);
    //    }
    //    #endregion

    //    #region ������ӳ�
    //    /// <summary>
    //    /// ������ӳض�ʱ�������ڶ�ʱ������������
    //    /// </summary>
    //    private static Timer CheckPoolTimer;

    //    /// <summary>
    //    /// ������������ʱ����
    //    /// ʹ�����ߵȴ�ʱ��ķ�ʽ��ʹ���̳߳ؼ�鹤���ڿɿصķ�ʽ�½���
    //    /// ���޵ȴ�ʱ��ʱ����鹤��ֻ��ִ��һ�Ρ�
    //    /// ������һ�μ����ɵ�ʱ����������һ�εĵȴ���
    //    /// </summary>
    //    private static void CreateAndStartTimer()
    //    {
    //        if (CheckPoolTimer == null)
    //            CheckPoolTimer = new Timer(new TimerCallback(CheckPool), null, 10000, Timeout.Infinite);
    //        else
    //            CheckPoolTimer.Change(10000, Timeout.Infinite);
    //    }

    //    /// <summary>
    //    /// ��ʱ������ӳأ�ÿ�μ�鶼ɾ��ÿ�����ӳص�һ����������
    //    /// </summary>
    //    /// <param name="obj"></param>
    //    private static void CheckPool(Object obj)
    //    {
    //        // �Ƿ������ӱ��ر�
    //        Boolean IsClose = false;
    //        if (Pools != null && Pools.Values != null && Pools.Values.Count > 0)
    //        {
    //            foreach (AccessPool pool in Pools.Values)
    //            {
    //                Trace.WriteLine("CheckPool " + Pools.Count.ToString());
    //                if (pool.Check()) IsClose = true;
    //                Trace.WriteLine("CheckPool " + Pools.Count.ToString());
    //            }
    //        }
    //        if (IsClose) CreateAndStartTimer();
    //        //// �������ӳض�û�����ӱ��رգ���ô��ֹͣ��ʱ������ʡ�߳���Դ
    //        //if (!IsClose && CheckPoolTimer != null)
    //        //{
    //        //    lock (CheckPoolTimer)
    //        //    {
    //        //        if (!IsClose && CheckPoolTimer != null)
    //        //        {
    //        //            CheckPoolTimer.Dispose();
    //        //            CheckPoolTimer = null;
    //        //        }
    //        //    }
    //        //}
    //    }
    //    #endregion
    //}
    #endregion

    #region ADOX��װ
    internal class ADOTabe : IDisposable
    {
        #region ADOX����
        private Table _Table;
        /// <summary>��</summary>
        public Table Table
        {
            get
            {
                if (_Table == null) _Table = Cat.Tables[TableName];
                return _Table;
            }
        }

        private String _ConnectionString;
        /// <summary>�����ַ���</summary>
        public String ConnectionString
        {
            get { return _ConnectionString; }
            set { _ConnectionString = value; }
        }

        private String _FileName;
        /// <summary>�ļ���</summary>
        public String FileName
        {
            get { return _FileName; }
            set { _FileName = value; }
        }

        private ConnectionClass _Conn;
        /// <summary>����</summary>
        public ConnectionClass Conn
        {
            get
            {
                if (_Conn == null)
                {
                    _Conn = new ConnectionClass();
                    _Conn.Open(ConnectionString, null, null, 0);
                }
                return _Conn;
            }
        }

        private Catalog _Cat;
        /// <summary></summary>
        public Catalog Cat
        {
            get
            {
                if (_Cat == null)
                {
                    _Cat = new CatalogClass();
                    _Cat.ActiveConnection = Conn;
                }
                return _Cat;
            }
        }
        #endregion

        #region DAO����
        private String _TableName;
        /// <summary>����</summary>
        public String TableName
        {
            get { return _TableName; }
            set { _TableName = value; }
        }

        private TableDef _TableDef;
        /// <summary>����</summary>
        public TableDef TableDef
        {
            get
            {
                if (_TableDef == null) _TableDef = Db.TableDefs[TableName];
                return _TableDef;
            }
        }

        private DBEngineClass _Dbe;
        /// <summary>����</summary>
        public DBEngineClass Dbe
        {
            get
            {
                if (_Dbe == null) _Dbe = new DBEngineClass();
                return _Dbe;
            }
        }

        private DAO.Database _Db;
        /// <summary></summary>
        public DAO.Database Db
        {
            get
            {
                if (_Db == null) _Db = Dbe.OpenDatabase(FileName, null, null, null);
                return _Db;
            }
        }
        #endregion

        #region ��չ����
        private List<ADOColumn> _Columns;
        /// <summary>�ֶμ���</summary>
        public List<ADOColumn> Columns
        {
            get
            {
                if (_Columns == null)
                {
                    Dictionary<String, DAO.Field> dic = new Dictionary<string, DAO.Field>();
                    foreach (DAO.Field item in TableDef.Fields)
                    {
                        dic.Add(item.Name, item);
                    }

                    _Columns = new List<ADOColumn>();
                    foreach (Column item in Table.Columns)
                    {
                        _Columns.Add(new ADOColumn(this, item, dic[item.Name]));
                        //_Columns.Add(new ADOColumn(this, item));
                    }
                }
                return _Columns;
            }
        }

        /// <summary>
        /// �Ƿ�֧��
        /// </summary>
        public Boolean Supported
        {
            get
            {
                try
                {
                    return Conn != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>����</summary>
        public String Description
        {
            get
            {
                DAO.Property p = TableDef.Properties["Description"];
                if (p == null && p.Value == null)
                    return null;
                else
                    return p.Value.ToString();
            }
            set
            {
                DAO.Property p = null;
                try
                {
                    p = TableDef.Properties["Description"];
                }
                catch { }

                if (p != null)
                {
                    p.Value = value;
                }
                else
                {
                    try
                    {
                        p = TableDef.CreateProperty("Description", DAO.DataTypeEnum.dbText, value, false);
                        //Thread.Sleep(1000);
                        TableDef.Properties.Append(p);
                    }
                    catch (Exception ex)
                    {
                        XTrace.WriteLine("��" + Table.Name + "û��Description���ԣ�" + ex.ToString()); ;
#if DEBUG
                        throw new Exception("��" + Table.Name + "û��Description���ԣ�", ex);
#endif
                    }
                }
            }
        }
        #endregion

        #region ����
        public ADOTabe(String connstr, String filename, String tablename)
        {
            ConnectionString = connstr;
            FileName = filename;
            TableName = tablename;
        }

        ~ADOTabe()
        {
            Dispose();
        }

        private Boolean disposed = false;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (_Columns != null && _Columns.Count > 0)
            {
                foreach (ADOColumn item in _Columns)
                {
                    item.Dispose();
                }
            }
            if (_Table != null) Marshal.ReleaseComObject(_Table);
            if (_Cat != null) Marshal.ReleaseComObject(_Cat);
            if (_Conn != null)
            {
                _Conn.Close();
                Marshal.ReleaseComObject(_Conn);
            }

            if (_TableDef != null) Marshal.ReleaseComObject(_TableDef);
            if (_Db != null)
            {
                _Db.Close();
                Marshal.ReleaseComObject(_Db);
            }
            if (_Dbe != null) Marshal.ReleaseComObject(_Dbe);
        }
        #endregion
    }

    internal class ADOColumn : IDisposable
    {
        #region ����
        private Column _Column;
        /// <summary>�ֶ�</summary>
        public Column Column
        {
            get { return _Column; }
            set { _Column = value; }
        }

        private ADOTabe _Table;
        /// <summary>��</summary>
        public ADOTabe Table
        {
            get { return _Table; }
            set { _Table = value; }
        }
        #endregion

        #region DAO����
        private DAO.Field _Field;
        /// <summary>�ֶ�</summary>
        public DAO.Field Field
        {
            get { return _Field; }
            set { _Field = value; }
        }
        #endregion

        #region ��չ����
        /// <summary>
        /// ����
        /// </summary>
        public String Name
        {
            get { return Column.Name; }
            set { Column.Name = value; }
        }

        /// <summary>����</summary>
        public String Description
        {
            get
            {
                ADOX.Property p = Column.Properties["Description"];
                if (p == null && p.Value == null)
                    return null;
                else
                    return p.Value.ToString();
            }
            set
            {
                ADOX.Property p = Column.Properties["Description"];
                if (p != null)
                    p.Value = value;
                else
                    throw new Exception("��" + Column.Name + "û��Description���ԣ�");
            }
        }

        /// <summary>����</summary>
        public String Default
        {
            get
            {
                ADOX.Property p = Column.Properties["Default"];
                if (p == null && p.Value == null)
                    return null;
                else
                    return p.Value.ToString();
            }
            set
            {
                ADOX.Property p = Column.Properties["Default"];
                if (p != null)
                    p.Value = value;
                else
                    throw new Exception("��" + Column.Name + "û��Default���ԣ�");
            }
        }

        /// <summary>
        /// �Ƿ�����
        /// </summary>
        public Boolean AutoIncrement
        {
            get
            {
                ADOX.Property p = Column.Properties["Autoincrement"];
                if (p == null && p.Value == null)
                    return false;
                else
                    return (Boolean)p.Value;
            }
            set
            {
                ADOX.Property p = Column.Properties["Autoincrement"];
                if (p != null)
                    p.Value = value;
                else
                    throw new Exception("��" + Column.Name + "û��Autoincrement���ԣ�");
            }
        }

        /// <summary>
        /// �Ƿ������
        /// </summary>
        public Boolean Nullable
        {
            get
            {
                ADOX.Property p = Column.Properties["Nullable"];
                if (p == null && p.Value == null)
                    return false;
                else
                    return (Boolean)p.Value;
            }
            set
            {
                ADOX.Property p = Column.Properties["Nullable"];
                if (p != null)
                    p.Value = value;
                else
                    throw new Exception("��" + Column.Name + "û��Nullable���ԣ�");
            }
        }
        #endregion

        #region ����
        public ADOColumn(ADOTabe table, Column column, DAO.Field field)
        {
            Table = table;
            Column = column;
            Field = field;
        }

        //public ADOColumn(ADOTabe table, Column column)
        //{
        //    Table = table;
        //    Column = column;
        //}

        ~ADOColumn()
        {
            Dispose();
        }

        private Boolean disposed = false;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (Column != null) Marshal.ReleaseComObject(Column);
        }
        #endregion
    }
    #endregion
}