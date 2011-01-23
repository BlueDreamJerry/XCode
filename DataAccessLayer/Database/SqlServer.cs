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
    /// <summary>
    /// SqlServer���ݿ�
    /// </summary>
    internal class SqlServerSession : DbSession<SqlServerSession>
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
        #endregion

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
                Int32 rs = Convert.ToInt32(cmd.ExecuteScalar());
                //AutoClose();
                return rs;
            }
            catch (DbException ex)
            {
                throw OnException(ex, cmd.CommandText);
            }
            finally
            {
                AutoClose();
            }
        }
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
                //һ���԰����еı�˵�������
                DataSet ds = Query(DescriptionSql);
                DataTable DescriptionTable = ds == null || ds.Tables == null || ds.Tables.Count < 1 ? null : ds.Tables[0];

                DataTable dt = GetSchema("Tables", null);
                if (dt == null || dt.Rows == null || dt.Rows.Count < 1) return null;

                AllFields = Query(SchemaSql).Tables[0];

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
                throw new XDbSessionException(this, "ȡ�����б��ܳ���", ex);
            }

            //List<XTable> list = null;
            //try
            //{
            //    DataTable dt = GetSchema("Tables", null);

            //    //һ���԰����еı�˵�������
            //    DataSet ds = Query(DescriptionSql);
            //    DataTable DescriptionTable = ds == null || ds.Tables == null || ds.Tables.Count < 1 ? null : ds.Tables[0];

            //    list = new List<XTable>();
            //    if (dt != null && dt.Rows != null && dt.Rows.Count > 0)
            //    {
            //        AllFields = Query(SchemaSql).Tables[0];

            //        foreach (DataRow drTable in dt.Rows)
            //        {
            //            if (drTable["TABLE_NAME"].ToString() != "dtproperties" &&
            //                drTable["TABLE_NAME"].ToString() != "sysconstraints" &&
            //                drTable["TABLE_NAME"].ToString() != "syssegments" &&
            //               (drTable["TABLE_TYPE"].ToString() == "BASE TABLE" || drTable["TABLE_TYPE"].ToString() == "VIEW"))
            //            {
            //                XTable xt = new XTable();
            //                xt.ID = list.Count + 1;
            //                xt.Name = drTable["TABLE_NAME"].ToString();

            //                DataRow[] drs = DescriptionTable == null ? null : DescriptionTable.Select("n='" + xt.Name + "'");
            //                xt.Description = drs == null || drs.Length < 1 ? "" : drs[0][1].ToString();

            //                xt.IsView = drTable["TABLE_TYPE"].ToString() == "VIEW";
            //                xt.DbType = DbType;
            //                xt.Fields = GetFields(xt);

            //                list.Add(xt);
            //            }
            //        }
            //    }
            //}
            //catch (DbException ex)
            //{
            //    throw new XDbException(this, "ȡ�����б��ܳ���", ex);
            //}

            //if (list == null || list.Count < 1) return null;

            //return list;
        }

        private DataTable AllFields = null;

        ///// <summary>
        ///// ȡ��ָ����������й���
        ///// </summary>
        ///// <param name="table"></param>
        ///// <returns></returns>
        //protected override List<XField> GetFields(XTable table)
        //{
        //    if (AllFields == null) return base.GetFields(table);

        //    DataRow[] rows = AllFields.Select("����='" + table.Name + "'", null);
        //    if (rows == null || rows.Length < 1) return base.GetFields(table);

        //    List<XField> list = new List<XField>();
        //    //DataColumnCollection columns = GetColumns(xt.Name);
        //    foreach (DataRow dr in rows)
        //    {
        //        XField field = table.CreateField();
        //        field.ID = Int32.Parse(dr["�ֶ����"].ToString());
        //        field.Name = dr["�ֶ���"].ToString();
        //        field.RawType = dr["����"].ToString();
        //        //xf.DataType = FieldTypeToClassType(dr["����"].ToString());
        //        //field.DataType = FieldTypeToClassType(field);
        //        field.Identity = Boolean.Parse(dr["��ʶ"].ToString());

        //        //if (columns != null && columns.Contains(xf.Name))
        //        //{
        //        //    DataColumn dc = columns[xf.Name];
        //        //    xf.DataType = dc.DataType;
        //        //}

        //        field.PrimaryKey = Boolean.Parse(dr["����"].ToString());

        //        field.Length = Int32.Parse(dr["����"].ToString());
        //        field.NumOfByte = Int32.Parse(dr["ռ���ֽ���"].ToString());
        //        field.Digit = Int32.Parse(dr["С��λ��"].ToString());

        //        field.Nullable = Boolean.Parse(dr["�����"].ToString());
        //        field.Default = dr["Ĭ��ֵ"].ToString();
        //        field.Description = dr["�ֶ�˵��"].ToString();

        //        //����Ĭ��ֵ
        //        while (!String.IsNullOrEmpty(field.Default) && field.Default[0] == '(' && field.Default[field.Default.Length - 1] == ')')
        //        {
        //            field.Default = field.Default.Substring(1, field.Default.Length - 2);
        //        }
        //        if (!String.IsNullOrEmpty(field.Default)) field.Default = field.Default.Trim(new Char[] { '"', '\'' });

        //        list.Add(field);
        //    }

        //    return list;
        //}

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

        #region �ֶ����͵��������Ͷ��ձ�
        //public override Type FieldTypeToClassType(String type)
        //{
        //    switch (type)
        //    {
        //        case "text":
        //        case "uniqueidentifier":
        //        case "ntext":
        //        case "varchar":
        //        case "char":
        //        case "timestamp":
        //        case "nvarchar":
        //        case "nchar":
        //            return typeof(String);
        //        case "bit":
        //            return typeof(Boolean);
        //        case "tinyint":
        //        case "smallint":
        //            return typeof(Int16);
        //        case "int":
        //        case "numeric":
        //            return typeof(Int32);
        //        case "bigint":
        //            return typeof(Int64);
        //        case "decimal":
        //        case "money":
        //        case "smallmoney":
        //            return typeof(Decimal);
        //        case "smallldatetime":
        //        case "datetime":
        //            return typeof(DateTime);
        //        case "real":
        //        case "float":
        //            return typeof(Double);
        //        case "image":
        //        case "sql_variant":
        //        case "varbinary":
        //        case "binary":
        //        case "systemname":
        //            return typeof(Byte[]);
        //        default:
        //            return typeof(String);
        //    }
        //    //if (type.Equals("Int32", StringComparison.OrdinalIgnoreCase)) return "Int32";
        //    //if (type.Equals("varchar", StringComparison.OrdinalIgnoreCase)) return "String";
        //    //if (type.Equals("text", StringComparison.OrdinalIgnoreCase)) return "String";
        //    //if (type.Equals("double", StringComparison.OrdinalIgnoreCase)) return "Double";
        //    //if (type.Equals("datetime", StringComparison.OrdinalIgnoreCase)) return "DateTime";
        //    //if (type.Equals("Int32", StringComparison.OrdinalIgnoreCase)) return "Int32";
        //    //if (type.Equals("Int32", StringComparison.OrdinalIgnoreCase)) return "Int32";
        //    //throw new Exception("Error");
        //}
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
                        //sb.Append("SELECT ");
                        //sb.Append("����=d.name,");
                        //sb.Append("�ֶ����=a.colorder,");
                        //sb.Append("�ֶ���=a.name,");
                        //sb.Append("��ʶ=case when COLUMNPROPERTY( a.id,a.name,'IsIdentity')=1 then Convert(Bit,1) else Convert(Bit,0) end,");
                        //sb.Append("����=case when exists(SELECT 1 FROM sysobjects where xtype='PK' and name in (");
                        //sb.Append("SELECT name FROM sysindexes WHERE id = a.id AND indid in(");
                        //sb.Append("SELECT indid FROM sysindexkeys WHERE id = a.id AND colid=a.colid");
                        //sb.Append("))) then Convert(Bit,1) else Convert(Bit,0) end,");
                        //sb.Append("����=b.name,");
                        //sb.Append("ռ���ֽ���=a.length,");
                        //sb.Append("����=COLUMNPROPERTY(a.id,a.name,'PRECISION'),");
                        //sb.Append("С��λ��=isnull(COLUMNPROPERTY(a.id,a.name,'Scale'),0),");
                        //sb.Append("�����=case when a.isnullable=1 then Convert(Bit,1)else Convert(Bit,0) end,");
                        //sb.Append("Ĭ��ֵ=isnull(e.text,''),");
                        //sb.Append("�ֶ�˵��=isnull(g.[value],'')");
                        //sb.Append("FROM syscolumns a ");
                        //sb.Append("left join systypes b on a.xtype=b.xusertype ");
                        //sb.Append("inner join sysobjects d on a.id=d.id  and d.xtype='U' ");
                        //sb.Append("left join syscomments e on a.cdefault=e.id ");
                        sb.Append("left join sys.extended_properties g on a.id=g.major_id and a.colid=g.minor_id and g.name = 'MS_Description'  ");
                        //sb.Append("order by a.id,a.colorder");
                    }
                    else
                    {
                        //sb.Append("SELECT ");
                        //sb.Append("����=d.name,");
                        //sb.Append("�ֶ����=a.colorder,");
                        //sb.Append("�ֶ���=a.name,");
                        //sb.Append("��ʶ=case when COLUMNPROPERTY( a.id,a.name,'IsIdentity')=1 then Convert(Bit,1) else Convert(Bit,0) end,");
                        //sb.Append("����=case when exists(SELECT 1 FROM sysobjects where xtype='PK' and name in (");
                        //sb.Append("SELECT name FROM sysindexes WHERE id = a.id AND indid in(");
                        //sb.Append("SELECT indid FROM sysindexkeys WHERE id = a.id AND colid=a.colid");
                        //sb.Append("))) then Convert(Bit,1) else Convert(Bit,0) end,");
                        //sb.Append("����=b.name,");
                        //sb.Append("ռ���ֽ���=a.length,");
                        //sb.Append("����=COLUMNPROPERTY(a.id,a.name,'PRECISION'),");
                        //sb.Append("С��λ��=isnull(COLUMNPROPERTY(a.id,a.name,'Scale'),0),");
                        //sb.Append("�����=case when a.isnullable=1 then Convert(Bit,1)else Convert(Bit,0) end,");
                        //sb.Append("Ĭ��ֵ=isnull(e.text,''),");
                        //sb.Append("�ֶ�˵��=isnull(g.[value],'')");
                        //sb.Append("FROM syscolumns a ");
                        //sb.Append("left join systypes b on a.xtype=b.xusertype ");
                        //sb.Append("inner join sysobjects d on a.id=d.id  and d.xtype='U' ");
                        //sb.Append("left join syscomments e on a.cdefault=e.id ");
                        sb.Append("left join sysproperties g on a.id=g.id and a.colid=g.smallid  ");
                        //sb.Append("order by a.id,a.colorder");
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
        //public override string GetSchemaSQL(DDLSchema schema, params object[] values)
        //{
        //    if (schema == DDLSchema.DropDatabase) return DropDatabaseSQL((String)values[0]);

        //    return base.GetSchemaSQL(schema, values);
        //}

        public override object SetSchema(DDLSchema schema, params object[] values)
        {
            Object obj = null;
            String dbname = String.Empty;
            String databaseName = String.Empty;
            switch (schema)
            {
                case DDLSchema.DatabaseExist:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = DatabaseName;
                    values = new Object[] { databaseName };

                    dbname = DatabaseName;

                    //���ָ�������ݿ��������Ҳ���master�����л���master
                    if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
                    {
                        DatabaseName = "master";
                        obj = QueryCount(GetSchemaSQL(schema, values)) > 0;
                        DatabaseName = dbname;
                        return obj;
                    }
                    else
                    {
                        return QueryCount(GetSchemaSQL(schema, values)) > 0;
                    }
                case DDLSchema.TableExist:
                    return QueryCount(GetSchemaSQL(schema, values)) > 0;
                case DDLSchema.CreateDatabase:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = DatabaseName;
                    values = new Object[] { databaseName, values == null || values.Length < 2 ? null : values[1] };

                    dbname = DatabaseName;
                    DatabaseName = "master";
                    obj = base.SetSchema(schema, values);
                    DatabaseName = dbname;
                    return obj;
                case DDLSchema.DropDatabase:
                    databaseName = values == null || values.Length < 1 ? null : (String)values[0];
                    if (String.IsNullOrEmpty(databaseName)) databaseName = DatabaseName;
                    values = new Object[] { databaseName, values == null || values.Length < 2 ? null : values[1] };

                    dbname = DatabaseName;
                    DatabaseName = "master";
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
                    try { count = Execute(sb.ToString()); }
                    catch { }
                    obj = Execute(String.Format("Drop Database {0}", FormatKeyWord(dbname))) > 0;
                    //sb.AppendFormat("Drop Database [{0}]", dbname);

                    DatabaseName = dbname;
                    return obj;
                default:
                    break;
            }
            return base.SetSchema(schema, values);
        }

        /// <summary>
        /// �ֶ�Ƭ��
        /// </summary>
        /// <param name="field"></param>
        /// <param name="onlyDefine">�������塣�����������������������ʹ��Ĭ��ֵ</param>
        /// <returns></returns>
        public override String FieldClause(XField field, Boolean onlyDefine)
        {
            StringBuilder sb = new StringBuilder();

            //�ֶ���
            //sb.AppendFormat("[{0}] ", field.Name);
            sb.AppendFormat("{0} ", FormatKeyWord(field.Name));

            //����
            TypeCode tc = Type.GetTypeCode(field.DataType);
            switch (tc)
            {
                case TypeCode.Boolean:
                    sb.Append("[bit]");
                    break;
                case TypeCode.Byte:
                    sb.Append("[byte]");
                    break;
                case TypeCode.Char:
                    sb.Append("[char]");
                    break;
                case TypeCode.DBNull:
                    break;
                case TypeCode.DateTime:
                    sb.Append("[datetime]");
                    break;
                case TypeCode.Decimal:
                    sb.Append("[money]");
                    if (onlyDefine && field.Identity) sb.Append(" IDENTITY(1,1)");
                    break;
                case TypeCode.Double:
                    sb.Append("[float]");
                    break;
                case TypeCode.Empty:
                    break;
                case TypeCode.Int16:
                case TypeCode.UInt16:
                    sb.Append("[smallint]");
                    if (onlyDefine && field.Identity) sb.Append(" IDENTITY(1,1)");
                    break;
                case TypeCode.Int32:
                case TypeCode.UInt32:
                    sb.Append("[int]");
                    if (onlyDefine && field.Identity) sb.Append(" IDENTITY(1,1)");
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    sb.Append("[bigint]");
                    if (onlyDefine && field.Identity) sb.Append(" IDENTITY(1,1)");
                    break;
                case TypeCode.Object:
                    break;
                case TypeCode.SByte:
                    sb.Append("[byte]");
                    break;
                case TypeCode.Single:
                    sb.Append("[float]");
                    break;
                case TypeCode.String:
                    Int32 len = field.Length;
                    if (len < 1) len = 50;
                    if (len > 4000)
                        sb.Append("[ntext]");
                    else
                        sb.AppendFormat("[nvarchar]({0})", len);
                    break;
                default:
                    break;
            }

            //�Ƿ�Ϊ��
            if (!field.PrimaryKey && !field.Identity)
            {
                if (field.Nullable)
                    sb.Append(" NULL");
                else
                {
                    sb.Append(" NOT NULL");
                }
            }

            //Ĭ��ֵ
            if (onlyDefine && !String.IsNullOrEmpty(field.Default))
            {
                if (tc == TypeCode.String)
                    sb.AppendFormat(" DEFAULT ('{0}')", field.Default);
                else if (tc == TypeCode.DateTime)
                {
                    String d = field.Default;
                    //if (String.Equals(d, "now()", StringComparison.OrdinalIgnoreCase)) d = "getdate()";
                    if (String.Equals(d, "now()", StringComparison.OrdinalIgnoreCase)) d = Database.DateTimeNow;
                    sb.AppendFormat(" DEFAULT {0}", d);
                }
                else
                    sb.AppendFormat(" DEFAULT {0}", field.Default);
            }
            //else if (!onlyDefine && !field.PrimaryKey && !field.Nullable)
            //{
            //    //�ڶ�������У����ֶβ�����գ�����û��Ĭ��ֵʱ������Ĭ��ֵ
            //    if (!includeDefault || String.IsNullOrEmpty(field.Default))
            //    {
            //        if (tc == TypeCode.String)
            //            sb.AppendFormat(" DEFAULT ('{0}')", "");
            //        else if (tc == TypeCode.DateTime)
            //        {
            //            String d = SqlDateTime.MinValue.Value.ToString("yyyy-MM-dd HH:mm:ss");
            //            //d = "1900-01-01";
            //            sb.AppendFormat(" DEFAULT '{0}'", d);
            //        }
            //        else
            //            sb.AppendFormat(" DEFAULT {0}", "''");
            //    }
            //}

            return sb.ToString();
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

        public override string CreateTableSQL(XTable table)
        {
            List<XField> Fields = new List<XField>(table.Fields);
            Fields.Sort(delegate(XField item1, XField item2) { return item1.ID.CompareTo(item2.ID); });

            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("CREATE TABLE {0}(", FormatKeyWord(table.Name));
            List<String> keys = new List<string>();
            for (Int32 i = 0; i < Fields.Count; i++)
            {
                sb.AppendLine();
                sb.Append("\t");
                sb.Append(FieldClause(Fields[i], true));
                if (i < Fields.Count - 1) sb.Append(",");

                if (Fields[i].PrimaryKey) keys.Add(Fields[i].Name);
            }

            //����
            if (keys.Count > 0)
            {
                sb.Append(",");
                sb.AppendLine();
                sb.Append("\t");
                sb.AppendFormat("CONSTRAINT [PK_{0}] PRIMARY KEY CLUSTERED", table.Name);
                sb.AppendLine();
                sb.Append("\t");
                sb.Append("(");
                for (Int32 i = 0; i < keys.Count; i++)
                {
                    sb.AppendLine();
                    sb.Append("\t\t");
                    sb.AppendFormat("{0} ASC", FormatKeyWord(keys[i]));
                    if (i < keys.Count - 1) sb.Append(",");
                }
                sb.AppendLine();
                sb.Append("\t");
                sb.Append(") ON [PRIMARY]");
            }

            sb.AppendLine();
            sb.Append(") ON [PRIMARY]");

            //ע��
            if (!String.IsNullOrEmpty(table.Description))
            {
                String sql = AddTableDescriptionSQL(table.Name, table.Description);
                if (!String.IsNullOrEmpty(sql))
                {
                    sb.AppendLine(";");
                    sb.Append(sql);
                }
            }
            //�ֶ�ע��
            foreach (XField item in table.Fields)
            {
                if (!String.IsNullOrEmpty(item.Description))
                {
                    sb.AppendLine(";");
                    sb.Append(AddColumnDescriptionSQL(table.Name, item.Name, item.Description));
                }
            }

            return sb.ToString();
        }

        public override string TableExistSQL(String tablename)
        {
            if (IsSQL2005)
                return String.Format("select * from sysobjects where xtype='U' and name='{0}'", tablename);
            else
                return String.Format("SELECT * FROM sysobjects WHERE id = OBJECT_ID(N'[dbo].{0}') AND OBJECTPROPERTY(id, N'IsUserTable') = 1", FormatKeyWord(tablename));
        }

        public override string AddTableDescriptionSQL(String tablename, String description)
        {
            return String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", tablename, description, level0type);
        }

        public override string DropTableDescriptionSQL(String tablename)
        {
            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{1}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}'", tablename, level0type);
        }

        public override string AddColumnSQL(string tablename, XField field)
        {
            String sql = String.Format("Alter TABLE {0} Add {1}", FormatKeyWord(tablename), FieldClause(field, true));
            //if (!String.IsNullOrEmpty(field.Default)) sql += ";" + AddDefaultSQL(tablename, field.Name, field.Description);
            if (!String.IsNullOrEmpty(field.Description))
            {
                //AddColumnDescriptionSQL�л����DropColumnDescriptionSQL�����ﲻ��Ҫ��
                //sql += ";" + Environment.NewLine + DropColumnDescriptionSQL(tablename, field.Name);
                sql += ";" + Environment.NewLine + AddColumnDescriptionSQL(tablename, field.Name, field.Description);
            }
            return sql;
        }

        public override string AlterColumnSQL(string tablename, XField field)
        {
            String sql = String.Format("Alter Table {0} Alter Column {1}", FormatKeyWord(tablename), FieldClause(field, false));
            if (!String.IsNullOrEmpty(field.Default)) sql += ";" + Environment.NewLine + AddDefaultSQL(tablename, field);
            if (!String.IsNullOrEmpty(field.Description)) sql += ";" + Environment.NewLine + AddColumnDescriptionSQL(tablename, field.Name, field.Description);
            return sql;
        }

        public override string DropColumnSQL(string tablename, string columnname)
        {
            //ɾ��Ĭ��ֵ
            String sql = DeleteConstraintsSQL(tablename, columnname, null);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;

            //ɾ������
            String sql2 = DeleteConstraintsSQL(tablename, columnname, "PK");
            if (!String.IsNullOrEmpty(sql2)) sql += sql2 + ";" + Environment.NewLine;

            sql += base.DropColumnSQL(tablename, columnname);
            return sql;
        }

        public override string AddColumnDescriptionSQL(String tablename, String columnname, String description)
        {
            String sql = DropColumnDescriptionSQL(tablename, columnname);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;
            sql += String.Format("EXEC dbo.sp_addextendedproperty @name=N'MS_Description', @value=N'{1}' , @level0type=N'{3}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{2}'", tablename, description, columnname, level0type);
            return sql;
        }

        public override string DropColumnDescriptionSQL(String tablename, String columnname)
        {
            //StringBuilder sb = new StringBuilder();
            //sb.Append("IF EXISTS (");
            //sb.AppendFormat("select * from syscolumns a inner join sysproperties g on a.id=g.id and a.colid=g.smallid and g.name='MS_Description' inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", tablename, columnname);
            //sb.AppendLine(")");
            //sb.AppendLine("BEGIN");
            //sb.AppendFormat("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'USER',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{1}'", tablename, columnname);
            //sb.AppendLine();
            //sb.Append("END");
            //return sb.ToString();

            String sql = String.Format("select * from syscolumns a inner join sysproperties g on a.id=g.id and a.colid=g.smallid and g.name='MS_Description' inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", tablename, columnname);
            Int32 count = QueryCount(sql);
            if (count <= 0) return null;

            return String.Format("EXEC dbo.sp_dropextendedproperty @name=N'MS_Description', @level0type=N'{2}',@level0name=N'dbo', @level1type=N'TABLE',@level1name=N'{0}', @level2type=N'COLUMN',@level2name=N'{1}'", tablename, columnname, level0type);
        }

        public override string AddDefaultSQL(string tablename, XField field)
        {
            String sql = DropDefaultSQL(tablename, field.Name);
            if (!String.IsNullOrEmpty(sql)) sql += ";" + Environment.NewLine;
            if (Type.GetTypeCode(field.DataType) == TypeCode.String)
                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT N'{2}' FOR {1}", tablename, field.Name, field.Default);
            else if (Type.GetTypeCode(field.DataType) == TypeCode.DateTime)
            {
                String dv = field.Default;
                if (!String.IsNullOrEmpty(dv) && dv.Equals("now()", StringComparison.OrdinalIgnoreCase)) dv = "getdate()";
                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", tablename, field.Name, dv);
            }
            else
                sql += String.Format("ALTER TABLE {0} ADD CONSTRAINT DF_{0}_{1} DEFAULT {2} FOR {1}", tablename, field.Name, field.Default);
            return sql;
        }

        public override string DropDefaultSQL(string tablename, string columnname)
        {
            return DeleteConstraintsSQL(tablename, columnname, "D");
        }

        /// <summary>
        /// ɾ��Լ���ű���
        /// </summary>
        /// <param name="tablename"></param>
        /// <param name="columnname"></param>
        /// <param name="type">Լ�����ͣ�Ĭ��ֵ��D�����δָ������ɾ������Լ��</param>
        /// <returns></returns>
        protected virtual String DeleteConstraintsSQL(String tablename, String columnname, String type)
        {
            String sql = null;
            if (IsSQL2005)
                sql = String.Format("select b.name from sys.tables a inner join sys.default_constraints b on a.object_id=b.parent_object_id inner join sys.columns c on a.object_id=c.object_id and b.parent_column_id=c.column_id where a.name='{0}' and c.name='{1}'", tablename, columnname);
            else
                sql = String.Format("select b.name from syscolumns a inner join sysobjects b on a.cdefault=b.id inner join sysobjects c on a.id=c.id where a.name='{1}' and c.name='{0}'", tablename, columnname);
            if (!String.IsNullOrEmpty(type)) sql += String.Format(" and b.xtype='{0}'", type);
            if (type == "PK") sql = String.Format("select c.name from sysobjects a inner join syscolumns b on a.id=b.id  inner join sysobjects c on c.parent_obj=a.id where a.name='{0}' and b.name='{1}' and c.xtype='PK'", tablename, columnname);
            DataSet ds = Query(sql);
            if (ds == null || ds.Tables == null || ds.Tables[0].Rows.Count < 1) return null;

            StringBuilder sb = new StringBuilder();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                String name = dr[0].ToString();
                if (sb.Length > 0) sb.AppendLine(";");
                sb.AppendFormat("ALTER TABLE {0} DROP CONSTRAINT {1}", FormatKeyWord(tablename), name);
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
        #endregion
    }

    class SqlServer : DbBase<SqlServer, SqlServerSession>
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
                    //�л���master��
                    DbSession session = CreateSession() as DbSession;
                    String dbname = session.DatabaseName;
                    //���ָ�������ݿ��������Ҳ���master�����л���master
                    if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
                    {
                        session.DatabaseName = "master";
                    }

                    //ȡ���ݿ�汾
                    if (!session.Opened) session.Open();
                    String ver = session.Conn.ServerVersion;
                    session.AutoClose();

                    _IsSQL2005 = !ver.StartsWith("08");

                    if (!String.IsNullOrEmpty(dbname) && !String.Equals(dbname, "master", StringComparison.OrdinalIgnoreCase))
                    {
                        session.DatabaseName = dbname;
                    }
                }
                return _IsSQL2005.Value;
            }
            set { _IsSQL2005 = value; }
        }
        #endregion

        #region ��ҳ
        /// <summary>
        /// �����ҳSQL
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
        /// <param name="keyColumn">Ψһ��������not in��ҳ</param>
        /// <returns>��ҳSQL</returns>
        public override String PageSplit(string sql, Int32 startRowIndex, Int32 maximumRows, string keyColumn)
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
        /// <param name="startRowIndex">��ʼ�У�0��ʼ</param>
        /// <param name="maximumRows">��󷵻�����</param>
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
                sql = String.Format("Select * From (Select row_number() over({2}) as row_number, * From {1}) XCode_Temp_b Where row_Number>={0}", startRowIndex + 1, sql, orderBy);
            else
                sql = String.Format("Select * From (Select row_number() over({3}) as row_number, * From {1}) XCode_Temp_b Where row_Number Between {0} And {2}", startRowIndex + 1, sql, startRowIndex + maximumRows, orderBy);

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
        /// ��ʽ��ʱ��ΪSQL�ַ���
        /// </summary>
        /// <param name="dateTime">ʱ��ֵ</param>
        /// <returns></returns>
        public override String FormatDateTime(DateTime dateTime)
        {
            return String.Format("'{0:yyyy-MM-dd HH:mm:ss}'", dateTime);
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
        }
        #endregion
    }
}