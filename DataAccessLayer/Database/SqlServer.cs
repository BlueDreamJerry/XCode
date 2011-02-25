using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using XCode.Exceptions;

namespace XCode.DataAccessLayer
{
    class SqlServer : RemoteDb
    {
        #region ����
        /// <summary>
        /// �������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other
        /// </summary>
        public override DatabaseType DbType
        {
            get { return DatabaseType.SqlServer; }
        }

        /// <summary>����</summary>
        public override DbProviderFactory Factory
        {
            get { return SqlClientFactory.Instance; }
        }

        private Boolean? _IsSQL2005;
        /// <summary>�Ƿ�SQL2005������</summary>
        public Boolean IsSQL2005
        {
            get
            {
                if (_IsSQL2005 == null)
                {
                    if (String.IsNullOrEmpty(ConnectionString)) return false;
                    try
                    {
                        //�л���master��
                        DbSession session = CreateSession() as DbSession;
                        String dbname = session.DatabaseName;
                        //���ָ�������ݿ��������Ҳ���master�����л���master
                        if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, SystemDatabaseName, StringComparison.OrdinalIgnoreCase))
                        {
                            session.DatabaseName = SystemDatabaseName;
                        }

                        //ȡ���ݿ�汾
                        if (!session.Opened) session.Open();
                        String ver = session.Conn.ServerVersion;
                        session.AutoClose();

                        _IsSQL2005 = !ver.StartsWith("08");

                        if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, SystemDatabaseName, StringComparison.OrdinalIgnoreCase))
                        {
                            session.DatabaseName = dbname;
                        }
                    }
                    catch { _IsSQL2005 = false; }
                }
                return _IsSQL2005.Value;
            }
            set { _IsSQL2005 = value; }
        }
        #endregion

        #region ����
        /// <summary>
        /// �������ݿ�Ự
        /// </summary>
        /// <returns></returns>
        protected override IDbSession OnCreateSession()
        {
            return new SqlServerSession();
        }

        /// <summary>
        /// ����Ԫ���ݶ���
        /// </summary>
        /// <returns></returns>
        protected override IMetaData OnCreateMetaData()
        {
            return new SqlServerMetaData();
        }
        #endregion

        #region ��ҳ
        /// <summary>
        /// �����ҳSQL
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��ҳSQL</returns>
        public override String PageSplit(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0 && maximumRows < 1) return sql;

            // ָ������ʼ�У�������SQL2005�����ϰ汾��ʹ��RowNumber�㷨
            if (startRowIndex > 0 && IsSQL2005) return PageSplitRowNumber(sql, startRowIndex, maximumRows, keyColumn);

            // ���û��Order By��ֱ�ӵ��û��෽��
            // �����ַ����жϣ������ʸߣ�����������ߴ���Ч��
            if (!sql.Contains(" Order "))
            {
                if (!sql.ToLower().Contains(" order ")) return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            //// ʹ����������ϸ��жϡ��������Order By���������ұ�û��������)��������order by���Ҳ����Ӳ�ѯ�ģ�����Ҫ���⴦��
            //MatchCollection ms = Regex.Matches(sql, @"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            //if (ms == null || ms.Count < 1 || ms[0].Index < 1)
            String sql2 = sql;
            String orderBy = CheckOrderClause(ref sql2);
            if (String.IsNullOrEmpty(orderBy))
            {
                return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            // ��ȷ����sql����㺬��order by���ټ��������Ƿ���top����Ϊû��top��order by�ǲ�������Ϊ�Ӳ�ѯ��
            if (Regex.IsMatch(sql, @"^[^(]+\btop\b", RegexOptions.Compiled | RegexOptions.IgnoreCase))
            {
                return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }
            //String orderBy = sql.Substring(ms[0].Index);

            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0)
            {
                if (maximumRows < 1)
                    return sql;
                else
                    return String.Format("Select Top {0} * From {1} {2}", maximumRows, CheckSimpleSQL(sql2), orderBy);
                //return String.Format("Select Top {0} * From {1} {2}", maximumRows, CheckSimpleSQL(sql.Substring(0, ms[0].Index)), orderBy);
            }

            #region Max/Min��ҳ
            // ���Ҫʹ��max/min��ҳ��������keyColumn������asc����desc
            if (keyColumn.ToLower().EndsWith(" desc") || keyColumn.ToLower().EndsWith(" asc") || keyColumn.ToLower().EndsWith(" unknown"))
            {
                String str = PageSplitMaxMin(sql, startRowIndex, maximumRows, keyColumn);
                if (!String.IsNullOrEmpty(str)) return str;
                keyColumn = keyColumn.Substring(0, keyColumn.IndexOf(" "));
            }
            #endregion

            sql = CheckSimpleSQL(sql2);

            if (String.IsNullOrEmpty(keyColumn)) throw new ArgumentNullException("keyColumn", "�����õ�not in��ҳ�㷨Ҫ��ָ�������У�");

            if (maximumRows < 1)
                sql = String.Format("Select * From {1} Where {2} Not In(Select Top {0} {2} From {1} {3}) {3}", startRowIndex, sql, keyColumn, orderBy);
            else
                sql = String.Format("Select Top {0} * From {1} Where {2} Not In(Select Top {3} {2} From {1} {4}) {4}", maximumRows, sql, keyColumn, startRowIndex, orderBy);
            return sql;
        }

        /// <summary>
        /// ����д����ȡ��ҳ
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <param name="keyColumn">�����С�����not in��ҳ</param>
        /// <returns></returns>
        public String PageSplitRowNumber(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0)
            {
                if (maximumRows < 1)
                    return sql;
                else
                    return base.PageSplit(sql, startRowIndex, maximumRows, keyColumn);
            }

            String orderBy = String.Empty;
            if (sql.ToLower().Contains(" order "))
            {
                // ʹ����������ϸ��жϡ��������Order By���������ұ�û��������)��������order by���Ҳ����Ӳ�ѯ�ģ�����Ҫ���⴦��
                //MatchCollection ms = Regex.Matches(sql, @"\border\s*by\b([^)]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                //if (ms != null && ms.Count > 0 && ms[0].Index > 0)
                String sql2 = sql;
                String orderBy2 = CheckOrderClause(ref sql2);
                if (String.IsNullOrEmpty(orderBy))
                {
                    // ��ȷ����sql����㺬��order by���ټ��������Ƿ���top����Ϊû��top��order by�ǲ�������Ϊ�Ӳ�ѯ��
                    if (!Regex.IsMatch(sql, @"^[^(]+\btop\b", RegexOptions.Compiled | RegexOptions.IgnoreCase))
                    {
                        //orderBy = sql.Substring(ms[0].Index).Trim();
                        //sql = sql.Substring(0, ms[0].Index).Trim();
                        orderBy = orderBy2.Trim();
                        sql = sql2.Trim();
                    }
                }
            }

            if (String.IsNullOrEmpty(orderBy)) orderBy = "Order By " + keyColumn;
            sql = CheckSimpleSQL(sql);

            //row_number()��1��ʼ
            if (maximumRows < 1)
                sql = String.Format("Select * From (Select *, row_number() over({2}) as rowNumber From {1}) XCode_Temp_b Where rowNumber>={0}", startRowIndex + 1, sql, orderBy);
            else
                sql = String.Format("Select * From (Select *, row_number() over({3}) as rowNumber From {1}) XCode_Temp_b Where rowNumber Between {0} And {2}", startRowIndex + 1, sql, startRowIndex + maximumRows, orderBy);

            return sql;
        }
        #endregion

        #region ���ݿ�����
        /// <summary>
        /// ��ǰʱ�亯��
        /// </summary>
        public override String DateTimeNow { get { return "getdate()"; } }

        /// <summary>
        /// ��Сʱ��
        /// </summary>
        public override DateTime DateTimeMin { get { return SqlDateTime.MinValue.Value; } }

        /// <summary>
        /// ���ı�����
        /// </summary>
        public override Int32 LongTextLength { get { return 4000; } }

        /// <summary>
        /// ��ʽ��ʱ��ΪSQL�ַ���
        /// </summary>
        /// <param name="dateTime">ʱ��ֵ</param>
        /// <returns></returns>
        public override String FormatDateTime(DateTime dateTime)
        {
            return "{ts" + String.Format("'{0:yyyy-MM-dd HH:mm:ss}'", dateTime) + "}";
        }

        /// <summary>
        /// ��ʽ���ؼ���
        /// </summary>
        /// <param name="keyWord">�ؼ���</param>
        /// <returns></returns>
        public override String FormatKeyWord(String keyWord)
        {
            //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
            if (String.IsNullOrEmpty(keyWord)) return keyWord;

            if (keyWord.StartsWith("[") && keyWord.EndsWith("]")) return keyWord;

            return String.Format("[{0}]", keyWord);
        }

        /// <summary>ϵͳ���ݿ���</summary>
        public override String SystemDatabaseName { get { return "master"; } }

        public override string FormatValue(XField field, object value)
        {
            TypeCode code = Type.GetTypeCode(field.DataType);
            Boolean isNullable = field.Nullable;

            if (code == TypeCode.String)
            {
                // �������� Hannibal �ڴ���������վʱ���ֲ��������Ϊ���룬�������Nǰ׺
                if (value == null) return isNullable ? "null" : "''";
                if (String.IsNullOrEmpty(value.ToString()) && isNullable) return "null";

                if (field.RawType == "ntext" || field.RawType.StartsWith("nchar") || field.RawType.StartsWith("nvarchar"))
                    return "N'" + value.ToString().Replace("'", "''") + "'";
                else
                    return "'" + value.ToString().Replace("'", "''") + "'";
            }
            else if (field.DataType == typeof(Guid))
            {
                if (value == null) return isNullable ? "null" : "''";

                return String.Format("'{0}'", value);
            }

            return base.FormatValue(field, value);
        }
        #endregion
    }

    /// <summary>
    /// SqlServer���ݿ�
    /// </summary>
    internal class SqlServerSession : RemoteDbSession
    {
        #region ��ѯ
        /// <summary>
        /// ���ٲ�ѯ�����¼��������ƫ��
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public override int QueryCountFast(string tableName)
        {
            String sql = String.Format("select rows from sysindexes where id = object_id('{0}') and indid in (0,1)", tableName);

            QueryTimes++;
            DbCommand cmd = PrepareCommand();
            cmd.CommandText = sql;
            if (Debug) WriteLog(cmd.CommandText);
            try
            {
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally { AutoClose(); }
        }

        /// <summary>
        /// ִ�в�����䲢���������е��Զ����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns>�����е��Զ����</returns>
        public override long InsertAndGetIdentity(string sql)
        {
            //SQLServerд��
            sql = "SET NOCOUNT ON;" + sql + ";Select SCOPE_IDENTITY()";

            return Int64.Parse(ExecuteScalar(sql).ToString());
        }
        #endregion
    }

    /// <summary>
    /// SqlServerԪ����
    /// </summary>
    class SqlServerMetaData : RemoteDbMetaData
    {
        #region ����
        /// <summary>
        /// �Ƿ�SQL2005
        /// </summary>
        public Boolean IsSQL2005 { get { return (Database as SqlServer).IsSQL2005; } }

        /// <summary>
        /// 0������
        /// </summary>
        public String level0type { get { return IsSQL2005 ? "SCHEMA" : "USER"; } }

        ///// <summary>���ݿ���</summary>
        //public String DatabaseName
        //{
        //    get { return Database.CreateSession().DatabaseName; }
        //    set { Database.CreateSession().DatabaseName = value; }
        //}
        #endregion

        #region ����
        /// <summary>
        /// ȡ�����б���
        /// </summary>
        /// <returns></returns>
        public override List<XTable> GetTables()
        {
            try
            {
                IDbSession session = Database.CreateSession();

                //һ���԰����еı�˵�������
                DataSet ds = session.Query(DescriptionSql);
                DataTable DescriptionTable = ds == null || ds.Tables == null || ds.Tables.Count < 1 ? null : ds.Tables[0];

                DataTable dt = GetSchema("Tables", null);
                if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return null;

                AllFields = session.Query(SchemaSql).Tables[0];

                // �г��û���
                DataRow[] rows = dt.Select(String.Format("{0}='BASE TABLE' Or {0}='VIEW'", "TABLE_TYPE"));
                List<XTable> list = GetTables(rows);
                if (list == null || list.Count < 1) return list;

                // ������ע
                foreach (XTable item in list)
                {
                    DataRow[] drs = DescriptionTable == null ? null : DescriptionTable.Select("n='" + item.Name + "'");
                    item.Description = drs == null || drs.Length < 1 ? "" : drs[0][1].ToString();
                }

                return list;
            }
            catch (DbException ex)
            {
                throw new XDbMetaDataException(this, "ȡ�����б��ܳ���", ex);
            }
        }

        private DataTable AllFields = null;

        protected override void FixField(XField field, DataRow dr)
        {
            base.FixField(field, dr);

            DataRow[] rows = AllFields.Select("����='" + field.Table.Name + "' And �ֶ���='" + field.Name + "'", null);
            if (rows != null && rows.Length > 0)
            {
                DataRow dr2 = rows[0];

                field.Identity = GetDataRowValue<Boolean>(dr2, "��ʶ");
                field.PrimaryKey = GetDataRowValue<Boolean>(dr2, "����");
                field.NumOfByte = GetDataRowValue<Int32>(dr2, "ռ���ֽ���");
                field.Description = GetDataRowValue<String>(dr2, "�ֶ�˵��");
            }

            // ����Ĭ��ֵ
            if (!String.IsNullOrEmpty(field.Default))
            {
                //field.Default = field.Default.Trim(new Char[] { '(', ')' });
                field.Default = DbBase.Trim(field.Default, "\"", "\"");
                field.Default = DbBase.Trim(field.Default, "\'", "\'");
                field.Default = DbBase.Trim(field.Default, "(", ")");
            }
        }

        /// <summary>
        /// �����ء���������
        /// </summary>
        protected override DataTable PrimaryKeys
        {
            get
            {
                if (_PrimaryKeys == null)
                {
                    DataTable pks = GetSchema("IndexColumns", new String[] { null, null, null });
                    if (pks == null) return null;

                    //// ȡ����������
                    //DataTable dt = GetSchema("Indexes", new String[] { null, null, null });
                    //// ȡ������ӵ�������ı���
                    //Dictionary<String, Int32> dic = new Dictionary<string, int>();
                    //foreach (DataRow item in dt.Rows)
                    //{
                    //    String name = GetDataRowValue<String>(item, "table_name");
                    //}

                    //DataRow[] drs = dt.Select("type_desc='NONCLUSTERED'");
                    //if (drs != null && drs.Length > 0)
                    //{

                    //}

                    _PrimaryKeys = pks;
                }
                return _PrimaryKeys;
            }
        }

        //protected override string GetFieldType(XField field)
        //{
        //    String typeName = base.GetFieldType(field);

        //    if (field.Identity) typeName += " IDENTITY(1,1)";

        //    return typeName;
        //}

        protected override string GetFieldConstraints(XField field, Boolean onlyDefine)
        {
            String str = base.GetFieldConstraints(field, onlyDefine);

            if (field.Identity) str = " IDENTITY(1,1)" + str;

            return str;
        }
        #endregion

        #region ȡ���ֶ���Ϣ��SQLģ��
        private String _SchemaSql = "";
        /// <summary>
        /// ����SQL
        /// </summary>
        public virtual String SchemaSql
        {
            get
            {
                if (String.IsNullOrEmpty(_SchemaSql))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("SELECT ");
                    sb.Append("����=d.name,");
                    sb.Append("�ֶ����=a.colorder,");
                    sb.Append("�ֶ���=a.name,");
                    sb.Append("��ʶ=case when COLUMNPROPERTY( a.id,a.name,'IsIdentity')=1 then Convert(Bit,1) else Convert(Bit,0) end,");
                    sb.Append("����=case when exists(SELECT 1 FROM sysobjects where xtype='PK' and name in (");
                    sb.Append("SELECT name FROM sysindexes WHERE id = a.id AND indid in(");
                    sb.Append("SELECT indid FROM sysindexkeys WHERE id = a.id AND colid=a.colid");
                    sb.Append("))) then Convert(Bit,1) else Convert(Bit,0) end,");
                    sb.Append("����=b.name,");
                    sb.Append("ռ���ֽ���=a.length,");
                    sb.Append("����=COLUMNPROPERTY(a.id,a.name,'PRECISION'),");
                    sb.Append("С��λ��=isnull(COLUMNPROPERTY(a.id,a.name,'Scale'),0),");
                    sb.Append("�����=case when a.isnullable=1 then Convert(Bit,1)else Convert(Bit,0) end,");
                    sb.Append("Ĭ��ֵ=isnull(e.text,''),");
                    sb.Append("�ֶ�˵��=isnull(g.[value],'')");
                    sb.Append("FROM syscolumns a ");
                    sb.Append("left join systypes b on a.xtype=b.xusertype ");
                    sb.Append("inner join sysobjects d on a.id=d.id  and d.xtype='U' ");
                    sb.Append("left join syscomments e on a.cdefault=e.id ");
                    if (IsSQL2005)
                    {
                        sb.Append("left join sys.extended_properties g on a.id=g.major_id and a.colid=g.minor_id and g.name = 'MS_Description'  ");
                    }
                    else
                    {
                        sb.Append("left join sysproperties g on a.id=g.id and a.colid=g.smallid  ");
                    }
                    sb.Append("order by a.id,a.colorder");
                    _SchemaSql = sb.ToString();
                }
                return _SchemaSql;
            }
        }

        private readonly String _DescriptionSql2000 = "select b.name n, a.value v from sysproperties a inner join sysobjects b on a.id=b.id where a.smallid=0";
        private readonly String _DescriptionSql2005 = "select b.name n, a.value v from sys.extended_properties a inner join sysobjects b on a.major_id=b.id and a.minor_id=0 and a.name = 'MS_Description'";
        /// <summary>
        /// ȡ��˵��SQL
        /// </summary>
        public virtual String DescriptionSql { get { return IsSQL2005 ? _DescriptionSql2005 : _DescriptionSql2000; } }
        #endregion

        #region ���ݶ���
        public override object SetSchema(DDLSchema schema, params object[] values)
        {
            IDbSession session = Database.CreateSession();

            Object obj = null;
            String dbname = String.Empty;
            String databaseName = String.Empty;
            switch (schema)
            {
                case DDLSchema.DropDatabase:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = session.DatabaseName;
                    values = new Object[] { databaseName, values == null || values.Length < 2 ? null : values[1] };

                    dbname = session.DatabaseName;
                    session.DatabaseName = SystemDatabaseName;
                    //obj = base.SetSchema(schema, values);
                    //if (Execute(String.Format("Drop Database [{0}]", dbname)) < 1)
                    //{
                    //    Execute(DropDatabaseSQL(databaseName));
                    //}
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("use master");
                    sb.AppendLine(";");
                    sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
                    sb.AppendLine("declare   #spid   cursor   for");
                    sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", dbname);
                    sb.AppendLine();
                    sb.AppendLine("open   #spid");
                    sb.AppendLine("fetch   next   from   #spid   into   @spid");
                    sb.AppendLine("while   @@fetch_status=0");
                    sb.AppendLine("begin");
                    sb.AppendLine("exec('kill   '+@spid)");
                    sb.AppendLine("fetch   next   from   #spid   into   @spid");
                    sb.AppendLine("end");
                    sb.AppendLine("close   #spid");
                    sb.AppendLine("deallocate   #spid");

                    Int32 count = 0;
                    try { count = session.Execute(sb.ToString()); }
                    catch { }
                    obj = session.Execute(String.Format("Drop Database {0}", FormatKeyWord(dbname))) > 0;
                    //sb.AppendFormat("Drop Database [{0}]", dbname);

                    session.DatabaseName = dbname;
                    return obj;
                default:
                    break;
            }
            return base.SetSchema(schema, values);
        }

        public override string CreateDatabaseSQL(string dbname, string file)
        {
            if (String.IsNullOrEmpty(file)) return String.Format("CREATE DATABASE {0}", FormatKeyWord(dbname));

            String logfile = String.Empty;
            if (!String.IsNullOrEmpty(file))
            {
                if ((file.Length < 2 || file[1] != Path.VolumeSeparatorChar))
                {
                    file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                }
                file = String.Format("FILENAME = N'{0}' , ", file);
                logfile = file.Substring(0, file.Length - 3) + "ldf";
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("CREATE DATABASE {0} ON  PRIMARY", FormatKeyWord(dbname));
            sb.AppendLine();
            sb.AppendFormat(@"( NAME = N'{0}_Data', {1}SIZE = 1024 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, file);
            sb.AppendLine();
            sb.Append("LOG ON ");
            sb.AppendLine();
            sb.AppendFormat(@"( NAME = N'{0}_Log', {1}SIZE = 1024 , MAXSIZE = UNLIMITED, FILEGROWTH = 10%)", dbname, logfile);
            sb.AppendLine();

            return sb.ToString();
        }

        public override string DatabaseExistSQL(string dbname)
        {
            return String.Format("SELECT * FROM sysdatabases WHERE name = N'{0}'", dbname);
        }

        //public override string CreateTableSQL(XTable table)
        //{
        //    List<XField> Fields = new List<XField>(table.Fields);
        //    Fields.Sort(delegate(XField item1, XField item2) { return item1.ID.CompareTo(item2.ID); });

        //    StringBuilder sb = new StringBuilder();

        //    sb.AppendFormat("Create Table {0}(", FormatKeyWord(table.Name));
        //    List<String> keys = new List<string>();
        //    for (Int32 i = 0; i < Fields.Count; i++)
        //    {
        //        sb.AppendLine();
        //        sb.Append("\t");
        //        sb.Append(FieldClause(Fields[i], true));
        //        if (i < Fields.Count - 1) sb.Append(",");

        //        if (Fields[i].PrimaryKey) keys.Add(Fields[i].Name);
        //    }

        //    //����
        //    if (keys.Count > 0)
        //    {
        //        sb.Append(",");
        //        sb.AppendLine();
        //        sb.Append("\t");
        //        sb.AppendFormat("CONSTRAINT [PK_{0}] PRIMARY KEY CLUSTERED", table.Name);
        //        sb.AppendLine();
        //        sb.Append("\t");
        //        sb.Append("(");
        //        for (Int32 i = 0; i < keys.Count; i++)
        //        {
        //            sb.AppendLine();
        //            sb.Append("\t\t");
        //            sb.AppendFormat("{0} ASC", FormatKeyWord(keys[i]));
        //            if (i < keys.Count - 1) sb.Append(",");
        //        }
        //        sb.AppendLine();
        //        sb.Append("\t");
        //        sb.Append(") ON [PRIMARY]");
        //    }

        //    sb.AppendLine();
        //    sb.Append(") ON [PRIMARY]");

        //    ////ע��
        //    //if (!String.IsNullOrEmpty(table.Description))
        //    //{
        //    //    String sql = AddTableDescriptionSQL(table.Name, table.Description);
        //    //    if (!String.IsNullOrEmpty(sql))
        //    //    {
        //    //        sb.AppendLine(";");
        //    //        sb.Append(sql);
        //    //    }
        //    //}
        //    ////�ֶ�ע��
        //    //foreach (XField item in table.Fields)
        //    //{
        //    //    if (!String.IsNullOrEmpty(item.Description))
        //    //    {
        //    //        sb.AppendLine(";");
        //    //        sb.Append(AddColumnDescriptionSQL(table.Name, item.Name, item.Description));
        //    //    }
        //    //}

        //    return sb.ToString();
        //}

        public override string TableExistSQL(XTable table)
        {
            if (IsSQL2005)
                return String.Format("select * from sysobjects where xtype='U' and name='{0}'", table.Name);
            else
                return String.Format("SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'[dbo].{0}') AND OBJECTPROPERTY(id, N'IsUserTable') = 1", FormatKeyWord(table.Name));
        }

        public override string AddTableDescriptionSQL(XTable table)
        {
            return String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", table.Name, table.Description, level0type);
        }

        public override string DropTableDescriptionSQL(XTable table)
        {
            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{1}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", table.Name, level0type);
        }

        public override string AddColumnSQL(XField field)
        {
            String sql = String.Format("Alter Table {0} Add {1}", FormatKeyWord(field.Table.Name), FieldClause(field, true));
            ////if (!String.IsNullOrEmpty(field.Default)) sql += ";" + AddDefaultSQL(tablename, field.Name, field.Description);
            //if (!String.IsNullOrEmpty(field.Description))
            //{
            //    //AddColumnDescriptionSQL�л����DropColumnDescriptionSQL�����ﲻ��Ҫ��
            //    //sql += ";" + Environment.NewLine + DropColumnDescriptionSQL(tablename, field.Name);
            //    sql += ";" + Environment.NewLine + AddColumnDescriptionSQL(tablename, field.Name, field.Description);
            //}
            return sql;
        }

        public override string AlterColumnSQL(XField field)
        {
            String sql = String.Format("Alter Table {0} Alter Column {1}", FormatKeyWord(field.Table.Name), FieldClause(field, false));
            //if (!String.IsNullOrEmpty(field.Default)) sql += ";" + Environment.NewLine + AddDefaultSQL(tablename, field);
            //if (!String.IsNullOrEmpty(field.Description)) sql += ";" + Environment.NewLine + AddColumnDescriptionSQL(tablename, field.Name, field.Description);
            return sql;
        }

        public override string DropColumnSQL(XField field)
        {
            //ɾ��Ĭ��ֵ
            String sql = DeleteConstraintsSQL(field, null);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

            //ɾ������
            String sql2 = DeleteConstraintsSQL(field, "PK");
            if (!String.IsNullOrEmpty(sql2)) sql += sql2 + ";" + Environment.NewLine;

            sql += base.DropColumnSQL(field);
            return sql;
        }

        public override string AddColumnDescriptionSQL(XField field)
        {
            //String sql = DropColumnDescriptionSQL(tablename, columnname);
            //if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;
            String sql = String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{3}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{2}'", field.Table.Name, field.Description, field.Name, level0type);
            return sql;
        }

        public override string DropColumnDescriptionSQL(XField field)
        {
            //String sql = String.Empty;
            //if (!IsSQL2005)
            //    sql = String.Format("select * from syscolumns a inner join sysproperties g on a.id=g.id and a.colid=g.smallid and g.name='MS_Description' inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", tablename, columnname);
            //else
            //    sql = String.Format("select * from syscolumns a inner join sys.extended_properties g on a.id=g.major_id and a.colid=g.minor_id and g.name = 'MS_Description' inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", tablename, columnname);
            //Int32 count = Database.CreateSession().QueryCount(sql);
            //if (count <= 0) return null;

            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{1}'", field.Table.Name, field.Name, level0type);
        }

        public override string AddDefaultSQL(XField field)
        {
            String sql = DropDefaultSQL(field);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;
            if (Type.GetTypeCode(field.DataType) == TypeCode.String)
                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT N'{2}' FOR {1}", field.Table.Name, field.Name, field.Default);
            else if (Type.GetTypeCode(field.DataType) == TypeCode.DateTime)
            {
                //String dv = field.Default;
                //if (!String.IsNullOrEmpty(dv) && dv.Equals("now()", StringComparison.OrdinalIgnoreCase)) dv = "getdate()";
                String dv = CheckAndGetDefaultDateTimeNow(field.Table.DbType, field.Default);

                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", field.Table.Name, field.Name, dv);
            }
            else
                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", field.Table.Name, field.Name, field.Default);
            return sql;
        }

        public override string DropDefaultSQL(XField field)
        {
            return DeleteConstraintsSQL(field, "D");
        }

        /// <summary>
        /// ɾ��Լ���ű���
        /// </summary>
        /// <param name="field"></param>
        /// <param name="type">Լ�����ͣ�Ĭ��ֵ��D�����δָ������ɾ������Լ��</param>
        /// <returns></returns>
        protected virtual String DeleteConstraintsSQL(XField field, String type)
        {
            String sql = null;
            if (type == "PK")
            {
                sql = String.Format("select c.name from sysobjects a inner join syscolumns b on a.id=b.id  inner join sysobjects c on c.parent_obj=a.id where a.name='{0}' and b.name='{1}' and c.xtype='PK'", field.Table.Name, field.Name);
            }
            else
            {
                if (IsSQL2005)
                    sql = String.Format("select b.name from sys.tables a inner join sys.default_constraints b on a.object_id=b.parent_object_id inner join sys.columns c on a.object_id=c.object_id and b.parent_column_id=c.column_id where a.name='{0}' and c.name='{1}'", field.Table.Name, field.Name);
                else
                    sql = String.Format("select b.name from syscolumns a inner join sysobjects b on a.cdefault=b.id inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", field.Table.Name, field.Name);
                if (!String.IsNullOrEmpty(type)) sql += String.Format(" and b.xtype='{0}'", type);
            }
            DataSet ds = Database.CreateSession().Query(sql);
            if (ds == null || ds.Tables == null || ds.Tables[0].Rows.Count < 1) return null;

            StringBuilder sb = new StringBuilder();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                String name = dr[0].ToString();
                if (sb.Length > 0) sb.AppendLine(";");
                sb.AppendFormat("ALTER TABLE {0} DROP CONSTRAINT {1}", FormatKeyWord(field.Table.Name), name);
            }
            return sb.ToString();
        }

        public override String DropDatabaseSQL(String dbname)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("use master");
            sb.AppendLine(";");
            sb.AppendLine("declare   @spid   varchar(20),@dbname   varchar(20)");
            sb.AppendLine("declare   #spid   cursor   for");
            sb.AppendFormat("select   spid=cast(spid   as   varchar(20))   from   master..sysprocesses   where   dbid=db_id('{0}')", dbname);
            sb.AppendLine();
            sb.AppendLine("open   #spid");
            sb.AppendLine("fetch   next   from   #spid   into   @spid");
            sb.AppendLine("while   @@fetch_status=0");
            sb.AppendLine("begin");
            sb.AppendLine("exec('kill   '+@spid)");
            sb.AppendLine("fetch   next   from   #spid   into   @spid");
            sb.AppendLine("end");
            sb.AppendLine("close   #spid");
            sb.AppendLine("deallocate   #spid");
            sb.AppendLine(";");
            sb.AppendFormat("Drop Database {0}", FormatKeyWord(dbname));
            return sb.ToString();
        }
        #endregion
    }
}