using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Reflection;
using System.Runtime.InteropServices;
using ADODB;
using ADOX;
using DAO;
using NewLife;
using NewLife.Log;
using XCode.Common;

namespace XCode.DataAccessLayer
{
    class Access : FileDbBase
    {
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
        /// <summary>
        /// �������ݿ�Ự
        /// </summary>
        /// <returns></returns>
        protected override IDbSession OnCreateSession()
        {
            return new AccessSession();
        }

        /// <summary>
        /// ����Ԫ���ݶ���
        /// </summary>
        /// <returns></returns>
        protected override IMetaData OnCreateMetaData()
        {
            return new AccessMetaData();
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
        /// ���ı�����
        /// </summary>
        public override Int32 LongTextLength { get { return 255; } }

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

        #region ƽ̨���
        /// <summary>
        /// �Ƿ�֧��
        /// </summary>
        public static void CheckSupport()
        {
            Module module = typeof(Object).Module;

            PortableExecutableKinds kind;
            ImageFileMachine machine;
            module.GetPEKind(out kind, out machine);

            if (machine != ImageFileMachine.I386) throw new NotSupportedException("64λƽ̨��֧��OLEDB������");
        }
        #endregion
    }

    /// <summary>
    /// Access���ݿ�
    /// </summary>
    internal class AccessSession : FileDbSession
    {
        #region ����ʹ�����ӳ�
        /// <summary>
        /// �򿪡�����д��Ϊ�˽������ݿ�
        /// </summary>
        public override void Open()
        {
            Access.CheckSupport();

            base.Open();
        }
        #endregion

        #region �������� ��ѯ/ִ��
        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns>�����е��Զ����</returns>
        public override Int64 InsertAndGetIdentity(String sql)
        {
            ExecuteTimes++;
            if (Debug) WriteLog(sql);
            try
            {
                DbCommand cmd = PrepareCommand();
                cmd.CommandText = sql;
                Int64 rs = cmd.ExecuteNonQuery();
                if (rs > 0)
                {
                    cmd.CommandText = "Select @@Identity";
                    rs = Int64.Parse(cmd.ExecuteScalar().ToString());
                }
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, sql);
            }
            finally
            {
                AutoClose();
            }
        }
        #endregion
    }

    /// <summary>
    /// AccessԪ����
    /// </summary>
    class AccessMetaData : FileDbMetaData
    {
        #region ����
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
                using (ADOTabe table = GetTable(xt.Name))
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
                DataRow[] drs = FindDataType(field, "" + id, isLong);
                if (drs != null && drs.Length > 0)
                {
                    String typeName = GetDataRowValue<String>(drs[0], "TypeName");
                    field.RawType = typeName;

                    if (TryGetDataRowValue<String>(drs[0], "DataType", out typeName)) field.DataType = Type.GetType(typeName);

                    // ������ע����
                    if (field.DataType == typeof(String) && drs.Length > 1)
                    {
                        isLong = (flag & 0x80) == 0x80;
                        drs = FindDataType(field, "" + id, isLong);
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

        protected override string GetFieldConstraints(XField field, Boolean onlyDefine)
        {
            String str = base.GetFieldConstraints(field, onlyDefine);

            if (field.Identity) str = " AUTOINCREMENT(1,1)" + str;

            return str;
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
            //Object obj = null;
            switch (schema)
            {
                //case DDLSchema.CreateDatabase:
                //    CreateDatabase();
                //    return null;
                //case DDLSchema.DropDatabase:
                //    return null;
                //case DDLSchema.DatabaseExist:
                //    return File.Exists(FileName);
                //case DDLSchema.CreateTable:
                //    obj = base.SetSchema(DDLSchema.CreateTable, values);
                //    XTable table = values[0] as XTable;
                //    if (!String.IsNullOrEmpty(table.Description)) AddTableDescription(table.Name, table.Description);
                //    foreach (XField item in table.Fields)
                //    {
                //        if (!String.IsNullOrEmpty(item.Description)) AddColumnDescription(table.Name, item.Name, item.Description);
                //    }
                //    return obj;
                //case DDLSchema.DropTable:
                //    break;
                //case DDLSchema.TableExist:
                //    DataTable dt = GetSchema("Tables", new String[] { null, null, (String)values[0], "TABLE" });
                //    if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return false;
                //    return true;
                case DDLSchema.AddTableDescription:
                    return AddTableDescription((String)values[0], (String)values[1]);
                case DDLSchema.DropTableDescription:
                    return DropTableDescription((String)values[0]);
                //case DDLSchema.AddColumn:
                //    obj = base.SetSchema(DDLSchema.AddColumn, values);
                //    AddColumnDescription((String)values[0], ((XField)values[1]).Name, ((XField)values[1]).Description);
                //    return obj;
                //case DDLSchema.AlterColumn:
                //    break;
                //case DDLSchema.DropColumn:
                //    break;
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
        #endregion

        #region �������ݿ�
        /// <summary>
        /// �������ݿ�
        /// </summary>
        protected override void CreateDatabase()
        {
            FileSource.ReleaseFile("Database.mdb", FileName, true);
        }
        #endregion

        #region ����ֶα�ע
        public Boolean AddTableDescription(String tablename, String description)
        {
            try
            {
                using (ADOTabe table = GetTable(tablename))
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
                using (ADOTabe table = GetTable(tablename))
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
                using (ADOTabe table = GetTable(tablename))
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

        #region ��������
        //DataRow[] FindDataType(Int32 typeID, Boolean? isLong)
        //{
        //    DataTable dt = DataTypes;
        //    if (dt == null) return null;

        //    DataRow[] drs = null;
        //    if (isLong == null)
        //    {
        //        drs = dt.Select(String.Format("NativeDataType={0}", typeID));
        //        if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0}", typeID));
        //    }
        //    else
        //    {
        //        drs = dt.Select(String.Format("NativeDataType={0} And IsLong={1}", typeID, isLong.Value));
        //        if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0} And IsLong={1}", typeID, isLong.Value));
        //    }
        //    return drs;
        //}

        protected override DataRow[] FindDataType(XField field, string typeName, bool? isLong)
        {
            DataRow[] drs = base.FindDataType(field, typeName, isLong);
            if (drs != null && drs.Length > 0) return drs;

            DataTable dt = DataTypes;
            if (dt == null) return null;

            if (isLong == null)
            {
                drs = dt.Select(String.Format("NativeDataType={0}", typeName));
                if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0}", typeName));
            }
            else
            {
                drs = dt.Select(String.Format("NativeDataType={0} And IsLong={1}", typeName, isLong.Value));
                if (drs == null || drs.Length < 1) drs = dt.Select(String.Format("ProviderDbType={0} And IsLong={1}", typeName, isLong.Value));
            }

            return drs;
        }

        protected override void SetFieldType(XField field, string typeName)
        {
            DataTable dt = DataTypes;
            if (dt == null) return;

            DataRow[] drs = FindDataType(field, typeName, null);
            if (drs == null || drs.Length < 1) return;

            // ����ԭʼ����
            if (TryGetDataRowValue<String>(drs[0], "TypeName", out typeName)) field.RawType = typeName;

            base.SetFieldType(field, typeName);
        }
        #endregion

        #region ��������
        ADOTabe GetTable(String tableName)
        {
            return new ADOTabe(Database.ConnectionString, FileName, tableName);
        }
        #endregion
    }

    #region ADOX��װ
    internal class ADOTabe : DisposeBase
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

        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

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

    internal class ADOColumn : DisposeBase
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

        protected override void OnDispose(bool disposing)
        {
            base.OnDispose(disposing);

            if (Column != null) Marshal.ReleaseComObject(Column);
        }
        #endregion
    }
    #endregion
}