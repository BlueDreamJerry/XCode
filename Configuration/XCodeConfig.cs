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
        private static Dictionary<Type, ReadOnlyList<FieldItem>> _Fields = new Dictionary<Type, ReadOnlyList<FieldItem>>();
        /// <summary>
        /// ȡ��ָ����İﶨ�����ݱ��ֶε����ԡ�
        /// ĳЩ�����ֶο���ֻ������չ�����ݣ������ﶨ�����ݱ��ֶΣ�
        /// ���ֵķ������ǣ�DataObjectField�����Ƿ�Ϊ�ա�
        /// ��̬���档
        /// </summary>
        /// <param name="t">ʵ������</param>
        /// <returns>�ﶨ�����ݱ��ֶε����Զ����б�</returns>
        public static List<FieldItem> Fields(Type t)
        {
            if (_Fields.ContainsKey(t) && !_Fields[t].Changed) return _Fields[t];
            lock (_Fields)
            {
                if (_Fields.ContainsKey(t))
                {
                    if (_Fields[t].Changed) _Fields[t] = _Fields[t].Keep();
                    return _Fields[t];
                }

                List<FieldItem> cFields = AllFields(t);
                cFields = cFields.FindAll(delegate(FieldItem item) { return item.DataObjectField != null; });
                ReadOnlyList<FieldItem> list = new ReadOnlyList<FieldItem>(cFields);
                _Fields.Add(t, list);
                return list;
            }
        }

        private static Dictionary<Type, ReadOnlyList<FieldItem>> _AllFields = new Dictionary<Type, ReadOnlyList<FieldItem>>();
        /// <summary>
        /// ȡ��ָ����������������ԡ�
        /// ��̬���档
        /// </summary>
        /// <param name="t">ʵ������</param>
        /// <returns>�����������Զ����б�</returns>
        public static List<FieldItem> AllFields(Type t)
        {
            if (_AllFields.ContainsKey(t) && !_AllFields[t].Changed) return _AllFields[t];
            lock (_AllFields)
            {
                if (_AllFields.ContainsKey(t))
                {
                    if (_AllFields[t].Changed) _AllFields[t] = _AllFields[t].Keep();
                    return _AllFields[t];
                }

                List<FieldItem> list = new List<FieldItem>();
                PropertyInfo[] pis = t.GetProperties();
                List<String> names = new List<String>();
                foreach (PropertyInfo item in pis)
                {
                    FieldItem field = new FieldItem();
                    field.Property = item;
                    field.Column = BindColumnAttribute.GetCustomAttribute(item);
                    field.DataObjectField = DataObjectAttribute.GetCustomAttribute(item, typeof(DataObjectFieldAttribute)) as DataObjectFieldAttribute;
                    list.Add(field);

                    if (names.Contains(item.Name)) throw new XCodeException(String.Format("{0}���г����ظ�����{1}", t.Name, item.Name));
                    names.Add(item.Name);
                }
                ReadOnlyList<FieldItem> list2 = new ReadOnlyList<FieldItem>(list);
                _AllFields.Add(t, list2);
                return list2;
            }
        }

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
        /// <param name="t">ʵ������</param>
        /// <returns>ʵ����󶨵����ݱ�</returns>
        public static BindTableAttribute Table(Type t)
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

            return _Tables.GetItem(t, delegate(Type key) { return BindTableAttribute.GetCustomAttribute(key); });
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

        private static Dictionary<Type, ReadOnlyList<FieldItem>> _Unique = new Dictionary<Type, ReadOnlyList<FieldItem>>();
        /// <summary>
        /// Ψһ��
        /// ����б�ʶ�У��򷵻ر�ʶ�м��ϣ�
        /// ���򣬷����������ϡ�
        /// </summary>
        /// <param name="t">ʵ������</param>
        /// <returns>Ψһ������</returns>
        public static List<FieldItem> Unique(Type t)
        {
            if (_Unique.ContainsKey(t) && !_Unique[t].Changed) return _Unique[t];
            lock (_Unique)
            {
                if (_Unique.ContainsKey(t))
                {
                    if (_Unique[t].Changed) _Unique[t] = _Unique[t].Keep();
                    return _Unique[t];
                }

                List<FieldItem> list = new List<FieldItem>();
                foreach (FieldItem fi in Fields(t))
                {
                    if (fi.DataObjectField.IsIdentity)
                    {
                        list.Add(fi);
                    }
                }
                if (list.Count < 1) // û�б�ʶ�У�ʹ������
                {
                    foreach (FieldItem fi in Fields(t))
                    {
                        if (fi.DataObjectField.PrimaryKey)
                        {
                            list.Add(fi);
                        }
                    }
                }
                ReadOnlyList<FieldItem> list2 = new ReadOnlyList<FieldItem>(list);
                _Unique.Add(t, list2);
                return list2;
            }
        }

        private static DictionaryCache<Type, String> _Selects = new DictionaryCache<Type, String>();
        /// <summary>
        /// ȡ��ָ�����Ӧ��Select�־��ַ�����
        /// ��̬���档
        /// </summary>
        /// <param name="type">ʵ������</param>
        /// <returns>Select�־��ַ���</returns>
        public static String Selects(Type type)
        {
            //if (_Selects.ContainsKey(t)) return _Selects[t];
            //lock (_Selects)
            //{
            //    if (_Selects.ContainsKey(t)) return _Selects[t];

            return _Selects.GetItem(type, delegate(Type key)
            {
                StringBuilder sbSelects = new StringBuilder();
                foreach (FieldItem fi in Fields(key))
                {
                    if (sbSelects.Length > 0) sbSelects.Append(", ");
                    sbSelects.AppendFormat("{0}", fi.ColumnName);
                }
                String str = sbSelects.ToString();
                //_Selects.Add(key, str);
                return str;
            });
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
            //if (_SelectsEx.ContainsKey(t)) return _SelectsEx[t];
            //lock (_SelectsEx)
            //{
            //    if (_SelectsEx.ContainsKey(t)) return _SelectsEx[t];
            return _SelectsEx.GetItem(type, delegate(Type key)
            {
                String prefix = ColumnPrefix(key);
                String tablename = TableName(key);
                StringBuilder sbSelects = new StringBuilder();
                foreach (FieldItem fi in Fields(key))
                {
                    if (sbSelects.Length > 0) sbSelects.Append(", ");
                    sbSelects.AppendFormat("{0}.{1} as {2}{1}", tablename, fi.ColumnName, prefix);
                }
                String str = sbSelects.ToString();
                //_SelectsEx.Add(key, str);
                return str;
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