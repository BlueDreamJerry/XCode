using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Web.Services;
using System.Xml.Serialization;
using NewLife.IO;
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
            EntityFactory.Register(Meta.ThisType, new EntityOperate());

            // 1�����Գ�ʼ����ʵ�����͵Ĳ�������
            // 2��CreateOperate����ʵ����һ��TEntity���󣬴Ӷ�����TEntity�ľ�̬���캯����
            // ����ʵ��Ӧ���У�ֱ�ӵ���Entity�ľ�̬����ʱ��û������TEntity�ľ�̬���캯����
            TEntity entity = new TEntity();

            ////! ��ʯͷ 2011-03-14 ���¹��̸�Ϊ�첽����
            ////  ��ȷ�ϣ���ʵ���ྲ̬���캯����ʹ����EntityFactory.CreateOperate(Type)����ʱ�����ܳ���������
            ////  ��Ϊ���߶�������EntityFactory�е�op_cache����CreateOperate(Type)�õ�op_cache�󣬻���Ҫ�ȴ���ǰ��̬���캯��ִ����ɡ�
            ////  ��ȷ���������Ƿ��������֢
            //ThreadPool.QueueUserWorkItem(delegate
            //{
            //    EntityFactory.CreateOperate(Meta.ThisType, entity);
            //});
        }

        /// <summary>
        /// ����ʵ��
        /// </summary>
        /// <returns></returns>
        protected virtual TEntity CreateInstance()
        {
            //return new TEntity();
            // new TEntity�ᱻ����ΪActivator.CreateInstance<TEntity>()��������Activator.CreateInstance()��
            // Activator.CreateInstance()�л��湦�ܣ������͵��Ǹ�û��
            return Activator.CreateInstance(Meta.ThisType) as TEntity;
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
        public static EntityList<TEntity> LoadData(DataTable dt)
        {
            if (dt == null || dt.Rows.Count < 1) return null;

            // ׼����ʵ���б�
            EntityList<TEntity> list = new EntityList<TEntity>(dt.Rows.Count);

            // ���㶼����Щ�ֶο��Լ������ݣ�Ĭ����ʹ����BindColumn���Ե����ԣ�Ȼ����Ǳ������
            // ��Ȼ��Ҫ���ݼ���������в��У�Ҳ����ȡʵ��������ݼ��Ľ���
            List<FieldItem> ps = CheckColumn(dt);

            // ����ʵ������ߣ�����ʵ������ߴ���ʵ�����
            IEntityOperate factory = Meta.Factory;

            // ����ÿһ�����ݣ�����Ϊʵ��
            foreach (DataRow dr in dt.Rows)
            {
                //TEntity obj = new TEntity();
                // ��ʵ������ߴ���ʵ�������Ϊʵ������߿��ܸ���
                TEntity obj = factory.Create() as TEntity;
                obj.LoadData(dr, ps);
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

            // ���㶼����Щ�ֶο��Լ�������
            List<FieldItem> ps = CheckColumn(dr.Table);
            LoadData(dr, ps);
        }

        static String[] TrueString = new String[] { "true", "y", "yes", "1" };
        static String[] FalseString = new String[] { "false", "n", "no", "0" };

        /// <summary>
        /// ��һ�������ж���������ݡ�ָ��Ҫ�������ݵ��ֶΡ�
        /// </summary>
        /// <param name="dr">������</param>
        /// <param name="ps">Ҫ�������ݵ��ֶ�</param>
        /// <returns></returns>
        private void LoadData(DataRow dr, IList<FieldItem> ps)
        {
            if (dr == null) return;

            // ���û�д���Ҫ�������ݵ��ֶΣ���ʹ��ȫ����������
            // �������һ�㲻�ᷢ�������Ҳ���÷�������Ϊ���п��ܵ��±���
            if (ps == null || ps.Count < 1) ps = Meta.Fields;

            foreach (FieldItem fi in ps)
            {
                // ����dr[fi.ColumnName]��Ϊһ��
                Object v = dr[fi.ColumnName];
                Object v2 = this[fi.Name];

                // ��������ͬ���ݵĸ�ֵ
                if (Object.Equals(v, v2)) continue;

                if (fi.Type == typeof(String))
                {
                    // ��������ַ����Կ��ַ����ĸ�ֵ
                    if (v != null && String.IsNullOrEmpty(v.ToString()))
                    {
                        if (v2 == null || String.IsNullOrEmpty(v2.ToString())) continue;
                    }
                }
                else if (fi.Type == typeof(Boolean))
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
        }

        /// <summary>
        /// ���ʵ�����е���Щ�ֶ������ݱ���
        /// </summary>
        /// <param name="dt">���ݱ�</param>
        /// <returns></returns>
        private static List<FieldItem> CheckColumn(DataTable dt)
        {
            List<FieldItem> ps = new List<FieldItem>();
            foreach (FieldItem item in Meta.AllFields)
            {
                if (String.IsNullOrEmpty(item.ColumnName)) continue;

                if (dt.Columns.Contains(item.ColumnName)) ps.Add(item);
            }
            return ps;
        }

        /// <summary>
        /// �����ݸ��Ƶ������ж����С�
        /// </summary>
        /// <param name="dr">������</param>
        public virtual DataRow ToData(ref DataRow dr)
        {
            if (dr == null) return null;

            foreach (FieldItem fi in Meta.AllFields)
            {
                // ���dr���Ƿ��и����Ե��С����ǵ�Select�����ǲ������ģ���ʱ��ֻ��Ҫ�ֲ����
                if (dr.Table.Columns.Contains(fi.ColumnName))
                    dr[fi.ColumnName] = this[fi.Name];
            }
            return dr;
        }
        #endregion

        #region ����
        /// <summary>
        /// �������ݣ�ͨ������OnInsertʵ�֣�����������������֤�����񱣻�֧�֣���������ʵ���¼�֧�֡�
        /// </summary>
        /// <returns></returns>
        public override Int32 Insert()
        {
            Valid(true);

            Meta.BeginTrans();
            try
            {
                Int32 rs = OnInsert();

                Meta.Commit();

                return rs;
            }
            catch { Meta.Rollback(); throw; }
        }

        /// <summary>
        /// �Ѹö���־û������ݿ⡣�÷����ṩԭ�������ݲ��������������أ���������Insert���档
        /// </summary>
        /// <returns></returns>
        protected virtual Int32 OnInsert()
        {
            String sql = SQL(this, DataObjectMethodType.Insert);
            if (String.IsNullOrEmpty(sql)) return 0;

            Int32 rs = 0;

            //����Ƿ��б�ʶ�У���ʶ����Ҫ���⴦��
            FieldItem field = Meta.Table.Identity;
            if (field != null && field.IsIdentity)
            {
                Int64 res = Meta.InsertAndGetIdentity(sql);
                if (res > 0) this[field.Name] = res;
                rs = res > 0 ? 1 : 0;
            }
            else
            {
                rs = Meta.Execute(sql);
            }

            //��������ݣ������������ε���Save����ظ��ύ
            if (Dirtys != null)
            {
                Dirtys.Clear();
                Dirtys = null;
            }
            return rs;
        }

        /// <summary>
        /// �������ݣ�ͨ������OnUpdateʵ�֣�����������������֤�����񱣻�֧�֣���������ʵ���¼�֧�֡�
        /// </summary>
        /// <returns></returns>
        public override Int32 Update()
        {
            Valid(false);

            Meta.BeginTrans();
            try
            {
                Int32 rs = OnUpdate();

                Meta.Commit();

                return rs;
            }
            catch { Meta.Rollback(); throw; }
        }

        /// <summary>
        /// �������ݿ�
        /// </summary>
        /// <returns></returns>
        protected virtual Int32 OnUpdate()
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
        /// ɾ�����ݣ�ͨ������OnDeleteʵ�֣�����������������֤�����񱣻�֧�֣���������ʵ���¼�֧�֡�
        /// </summary>
        /// <returns></returns>
        public override Int32 Delete()
        {
            Meta.BeginTrans();
            try
            {
                Int32 rs = OnDelete();

                Meta.Commit();

                return rs;
            }
            catch { Meta.Rollback(); throw; }
        }

        /// <summary>
        /// �����ݿ���ɾ���ö���
        /// </summary>
        /// <returns></returns>
        protected virtual Int32 OnDelete()
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
            FieldItem fi = Meta.Table.Identity;
            if (fi != null) return Convert.ToInt64(this[fi.Name]) > 0 ? Update() : Insert();

            fi = Meta.Unique;
            if (fi != null) return IsNullKey(this[fi.Name]) ? Insert() : Update();

            return FindCount(DefaultCondition(this), null, null, 0, 0) > 0 ? Update() : Insert();
        }

        /// <summary>
        /// ��֤���ݣ�ͨ���׳��쳣�ķ�ʽ��ʾ��֤ʧ�ܡ�������д�ߵ��û����ʵ�֣���Ϊ�������ܸ��������ֶε����Խ���������֤��
        /// </summary>
        /// <param name="isNew">�Ƿ�������</param>
        public virtual void Valid(Boolean isNew)
        {
        }

        /// <summary>
        /// ����ָ������������Ƿ��Ѵ��ڣ��������ڣ��׳�ArgumentOutOfRangeException�쳣
        /// </summary>
        /// <param name="names"></param>
        public virtual void CheckExist(params String[] names)
        {
            if (Exist(names))
            {
                StringBuilder sb = new StringBuilder();
                String name = null;
                for (int i = 0; i < names.Length; i++)
                {
                    if (sb.Length > 0) sb.Append("��");

                    FieldItem field = Meta.Table.FindByName(names[i]);
                    if (field != null) name = field.Description;
                    if (String.IsNullOrEmpty(name)) name = names[i];

                    sb.AppendFormat("{0}={1}", name, this[names[i]]);
                }

                name = Meta.Table.Description;
                if (String.IsNullOrEmpty(name)) name = Meta.ThisType.Name;
                sb.AppendFormat(" ��{0}�Ѵ��ڣ�", name);

                throw new ArgumentOutOfRangeException(names[0], this[names[0]], sb.ToString());
            }
        }

        /// <summary>
        /// ����ָ����������ݣ����������Ƿ��Ѵ���
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        public virtual Boolean Exist(params String[] names)
        {
            // ����ָ�����������з��ϵ����ݣ�Ȼ��ȶԡ�
            // ��Ȼ��Ҳ����ͨ��ָ������������ϣ��ҵ�ӵ��ָ���������ǲ��ǵ�ǰ���������ݣ�ֻ���¼����
            Object[] values = new Object[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                values[i] = this[names[i]];
            }

            FieldItem field = Meta.Unique;
            // ����ǿ������������ֱ���жϼ�¼���ķ�ʽ���Լӿ��ٶ�
            if (IsNullKey(this[field.Name])) return FindCount(names, values) > 0;

            EntityList<TEntity> list = FindAll(names, values);
            if (list == null) return false;
            if (list.Count > 1) return true;

            return !Object.Equals(this[field.Name], list[0][field.Name]);
        }

        //public event EventHandler<CancelEventArgs> Inserting;
        //public event EventHandler<EventArgs<Int32>> Inserted;
        #endregion

        #region ���ҵ���ʵ��
        /// <summary>
        /// ���������Լ���Ӧ��ֵ�����ҵ���ʵ��
        /// </summary>
        /// <param name="name">��������</param>
        /// <param name="value">����ֵ</param>
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
        /// <param name="names">�������Ƽ���</param>
        /// <param name="values">����ֵ����</param>
        /// <returns></returns>
        public static TEntity Find(String[] names, Object[] values)
        {
            if (names.Length == 1)
            {
                FieldItem field = Meta.Table.FindByName(names[0]);
                if (field != null && (field.IsIdentity || field.PrimaryKey))
                {
                    // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
                    if (IsNullKey(values[0])) return null;

                    // ��������������ѯ����¼���϶���Ψһ�ģ�����Ҫָ����¼��������
                    //IList<TEntity> list = FindAll(MakeCondition(field, values[0], "="), null, null, 0, 0);
                    SelectBuilder builder = new SelectBuilder();
                    builder.Table = Meta.FormatName(Meta.TableName);
                    builder.Where = MakeCondition(field, values[0], "=");
                    IList<TEntity> list = FindAll(builder.ToString());
                    if (list == null || list.Count < 1)
                        return null;
                    else
                        return list[0];
                }
            }

            return Find(MakeCondition(names, values, "And"));
        }

        /// <summary>
        /// �����������ҵ���ʵ��
        /// </summary>
        /// <param name="whereClause">��ѯ����</param>
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
        /// <param name="key">Ψһ������ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKey(Object key)
        {
            FieldItem field = Meta.Unique;
            if (field == null) throw new ArgumentNullException("Meta.Unique", "FindByKey����Ҫ��ñ���Ψһ������");

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
            if (IsNullKey(key)) return null;

            return Find(field.Name, key);
        }

        /// <summary>
        /// ����������ѯһ��ʵ��������ڱ��༭
        /// </summary>
        /// <param name="key">Ψһ������ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKeyForEdit(Object key)
        {
            FieldItem field = Meta.Unique;
            if (field == null) throw new ArgumentNullException("Meta.Unique", "FindByKeyForEdit����Ҫ��ñ���Ψһ������");

            // ����Ϊ��ʱ��������ʵ��
            if (key == null)
            {
                //IEntityOperate factory = EntityFactory.CreateOperate(Meta.ThisType);
                return Meta.Factory.Create() as TEntity;
            }

            Type type = field.Type;

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ��������ʵ��
            if (IsNullKey(key))
            {
                if (IsInt(type) && !field.IsIdentity && DAL.Debug) DAL.WriteLog("{0}��{1}�ֶ����������������Ƿ�����������������", Meta.TableName, field.ColumnName);

                return Meta.Factory.Create() as TEntity;
            }

            // ���⣬һ�ɷ��� ����ֵ����ʹ�����ǿա������������Ҳ������ݵ�����¸������ؿգ���Ϊ�������Ҳ������ݶ��ѣ���������ʵ���ᵼ��ǰ����Ϊ��������������
            TEntity entity = Find(field.Name, key);

            // �ж�ʵ��
            if (entity == null)
            {
                String msg = null;
                if (IsNullKey(key))
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}������δ��������������", key, Meta.Table.Description);
                else
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}��", key, Meta.Table.Description);

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

        /// <summary>
        /// ָ�����Ƿ�Ϊ�ա�һ��ҵ��ϵͳ��Ʋ���������Ϊ�գ�����������0���ַ����Ŀ�
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        static Boolean IsNullKey(Object key)
        {
            if (key == null) return true;

            Type type = key.GetType();

            //if (IsInt(type))
            //{
            //    int i = (int)key;
            //    //������Ҫת������ȷ���ͷ������������ת���쳣
            //    return ((Int64)i) <= 0;
            //}
            //if (IsInt(type))
            //{
            //����key��ʵ���������������Ƶ����������Ա������ʵ�ʴ���Ĳ������ͷֱ����װ�����
            //������������ͷֱ���лᵼ������ת��ʧ���׳��쳣
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Int16: return ((Int16)key) <= 0;
                case TypeCode.Int32: return ((Int32)key) <= 0;
                case TypeCode.Int64: return ((Int64)key) <= 0;
                case TypeCode.UInt16: return ((UInt16)key) <= 0;
                case TypeCode.UInt32: return ((UInt32)key) <= 0;
                case TypeCode.UInt64: return ((UInt64)key) <= 0;
                case TypeCode.String: return String.IsNullOrEmpty((String)key);
                default: break;
            }
            //}
            //if (type == typeof(String)) return String.IsNullOrEmpty((String)key);

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
            return FindAll(SQL(null, DataObjectMethodType.Fill));
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
            //Int32 count = Meta.Count;
            //if (startRowIndex > 500000 && count > 1000000)

            // �����Ż���������ÿ�ζ�����Meta.Count�������γ�һ�β�ѯ����Ȼ��β�ѯʱ����Ĳ���
            // ���Ǿ��������ѯ��������Ҫ�������Ƶĺ��������Ż�����Ȼ�����startRowIndex���ᵲס99%���ϵ��˷�
            Int32 count = 0;
            if (startRowIndex > 500000 && (count = Meta.Count) > 1000000)
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
                    FieldItem fi = Meta.Unique;
                    if (String.IsNullOrEmpty(order) && fi != null && fi.IsIdentity) order = fi.Name + " Desc";

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
            if (String.IsNullOrEmpty(name)) return FindAll(null, orderClause, null, startRowIndex, maximumRows);

            FieldItem field = Meta.Table.FindByName(name);
            if (field != null && (field.IsIdentity || field.PrimaryKey))
            {
                // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
                if (IsNullKey(value)) return null;

                // ��������������ѯ����¼���϶���Ψһ�ģ�����Ҫָ����¼��������
                //return FindAll(MakeCondition(field, value, "="), null, null, 0, 0);
                SelectBuilder builder = new SelectBuilder();
                builder.Table = Meta.FormatName(Meta.TableName);
                builder.Where = MakeCondition(field, value, "=");
                return FindAll(builder.ToString());
            }

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
        #endregion

        #region �����ѯ
        /// <summary>
        /// ���������Լ���Ӧ��ֵ���ڻ����в��ҵ���ʵ��
        /// </summary>
        /// <param name="name">��������</param>
        /// <param name="value">����ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindWithCache(String name, Object value)
        {
            return Entity<TEntity>.Meta.Cache.Entities.Find(name, value);
        }

        /// <summary>
        /// �������л���
        /// </summary>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllWithCache()
        {
            return Entity<TEntity>.Meta.Cache.Entities;
        }

        /// <summary>
        /// ���������Լ���Ӧ��ֵ���ڻ����л�ȡ����ʵ�����
        /// </summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllWithCache(String name, Object value)
        {
            return Entity<TEntity>.Meta.Cache.Entities;
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

            //return Meta.QueryCount(SQL(null, DataObjectMethodType.Fill));
            //return Meta.Count;

            //SelectBuilder sb = new SelectBuilder(Meta.DbType);
            //sb.Column = "Count(*)";
            //sb.Table = Meta.FormatName(Meta.TableName);

            //return Meta.QueryCount(sb);

            return FindCount(null, null, null, 0, 0);
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
            ////�������Where�־䣬ֱ�ӵ���FindCount�����Խ��������㷨ȡ���ܼ�¼��
            //if (String.IsNullOrEmpty(whereClause)) return FindCount();

            //String sql = PageSplitSQL(whereClause, null, selects, 0, 0);
            //return Meta.QueryCount(sql);

            SelectBuilder sb = new SelectBuilder(Meta.DbType);
            //sb.Column = "Count(*)";
            sb.Table = Meta.FormatName(Meta.TableName);
            sb.Where = whereClause;

            return Meta.QueryCount(sb);
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
        [DisplayName("����")]
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
        [DisplayName("����")]
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
        //[DataObjectMethod(DataObjectMethodType.Update, true)]
        public static Int32 Save(TEntity obj)
        {
            return obj.Save();
        }
        #endregion

        #region ��������
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
                        if (fi.IsIdentity)
                        {
                            idv = Meta.DBO.Db.FormatIdentity(fi.Field, obj[fi.Name]);
                            //if (String.IsNullOrEmpty(idv)) continue;
                            // ������String.Empty��Ϊ�����
                            if (idv == null) continue;
                        }

                        // ��Ĭ��ֵ������û������ֵʱ��������������
                        if (!String.IsNullOrEmpty(fi.DefaultValue) && !obj.Dirtys[fi.Name]) continue;

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

                        if (!fi.IsIdentity)
                            sbValues.Append(Meta.FormatValue(fi, obj[fi.Name])); // ����
                        else
                            sbValues.Append(idv);
                    }
                    return String.Format("Insert Into {0}({1}) Values({2})", Meta.FormatName(Meta.TableName), sbNames.ToString(), sbValues.ToString());
                case DataObjectMethodType.Update:
                    sbNames = new StringBuilder();
                    // ֻ����û�и��²���
                    foreach (FieldItem fi in Meta.Fields)
                    {
                        if (fi.IsIdentity) continue;

                        //�������ж�
                        if (!obj.Dirtys[fi.Name]) continue;

                        if (!isFirst)
                            sbNames.Append(", "); // �Ӷ���
                        else
                            isFirst = false;
                        sbNames.Append(Meta.FormatName(fi.ColumnName));
                        sbNames.Append("=");
                        //sbNames.Append(SqlDataFormat(obj[fi.Name], fi)); // ����
                        sbNames.Append(Meta.FormatValue(fi, obj[fi.Name])); // ����
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

            StringBuilder sb = new StringBuilder();
            for (Int32 i = 0; i < names.Length; i++)
            {
                FieldItem fi = Meta.Table.FindByName(names[i]);
                if (fi == null) throw new ArgumentException("��[" + Meta.ThisType.FullName + "]�в�����[" + names[i] + "]����");

                // ͬʱ����SQL��䡣names�������б�����ת���ɶ�Ӧ���ֶ��б�
                if (i > 0) sb.AppendFormat(" {0} ", action);
                //sb.AppendFormat("{0}={1}", Meta.FormatName(fi.ColumnName), Meta.FormatValue(fi, values[i]));
                sb.Append(MakeCondition(fi, values[i], "="));
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
            FieldItem field = Meta.Table.FindByName(name);
            if (field == null) return String.Format("{0}{1}{2}", Meta.FormatName(name), action, Meta.FormatValue(name, value));

            return MakeCondition(field, value, action);
        }

        /// <summary>
        /// ��������
        /// </summary>
        /// <param name="field">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="action">����С�ڵȷ���</param>
        /// <returns></returns>
        public static String MakeCondition(FieldItem field, Object value, String action)
        {
            if (!String.IsNullOrEmpty(action) && action.Contains("{0}"))
                return Meta.FormatName(field.ColumnName) + String.Format(action, Meta.FormatValue(field, value));
            else
                return String.Format("{0}{1}{2}", Meta.FormatName(field.ColumnName), action, Meta.FormatValue(field, value));
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
            // ��ʶ����Ϊ��ѯ�ؼ���
            FieldItem fi = Meta.Table.Identity;
            if (fi != null) return MakeCondition(fi, obj[fi.Name], "=");

            // ������Ϊ��ѯ�ؼ���
            FieldItem[] ps = Meta.Table.PrimaryKeys;
            // û�б�ʶ�к�����������ȡ�������ݵ����
            if (ps == null || ps.Length < 1) return null;

            StringBuilder sb = new StringBuilder();
            foreach (FieldItem item in ps)
            {
                if (sb.Length > 0) sb.Append(" And ");
                sb.Append(Meta.FormatName(item.ColumnName));
                sb.Append("=");
                sb.Append(Meta.FormatValue(item, obj[item.Name]));
            }
            return sb.ToString();
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
            SelectBuilder builder = new SelectBuilder();
            builder.Column = selects;
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
                if (fi.IsIdentity || IsInt(fi.Type))
                {
                    keyColumn += " Desc";

                    // Ĭ�ϻ�ȡ����ʱ��������Ҫָ����װ�����ֶν��򣬷���ʹ��ϰ��
                    // ��GroupByҲ���ܼ�����
                    if (String.IsNullOrEmpty(builder.OrderBy) && String.IsNullOrEmpty(builder.GroupBy)) builder.OrderBy = keyColumn;
                }
                //if (fi.IsIdentity || IsInt(fi.Type)) keyColumn += " Unknown";

                //if (String.IsNullOrEmpty(builder.OrderBy)) builder.OrderBy = keyColumn;
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

                Object obj = null;
                if (Extends.TryGetValue(name, out obj)) return obj;

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

                //foreach (FieldItem fi in Meta.AllFields)
                //    if (fi.Name == name) { fi.Property.SetValue(this, value, null); return; }

                if (Extends.ContainsKey(name))
                    Extends[name] = value;
                else
                    Extends.Add(name, value);

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
            // ��ÿһ���������Լ���XmlĬ��ֵ���ԣ���Xml���л�ʱ�ܿ�������Ĭ��ֵ��ͬ���������ԣ�����Xml��С
            XmlAttributeOverrides ovs = new XmlAttributeOverrides();
            TEntity entity = new TEntity();
            foreach (FieldItem item in Meta.Fields)
            {
                XmlAttributes atts = new XmlAttributes();
                atts.XmlAttribute = new XmlAttributeAttribute();
                atts.XmlDefaultValue = entity[item.Name];
                ovs.Add(item.DeclaringType, item.Name, atts);
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
                //IEntityOperate factory = EntityFactory.CreateOperate(typeof(TEntity));
                XmlSerializer serial = ((TEntity)Meta.Factory).CreateXmlSerializer();
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

        #region ���뵼��Json
        /// <summary>
        /// ����
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static TEntity FromJson(String json)
        {
            return new Json().Deserialize<TEntity>(json);
        }
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
        #endregion

        #region ��չ����
        /// <summary>
        /// ��ȡ�����ڵ�ǰʵ�������չ����
        /// </summary>
        /// <typeparam name="TResult">��������</typeparam>
        /// <param name="key">��</param>
        /// <param name="func">�ص�</param>
        /// <returns></returns>
        protected TResult GetExtend<TResult>(String key, Func<String, Object> func)
        {
            return GetExtend<TEntity, TResult>(key, func);
        }

        /// <summary>
        /// ��ȡ�����ڵ�ǰʵ�������չ����
        /// </summary>
        /// <typeparam name="TResult">��������</typeparam>
        /// <param name="key">��</param>
        /// <param name="func">�ص�</param>
        /// <param name="cacheDefault">�Ƿ񻺴�Ĭ��ֵ����ѡ������Ĭ�ϻ���</param>
        /// <returns></returns>
        protected TResult GetExtend<TResult>(String key, Func<String, Object> func, Boolean cacheDefault)
        {
            return GetExtend<TEntity, TResult>(key, func);
        }

        /// <summary>
        /// ���������ڵ�ǰʵ�������չ����
        /// </summary>
        /// <param name="key">��</param>
        /// <param name="value">ֵ</param>
        protected void SetExtend(String key, Object value)
        {
            SetExtend<TEntity>(key, value);
        }
        #endregion
    }
}