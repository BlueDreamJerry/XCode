using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Services;
using System.Xml.Serialization;
using NewLife.Reflection;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Exceptions;

namespace XCode
{
    /// <summary>
    /// ����ʵ������ࡣ��������ʵ���඼����̳и��ࡣ
    /// </summary>
    [Serializable]
    public partial class Entity<TEntity> : EntityBase where TEntity : Entity<TEntity>, new()
    {
        #region ���캯��
        /// <summary>
        /// ��̬����
        /// </summary>
        static Entity()
        {
            // 1�����Գ�ʼ����ʵ�����͵Ĳ�������
            // 2��CreateOperate����ʵ����һ��TEntity���󣬴Ӷ�����TEntity�ľ�̬���캯����
            // ����ʵ��Ӧ���У�ֱ�ӵ���Entity�ľ�̬����ʱ��û������TEntity�ľ�̬���캯����
            TEntity entity = new TEntity();

            //! ��ʯͷ 2011-03-14 ���¹��̸�Ϊ�첽����
            //  ��ȷ�ϣ���ʵ���ྲ̬���캯����ʹ����EntityFactory.CreateOperate(Type)����ʱ�����ܳ���������
            //  ��Ϊ���߶�������EntityFactory�е�op_cache����CreateOperate(Type)�õ�op_cache�󣬻���Ҫ�ȴ���ǰ��̬���캯��ִ����ɡ�
            //  ��ȷ���������Ƿ��������֢
            //ThreadPool.QueueUserWorkItem(delegate
            //{
            EntityFactory.CreateOperate(Meta.ThisType, entity);
            //});
        }

        /// <summary>
        /// ����ʵ��
        /// </summary>
        /// <returns></returns>
        internal override IEntity CreateInternal()
        {
            return CreateInstance();
        }

        /// <summary>
        /// ����ʵ��
        /// </summary>
        /// <returns></returns>
        protected virtual TEntity CreateInstance()
        {
            return new TEntity();
        }
        #endregion

        #region �������
        /// <summary>
        /// ���ؼ�¼��
        /// </summary>
        /// <param name="ds">��¼��</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> LoadData(DataSet ds)
        {
            if (ds == null || ds.Tables.Count < 1 || ds.Tables[0].Rows.Count < 1) return null;
            return LoadData(ds.Tables[0]);
        }

        /// <summary>
        /// �������ݱ�
        /// </summary>
        /// <param name="dt">���ݱ�</param>
        /// <returns>ʵ������</returns>
        protected static EntityList<TEntity> LoadData(DataTable dt)
        {
            if (dt == null || dt.Rows.Count < 1) return null;
            return LoadData(dt, null);
        }

        /// <summary>
        /// �������ݱ�
        /// </summary>
        /// <param name="dt">���ݱ�</param>
        /// <param name="jointypes"></param>
        /// <returns>ʵ������</returns>
        protected static EntityList<TEntity> LoadData(DataTable dt, Type[] jointypes)
        {
            if (dt == null || dt.Rows.Count < 1) return null;
            EntityList<TEntity> list = new EntityList<TEntity>(dt.Rows.Count);
            String prefix = null;
            TableMapAttribute[] maps = null;
            Boolean hasprefix = false;
            if (jointypes != null && jointypes.Length > 0)
            {
                maps = XCodeConfig.TableMaps(Meta.ThisType, jointypes);
                prefix = Meta.ColumnPrefix;
                hasprefix = true;
            }
            IEntityOperate factory = EntityFactory.CreateOperate(Meta.ThisType);
            List<FieldItem> ps = CheckColumn(dt, prefix);
            foreach (DataRow dr in dt.Rows)
            {
                //TEntity obj = new TEntity();
                TEntity obj = factory.Create() as TEntity;
                obj.LoadData(dr, hasprefix, ps, maps);
                list.Add(obj);
            }
            return list;
        }

        /// <summary>
        /// ��һ�������ж���������ݡ������ع�������
        /// </summary>
        /// <param name="dr">������</param>
        public override void LoadData(DataRow dr)
        {
            if (dr == null) return;
            LoadData(dr, null);
        }

        /// <summary>
        /// ��һ�������ж���������ݡ�ָ��Ҫ������Щ������ʵ�������
        /// </summary>
        /// <param name="dr">������</param>
        /// <param name="jointypes">������</param>
        protected virtual void LoadData(DataRow dr, Type[] jointypes)
        {
            if (dr == null) return;
            String prefix = null;
            TableMapAttribute[] maps = null;
            Boolean hasprefix = false;
            if (jointypes != null && jointypes.Length > 0)
            {
                maps = XCodeConfig.TableMaps(Meta.ThisType, jointypes);
                prefix = Meta.ColumnPrefix;
                hasprefix = true;
            }
            List<FieldItem> ps = CheckColumn(dr.Table, prefix);
            LoadData(dr, hasprefix, ps, maps);
        }

        /// <summary>
        /// ��һ�������ж���������ݡ���ǰ׺��
        /// </summary>
        /// <param name="dr">������</param>
        /// <param name="ps">Ҫ�������ݵ��ֶ�</param>
        /// <returns></returns>
        protected virtual void LoadDataWithPrefix(DataRow dr, List<FieldItem> ps)
        {
            if (dr == null) return;
            if (ps == null || ps.Count < 1) ps = Meta.Fields;
            String prefix = Meta.ColumnPrefix;
            foreach (FieldItem fi in ps)
            {
                // ����dr[fi.ColumnName]��Ϊһ��
                Object v = dr[prefix + fi.ColumnNameEx];
                this[fi.Name] = v == DBNull.Value ? null : v;
            }
        }

        static String[] TrueString = new String[] { "true", "y", "yes", "1" };
        static String[] FalseString = new String[] { "false", "n", "no", "0" };

        /// <summary>
        /// ��һ�������ж���������ݡ�ָ��Ҫ�������ݵ��ֶΣ��Լ�Ҫ������Щ������ʵ�������
        /// </summary>
        /// <param name="dr">������</param>
        /// <param name="hasprefix">�Ƿ����ǰ׺</param>
        /// <param name="ps">Ҫ�������ݵ��ֶ�</param>
        /// <param name="maps">Ҫ������ʵ����</param>
        /// <returns></returns>
        private void LoadData(DataRow dr, Boolean hasprefix, List<FieldItem> ps, TableMapAttribute[] maps)
        {
            if (dr == null) return;
            if (ps == null || ps.Count < 1) ps = Meta.Fields;
            String prefix = null;
            if (hasprefix) prefix = Meta.ColumnPrefix;
            foreach (FieldItem fi in ps)
            {
                // ����dr[fi.ColumnName]��Ϊһ��
                Object v = dr[prefix + fi.ColumnNameEx];
                Object v2 = this[fi.Name];

                // ��������ͬ���ݵĸ�ֵ
                if (Object.Equals(v, v2)) continue;

                if (fi.Property.PropertyType == typeof(String))
                {
                    // ��������ַ����Կ��ַ����ĸ�ֵ
                    if (v != null && String.IsNullOrEmpty(v.ToString()))
                    {
                        if (v2 == null || String.IsNullOrEmpty(v2.ToString())) continue;
                    }
                }
                else if (fi.Property.PropertyType == typeof(Boolean))
                {
                    // �����ַ���תΪ������
                    if (v != null && v.GetType() == typeof(String))
                    {
                        String vs = v.ToString();
                        if (String.IsNullOrEmpty(vs))
                            v = false;
                        else
                        {
                            if (Array.IndexOf(TrueString, vs.ToLower()) >= 0)
                                v = true;
                            else if (Array.IndexOf(FalseString, vs.ToLower()) >= 0)
                                v = false;

                            //if (NewLife.Configuration.Config.GetConfig<Boolean>("XCode.Debug")) NewLife.Log.XTrace.WriteLine("�޷����ַ���{0}תΪ�����ͣ�", vs);
                            if (DAL.Debug) DAL.WriteLog("�޷����ַ���{0}תΪ�����ͣ�", vs);
                        }
                    }
                }

                //��Ӱ�������ݵ�״̬
                Boolean? b = null;
                if (Dirtys.ContainsKey(fi.Name)) b = Dirtys[fi.Name];

                this[fi.Name] = v == DBNull.Value ? null : v;

                if (b != null)
                    Dirtys[fi.Name] = b.Value;
                else
                    Dirtys.Remove(fi.Name);
            }
            //���������Ը�ֵ
            if (maps != null && maps.Length > 0)
            {
                foreach (TableMapAttribute item in maps)
                {
                    LoadDataEx(dr, item);
                }
            }
        }

        /// <summary>
        /// ��һ�������ж���������ݡ������÷���ʵ�֣�Ϊ�˸������ܣ�ʵ����Ӧ�����ظ÷�����
        /// </summary>
        /// <param name="dr"></param>
        /// <param name="map"></param>
        protected virtual void LoadDataEx(DataRow dr, TableMapAttribute map)
        {
            //����һ������
            Object obj = Activator.CreateInstance(map.MapEntity);
            //�ҵ�װ�����ݵķ���
            MethodInfo method = map.MapEntity.GetMethod("LoadDataWithPrefix");
            //���������װ������
            //method.Invoke(this, new Object[] { dr, null });
            MethodInfoX.Create(method).Invoke(this, new Object[] { dr, null });
            //���������Ը�ֵ
            map.LocalField.SetValue(this, obj, null);
        }

        /// <summary>
        /// ���ʵ�����е���Щ�ֶ������ݱ���
        /// </summary>
        /// <param name="dt">���ݱ�</param>
        /// <param name="prefix">�ֶ�ǰ׺</param>
        /// <returns></returns>
        private static List<FieldItem> CheckColumn(DataTable dt, String prefix)
        {
            //// ���dr���Ƿ��и����Ե��С����ǵ�Select�����ǲ������ģ���ʱ��ֻ��Ҫ�ֲ����
            //List<FieldItem> allps = Meta.AllFields;
            //if (allps == null || allps.Count < 1) return null;

            //�����ǧ����ɾ��allps�е��������Ӱ�쵽ȫ�ֵ�Fields�����
            List<FieldItem> ps = new List<FieldItem>();
            //for (Int32 i = allps.Length - 1; i >= 0; i--)
            //{
            //    if (dt.Columns.Contains(prefix + allps[i].ColumnNameEx)) ps.Add(allps[i]);
            //}
            foreach (FieldItem item in Meta.AllFields)
            {
                if (dt.Columns.Contains(prefix + item.ColumnNameEx)) ps.Add(item);
            }
            return ps;

            //return Meta.AllFields.FindAll(delegate(FieldItem item)
            //{
            //    return dt.Columns.Contains(prefix + item.ColumnNameEx);
            //});
        }

        ///// <summary>
        ///// �����ݸ��Ƶ������ж����С�
        ///// </summary>
        ///// <param name="dr">������</param>
        //public virtual DataRow ToData(ref DataRow dr)
        //{
        //    if (dr == null) return null;
        //    List<FieldItem> ps = Meta.Fields;
        //    foreach (FieldItem fi in ps)
        //    {
        //        // ���dr���Ƿ��и����Ե��С����ǵ�Select�����ǲ������ģ���ʱ��ֻ��Ҫ�ֲ����
        //        if (dr.Table.Columns.Contains(fi.ColumnName))
        //            dr[fi.ColumnName] = this[fi.Name];
        //    }
        //    return dr;
        //}
        #endregion

        #region ����
        /// <summary>
        /// �Ѹö���־û������ݿ�
        /// </summary>
        /// <returns></returns>
        public override Int32 Insert()
        {
            String sql = SQL(this, DataObjectMethodType.Insert);

            //AC��SqlServer֧�ֻ�ȡ�����ֶε����±��
            //if (Meta.DbType == DatabaseType.Access ||
            //    Meta.DbType == DatabaseType.SqlServer ||
            //    Meta.DbType == DatabaseType.SqlServer2005)
            {
                //����Ƿ��б�ʶ�У���ʶ����Ҫ���⴦��
                //FieldItem[] ps = Meta.Uniques;
                FieldItem field = Meta.Unique;
                //if (ps != null && ps.Length > 0 && ps[0].DataObjectField != null && ps[0].DataObjectField.IsIdentity)
                if (field != null && field.DataObjectField != null && field.DataObjectField.IsIdentity)
                {
                    Int64 res = Meta.InsertAndGetIdentity(sql);
                    if (res > 0) this[field.Name] = res;
                    return res > 0 ? 1 : 0;
                }
            }
            return Meta.Execute(sql);
        }

        /// <summary>
        /// �������ݿ�
        /// </summary>
        /// <returns></returns>
        public override Int32 Update()
        {
            //û�������ݣ�����Ҫ����
            if (Dirtys == null || Dirtys.Count <= 0) return 0;

            String sql = SQL(this, DataObjectMethodType.Update);
            if (String.IsNullOrEmpty(sql)) return 0;
            Int32 rs = Meta.Execute(sql);

            //��������ݣ������ظ��ύ
            if (Dirtys != null)
            {
                Dirtys.Clear();
                Dirtys = null;
            }
            return rs;
        }

        /// <summary>
        /// �����ݿ���ɾ���ö���
        /// </summary>
        /// <returns></returns>
        public override Int32 Delete()
        {
            return Meta.Execute(SQL(this, DataObjectMethodType.Delete));
        }

        /// <summary>
        /// ���档��������������ݿ����Ƿ��Ѵ��ڸö����پ�������Insert��Update
        /// </summary>
        /// <returns></returns>
        public override Int32 Save()
        {
            //����ʹ�������ֶ��ж�
            FieldItem fi = Meta.Unique;
            if (fi != null && fi.DataObjectField.IsIdentity || fi.Property.PropertyType == typeof(Int32))
            {
                Int64 id = Convert.ToInt64(this[Meta.Unique.Name]);
                if (id > 0)
                    return Update();
                else
                    return Insert();
            }

            Int32 count = Meta.QueryCount(SQL(this, DataObjectMethodType.Select));

            if (count > 0)
                return Update();
            else
                return Insert();
        }
        #endregion

        #region ���ҵ���ʵ��
        /// <summary>
        /// ���������Լ���Ӧ��ֵ�����ҵ���ʵ��
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [WebMethod(Description = "���������Լ���Ӧ��ֵ�����ҵ���ʵ��")]
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity Find(String name, Object value)
        {
            return Find(new String[] { name }, new Object[] { value });
        }

        /// <summary>
        /// ���������б��Լ���Ӧ��ֵ�б����ҵ���ʵ��
        /// </summary>
        /// <param name="names"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public static TEntity Find(String[] names, Object[] values)
        {
            return Find(MakeCondition(names, values, "And"));
        }

        /// <summary>
        /// �����������ҵ���ʵ��
        /// </summary>
        /// <param name="whereClause"></param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity Find(String whereClause)
        {
            IList<TEntity> list = FindAll(whereClause, null, null, 0, 1);
            if (list == null || list.Count < 1)
                return null;
            else
                return list[0];
        }

        /// <summary>
        /// �����������ҵ���ʵ��
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKey(Object key)
        {
            FieldItem field = Meta.Unique;
            if (field == null) throw new ArgumentNullException("Meta.Unique", "FindByKey����Ҫ��ñ���Ψһ������");

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
            if (field.DataObjectField.IsIdentity && (key is Int32) && ((Int32)key) <= 0) return null;

            return Find(field.Name, key);
        }

        /// <summary>
        /// ����������ѯһ��ʵ��������ڱ��༭
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKeyForEdit(Object key)
        {
            FieldItem field = Meta.Unique;
            if (field == null) throw new ArgumentNullException("Meta.Unique", "FindByKeyForEdit����Ҫ��ñ���Ψһ������");

            // ����Ϊ��ʱ��������ʵ��
            if (key == null)
            {
                IEntityOperate factory = EntityFactory.CreateOperate(Meta.ThisType);
                return factory.Create() as TEntity;
            }

            Type type = field.Property.PropertyType;

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ��������ʵ��
            if (IsInt(type) && IsInt(key.GetType()) && ((Int32)key) <= 0)
            {
                if (field.DataObjectField.IsIdentity)
                {
                    IEntityOperate factory = EntityFactory.CreateOperate(Meta.ThisType);
                    return factory.Create() as TEntity;
                }
                else
                {
                    if (DAL.Debug) DAL.WriteLog("{0}��{1}�ֶ����������������Ƿ�����������������", Meta.TableName, field.ColumnName);
                }
            }

            // Ψһ�����ַ�����Ϊ��ʱ��������ʵ��
            if (type == typeof(String) && (key is String) && String.IsNullOrEmpty((String)key))
            {
                IEntityOperate factory = EntityFactory.CreateOperate(Meta.ThisType);
                return factory.Create() as TEntity;
            }

            // ���⣬һ�ɷ��� ����ֵ����ʹ�����ǿա������������Ҳ������ݵ�����¸������ؿգ���Ϊ�������Ҳ������ݶ��ѣ���������ʵ���ᵼ��ǰ����Ϊ��������������
            TEntity entity = Find(field.Name, key);

            // �ж�ʵ��
            if (entity == null)
            {
                String msg = null;
                if (IsInt(type) && IsInt(key.GetType()) && ((Int32)key) <= 0)
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}������δ��������������", key, Meta.Description);
                else
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}��", key, Meta.Description);

                throw new XCodeException(msg);
            }

            return entity;
        }

        /// <summary>
        /// �Ƿ�����������16λ��32λ��64λ�������޷��ź��з���
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        static Boolean IsInt(Type type)
        {
            if (type == null) return false;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                default:
                    break;
            }
            return false;
        }
        #endregion

        #region ��̬��ѯ
        /// <summary>
        /// ��ȡ����ʵ����󡣻�ȡ��������ʱ��ǳ���������
        /// </summary>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll()
        {
            return LoadData(Meta.Query(SQL(null, DataObjectMethodType.Fill)));
        }

        /// <summary>
        /// ��ѯ������ʵ����󼯺ϡ�
        /// �����Լ������ֶ�������ʹ�������Լ��ֶζ�Ӧ����������������ת��Ϊ����������
        /// </summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ�弯</returns>
        [WebMethod(Description = "��ѯ������ʵ����󼯺�")]
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows)
        {
            #region �������ݲ�ѯ�Ż�
            // ��������βҳ��ѯ�Ż�
            // �ں������ݷ�ҳ�У�ȡԽ�Ǻ���ҳ������Խ�������Կ��ǵ���ķ�ʽ
            // ֻ���ڰ������ݣ��ҿ�ʼ�д�����ʮ��ʱ��ʹ��
            Int32 count = Meta.Count;
            if (startRowIndex > 500000 && count > 1000000)
            {
                // ���㱾�β�ѯ�Ľ������
                if (!String.IsNullOrEmpty(whereClause)) count = FindCount(whereClause, orderClause, selects, startRowIndex, maximumRows);
                // �α����м�ƫ��
                if (startRowIndex * 2 > count)
                {
                    String order = orderClause;
                    Boolean bk = false; // �Ƿ�����

                    #region ������
                    // Ĭ���������ֶεĽ���
                    if (String.IsNullOrEmpty(order) && Meta.Unique != null && Meta.Unique.DataObjectField.IsIdentity)
                        order = Meta.Unique.Name + " Desc";

                    if (!String.IsNullOrEmpty(order))
                    {
                        String[] ss = order.Split(',');
                        StringBuilder sb = new StringBuilder();
                        foreach (String item in ss)
                        {
                            String fn = item;
                            String od = "asc";

                            Int32 p = fn.LastIndexOf(" ");
                            if (p > 0)
                            {
                                od = item.Substring(p).Trim().ToLower();
                                fn = item.Substring(0, p).Trim();
                            }

                            switch (od)
                            {
                                case "asc":
                                    od = "desc";
                                    break;
                                case "desc":
                                    //od = "asc";
                                    od = null;
                                    break;
                                default:
                                    bk = true;
                                    break;
                            }
                            if (bk) break;

                            if (sb.Length > 0) sb.Append(", ");
                            sb.AppendFormat("{0} {1}", fn, od);
                        }

                        order = sb.ToString();
                    }
                    #endregion

                    // û�������ʵ�ڲ��ʺ����ְ취����Ϊû�취����
                    if (!String.IsNullOrEmpty(order))
                    {
                        // ������������Ϊʵ������������
                        Int32 max = Math.Min(maximumRows, count - startRowIndex);
                        if (max <= 0) return null;
                        Int32 start = count - (startRowIndex + maximumRows);

                        String sql2 = PageSplitSQL(whereClause, order, selects, start, max);
                        EntityList<TEntity> list = LoadData(Meta.Query(sql2));
                        if (list == null || list.Count < 1) return null;
                        // ��Ϊ����ȡ�õ������ǵ������ģ�����������Ҫ�ٵ�һ��
                        list.Reverse();
                        return list;
                    }
                }
            }
            #endregion

            String sql = PageSplitSQL(whereClause, orderClause, selects, startRowIndex, maximumRows);
            return LoadData(Meta.Query(sql));
        }

        /// <summary>
        /// ���������б��Լ���Ӧ��ֵ�б���ȡ����ʵ�����
        /// </summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> FindAll(String[] names, Object[] values)
        {
            return FindAll(MakeCondition(names, values, "And"), null, null, 0, 0);
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ����ȡ����ʵ�����
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll(String name, Object value)
        {
            return FindAll(new String[] { name }, new Object[] { value });
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ����ȡ����ʵ�����
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll(String name, Object value, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindAllByName(name, value, null, startRowIndex, maximumRows);
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ����ȡ����ʵ�����
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public static EntityList<TEntity> FindAllByName(String name, Object value, String orderClause, Int32 startRowIndex, Int32 maximumRows)
        {
            if (String.IsNullOrEmpty(name))
                return FindAll(null, orderClause, null, startRowIndex, maximumRows);
            else
                return FindAll(MakeCondition(new String[] { name }, new Object[] { value }, "And"), orderClause, null, startRowIndex, maximumRows);
        }

        /// <summary>
        /// ��ѯSQL������ʵ��������顣
        /// Select������ֱ��ʹ�ò���ָ���Ĳ�ѯ�����в�ѯ���������κ�ת����
        /// </summary>
        /// <param name="sql">��ѯ���</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> FindAll(String sql)
        {
            return LoadData(Meta.Query(sql));
        }

        /// <summary>
        /// ��ѯ������ʵ��������顣
        /// ���ָ����jointypes��������ͬʱ���ز�����ָ���Ĺ�������
        /// </summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <param name="jointypes">Ҫ������ʵ�������б�</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> FindAllMultiple(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows, Type[] jointypes)
        {
            if (jointypes == null || jointypes.Length < 1) return FindAll(whereClause, orderClause, selects, startRowIndex, maximumRows);

            //���ݴ����ʵ�������б�������������Щ������
            TableMapAttribute[] maps = XCodeConfig.TableMaps(Meta.ThisType, jointypes);
            //û���ҵ�����ӳ�����Ե��ֶ�
            if (maps == null || maps.Length < 1) return FindAll(whereClause, orderClause, selects, startRowIndex, maximumRows);

            String LocalTableName = Meta.TableName;
            //׼��ƴ��SQL��ѯ���
            StringBuilder sb = new StringBuilder();
            sb.Append("Select ");
            //sb.Append(selects);
            if (String.IsNullOrEmpty(selects) || selects == "*" || selects.Trim() == "*")
            {
                sb.Append(XCodeConfig.SelectsEx(Meta.ThisType));
            }
            else
            {
                String[] ss = selects.Split(',');
                Boolean isfirst = false;
                foreach (String item in ss)
                {
                    if (!isfirst)
                    {
                        sb.Append(", ");
                        isfirst = true;
                    }
                    sb.AppendFormat("{0}.{1} as {2}{1}", LocalTableName, OqlToSql(item), Meta.ColumnPrefix);
                }
            }

            //����ÿһ��������ʵ�����ͱ���д���
            foreach (TableMapAttribute item in maps)
            {
                sb.Append(", ");
                sb.Append(XCodeConfig.SelectsEx(item.MapEntity));
            }
            sb.Append(" From ");
            sb.Append(LocalTableName);

            List<String> tables = new List<string>();
            tables.Add(LocalTableName);
            //����ÿһ��������ʵ�����ͱ���д���
            foreach (TableMapAttribute item in maps)
            {
                String tablename = XCodeConfig.TableName(item.MapEntity);
                tables.Add(tablename);
                sb.Append(" ");
                //��������
                sb.Append(item.MapType.ToString().Replace("_", " "));
                sb.Append(" ");
                //������
                sb.Append(tablename);
                sb.Append(" On ");
                sb.AppendFormat("{0}.{1}={2}.{3}", LocalTableName, item.LocalColumn, tablename, item.MapColumn);
            }

            if (!String.IsNullOrEmpty(whereClause))
            {
                //����ǰ׺
                whereClause = Regex.Replace(whereClause, "(w+)", "");
                sb.AppendFormat(" Where {0} ", OqlToSql(whereClause));
            }
            if (!String.IsNullOrEmpty(orderClause))
            {
                //����ǰ׺
                sb.AppendFormat(" Order By {0} ", OqlToSql(orderClause));
            }

            FieldItem fi = Meta.Unique;
            String keyColumn = null;
            if (fi != null)
            {
                keyColumn = Meta.ColumnPrefix + fi.ColumnName;
                // ����Desc��ǣ���ʹ��MaxMin��ҳ�㷨����ʶ�У���һ������Ϊ��������
                if (fi.DataObjectField.IsIdentity || fi.Property.PropertyType == typeof(Int32)) keyColumn += " Desc";
            }
            String sql = Meta.PageSplit(sb.ToString(), startRowIndex, maximumRows, keyColumn);
            DataSet ds = Meta.Query(sql, tables.ToArray());
            if (ds == null || ds.Tables.Count < 1 || ds.Tables[0].Rows.Count < 1) return null;

            return LoadData(ds.Tables[0], jointypes);
        }
        #endregion

        #region ȡ�ܼ�¼��
        /// <summary>
        /// �����ܼ�¼��
        /// </summary>
        /// <returns></returns>
        public static Int32 FindCount()
        {
            //Int32 count = Meta.Count;
            //if (count >= 1000) return count;

            return Meta.QueryCount(SQL(null, DataObjectMethodType.Fill));
            //return Meta.Count;
        }

        /// <summary>
        /// �����ܼ�¼��
        /// </summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>������</returns>
        [WebMethod(Description = "��ѯ�������ܼ�¼��")]
        public static Int32 FindCount(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows)
        {
            //�������Where�־䣬ֱ�ӵ���FindCount�����Խ��������㷨ȡ���ܼ�¼��
            if (String.IsNullOrEmpty(whereClause)) return FindCount();

            String sql = PageSplitSQL(whereClause, null, selects, 0, 0);
            return Meta.QueryCount(sql);
        }

        /// <summary>
        /// ���������б��Լ���Ӧ��ֵ�б������ܼ�¼��
        /// </summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String[] names, Object[] values)
        {
            return FindCount(MakeCondition(names, values, "And"), null, null, 0, 0);
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ�������ܼ�¼��
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String name, Object value)
        {
            return FindCount(name, value, 0, 0);
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ�������ܼ�¼��
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String name, Object value, Int32 startRowIndex, Int32 maximumRows)
        {
            return FindCountByName(name, value, null, startRowIndex, maximumRows);
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ�������ܼ�¼��
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>������</returns>
        public static Int32 FindCountByName(String name, Object value, String orderClause, int startRowIndex, int maximumRows)
        {
            if (String.IsNullOrEmpty(name))
                return FindCount(null, null, null, 0, 0);
            else
                return FindCount(MakeCondition(new String[] { name }, new Object[] { value }, "And"), null, null, 0, 0);
        }
        #endregion

        #region ��̬����
        /// <summary>
        /// ��һ��ʵ�����־û������ݿ�
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [WebMethod(Description = "����")]
        [DataObjectMethod(DataObjectMethodType.Insert, true)]
        public static Int32 Insert(TEntity obj)
        {
            return obj.Insert();
        }

        /// <summary>
        /// ��һ��ʵ�����־û������ݿ�
        /// </summary>
        /// <param name="names">���������б�</param>
        /// <param name="values">����ֵ�б�</param>
        /// <returns>������Ӱ�������</returns>
        public static Int32 Insert(String[] names, Object[] values)
        {
            if (names == null) throw new ArgumentNullException("names", "�����б��ֵ�б���Ϊ��");
            if (values == null) throw new ArgumentNullException("values", "�����б��ֵ�б���Ϊ��");

            if (names.Length != values.Length) throw new ArgumentException("�����б�����ֵ�б�һһ��Ӧ");
            //FieldItem[] fis = Meta.Fields;
            Dictionary<String, FieldItem> fs = new Dictionary<String, FieldItem>();
            foreach (FieldItem fi in Meta.Fields)
                fs.Add(fi.Name, fi);
            StringBuilder sbn = new StringBuilder();
            StringBuilder sbv = new StringBuilder();
            for (Int32 i = 0; i < names.Length; i++)
            {
                if (!fs.ContainsKey(names[i])) throw new ArgumentException("��[" + Meta.ThisType.FullName + "]�в�����[" + names[i] + "]����");
                // ͬʱ����SQL��䡣names�������б�����ת���ɶ�Ӧ���ֶ��б�
                if (i > 0)
                {
                    sbn.Append(", ");
                    sbv.Append(", ");
                }
                sbn.Append(Meta.FormatName(fs[names[i]].Name));
                //sbv.Append(SqlDataFormat(values[i], fs[names[i]]));
                sbv.Append(Meta.FormatValue(names[i], values[i]));
            }
            return Meta.Execute(String.Format("Insert Into {2}({0}) values({1})", sbn.ToString(), sbv.ToString(), Meta.FormatName(Meta.TableName)));
        }

        /// <summary>
        /// ��һ��ʵ�������µ����ݿ�
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [WebMethod(Description = "����")]
        [DataObjectMethod(DataObjectMethodType.Update, true)]
        public static Int32 Update(TEntity obj)
        {
            return obj.Update();
        }

        /// <summary>
        /// ����һ��ʵ������
        /// </summary>
        /// <param name="setClause">Ҫ���µ��������</param>
        /// <param name="whereClause">ָ��Ҫ���µ�ʵ��</param>
        /// <returns></returns>
        public static Int32 Update(String setClause, String whereClause)
        {
            if (String.IsNullOrEmpty(setClause) || !setClause.Contains("=")) throw new ArgumentException("�Ƿ�����");
            String sql = String.Format("Update {0} Set {1}", Meta.FormatName(Meta.TableName), setClause);
            if (!String.IsNullOrEmpty(whereClause)) sql += " Where " + whereClause;
            return Meta.Execute(sql);
        }

        /// <summary>
        /// ����һ��ʵ������
        /// </summary>
        /// <param name="setNames">���������б�</param>
        /// <param name="setValues">����ֵ�б�</param>
        /// <param name="whereNames">���������б�</param>
        /// <param name="whereValues">����ֵ�б�</param>
        /// <returns>������Ӱ�������</returns>
        public static Int32 Update(String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues)
        {
            String sc = MakeCondition(setNames, setValues, ", ");
            String wc = MakeCondition(whereNames, whereValues, " And ");
            return Update(sc, wc);
        }

        /// <summary>
        /// �����ݿ���ɾ��ָ��ʵ�����
        /// ʵ����Ӧ��ʵ�ָ÷�������һ����������Ψһ����������Ϊ����
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ����������������жϱ�ɾ���˶����У��Ӷ�֪�������Ƿ�ɹ�</returns>
        [WebMethod(Description = "ɾ��")]
        [DataObjectMethod(DataObjectMethodType.Delete, true)]
        public static Int32 Delete(TEntity obj)
        {
            return obj.Delete();
        }

        /// <summary>
        /// �����ݿ���ɾ��ָ��������ʵ�����
        /// </summary>
        /// <param name="whereClause">��������</param>
        /// <returns></returns>
        public static Int32 Delete(String whereClause)
        {
            String sql = String.Format("Delete From {0}", Meta.FormatName(Meta.TableName));
            if (!String.IsNullOrEmpty(whereClause)) sql += " Where " + whereClause;
            return Meta.Execute(sql);
        }

        /// <summary>
        /// �����ݿ���ɾ��ָ�������б��ֵ�б����޶���ʵ�����
        /// </summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns></returns>
        public static Int32 Delete(String[] names, Object[] values)
        {
            return Delete(MakeCondition(names, values, "And"));
        }

        /// <summary>
        /// ��һ��ʵ�������µ����ݿ�
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [WebMethod(Description = "����")]
        [DataObjectMethod(DataObjectMethodType.Update, true)]
        public static Int32 Save(TEntity obj)
        {
            return obj.Save();
        }
        #endregion

        #region ��������
        private static DateTime year1900 = new DateTime(1900, 1, 1);
        private static DateTime year1753 = new DateTime(1753, 1, 1);
        private static DateTime year9999 = new DateTime(9999, 1, 1);

        /// <summary>
        /// ȡ��һ��ֵ��Sqlֵ��
        /// �����ֵ���ַ�������ʱ�����ڸ�ֵǰ��ӵ����ţ�
        /// </summary>
        /// <param name="obj">����</param>
        /// <param name="field">�ֶ�����</param>
        /// <returns>Sqlֵ���ַ�����ʽ</returns>
        [Obsolete("���Ϊʹ��Meta.FormatValue")]
        public static String SqlDataFormat(Object obj, String field)
        {
            //foreach (FieldItem item in Meta.Fields)
            //{
            //    if (!String.Equals(item.Name, field, StringComparison.OrdinalIgnoreCase)) continue;

            //    return SqlDataFormat(obj, item);
            //}
            //return null;

            return Meta.FormatValue(field, obj);
        }

        /// <summary>
        /// ȡ��һ��ֵ��Sqlֵ��
        /// �����ֵ���ַ�������ʱ�����ڸ�ֵǰ��ӵ����ţ�
        /// </summary>
        /// <param name="obj">����</param>
        /// <param name="field">�ֶ�����</param>
        /// <returns>Sqlֵ���ַ�����ʽ</returns>
        [Obsolete("���Ϊʹ��Meta.FormatValue")]
        public static String SqlDataFormat(Object obj, FieldItem field)
        {
            return Meta.FormatValue(field.Name, obj);

            //Boolean isNullable = field.DataObjectField.IsNullable;
            ////String typeName = field.Property.PropertyType.FullName;
            //TypeCode code = Type.GetTypeCode(field.Property.PropertyType);
            ////if (typeName.Contains("String"))
            //if (code == TypeCode.String)
            //{
            //    if (obj == null) return isNullable ? "null" : "''";
            //    if (String.IsNullOrEmpty(obj.ToString()) && isNullable) return "null";
            //    return "'" + obj.ToString().Replace("'", "''") + "'";
            //}
            ////else if (typeName.Contains("DateTime"))
            //else if (code == TypeCode.DateTime)
            //{
            //    if (obj == null) return isNullable ? "null" : "''";
            //    DateTime dt = Convert.ToDateTime(obj);

            //    //if (Meta.DbType == DatabaseType.Access) return "#" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "#";
            //    //if (Meta.DbType == DatabaseType.Access) return Meta.FormatDateTime(dt);

            //    //if (Meta.DbType == DatabaseType.Oracle)
            //    //    return String.Format("To_Date('{0}', 'YYYYMMDDHH24MISS')", dt.ToString("yyyyMMddhhmmss"));
            //    // SqlServer�ܾ������䲻��ʶ��Ϊ 1753 �굽 9999 �������ڵ�ֵ
            //    if (Meta.DbType == DatabaseType.SqlServer)// || Meta.DbType == DatabaseType.SqlServer2005)
            //    {
            //        if (dt < year1753 || dt > year9999) return isNullable ? "null" : "''";
            //    }
            //    if ((dt == DateTime.MinValue || dt == year1900) && isNullable) return "null";
            //    //return "'" + dt.ToString("yyyy-MM-dd HH:mm:ss") + "'";
            //    return Meta.FormatDateTime(dt);
            //}
            ////else if (typeName.Contains("Boolean"))
            //else if (code == TypeCode.Boolean)
            //{
            //    if (obj == null) return isNullable ? "null" : "";
            //    //if (Meta.DbType == DatabaseType.SqlServer || Meta.DbType == DatabaseType.SqlServer2005)
            //    //    return Convert.ToBoolean(obj) ? "1" : "0";
            //    //else
            //    //    return obj.ToString();

            //    if (Meta.DbType == DatabaseType.Access)
            //        return obj.ToString();
            //    else
            //        return Convert.ToBoolean(obj) ? "1" : "0";
            //}
            //else if (field.Property.PropertyType == typeof(Byte[]))
            //{
            //    Byte[] bts = (Byte[])obj;
            //    if (bts == null || bts.Length < 1) return "0x0";

            //    return "0x" + BitConverter.ToString(bts).Replace("-", null);
            //}
            //else
            //{
            //    if (obj == null) return isNullable ? "null" : "";
            //    return obj.ToString();
            //}
        }

        /// <summary>
        /// ��SQLģ���ʽ��ΪSQL���
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <param name="methodType"></param>
        /// <returns>SQL�ַ���</returns>
        public static String SQL(Entity<TEntity> obj, DataObjectMethodType methodType)
        {
            String sql;
            StringBuilder sbNames;
            StringBuilder sbValues;
            Boolean isFirst = true;
            switch (methodType)
            {
                case DataObjectMethodType.Fill:
                    //return String.Format("Select {0} From {1}", Meta.Selects, Meta.TableName);
                    return String.Format("Select * From {0}", Meta.FormatName(Meta.TableName));
                case DataObjectMethodType.Select:
                    sql = DefaultCondition(obj);
                    // û�б�ʶ�к�����������ȡ�������ݵ����
                    if (String.IsNullOrEmpty(sql)) throw new Exception("ʵ����ȱ��������");
                    return String.Format("Select * From {0} Where {1}", Meta.FormatName(Meta.TableName), sql);
                case DataObjectMethodType.Insert:
                    sbNames = new StringBuilder();
                    sbValues = new StringBuilder();
                    // ֻ����û�в������
                    foreach (FieldItem fi in Meta.Fields)
                    {
                        // ��ʶ�в���Ҫ���룬������Ͷ���Ҫ
                        String idv = null;
                        if (fi.DataObjectField.IsIdentity)
                        {
                            idv = Meta.DBO.Db.FormatIdentity(XCodeConfig.GetField(Meta.ThisType, fi.Name), obj[fi.Name]);
                            //if (String.IsNullOrEmpty(idv)) continue;
                            // ������String.Empty��Ϊ�����
                            if (idv == null) continue;
                        }

                        // ��Ĭ��ֵ������û������ֵʱ��������������
                        if (!String.IsNullOrEmpty(fi.Column.DefaultValue) && !obj.Dirtys[fi.Name]) continue;

                        if (!isFirst) sbNames.Append(", "); // �Ӷ���
                        sbNames.Append(Meta.FormatName(fi.ColumnName));
                        if (!isFirst)
                            sbValues.Append(", "); // �Ӷ���
                        else
                            isFirst = false;

                        //// �ɿ����Ͳ����
                        //if (!obj.Dirtys[fi.Name] && fi.DataObjectField.IsNullable)
                        //    sbValues.Append("null");
                        //else
                        //sbValues.Append(SqlDataFormat(obj[fi.Name], fi)); // ����

                        if (!fi.DataObjectField.IsIdentity)
                            sbValues.Append(Meta.FormatValue(fi.Name, obj[fi.Name])); // ����
                        else
                            sbValues.Append(idv);
                    }
                    return String.Format("Insert Into {0}({1}) Values({2})", Meta.FormatName(Meta.TableName), sbNames.ToString(), sbValues.ToString());
                case DataObjectMethodType.Update:
                    sbNames = new StringBuilder();
                    // ֻ����û�и��²���
                    foreach (FieldItem fi in Meta.Fields)
                    {
                        if (fi.DataObjectField.IsIdentity) continue;

                        //�������ж�
                        if (!obj.Dirtys[fi.Name]) continue;

                        if (!isFirst)
                            sbNames.Append(", "); // �Ӷ���
                        else
                            isFirst = false;
                        sbNames.Append(Meta.FormatName(fi.ColumnName));
                        sbNames.Append("=");
                        //sbNames.Append(SqlDataFormat(obj[fi.Name], fi)); // ����
                        sbNames.Append(Meta.FormatValue(fi.Name, obj[fi.Name])); // ����
                    }

                    if (sbNames.Length <= 0) return null;

                    sql = DefaultCondition(obj);
                    if (String.IsNullOrEmpty(sql)) return null;
                    return String.Format("Update {0} Set {1} Where {2}", Meta.FormatName(Meta.TableName), sbNames.ToString(), sql);
                case DataObjectMethodType.Delete:
                    // ��ʶ����Ϊɾ���ؼ���
                    sql = DefaultCondition(obj);
                    if (String.IsNullOrEmpty(sql))
                        return null;
                    return String.Format("Delete From {0} Where {1}", Meta.FormatName(Meta.TableName), sql);
            }
            return null;
        }

        /// <summary>
        /// ���������б��ֵ�б������ѯ������
        /// ���繹����������Ʋ�ѯ������
        /// </summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <param name="action">���Ϸ�ʽ</param>
        /// <returns>�����Ӵ�</returns>
        [WebMethod(Description = "�����ѯ����")]
        public static String MakeCondition(String[] names, Object[] values, String action)
        {
            if (names == null) throw new ArgumentNullException("names", "�����б��ֵ�б���Ϊ��");
            if (values == null) throw new ArgumentNullException("values", "�����б��ֵ�б���Ϊ��");

            if (names.Length != values.Length) throw new ArgumentException("�����б�����ֵ�б�һһ��Ӧ");
            Dictionary<String, FieldItem> fs = new Dictionary<String, FieldItem>();
            foreach (FieldItem fi in Meta.Fields)
                fs.Add(fi.Name.ToLower(), fi);
            StringBuilder sb = new StringBuilder();
            for (Int32 i = 0; i < names.Length; i++)
            {
                FieldItem fi = null;
                if (!fs.TryGetValue(names[i].ToLower(), out fi))
                    throw new ArgumentException("��[" + Meta.ThisType.FullName + "]�в�����[" + names[i] + "]����");

                // ͬʱ����SQL��䡣names�������б�����ת���ɶ�Ӧ���ֶ��б�
                if (i > 0) sb.AppendFormat(" {0} ", action);
                sb.AppendFormat("{0}={1}", Meta.FormatName(fi.ColumnName), Meta.FormatValue(fi.Name, values[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="action">����С�ڵȷ���</param>
        /// <returns></returns>
        public static String MakeCondition(String name, Object value, String action)
        {
            //foreach (FieldItem item in Meta.Fields)
            //{
            //    if (item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            //    {
            //        return String.Format("{0}{1}{2}", item.Name, action, SqlDataFormat(value, item));
            //    }
            //}

            return String.Format("{0}{1}{2}", Meta.FormatName(name), action, Meta.FormatValue(name, value));

            throw new Exception("�Ҳ���[" + name + "]���ԣ�");
        }

        /// <summary>
        /// Ĭ��������
        /// ���б�ʶ�У���ʹ��һ����ʶ����Ϊ������
        /// ������������ʹ��ȫ��������Ϊ������
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>����</returns>
        protected static String DefaultCondition(Entity<TEntity> obj)
        {
            Type t = obj.GetType();
            // Ψһ����Ϊ��ѯ�ؼ���
            List<FieldItem> ps = Meta.Uniques;
            // û�б�ʶ�к�����������ȡ�������ݵ����
            if (ps == null || ps.Count < 1) return null;
            // ��ʶ����Ϊ��ѯ�ؼ���
            if (ps[0].DataObjectField.IsIdentity)
            {
                return String.Format("{0}={1}", Meta.FormatName(ps[0].ColumnName), Meta.FormatValue(ps[0].Name, obj[ps[0].Name]));
            }
            // ������Ϊ��ѯ�ؼ���
            StringBuilder sb = new StringBuilder();
            foreach (FieldItem fi in ps)
            {
                if (sb.Length > 0) sb.Append(" And ");
                sb.Append(Meta.FormatName(fi.ColumnName));
                sb.Append("=");
                //sb.Append(SqlDataFormat(obj[fi.Name], fi));
                sb.Append(Meta.FormatValue(fi.Name, obj[fi.Name]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// �Ѷ���Oqlת����Ϊ��׼TSql
        /// </summary>
        /// <param name="oql">ʵ�����oql</param>
        /// <returns>Sql�ַ���</returns>
        protected static String OqlToSql(String oql)
        {
            if (String.IsNullOrEmpty(oql)) return oql;
            String sql = oql;
            if (Meta.ThisType.Name != Meta.TableName)
                sql = Regex.Replace(sql, @"\b" + Meta.ThisType.Name + @"\b", Meta.TableName, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach (FieldItem fi in Meta.Fields)
                if (fi.Name != fi.ColumnName)
                    sql = Regex.Replace(sql, @"\b" + fi.Name + @"\b", fi.ColumnName, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return sql;
        }

        /// <summary>
        /// ȡ��ָ��ʵ�����͵ķ�ҳSQL
        /// </summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>��ҳSQL</returns>
        protected static String PageSplitSQL(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows)
        {
            //StringBuilder sb = new StringBuilder();
            //sb.Append("Select ");

            //// MSSQL��Access���ݿ⣬�ʺ�ʹ��Top
            //Boolean isTop = (Meta.DbType == DatabaseType.Access || Meta.DbType == DatabaseType.SqlServer || Meta.DbType == DatabaseType.SqlServer2005) && startRowIndex <= 0 && maximumRows > 0;
            //if (isTop) sb.AppendFormat("Top {0} ", maximumRows);

            //sb.Append(String.IsNullOrEmpty(selects) ? "*" : OqlToSql(selects));
            //sb.Append(" From ");
            //sb.Append(Meta.FormatKeyWord(Meta.TableName));
            //if (!String.IsNullOrEmpty(whereClause)) sb.AppendFormat(" Where {0} ", OqlToSql(whereClause));
            //if (!String.IsNullOrEmpty(orderClause)) sb.AppendFormat(" Order By {0} ", OqlToSql(orderClause));
            //String sql = sb.ToString();

            //// �������м�¼
            //if (startRowIndex <= 0 && maximumRows <= 0) return sql;

            //// ʹ��Top
            //if (isTop) return sql;

            //return PageSplitSQL(sql, startRowIndex, maximumRows);

            SelectBuilder builder = new SelectBuilder();
            builder.Column = selects;
            //builder.Table = Meta.TableName;
            builder.Table = Meta.FormatName(Meta.TableName);
            builder.OrderBy = orderClause;
            // ���ǣ�ĳЩ��Ŀ�п�����where��ʹ����GroupBy���ڷ�ҳʱ���ܱ���
            builder.Where = whereClause;

            // �������м�¼
            if (startRowIndex <= 0 && maximumRows <= 0) return builder.ToString();

            return PageSplitSQL(builder, startRowIndex, maximumRows);
        }

        /// <summary>
        /// ȡ��ָ��ʵ�����͵ķ�ҳSQL
        /// </summary>
        /// <param name="builder">��ѯ������</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>��ҳSQL</returns>
        protected static String PageSplitSQL(SelectBuilder builder, Int32 startRowIndex, Int32 maximumRows)
        {
            FieldItem fi = Meta.Unique;
            String keyColumn = null;
            if (fi != null)
            {
                keyColumn = fi.ColumnName;
                // ����Desc��ǣ���ʹ��MaxMin��ҳ�㷨����ʶ�У���һ������Ϊ��������
                if (fi.DataObjectField.IsIdentity || fi.Property.PropertyType == typeof(Int32)) keyColumn += " Desc";

                if (String.IsNullOrEmpty(builder.OrderBy)) builder.OrderBy = keyColumn;
            }
            return Meta.PageSplit(builder, startRowIndex, maximumRows, keyColumn);
        }
        #endregion

        #region ��ȡ/���� �ֶ�ֵ
        /// <summary>
        /// ��ȡ/���� �ֶ�ֵ��
        /// һ������������ʵ�֡�
        /// ����ʵ�������д���������Ա��ⷢ�������������ġ�
        /// �����Ѿ�ʵ����ͨ�õĿ��ٷ��ʣ�����������Ȼ��д�������ӿ��ƣ�
        /// �����ֶ�����������ǰ�����_������Ҫ����ʵ���ֶβ������������ʣ�����һ�ɰ����Դ���
        /// </summary>
        /// <param name="name">�ֶ���</param>
        /// <returns></returns>
        public override Object this[String name]
        {
            get
            {
                //ƥ���ֶ�
                if (Meta.FieldNames.Contains(name))
                {
                    FieldInfoX field = FieldInfoX.Create(this.GetType(), "_" + name);
                    if (field != null) return field.GetValue(this);
                }

                //����ƥ������
                PropertyInfoX property = PropertyInfoX.Create(this.GetType(), name);
                if (property != null) return property.GetValue(this);

                throw new ArgumentException("��[" + this.GetType().FullName + "]�в�����[" + name + "]����");
            }
            set
            {
                //ƥ���ֶ�
                if (Meta.FieldNames.Contains(name))
                {
                    FieldInfoX field = FieldInfoX.Create(this.GetType(), "_" + name);
                    if (field != null)
                    {
                        field.SetValue(this, value);
                        return;
                    }
                }

                //����ƥ������
                PropertyInfoX property = PropertyInfoX.Create(this.GetType(), name);
                if (property != null)
                {
                    property.SetValue(this, value);
                    return;
                }

                foreach (FieldItem fi in Meta.AllFields)
                    if (fi.Name == name) { fi.Property.SetValue(this, value, null); return; }

                throw new ArgumentException("��[" + this.GetType().FullName + "]�в�����[" + name + "]����");
            }
        }
        #endregion

        #region ���뵼��XML
        /// <summary>
        /// ����Xml���л���
        /// </summary>
        /// <returns></returns>
        protected override XmlSerializer CreateXmlSerializer()
        {
            XmlAttributeOverrides ovs = new XmlAttributeOverrides();
            TEntity entity = new TEntity();
            foreach (FieldItem item in Meta.Fields)
            {
                XmlAttributes atts = new XmlAttributes();
                atts.XmlAttribute = new XmlAttributeAttribute();
                atts.XmlDefaultValue = entity[item.Name];
                ovs.Add(item.Property.DeclaringType, item.Name, atts);
            }
            return new XmlSerializer(this.GetType(), ovs);
        }

        /// <summary>
        /// ����
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static TEntity FromXml(String xml)
        {
            if (!String.IsNullOrEmpty(xml)) xml = xml.Trim();

            StopExtend = true;
            try
            {
                IEntityOperate factory = EntityFactory.CreateOperate(typeof(TEntity));
                XmlSerializer serial = ((TEntity)factory).CreateXmlSerializer();
                using (StringReader reader = new StringReader(xml))
                {
                    return serial.Deserialize(reader) as TEntity;
                }
            }
            finally { StopExtend = false; }
        }

        ///// <summary>
        ///// �߼����л�
        ///// </summary>
        ///// <param name="writer">�ı���д��</param>
        ///// <param name="propertyAsAttribute">������ΪXml���Խ������л�</param>
        ///// <param name="hasNamespace"></param>
        //public virtual void Serialize(TextWriter writer, Boolean propertyAsAttribute, Boolean hasNamespace)
        //{
        //    XmlAttributeOverrides overrides = null;
        //    overrides = new XmlAttributeOverrides();
        //    Type type = this.GetType();
        //    //IList<FieldItem> fs = FieldItem.GetDataObjectFields(type);
        //    PropertyInfo[] pis = type.GetProperties();
        //    //foreach (FieldItem item in fs)
        //    foreach (PropertyInfo item in pis)
        //    {
        //        if (!item.CanRead) continue;

        //        if (propertyAsAttribute)
        //        {
        //            XmlAttributeAttribute att = new XmlAttributeAttribute();
        //            XmlAttributes xas = new XmlAttributes();
        //            xas.XmlAttribute = att;
        //            overrides.Add(type, item.Name, xas);
        //        }
        //        else
        //        {
        //            XmlAttributes xas = new XmlAttributes();
        //            xas.XmlElements.Add(new XmlElementAttribute());
        //            overrides.Add(type, item.Name, xas);
        //        }
        //    }

        //    XmlSerializer serial = new XmlSerializer(this.GetType(), overrides);
        //    using (MemoryStream stream = new MemoryStream())
        //    {
        //        serial.Serialize(writer, this);
        //        writer.Close();
        //    }
        //}

        ///// <summary>
        ///// �߼����л�
        ///// </summary>
        ///// <param name="propertyAsAttribute">������ΪXml���Խ������л�</param>
        ///// <param name="hasNamespace"></param>
        ///// <returns></returns>
        //public virtual String Serialize(Boolean propertyAsAttribute, Boolean hasNamespace)
        //{
        //    using (MemoryStream stream = new MemoryStream())
        //    {
        //        StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
        //        Serialize(writer, propertyAsAttribute, hasNamespace);
        //        writer.Close();
        //        return Encoding.UTF8.GetString(stream.ToArray());
        //    }
        //}
        #endregion

        #region ��¡
        /// <summary>
        /// ������ǰ����Ŀ�¡���󣬽����������ֶ�
        /// </summary>
        /// <returns></returns>
        public override Object Clone()
        {
            return CloneEntity();
        }

        /// <summary>
        /// ��¡ʵ�塣������ǰ����Ŀ�¡���󣬽����������ֶ�
        /// </summary>
        /// <returns></returns>
        public virtual TEntity CloneEntity()
        {
            //TEntity obj = new TEntity();
            TEntity obj = CreateInstance();
            foreach (FieldItem fi in Meta.Fields)
            {
                obj[fi.Name] = this[fi.Name];
            }
            return obj;
        }
        #endregion

        #region ����
        /// <summary>
        /// �����ء�
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Meta.FieldNames.Contains("Name"))
                return this["Name"] == null ? null : this["Name"].ToString();
            else if (Meta.FieldNames.Contains("ID"))
                return this["ID"] == null ? null : this["ID"].ToString();
            else
                return "ʵ��" + Meta.ThisType.Name;
        }
        #endregion

        #region ������
        //[NonSerialized]
        //private DirtyCollection _Dirtys;
        ///// <summary>�����ԡ��洢��Щ���Ե����ݱ��޸Ĺ��ˡ�</summary>
        //[XmlIgnore]
        //protected DirtyCollection Dirtys
        //{
        //    get
        //    {
        //        if (_Dirtys == null) _Dirtys = new DirtyCollection();
        //        return _Dirtys;
        //    }
        //    set { _Dirtys = value; }
        //}

        /// <summary>
        /// �����������ݵ�������
        /// </summary>
        /// <param name="isDirty">�ı������Ե����Ը���</param>
        /// <returns></returns>
        protected override Int32 SetDirty(Boolean isDirty)
        {
            Int32 count = 0;
            foreach (String item in Meta.FieldNames)
            {
                Boolean b = false;
                if (isDirty)
                {
                    if (!Dirtys.TryGetValue(item, out b) || !b)
                    {
                        Dirtys[item] = true;
                        count++;
                    }
                }
                else
                {
                    if (Dirtys == null || Dirtys.Count < 1) break;
                    if (Dirtys.TryGetValue(item, out b) && b)
                    {
                        Dirtys[item] = false;
                        count++;
                    }
                }
            }
            return count;
        }

        ///// <summary>
        ///// ���Ըı䡣����ʱ�ǵõ��û���ĸ÷��������������������ԣ��������ݽ��޷�Update�����ݿ⡣
        ///// </summary>
        ///// <param name="fieldName">�ֶ���</param>
        ///// <param name="newValue">������ֵ</param>
        ///// <returns>�Ƿ�����ı�</returns>
        //protected virtual Boolean OnPropertyChange(String fieldName, Object newValue)
        //{
        //    Dirtys[fieldName] = true;
        //    return true;
        //}
        #endregion

        #region ��չ����
        /// <summary>
        /// ��ȡ�����ڵ�ǰʵ�������չ����
        /// </summary>
        /// <typeparam name="TResult">��������</typeparam>
        /// <param name="key">��ֵ</param>
        /// <param name="func">�ص�</param>
        /// <param name="cacheDefault">�Ƿ񻺴�Ĭ��ֵ����ѡ������Ĭ�ϻ���</param>
        /// <returns></returns>
        protected TResult GetExtend<TResult>(String key, Func<String, Object> func, Boolean cacheDefault = true)
        {
            return GetExtend<TEntity, TResult>(key, func);
        }

        /// <summary>
        /// ���������ڵ�ǰʵ�������չ����
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected void SetExtend(String key, Object value)
        {
            SetExtend<TEntity>(key, value);
        }
        #endregion

        #region �Զ��޸����ݱ�ṹ
        //private static Object schemasLock = new Object();
        //private static Boolean hasChecked = false;
        ///// <summary>
        ///// ������ݱ�ܹ��Ƿ��ѱ��޸�
        ///// </summary>
        //private static void CheckModify()
        //{
        //    if (hasChecked) return;
        //    lock (schemasLock)
        //    {
        //        if (hasChecked) return;

        //        DatabaseSchema schema = new DatabaseSchema(Meta.ConnName, Meta.ThisType);
        //        schema.BeginCheck();

        //        hasChecked = true;
        //    }
        //}
        #endregion
    }
}