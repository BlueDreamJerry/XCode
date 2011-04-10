using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.Common;
using System.Reflection;
using System.Text;
using NewLife.Collections;
using XCode.DataAccessLayer;
using XCode.Exceptions;
using NewLife.Configuration;

namespace XCode.Configuration
{
    /// <summary>
    /// ʵ�������ù�����
    /// </summary>
    internal class XCodeConfig
    {
        private static DictionaryCache<Type, TableMapAttribute[]> _AllTableMaps = new DictionaryCache<Type, TableMapAttribute[]>();
        /// <summary>
        /// ���ж��ӳ��
        /// </summary>
        /// <param name="type">ʵ������</param>
        /// <returns>���ж��ӳ���б�</returns>
        static TableMapAttribute[] AllTableMaps(Type type)
        {
            //if (_AllTableMaps.ContainsKey(t)) return _AllTableMaps[t];
            //lock (_AllTableMaps)
            //{
            //    if (_AllTableMaps.ContainsKey(t)) return _AllTableMaps[t];
            return _AllTableMaps.GetItem(type, delegate(Type key)
            {
                List<TableMapAttribute> maps = new List<TableMapAttribute>();
                PropertyInfo[] pis = key.GetProperties();
                foreach (PropertyInfo pi in pis)
                {
                    TableMapAttribute table = TableMapAttribute.GetCustomAttribute(pi);
                    maps.Add(table);
                }
                //_AllTableMaps.Add(key, maps.ToArray());
                return maps.ToArray();
            });
        }

        /// <summary>
        /// ����ָ�����͵�ӳ��
        /// </summary>
        /// <param name="type"></param>
        /// <param name="jointypes"></param>
        /// <returns></returns>
        public static TableMapAttribute[] TableMaps(Type type, Type[] jointypes)
        {
            //ȡ������ӳ���ϵ
            List<Type> joinlist = new List<Type>(jointypes);
            //���ݴ����ʵ�������б�������������Щ������
            List<TableMapAttribute> maps = new List<TableMapAttribute>();
            foreach (TableMapAttribute item in AllTableMaps(type))
            {
                Type t = joinlist.Find(delegate(Type elm) { return elm == item.MapEntity; });
                if (t != null)
                {
                    maps.Add(item);
                    joinlist.Remove(t);
                }
            }
            return maps.ToArray();
        }

        private static DictionaryCache<Type, BindTableAttribute> _Tables = new DictionaryCache<Type, BindTableAttribute>();
        /// <summary>
        /// ȡ��ָ��������ݱ�
        /// ��̬���档
        /// </summary>
        /// <param name="type">ʵ������</param>
        /// <returns>ʵ����󶨵����ݱ�</returns>
        public static BindTableAttribute Table(Type type)
        {
            //if (_Tables.ContainsKey(t)) return _Tables[t];
            //lock (_Tables)
            //{
            //    if (_Tables.ContainsKey(t)) return _Tables[t];

            //    BindTableAttribute table = BindTableAttribute.GetCustomAttribute(t);

            //    _Tables.Add(t, table);

            //    ////�������ʵ����Ȩ
            //    //if (XLicense.License.EntityCount != _Tables.Count)
            //    //    XLicense.License.EntityCount = _Tables.Count;

            //    return table;
            //}

            return _Tables.GetItem(type, delegate(Type key) { return BindTableAttribute.GetCustomAttribute(key); });
        }

        private static DictionaryCache<Type, XTable> _XTables = new DictionaryCache<Type, XTable>();
        /// <summary>
        /// ��ȡ���Ͷ�Ӧ��XTable
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static XTable GetTable(Type type)
        {
            return _XTables.GetItem(type, delegate(Type key)
            {
                BindTableAttribute bt = Table(key);
                XTable table = new XTable();
                table.Name = bt.Name;
                table.DbType = bt.DbType;
                table.Description = bt.Description;

                table.Fields = new List<XField>();
                foreach (FieldItem fi in FieldItem.Fields(key))
                {
                    XField f = table.CreateField();
                    fi.Fill(f);

                    table.Fields.Add(f);
                }

                return table;
            });
        }

        /// <summary>
        /// ��ȡ����ָ�����Ƶ��ֶ�
        /// </summary>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static XField GetField(Type type, String name)
        {
            XTable table = GetTable(type);
            if (table == null || table.Fields == null) return null;

            foreach (XField item in table.Fields)
            {
                if (item.Name == name) return item;
            }
            return null;
        }

        /// <summary>
        /// ȡ��ָ��������ݱ�����
        /// ��̬���档
        /// ���⴦��Oracle���ݿ⣬�ڱ���ǰ���Ϸ��������û�����
        /// </summary>
        /// <param name="t">ʵ������</param>
        /// <returns>ʵ����󶨵����ݱ�</returns>
        public static String TableName(Type t)
        {
            BindTableAttribute table = Table(t);
            String str;
            if (table != null)
                str = table.Name;
            else
                str = t.Name;

            // ���⴦��Oracle���ݿ⣬�ڱ���ǰ���Ϸ��������û�����
            //DAL dal = StaticDBO(t);
            DAL dal = DAL.Create(ConnName(t));
            if (dal != null && !str.Contains("."))
            {
                if (dal.DbType == DatabaseType.Oracle)
                {
                    //DbConnectionStringBuilder ocsb = dal.Db.Factory.CreateConnectionStringBuilder();
                    //ocsb.ConnectionString = dal.ConnStr;
                    // �����û���
                    //String UserID = (String)ocsb["User ID"];
                    String UserID = (dal.Db as Oracle).UserID;
                    if (!String.IsNullOrEmpty(UserID)) str = UserID + "." + str;
                }
            }
            return str;
        }

        private static Dictionary<Type, String> _ConnName = new Dictionary<Type, String>();
        /// <summary>
        /// ȡ��ָ��������ݿ���������
        /// ��̬���档
        /// </summary>
        /// <param name="t">ʵ������</param>
        /// <returns>ʵ����󶨵����ݿ�������</returns>
        public static String ConnName(Type t)
        {
            BindTableAttribute table = Table(t);

            String connName = null;
            if (table != null) connName = table.ConnName;

            String str = FindConnMap(connName, t.Name);
            return String.IsNullOrEmpty(str) ? connName : str;
        }

        private static List<String> _ConnMaps;
        /// <summary>
        /// ������ӳ��
        /// </summary>
        private static List<String> ConnMaps
        {
            get
            {
                if (_ConnMaps != null) return _ConnMaps;
                _ConnMaps = new List<String>();
                //String str = ConfigurationManager.AppSettings["XCodeConnMaps"];
                String str = Config.GetConfig<String>("XCode.ConnMaps", Config.GetConfig<String>("XCodeConnMaps"));
                if (String.IsNullOrEmpty(str)) return _ConnMaps;
                String[] ss = str.Split(',');
                foreach (String item in ss)
                {
                    if (item.Contains("#") && !item.EndsWith("#") ||
                        item.Contains("@") && !item.EndsWith("@")) _ConnMaps.Add(item.Trim());
                }
                return _ConnMaps;
            }
        }

        /// <summary>
        /// ��������������������������ӳ��
        /// </summary>
        /// <param name="connName"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        private static String FindConnMap(String connName, String className)
        {
            String name1 = connName + "#";
            String name2 = className + "@";

            foreach (String item in ConnMaps)
            {
                if (item.StartsWith(name1)) return item.Substring(name1.Length);
                if (item.StartsWith(name2)) return item.Substring(name2.Length);
            }
            return null;
        }

        private static DictionaryCache<Type, String> _SelectsEx = new DictionaryCache<Type, String>();
        /// <summary>
        /// ȡ��ָ�����Ӧ��Select�־��ַ�����ÿ���ֶξ���ǰ׺��
        /// ��̬���档
        /// </summary>
        /// <param name="type">ʵ������</param>
        /// <returns>Select�־��ַ���</returns>
        public static String SelectsEx(Type type)
        {
            return _SelectsEx.GetItem(type, delegate(Type key)
            {
                String prefix = ColumnPrefix(key);
                String tablename = TableName(key);
                StringBuilder sbSelects = new StringBuilder();
                foreach (FieldItem fi in FieldItem.Fields(key))
                {
                    if (sbSelects.Length > 0) sbSelects.Append(", ");
                    sbSelects.AppendFormat("{0}.{1} as {2}{1}", tablename, fi.ColumnName, prefix);
                }
                return sbSelects.ToString();
            });
        }

        /// <summary>
        /// ȡ���ֶ�ǰ׺
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static String ColumnPrefix(Type t)
        {
            return String.Format("XCode_Map_{0}_", XCodeConfig.TableName(t));
        }
    }
}