using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using XCode.Exceptions;
using System.IO;

namespace XCode.DataAccessLayer
{
    class Oracle : RemoteDb
    {
        #region ����
        /// <summary>
        /// �������ݿ����͡��ⲿDAL���ݿ�����ʹ��Other
        /// </summary>
        public override DatabaseType DbType
        {
            get { return DatabaseType.Oracle; }
        }

        private static DbProviderFactory _dbProviderFactory;
        /// <summary>
        /// �ṩ�߹���
        /// </summary>
        static DbProviderFactory dbProviderFactory
        {
            get
            {
                // ���ȳ���ʹ��Oracle.DataAccess
                if (_dbProviderFactory == null)
                {
                    try
                    {
                        _dbProviderFactory = GetProviderFactory("Oracle.DataAccess.dll", "Oracle.DataAccess.Client.OracleClientFactory");
                    }
                    catch (FileNotFoundException) { }
                    catch (Exception ex)
                    {
                        if (Debug) WriteLog(ex.ToString());
                    }
                }

                // �������ַ�ʽ�����Լ��أ�ǰ����ֻ��Ϊ�˼��ٶԳ��򼯵����ã��ڶ�����Ϊ�˱����һ����û��ע��
                if (_dbProviderFactory == null) _dbProviderFactory = DbProviderFactories.GetFactory("System.Data.OracleClient");
                if (_dbProviderFactory == null) _dbProviderFactory = GetProviderFactory("System.Data.OracleClient.dll", "System.Data.OracleClient.OracleClientFactory");
                //if (_dbProviderFactory == null) _dbProviderFactory = OracleClientFactory.Instance;

                return _dbProviderFactory;
            }
        }

        /// <summary>����</summary>
        public override DbProviderFactory Factory
        {
            //get { return OracleClientFactory.Instance; }
            get { return dbProviderFactory; }
        }

        private String _UserID;
        /// <summary>
        /// �û���UserID
        /// </summary>
        public String UserID
        {
            get
            {
                if (_UserID != null) return _UserID;
                _UserID = String.Empty;

                String connStr = ConnectionString;
                if (String.IsNullOrEmpty(connStr)) return null;

                DbConnectionStringBuilder ocsb = Factory.CreateConnectionStringBuilder();
                ocsb.ConnectionString = connStr;

                if (ocsb.ContainsKey("User ID")) _UserID = (String)ocsb["User ID"];

                return _UserID;
            }
        }

        /// <summary>ӵ����</summary>
        public override String Owner
        {
            get
            {
                // ����null��Empty���������ж��Ƿ��Ѽ���
                if (base.Owner == null)
                {
                    base.Owner = UserID;
                    if (String.IsNullOrEmpty(base.Owner)) base.Owner = String.Empty;
                }
                return base.Owner;
            }
            set { base.Owner = value; }
        }
        #endregion

        #region ����
        /// <summary>
        /// �������ݿ�Ự
        /// </summary>
        /// <returns></returns>
        protected override IDbSession OnCreateSession()
        {
            return new OracleSession();
        }

        /// <summary>
        /// ����Ԫ���ݶ���
        /// </summary>
        /// <returns></returns>
        protected override IMetaData OnCreateMetaData()
        {
            return new OracleMeta();
        }
        #endregion

        #region ��ҳ
        /// <summary>
        /// ����д����ȡ��ҳ
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <param name="keyColumn">�����С�����not in��ҳ</param>
        /// <returns></returns>
        public override String PageSplit(String sql, Int32 startRowIndex, Int32 maximumRows, String keyColumn)
        {
            // return base.Query(sql, startRowIndex, maximumRows, key);
            // �ӵ�һ�п�ʼ������Ҫ��ҳ
            if (startRowIndex <= 0)
            {
                if (maximumRows < 1)
                    return sql;
                else
                    return String.Format("Select * From ({1}) XCode_Temp_a Where rownum<={0}", maximumRows + 1, sql);
            }
            if (maximumRows < 1)
                sql = String.Format("Select * From ({1}) XCode_Temp_a Where rownum>={0}", startRowIndex + 1, sql);
            else
                sql = String.Format("Select * From (Select XCode_Temp_a.*, rownum as rowNumber From ({1}) XCode_Temp_a Where rownum<={2}) XCode_Temp_b Where rowNumber>={0}", startRowIndex + 1, sql, startRowIndex + maximumRows);
            //sql = String.Format("Select * From ({1}) a Where rownum>={0} and rownum<={2}", startRowIndex, sql, startRowIndex + maximumRows - 1);
            return sql;
        }

        public override string PageSplit(SelectBuilder builder, int startRowIndex, int maximumRows, string keyColumn)
        {
            return PageSplit(builder.ToString(), startRowIndex, maximumRows, keyColumn);
        }
        #endregion

        #region ���ݿ�����
        /// <summary>
        /// ��ǰʱ�亯��
        /// </summary>
        public override string DateTimeNow { get { return "sysdate"; } }

        /// <summary>
        /// �����ء���ʽ��ʱ��
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public override string FormatDateTime(DateTime dateTime)
        {
            return String.Format("To_Date('{0}', 'YYYY-MM-DD HH24:MI:SS')", dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        public override string FormatValue(XField field, object value)
        {
            TypeCode code = Type.GetTypeCode(field.DataType);
            Boolean isNullable = field.Nullable;

            if (code == TypeCode.String)
            {
                // �������� Hannibal �ڴ���������վʱ���ֲ��������Ϊ���룬�������Nǰ׺
                if (value == null) return isNullable ? "null" : "''";
                if (String.IsNullOrEmpty(value.ToString()) && isNullable) return "null";

                if (field.RawType == "NCLOB" || field.RawType.StartsWith("NCHAR") || field.RawType.StartsWith("NVARCHAR2"))
                    return "N'" + value.ToString().Replace("'", "''") + "'";
                else
                    return "'" + value.ToString().Replace("'", "''") + "'";
            }

            return base.FormatValue(field, value);
        }
        #endregion

        #region �ؼ���
        protected override string ReservedWordsStr
        {
            get { return "ALL,ALTER,AND,ANY,AS,ASC,BETWEEN,BY,CHAR,CHECK,CLUSTER,COMPRESS,CONNECT,CREATE,DATE,DECIMAL,DEFAULT,DELETE,DESC,DISTINCT,DROP,ELSE,EXCLUSIVE,EXISTS,FLOAT,FOR,FROM,GRANT,GROUP,HAVING,IDENTIFIED,IN,INDEX,INSERT,INTEGER,INTERSECT,INTO,IS,LIKE,LOCK,LONG,MINUS,MODE,NOCOMPRESS,NOT,NOWAIT,NULL,NUMBER,OF,ON,OPTION,OR,ORDER,PCTFREE,PRIOR,PUBLIC,RAW,RENAME,RESOURCE,REVOKE,SELECT,SET,SHARE,SIZE,SMALLINT,START,SYNONYM,TABLE,THEN,TO,TRIGGER,UNION,UNIQUE,UPDATE,VALUES,VARCHAR,VARCHAR2,VIEW,WHERE,WITH"; }
        }

        /// <summary>
        /// ��ʽ���ؼ���
        /// </summary>
        /// <param name="keyWord">����</param>
        /// <returns></returns>
        public override String FormatKeyWord(String keyWord)
        {
            //return String.Format("\"{0}\"", keyWord);

            //if (String.IsNullOrEmpty(keyWord)) throw new ArgumentNullException("keyWord");
            if (String.IsNullOrEmpty(keyWord)) return keyWord;

            Int32 pos = keyWord.LastIndexOf(".");

            if (pos < 0) return "\"" + keyWord + "\"";

            String tn = keyWord.Substring(pos + 1);
            if (tn.StartsWith("\"")) return keyWord;

            return keyWord.Substring(0, pos + 1) + "\"" + tn + "\"";
        }
        #endregion
    }

    /// <summary>
    /// Oracle���ݿ�
    /// </summary>
    internal class OracleSession : RemoteDbSession
    {
        #region �������� ��ѯ/ִ��
        /// <summary>
        /// ���ٲ�ѯ�����¼��������ƫ��
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public override int QueryCountFast(string tableName)
        {
            String sql = String.Format("select NUM_ROWS from sys.all_indexes where TABLE_OWNER='{0}' and TABLE_NAME='{1}'", (Database as Oracle).Owner.ToUpper(), tableName);

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

        ///// <summary>
        ///// ִ�в�����䲢���������е��Զ����
        ///// </summary>
        ///// <param name="sql">SQL���</param>
        ///// <returns>�����е��Զ����</returns>
        //public override Int64 InsertAndGetIdentity(String sql)
        //{
        //    throw new NotSupportedException("Oracle���ݿⲻ֧�ֲ���󷵻������е��Զ���ţ�");
        //}
        #endregion

        ///// <summary>
        ///// ȡ��ָ����������й���
        ///// </summary>
        ///// <param name="table"></param>
        ///// <returns></returns>
        //protected override List<XField> GetFields(XTable table)
        //{
        //    //DataColumnCollection columns = GetColumns(xt.Name);
        //    DataTable dt = GetSchema("Columns", new String[] { table.Owner, table.Name });

        //    List<XField> list = new List<XField>();
        //    DataRow[] drs = dt.Select("", "ID");
        //    List<String> pks = GetPrimaryKeys(table);
        //    foreach (DataRow dr in drs)
        //    {
        //        XField field = table.CreateField();
        //        field.ID = Int32.Parse(dr["ID"].ToString());
        //        field.Name = dr["COLUMN_NAME"].ToString();
        //        field.RawType = dr["DATA_TYPE"].ToString();
        //        //xf.DataType = FieldTypeToClassType(dr["DATATYPE"].ToString());
        //        //field.DataType = FieldTypeToClassType(field);
        //        field.Identity = false;

        //        //if (columns != null && columns.Contains(xf.Name))
        //        //{
        //        //    DataColumn dc = columns[xf.Name];
        //        //    xf.DataType = dc.DataType;
        //        //}

        //        field.Length = dr["LENGTH"] == DBNull.Value ? 0 : Int32.Parse(dr["LENGTH"].ToString());
        //        field.Digit = dr["SCALE"] == DBNull.Value ? 0 : Int32.Parse(dr["SCALE"].ToString());

        //        field.PrimaryKey = pks != null && pks.Contains(field.Name);

        //        if (Type.GetTypeCode(field.DataType) == TypeCode.Int32 && field.Digit > 0)
        //        {
        //            field.DataType = typeof(Double);
        //        }
        //        else if (Type.GetTypeCode(field.DataType) == TypeCode.DateTime)
        //        {
        //            //xf.Length = dr["DATETIME_PRECISION"] == DBNull.Value ? 0 : Int32.Parse(dr["DATETIME_PRECISION"].ToString());
        //            field.NumOfByte = 0;
        //            field.Digit = 0;
        //        }
        //        else
        //        {
        //            //if (dr["DATA_TYPE"].ToString() == "130" && dr["COLUMN_FLAGS"].ToString() == "234") //��ע����
        //            //{
        //            //    xf.Length = Int32.MaxValue;
        //            //    xf.NumOfByte = Int32.MaxValue;
        //            //}
        //            //else
        //            {
        //                field.Length = dr["LENGTH"] == DBNull.Value ? 0 : Int32.Parse(dr["LENGTH"].ToString());
        //                field.NumOfByte = 0;
        //            }
        //            field.Digit = 0;
        //        }

        //        try
        //        {
        //            field.Nullable = Boolean.Parse(dr["NULLABLE"].ToString());
        //        }
        //        catch
        //        {
        //            field.Nullable = dr["NULLABLE"].ToString() == "Y";
        //        }

        //        list.Add(field);
        //    }

        //    return list;
        //}

        #region �ֶ����͵��������Ͷ��ձ�
        ///// <summary>
        ///// �ֶ����͵��������Ͷ��ձ�
        ///// </summary>
        ///// <param name="type"></param>
        ///// <returns></returns>
        //public override Type FieldTypeToClassType(String type)
        //{
        //    switch (type)
        //    {
        //        case "CHAR":
        //        case "VARCHAR2":
        //        case "NCHAR":
        //        case "NVARCHAR2":
        //        case "CLOB":
        //        case "NCLOB":
        //            return typeof(String);
        //        case "NUMBER":
        //            return typeof(Int32);
        //        case "FLOAT":
        //            return typeof(Double);
        //        case "DATE":
        //        case "TIMESTAMP":
        //        case "TIMESTAMP(6)":
        //            return typeof(DateTime);
        //        case "LONG":
        //        case "LOB":
        //        case "RAW":
        //        case "BLOB":
        //            return typeof(Byte[]);
        //        default:
        //            return typeof(String);
        //    }
        //}
        #endregion
    }

    /// <summary>
    /// OracleԪ����
    /// </summary>
    class OracleMeta : RemoteDbMetaData
    {
        /// <summary>ӵ����</summary>
        public String Owner { get { return (Database as Oracle).Owner.ToUpper(); } }

        /// <summary>
        /// ȡ�����б���
        /// </summary>
        /// <returns></returns>
        public override List<XTable> GetTables()
        {
            try
            {
                //- ��Ҫ�գ���������úܲң��б��������ݱ�ʵ��̫����
                //if (String.Equals(user, "system")) user = null;

                DataTable dt = GetSchema("Tables", new String[] { Owner });

                // Ĭ���г������ֶ�
                DataRow[] rows = new DataRow[dt.Rows.Count];
                dt.Rows.CopyTo(rows, 0);
                return GetTables(rows);

            }
            catch (DbException ex)
            {
                throw new XDbMetaDataException(this, "ȡ�����б��ܳ���", ex);
            }
        }

        protected override void FixTable(XTable table, DataRow dr)
        {
            base.FixTable(table, dr);

            // ��ע�� USER_TAB_COMMENTS
            String sql = String.Format("Select COMMENTS From USER_TAB_COMMENTS Where TABLE_NAME='{0}'", table.Name);
            String comment = (String)Database.CreateSession().ExecuteScalar(sql);
            if (!String.IsNullOrEmpty(comment)) table.Description = comment;

            if (table == null || table.Fields == null || table.Fields.Count < 1) return;

            // ����
            Boolean exists = false;
            foreach (XField field in table.Fields)
            {
                // �����Ƿ�����
                if (field.DataType != typeof(Int16) &&
                    field.DataType != typeof(Int32) &&
                    field.DataType != typeof(Int64)) continue;

                String name = String.Format("SEQ_{0}_{1}", table.Name, field.Name);
                if (CheckSeqExists(name))
                {
                    field.Identity = true;
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                // ���ñ��Ƿ������У����У���������Ϊ����
                String name = String.Format("SEQ_{0}", table.Name);
                if (CheckSeqExists(name))
                {
                    foreach (XField field in table.Fields)
                    {
                        if (!field.PrimaryKey ||
                            (field.DataType != typeof(Int16) &&
                            field.DataType != typeof(Int32) &&
                            field.DataType != typeof(Int64))) continue;

                        field.Identity = true;
                        exists = true;
                        break;
                    }
                }
            }
        }

        Boolean CheckSeqExists(String name)
        {
            String sql = String.Format("SELECT Count(*) FROM ALL_SEQUENCES Where SEQUENCE_NAME='{0}' And SEQUENCE_OWNER='{1}'", name, Owner);
            return Convert.ToInt32(Database.CreateSession().ExecuteScalar(sql)) > 0;
        }

        /// <summary>
        /// ȡ��ָ����������й���
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        protected override List<XField> GetFields(XTable table)
        {
            DataTable dt = GetSchema("Columns", new String[] { Owner, table.Name, null });

            DataRow[] drs = null;
            if (dt.Columns.Contains("ID"))
                drs = dt.Select("", "ID");
            else
                drs = dt.Select("");

            List<XField> list = GetFields(table, drs);

            // �ֶ�ע��
            if (list != null && list.Count > 0)
            {
                String sql = String.Format("Select COLUMN_NAME, COMMENTS From USER_COL_COMMENTS Where TABLE_NAME='{0}'", table.Name);
                dt = Database.CreateSession().Query(sql).Tables[0];
                foreach (XField field in list)
                {
                    drs = dt.Select(String.Format("COLUMN_NAME='{0}'", field.Name));
                    if (drs != null && drs.Length > 0) field.Description = GetDataRowValue<String>(drs[0], "COMMENTS");
                }
            }

            return list;
        }

        protected override void FixField(XField field, DataRow drColumn, DataRow drDataType)
        {
            base.FixField(field, drColumn, drDataType);

            // ������������
            if (field.RawType.StartsWith("NUMBER"))
            {
                if (field.Scale == 0)
                {
                    // 0��ʾ���Ȳ����ƣ�Ϊ�˷���ʹ�ã�תΪ�����Int32
                    if (field.Precision == 0)
                        field.DataType = typeof(Int32);
                    else if (field.Precision == 1)
                        field.DataType = typeof(Boolean);
                    else if (field.Precision <= 5)
                        field.DataType = typeof(Int16);
                    else if (field.Precision <= 10)
                        field.DataType = typeof(Int32);
                    else
                        field.DataType = typeof(Int64);
                }
                else
                {
                    if (field.Precision == 0)
                        field.DataType = typeof(Decimal);
                    else if (field.Precision <= 5)
                        field.DataType = typeof(Single);
                    else if (field.Precision <= 10)
                        field.DataType = typeof(Double);
                }
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
                    DataTable pks = GetSchema("IndexColumns", new String[] { Owner, null, null, null, null });
                    if (pks == null) return null;

                    _PrimaryKeys = pks;
                }
                return _PrimaryKeys;
            }
        }
    }
}