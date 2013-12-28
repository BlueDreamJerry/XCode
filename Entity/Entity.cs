using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using NewLife.IO;
using NewLife.Reflection;
using NewLife.Xml;
using XCode.Common;
using XCode.Configuration;
using XCode.DataAccessLayer;
using XCode.Exceptions;
using XCode.Model;

namespace XCode
{
    /// <summary>����ʵ������ࡣ��������ʵ���඼����̳и��ࡣ</summary>
    [Serializable]
    public partial class Entity<TEntity> : EntityBase where TEntity : Entity<TEntity>, new()
    {
        #region ���캯��
        /// <summary>��̬����</summary>
        static Entity()
        {
            DAL.WriteDebugLog("��ʼ��ʼ��ʵ����{0}", Meta.ThisType.Name);

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

            DAL.WriteDebugLog("��ɳ�ʼ��ʵ����{0}", Meta.ThisType.Name);
        }

        /// <summary>����ʵ�塣</summary>
        /// <remarks>
        /// ������д�ķ�����ʵ��ʵ������һЩ��ʼ��������
        /// �мǣ�дΪʵ������������Ϊ�˷������أ���Ҫ���ص�ʵ�����Բ����ǵ�ǰʵ����
        /// </remarks>
        /// <param name="forEdit">�Ƿ�Ϊ�˱༭������������ǣ������ٴ���һЩ��صĳ�ʼ������</param>
        /// <returns></returns>
        [Obsolete("=>IEntityOperate")]
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        protected virtual TEntity CreateInstance(Boolean forEdit = false)
        {
            //return new TEntity();
            // new TEntity�ᱻ����ΪActivator.CreateInstance<TEntity>()��������Activator.CreateInstance()��
            // Activator.CreateInstance()�л��湦�ܣ������͵��Ǹ�û��
            //return Activator.CreateInstance(Meta.ThisType) as TEntity;
            return Meta.ThisType.CreateInstance() as TEntity;
        }
        #endregion

        #region �������
        /// <summary>���ؼ�¼����������ʱ���ؿռ��϶�����null��</summary>
        /// <param name="ds">��¼��</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> LoadData(DataSet ds)
        {
            if (ds == null || ds.Tables.Count < 1) return new EntityList<TEntity>();

            return LoadData(ds.Tables[0]);
        }

        /// <summary>�������ݱ�������ʱ���ؿռ��϶�����null��</summary>
        /// <param name="dt">���ݱ�</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> LoadData(DataTable dt)
        {
            if (dt == null) return new EntityList<TEntity>();

            var list = dreAccessor.LoadData(dt);
            // ����Ĭ���ۼ��ֶ�
            var fs = AdditionalFields;
            if (fs.Count > 0)
            {
                foreach (var entity in list)
                {
                    foreach (var item in fs)
                    {
                        entity.SetAdditionalField(item);
                    }
                }
            }
            foreach (EntityBase entity in list)
            {
                entity.OnLoad();
            }
            // ����һ������ת��
            var elist = list as EntityList<TEntity>;
            if (elist != null) return elist;

            return new EntityList<TEntity>(list);
        }

        /// <summary>��һ�������ж���������ݡ������ع�������</summary>
        /// <param name="dr">������</param>
        public override void LoadData(DataRow dr)
        {
            if (dr != null)
            {
                dreAccessor.LoadData(dr, this);
                OnLoad();
            }
        }

        /// <summary>�������ݶ�д����������ʱ���ؿռ��϶�����null��</summary>
        /// <param name="dr">���ݶ�д��</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> LoadData(IDataReader dr)
        {
            var list = dreAccessor.LoadData(dr);

            // ����Ĭ���ۼ��ֶ�
            var fs = AdditionalFields;
            if (fs.Count > 0)
            {
                foreach (var entity in list)
                {
                    foreach (var item in fs)
                    {
                        entity.SetAdditionalField(item);
                    }
                }
            }
            foreach (EntityBase entity in list)
            {
                entity.OnLoad();
            }
            // ����һ������ת��
            var elist = list as EntityList<TEntity>;
            if (elist != null) return elist;

            return new EntityList<TEntity>(list);
        }

        /// <summary>��һ�������ж���������ݡ������ع�������</summary>
        /// <param name="dr">���ݶ�д��</param>
        public override void LoadDataReader(IDataReader dr)
        {
            if (dr != null)
            {
                dreAccessor.LoadData(dr, this);
                OnLoad();

                // ����Ĭ���ۼ��ֶ�
                var fs = AdditionalFields;
                if (fs.Count > 0)
                {
                    foreach (var item in fs)
                    {
                        SetAdditionalField(item);
                    }
                }
            }
        }

        /// <summary>�����ݸ��Ƶ������ж����С�</summary>
        /// <param name="dr">������</param>
        public virtual DataRow ToData(ref DataRow dr) { return dr == null ? null : dreAccessor.ToData(this, ref dr); }

        private static IDataRowEntityAccessor dreAccessor { get { return XCodeService.CreateDataRowEntityAccessor(Meta.ThisType); } }
        #endregion

        #region ����
        private static IEntityPersistence persistence { get { return XCodeService.Container.ResolveInstance<IEntityPersistence>(); } }

        /// <summary>�������ݣ�<see cref="Valid"/>���������е���<see cref="OnInsert"/>��</summary>
        /// <returns></returns>
        public override Int32 Insert() { return DoAction(OnInsert, true); }

        /// <summary>�Ѹö���־û������ݿ⣬���/����ʵ�建�档</summary>
        /// <returns></returns>
        protected virtual Int32 OnInsert() { return Meta.Session.Insert(this); }

        /// <summary>�������ݣ�<see cref="Valid"/>���������е���<see cref="OnUpdate"/>��</summary>
        /// <returns></returns>
        public override Int32 Update() { return DoAction(OnUpdate, false); }

        /// <summary>�������ݿ⣬ͬʱ����ʵ�建��</summary>
        /// <returns></returns>
        protected virtual Int32 OnUpdate() { return Meta.Session.Update(this); }

        /// <summary>ɾ�����ݣ�ͨ���������е���OnDeleteʵ�֡�</summary>
        /// <remarks>
        /// ɾ��ʱ��������ҽ��������������ݣ��������ObjectDataSource֮���ɾ��������
        /// ������£�ʵ����û����������Ϣ������������Ϣ�������ᵼ���޷�ͨ����չ����ɾ���������ݡ�
        /// �����Ҫ�ܿ��û��ƣ�����������ݡ�
        /// </remarks>
        /// <returns></returns>
        public override Int32 Delete()
        {
            if (HasDirty)
            {
                // �Ƿ����ҽ���������������
                var names = Meta.Table.PrimaryKeys.Select(f => f.Name).OrderBy(k => k).ToArray();
                // �����������Ƿ���ڷ�������Ϊtrue��
                var names2 = Dirtys.Where(d => d.Value).Select(d => d.Key).OrderBy(k => k).ToArray();
                // ������ȣ���������
                if (names.SequenceEqual(names2))
                {
                    // �ٴβ�ѯ
                    var entity = Find(persistence.GetPrimaryCondition(this));
                    // ���Ŀ�����ݲ����ڣ���û��Ҫɾ����
                    if (entity == null) return 0;

                    // ���������ݺ���չ����
                    foreach (var item in names)
                    {
                        entity.Dirtys[item] = true;
                    }
                    foreach (var item in Extends)
                    {
                        entity.Extends[item.Key] = item.Value;
                    }

                    return entity.DoAction(OnDelete, null);
                }
            }

            return DoAction(OnDelete, null);
        }

        /// <summary>�����ݿ���ɾ���ö���ͬʱ��ʵ�建����ɾ��</summary>
        /// <returns></returns>
        protected virtual Int32 OnDelete() { return Meta.Session.Delete(this); }

        Int32 DoAction(Func<Int32> func, Boolean? isnew)
        {
            var session = Meta.Session;

            session.BeginTrans();
            try
            {
                if (isnew != null && enableValid) Valid(isnew.Value);

                Int32 rs = func();

                session.Commit();

                return rs;
            }
            catch { session.Rollback(); throw; }
        }

        /// <summary>���档��������������ݿ����Ƿ��Ѵ��ڸö����پ�������Insert��Update</summary>
        /// <returns></returns>
        public override Int32 Save()
        {
            //����ʹ�������ֶ��ж�
            var fi = Meta.Table.Identity;
            if (fi != null) return Convert.ToInt64(this[fi.Name]) > 0 ? Update() : Insert();

            fi = Meta.Unique;
            // ���Ψһ������Ϊ�գ�Ӧ��ͨ�������жϣ�������ֱ��Update
            if (fi != null && Helper.IsNullKey(this[fi.Name])) return Insert();

            return FindCount(persistence.GetPrimaryCondition(this), null, null, 0, 0) > 0 ? Update() : Insert();
        }

        /// <summary>����Ҫ��֤�ı��棬��ִ��Valid��һ�����ڿ��ٵ�������</summary>
        /// <returns></returns>
        public override Int32 SaveWithoutValid()
        {
            enableValid = false;
            try { return Save(); }
            finally { enableValid = true; }
        }

        [NonSerialized]
        Boolean enableValid = true;

        /// <summary>��֤���ݣ�ͨ���׳��쳣�ķ�ʽ��ʾ��֤ʧ�ܡ�</summary>
        /// <remarks>������д�ߵ��û����ʵ�֣���Ϊ������������ֶε�Ψһ��������������֤��</remarks>
        /// <param name="isNew">�Ƿ�������</param>
        public virtual void Valid(Boolean isNew)
        {
            // �����������ж�Ψһ��
            var table = Meta.Table.DataTable;
            if (table.Indexes != null && table.Indexes.Count > 0)
            {
                // ������������
                foreach (var item in table.Indexes)
                {
                    // ֻ����Ψһ����
                    if (!item.Unique) continue;

                    // ��ҪתΪ������Ҳ�����ֶ���
                    var columns = table.GetColumns(item.Columns);
                    if (columns == null || columns.Length < 1) continue;

                    // ����������
                    if (columns.All(c => c.Identity)) continue;

                    // ��¼�ֶ��Ƿ��и���
                    Boolean changed = false;
                    if (!isNew) changed = columns.Any(c => Dirtys[c.Name]);

                    // ���ڼ��
                    if (isNew || changed) CheckExist(columns.Select(c => c.Name).Distinct().ToArray());
                }
            }
        }

        /// <summary>����ָ������������Ƿ��Ѵ��ڣ����Ѵ��ڣ��׳�ArgumentOutOfRangeException�쳣</summary>
        /// <param name="names"></param>
        public virtual void CheckExist(params String[] names)
        {
            if (Exist(names))
            {
                var sb = new StringBuilder();
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

                throw new ArgumentOutOfRangeException(String.Join(",", names), this[names[0]], sb.ToString());
            }
        }

        /// <summary>����ָ����������ݣ����������Ƿ��Ѵ���</summary>
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

            var field = Meta.Unique;
            var val = this[field.Name];
            var cache = Meta.Session.Cache;
            if (!cache.Using)
            {
                // ����ǿ������������ֱ���жϼ�¼���ķ�ʽ���Լӿ��ٶ�
                if (Helper.IsNullKey(val)) return FindCount(names, values) > 0;

                var list = FindAll(names, values);
                if (list == null || list.Count < 1) return false;
                if (list.Count > 1) return true;

                return !Object.Equals(val, list[0][field.Name]);
            }
            else
            {
                // ����ǿ������������ֱ���жϼ�¼���ķ�ʽ���Լӿ��ٶ�
                var list = cache.Entities.FindAll(names, values, true);
                if (Helper.IsNullKey(this[field.Name])) return list.Count > 0;

                if (list == null || list.Count < 1) return false;
                if (list.Count > 1) return true;

                return !Object.Equals(val, list[0][field.Name]);
            }
        }
        #endregion

        #region ���ҵ���ʵ��
        /// <summary>���������Լ���Ӧ��ֵ�����ҵ���ʵ��</summary>
        /// <param name="name">��������</param>
        /// <param name="value">����ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity Find(String name, Object value) { return Find(new String[] { name }, new Object[] { value }); }

        /// <summary>���������б��Լ���Ӧ��ֵ�б����ҵ���ʵ��</summary>
        /// <param name="names">�������Ƽ���</param>
        /// <param name="values">����ֵ����</param>
        /// <returns></returns>
        public static TEntity Find(String[] names, Object[] values)
        {
            // �ж�����������
            if (names != null && names.Length == 1)
            {
                FieldItem field = Meta.Table.FindByName(names[0]);
                if (field != null && (field.IsIdentity || field.PrimaryKey))
                {
                    // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
                    if (Helper.IsNullKey(values[0])) return null;

                    return FindUnique(MakeCondition(field, values[0], "="));
                }
            }

            // �ж�Ψһ������Ψһ����Ҳ����Ҫ��ҳ
            IDataIndex di = Meta.Table.DataTable.GetIndex(names);
            if (di != null && di.Unique) return FindUnique(MakeCondition(names, values, "And"));

            return Find(MakeCondition(names, values, "And"));
        }

        /// <summary>
        /// ������������Ψһ�ĵ���ʵ�壬��Ϊ��Ψһ�ģ����Բ���Ҫ��ҳ������
        /// �����ȷ���Ƿ�Ψһ��һ����Ҫ���ø÷���������᷵�ش��������ݡ�
        /// </summary>
        /// <param name="whereClause">��ѯ����</param>
        /// <returns></returns>
        static TEntity FindUnique(String whereClause)
        {
            var session = Meta.Session;
            var builder = new SelectBuilder();
            builder.Table = session.FormatedTableName;
            // ���ǣ�ĳЩ��Ŀ�п�����where��ʹ����GroupBy���ڷ�ҳʱ���ܱ���
            builder.Where = whereClause;
            var list = LoadData(session.Query(builder, 0, 0));
            if (list == null || list.Count < 1) return null;

            if (list.Count > 1 && DAL.Debug)
            {
                DAL.WriteDebugLog("����FindUnique(\"{0}\")������ֻ�з���Ψһ��¼�Ĳ�ѯ������������ã�", whereClause);
                NewLife.Log.XTrace.DebugStack(5);
            }
            return list[0];
        }

        /// <summary>�����������ҵ���ʵ��</summary>
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

        /// <summary>�����������ҵ���ʵ��</summary>
        /// <param name="key">Ψһ������ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindByKey(Object key)
        {
            FieldItem field = Meta.Unique;
            if (field == null) throw new ArgumentNullException("Meta.Unique", "FindByKey����Ҫ��" + Meta.ThisType.FullName + "��Ψһ������");

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
            if (Helper.IsNullKey(key)) return null;

            return Find(field.Name, key);
        }

        /// <summary>����������ѯһ��ʵ��������ڱ��༭</summary>
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
                return Meta.Factory.Create(true) as TEntity;
            }

            Type type = field.Type;

            // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ��������ʵ��
            if (Helper.IsNullKey(key))
            {
                if (type.IsIntType() && !field.IsIdentity && DAL.Debug) DAL.WriteLog("{0}��{1}�ֶ����������������Ƿ�����������������", Meta.TableName, field.ColumnName);

                return Meta.Factory.Create(true) as TEntity;
            }

            // ���⣬һ�ɷ��� ����ֵ����ʹ�����ǿա������������Ҳ������ݵ�����¸������ؿգ���Ϊ�������Ҳ������ݶ��ѣ���������ʵ���ᵼ��ǰ����Ϊ��������������
            TEntity entity = Find(field.Name, key);

            // �ж�ʵ��
            if (entity == null)
            {
                String msg = null;
                if (Helper.IsNullKey(key))
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}������δ��������������", key, Meta.Table.Description);
                else
                    msg = String.Format("���������޷�ȡ�ñ��Ϊ{0}��{1}��", key, Meta.Table.Description);

                throw new XCodeException(msg);
            }

            return entity;
        }
        #endregion

        #region ��̬��ѯ
        /// <summary>��ȡ����ʵ����󡣻�ȡ��������ʱ��ǳ���������</summary>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll() { return FindAll(null, null, null, 0, 0); }

        /// <summary>��ѯ������ʵ����󼯺ϡ�</summary>
        /// <remarks>
        /// ����������ѯ�������Select @selects From Table Where @whereClause Order By @orderClause Limit @startRowIndex,@maximumRows��������׸���������˼�ˡ�
        /// </remarks>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ�弯</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows)
        {
            var session = Meta.Session;

            #region �������ݲ�ѯ�Ż�
            // ��������βҳ��ѯ�Ż�
            // �ں������ݷ�ҳ�У�ȡԽ�Ǻ���ҳ������Խ�������Կ��ǵ���ķ�ʽ
            // ֻ���ڰ������ݣ��ҿ�ʼ�д�����ʮ��ʱ��ʹ��

            // �����Ż���������ÿ�ζ�����Meta.Count�������γ�һ�β�ѯ����Ȼ��β�ѯʱ����Ĳ���
            // ���Ǿ��������ѯ��������Ҫ�������Ƶĺ��������Ż�����Ȼ�����startRowIndex���ᵲס99%���ϵ��˷�
            Int64 count = 0;
            if (startRowIndex > 500000 && (count = session.LongCount) > 1000000)
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
                        var sb = new StringBuilder();
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
                        var max = (Int32)Math.Min(maximumRows, count - startRowIndex);
                        //if (max <= 0) return null;
                        if (max <= 0) return new EntityList<TEntity>();
                        var start = (Int32)(count - (startRowIndex + maximumRows));

                        var builder2 = CreateBuilder(whereClause, order, selects, start, max);
                        var list = LoadData(session.Query(builder2, start, max));
                        if (list == null || list.Count < 1) return list;
                        // ��Ϊ����ȡ�õ������ǵ������ģ�����������Ҫ�ٵ�һ��
                        list.Reverse();
                        return list;
                    }
                }
            }
            #endregion

            var builder = CreateBuilder(whereClause, orderClause, selects, startRowIndex, maximumRows);
            return LoadData(session.Query(builder, startRowIndex, maximumRows));
        }

        /// <summary>���������б��Լ���Ӧ��ֵ�б���ȡ����ʵ�����</summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns>ʵ������</returns>
        public static EntityList<TEntity> FindAll(String[] names, Object[] values)
        {
            // �ж�����������
            if (names != null && names.Length == 1)
            {
                FieldItem field = Meta.Table.FindByName(names[0]);
                if (field != null && (field.IsIdentity || field.PrimaryKey))
                {
                    // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
                    if (Helper.IsNullKey(values[0])) return null;
                }
            }

            return FindAll(MakeCondition(names, values, "And"), null, null, 0, 0);
        }

        /// <summary>���������Լ���Ӧ��ֵ����ȡ����ʵ�����</summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAll(String name, Object value) { return FindAll(new String[] { name }, new Object[] { value }); }

        /// <summary>���������Լ���Ӧ��ֵ����ȡ����ʵ�����</summary>
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
                if (Helper.IsNullKey(value)) return new EntityList<TEntity>();

                // ��������������ѯ����¼���϶���Ψһ�ģ�����Ҫָ����¼��������
                return FindAll(MakeCondition(field, value, "="), null, null, 0, 0);
                //var builder = new SelectBuilder();
                //builder.Table = Meta.FormatName(Meta.TableName);
                //builder.Where = MakeCondition(field, value, "=");
                //return FindAll(builder.ToString());
            }

            return FindAll(MakeCondition(new String[] { name }, new Object[] { value }, "And"), orderClause, null, startRowIndex, maximumRows);
        }

        /// <summary>��ѯSQL������ʵ��������顣
        /// Select������ֱ��ʹ�ò���ָ���Ĳ�ѯ�����в�ѯ���������κ�ת����
        /// </summary>
        /// <param name="sql">��ѯ���</param>
        /// <returns>ʵ������</returns>
        [Obsolete("=>Session")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static EntityList<TEntity> FindAll(String sql) { return LoadData(Meta.Session.Query(sql)); }
        #endregion

        #region �����ѯ
        /// <summary>���������Լ���Ӧ��ֵ���ڻ����в��ҵ���ʵ��</summary>
        /// <param name="name">��������</param>
        /// <param name="value">����ֵ</param>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static TEntity FindWithCache(String name, Object value) { return Meta.Session.Cache.Entities.Find(name, value); }

        /// <summary>�������л���</summary>
        /// <returns></returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllWithCache() { return Meta.Session.Cache.Entities; }

        /// <summary>���������Լ���Ӧ��ֵ���ڻ����л�ȡ����ʵ�����</summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>ʵ������</returns>
        [DataObjectMethod(DataObjectMethodType.Select, false)]
        public static EntityList<TEntity> FindAllWithCache(String name, Object value) { return Meta.Session.Cache.Entities.FindAll(name, value); }
        #endregion

        #region ȡ�ܼ�¼��
        /// <summary>�����ܼ�¼��</summary>
        /// <returns></returns>
        public static Int32 FindCount() { return FindCount(null, null, null, 0, 0); }

        /// <summary>�����ܼ�¼��</summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By�����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <param name="selects">��ѯ�С����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ�С����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ�����С����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows)
        {
            var session = Meta.Session;

            // ����ܼ�¼������һ��Ϊ��������ܣ����ؿ��ٲ����Ҵ��л�����ܼ�¼��
            if (String.IsNullOrEmpty(whereClause) && session.Count > 10000) return session.Count;

            var sb = new SelectBuilder();
            sb.Table = session.FormatedTableName;
            sb.Where = whereClause;

            return session.QueryCount(sb);
        }

        /// <summary>���������б��Լ���Ӧ��ֵ�б������ܼ�¼��</summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String[] names, Object[] values)
        {
            // �ж�����������
            if (names != null && names.Length == 1)
            {
                FieldItem field = Meta.Table.FindByName(names[0]);
                if (field != null && (field.IsIdentity || field.PrimaryKey))
                {
                    // Ψһ��Ϊ�����Ҳ���С�ڵ���0ʱ�����ؿ�
                    if (Helper.IsNullKey(values[0])) return 0;
                }
            }

            return FindCount(MakeCondition(names, values, "And"), null, null, 0, 0);
        }

        /// <summary>���������Լ���Ӧ��ֵ�������ܼ�¼��</summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <returns>������</returns>
        public static Int32 FindCount(String name, Object value) { return FindCountByName(name, value, null, 0, 0); }

        /// <summary>���������Լ���Ӧ��ֵ�������ܼ�¼��</summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="orderClause">���򣬲���Order By�����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ�С����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ�����С����������壬����Ϊ�˱�����FindAll��ͬ�ķ���ǩ��</param>
        /// <returns>������</returns>
        public static Int32 FindCountByName(String name, Object value, String orderClause, int startRowIndex, int maximumRows)
        {
            if (String.IsNullOrEmpty(name))
                return FindCount(null, null, null, 0, 0);
            else
                return FindCount(new String[] { name }, new Object[] { value });
        }
        #endregion

        #region ��ȡ��ѯSQL
        /// <summary>��ȡ��ѯSQL����Ҫ���ڹ����Ӳ�ѯ</summary>
        /// <param name="whereClause">����������Where</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="selects">��ѯ��</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ�弯</returns>
        public static SelectBuilder FindSQL(String whereClause, String orderClause, String selects, Int32 startRowIndex = 0, Int32 maximumRows = 0)
        {
            var builder = CreateBuilder(whereClause, orderClause, selects, startRowIndex, maximumRows, false);
            return Meta.Session.PageSplit(builder, startRowIndex, maximumRows);
        }

        /// <summary>��ȡ��ѯΨһ����SQL������Select ID From Table</summary>
        /// <param name="whereClause"></param>
        /// <returns></returns>
        public static SelectBuilder FindSQLWithKey(String whereClause = null)
        {
            var f = Meta.Unique;
            return FindSQL(whereClause, null, f != null ? Meta.FormatName(f.ColumnName) : null, 0, 0);
        }
        #endregion

        #region �߼���ѯ
        /// <summary>��ѯ���������ļ�¼������ҳ������</summary>
        /// <param name="key">�ؼ���</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>ʵ�弯</returns>
        [DataObjectMethod(DataObjectMethodType.Select, true)]
        public static EntityList<TEntity> Search(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows) { return FindAll(SearchWhereByKeys(key, null), orderClause, null, startRowIndex, maximumRows); }

        /// <summary>��ѯ���������ļ�¼��������ҳ��������Ч������������ΪObjectDataSourceҪ������Searchͳһ</summary>
        /// <param name="key">�ؼ���</param>
        /// <param name="orderClause">���򣬲���Order By</param>
        /// <param name="startRowIndex">��ʼ�У�0��ʾ��һ��</param>
        /// <param name="maximumRows">��󷵻�������0��ʾ������</param>
        /// <returns>��¼��</returns>
        public static Int32 SearchCount(String key, String orderClause, Int32 startRowIndex, Int32 maximumRows) { return FindCount(SearchWhereByKeys(key, null), null, null, 0, 0); }

        /// <summary>�����ؼ��ֲ�ѯ����</summary>
        /// <param name="sb"></param>
        /// <param name="keys"></param>
        [Obsolete("=>SearchWhereByKeys(String keys)")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SearchWhereByKeys(StringBuilder sb, String keys) { SearchWhereByKeys(sb, keys, null); }

        /// <summary>�����ؼ��ֲ�ѯ����</summary>
        /// <param name="sb"></param>
        /// <param name="keys"></param>
        /// <param name="func"></param>
        [Obsolete("=>SearchWhereByKeys(String keys)")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SearchWhereByKeys(StringBuilder sb, String keys, Func<String, WhereExpression> func)
        {
            if (String.IsNullOrEmpty(keys)) return;

            String str = SearchWhereByKeys(keys, func);
            if (String.IsNullOrEmpty(str)) return;

            if (sb.Length > 0) sb.Append(" And ");
            if (str.Contains("Or") || str.ToLower().Contains("or"))
                sb.AppendFormat("({0})", str);
            else
                sb.Append(str);
        }

        /// <summary>���ݿո�ָ�Ĺؼ��ּ��Ϲ�����ѯ����</summary>
        /// <param name="keys">�ո�ָ�Ĺؼ��ּ���</param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static WhereExpression SearchWhereByKeys(String keys, Func<String, WhereExpression> func = null)
        {
            var exp = new WhereExpression();
            if (String.IsNullOrEmpty(keys)) return exp;

            if (func == null) func = SearchWhereByKey;

            var ks = keys.Split(new Char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var sb = exp.Builder;
            for (int i = 0; i < ks.Length; i++)
            {
                if (sb.Length > 0) sb.Append(" And ");

                String str = func(ks[i]);
                if (String.IsNullOrEmpty(str)) continue;

                if (str.Contains("Or") || str.ToLower().Contains("or"))
                    sb.AppendFormat("({0})", str);
                else
                    sb.Append(str);
            }

            return exp;
        }

        /// <summary>�����ؼ��ֲ�ѯ����</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static WhereExpression SearchWhereByKey(String key)
        {
            var exp = new WhereExpression();
            if (String.IsNullOrEmpty(key)) return exp;

            var sb = exp.Builder;
            foreach (var item in Meta.Fields)
            {
                if (item.Type != typeof(String)) continue;

                if (sb.Length > 0) sb.Append(" Or ");
                sb.AppendFormat("{0} like '%{1}%'", Meta.FormatName(item.Name), key);
            }

            return exp;
        }
        #endregion

        #region ��̬����
        /// <summary>��һ��ʵ�����־û������ݿ�</summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [DataObjectMethod(DataObjectMethodType.Insert, true)]
        public static Int32 Insert(TEntity obj) { return obj.Insert(); }

        /// <summary>��һ��ʵ�����־û������ݿ�</summary>
        /// <param name="names">���������б�</param>
        /// <param name="values">����ֵ�б�</param>
        /// <returns>������Ӱ�������</returns>
        public static Int32 Insert(String[] names, Object[] values)
        {
            return persistence.Insert(Meta.ThisType, names, values);
        }

        /// <summary>��һ��ʵ�������µ����ݿ�</summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [DataObjectMethod(DataObjectMethodType.Update, true)]
        public static Int32 Update(TEntity obj) { return obj.Update(); }

        /// <summary>����һ��ʵ������</summary>
        /// <param name="setClause">Ҫ���µ��������</param>
        /// <param name="whereClause">ָ��Ҫ���µ�ʵ��</param>
        /// <returns></returns>
        public static Int32 Update(String setClause, String whereClause)
        {
            return persistence.Update(Meta.ThisType, setClause, whereClause);
        }

        /// <summary>����һ��ʵ������</summary>
        /// <param name="setNames">���������б�</param>
        /// <param name="setValues">����ֵ�б�</param>
        /// <param name="whereNames">���������б�</param>
        /// <param name="whereValues">����ֵ�б�</param>
        /// <returns>������Ӱ�������</returns>
        public static Int32 Update(String[] setNames, Object[] setValues, String[] whereNames, Object[] whereValues)
        {
            return persistence.Update(Meta.ThisType, setNames, setValues, whereNames, whereValues);
        }

        /// <summary>
        /// �����ݿ���ɾ��ָ��ʵ�����
        /// ʵ����Ӧ��ʵ�ָ÷�������һ����������Ψһ����������Ϊ����
        /// </summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ����������������жϱ�ɾ���˶����У��Ӷ�֪�������Ƿ�ɹ�</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        [DataObjectMethod(DataObjectMethodType.Delete, true)]
        public static Int32 Delete(TEntity obj) { return obj.Delete(); }

        /// <summary>�����ݿ���ɾ��ָ��������ʵ�����</summary>
        /// <param name="whereClause">��������</param>
        /// <returns></returns>
        public static Int32 Delete(String whereClause)
        {
            return persistence.Delete(Meta.ThisType, whereClause);
        }

        /// <summary>�����ݿ���ɾ��ָ�������б��ֵ�б����޶���ʵ�����</summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <returns></returns>
        public static Int32 Delete(String[] names, Object[] values)
        {
            return persistence.Delete(Meta.ThisType, names, values);
        }

        /// <summary>��һ��ʵ�������µ����ݿ�</summary>
        /// <param name="obj">ʵ�����</param>
        /// <returns>������Ӱ�������</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static Int32 Save(TEntity obj) { return obj.Save(); }
        #endregion

        #region ����SQL���
        ///// <summary>��SQLģ���ʽ��ΪSQL���</summary>
        ///// <param name="obj">ʵ�����</param>
        ///// <param name="methodType"></param>
        ///// <returns>SQL�ַ���</returns>
        //[Obsolete("�ó�Ա�ں����汾�н����ٱ�֧�֣���ʹ��XCodeService.Resolve<IEntityPersistence>().GetSql()��")]
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //public static String SQL(Entity<TEntity> obj, DataObjectMethodType methodType) { return persistence.GetSql(obj, methodType); }

        /// <summary>
        /// ���������б��ֵ�б������ѯ������
        /// ���繹����������Ʋ�ѯ������
        /// </summary>
        /// <param name="names">�����б�</param>
        /// <param name="values">ֵ�б�</param>
        /// <param name="action">���Ϸ�ʽ</param>
        /// <returns>�����Ӵ�</returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static String MakeCondition(String[] names, Object[] values, String action)
        {
            //if (names == null || names.Length <= 0) throw new ArgumentNullException("names", "�����б��ֵ�б���Ϊ��");
            //if (values == null || values.Length <= 0) throw new ArgumentNullException("values", "�����б��ֵ�б���Ϊ��");
            if (names == null || names.Length <= 0) return null;
            if (values == null || values.Length <= 0) return null;
            if (names.Length != values.Length) throw new ArgumentException("�����б�����ֵ�б�һһ��Ӧ");

            StringBuilder sb = new StringBuilder();
            for (Int32 i = 0; i < names.Length; i++)
            {
                FieldItem fi = Meta.Table.FindByName(names[i]);
                if (fi == null) throw new ArgumentException("��[" + Meta.ThisType.FullName + "]�в�����[" + names[i] + "]����");

                // ͬʱ����SQL��䡣names�������б�����ת���ɶ�Ӧ���ֶ��б�
                if (i > 0) sb.AppendFormat(" {0} ", action.Trim());
                //sb.AppendFormat("{0}={1}", Meta.FormatName(fi.ColumnName), Meta.FormatValue(fi, values[i]));
                sb.Append(MakeCondition(fi, values[i], "="));
            }
            return sb.ToString();
        }

        /// <summary>��������</summary>
        /// <param name="name">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="action">����С�ڵȷ���</param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static String MakeCondition(String name, Object value, String action)
        {
            FieldItem field = Meta.Table.FindByName(name);
            if (field == null) return String.Format("{0}{1}{2}", Meta.FormatName(name), action, Meta.FormatValue(name, value));

            return MakeCondition(field, value, action);
        }

        /// <summary>��������</summary>
        /// <param name="field">����</param>
        /// <param name="value">ֵ</param>
        /// <param name="action">����С�ڵȷ���</param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static String MakeCondition(FieldItem field, Object value, String action)
        {
            if (!String.IsNullOrEmpty(action) && action.Contains("{0}"))
            {
                if (action.Contains("%"))
                    return Meta.FormatName(field.ColumnName) + " Like " + Meta.FormatValue(field, String.Format(action, value));
                else
                    return Meta.FormatName(field.ColumnName) + String.Format(action, Meta.FormatValue(field, value));
            }
            else
                return String.Format("{0}{1}{2}", Meta.FormatName(field.ColumnName), action, Meta.FormatValue(field, value));
        }

        ///// <summary>
        ///// Ĭ��������
        ///// ���б�ʶ�У���ʹ��һ����ʶ����Ϊ������
        ///// ������������ʹ��ȫ��������Ϊ������
        ///// </summary>
        ///// <param name="obj">ʵ�����</param>
        ///// <returns>����</returns>
        //[Obsolete("�ó�Ա�ں����汾�н����ٱ�֧�֣���ʹ��XCodeService.Resolve<IEntityPersistence>().GetPrimaryCondition()��")]
        //[EditorBrowsable(EditorBrowsableState.Never)]
        //protected static String DefaultCondition(Entity<TEntity> obj) { return persistence.GetPrimaryCondition(obj); }

        static SelectBuilder CreateBuilder(String whereClause, String orderClause, String selects, Int32 startRowIndex, Int32 maximumRows, Boolean needOrderByID = true)
        {
            var builder = new SelectBuilder();
            builder.Column = selects;
            builder.Table = Meta.Session.FormatedTableName;
            builder.OrderBy = orderClause;
            // ���ǣ�ĳЩ��Ŀ�п�����where��ʹ����GroupBy���ڷ�ҳʱ���ܱ���
            builder.Where = whereClause;

            // XCode����Ĭ������Ĺ����������������������Ĭ��
            // �������м�¼
            if (!needOrderByID && startRowIndex <= 0 && maximumRows <= 0) return builder;

            FieldItem fi = Meta.Unique;
            if (fi != null)
            {
                builder.Key = Meta.FormatName(fi.ColumnName);

                // Ĭ�ϻ�ȡ����ʱ��������Ҫָ�����������ֶν��򣬷���ʹ��ϰ��
                // ��GroupByҲ���ܼ�����
                if (String.IsNullOrEmpty(builder.OrderBy) && String.IsNullOrEmpty(builder.GroupBy))
                {
                    // ���ֽ�����������
                    var b = fi.Type.IsIntType() && fi.IsIdentity;
                    builder.IsDesc = b;
                    // ����û������builder.IsInt���·�ҳû��ѡ����ѵ�MaxMin��BUG����л @RICH(20371423)
                    builder.IsInt = b;

                    builder.OrderBy = builder.KeyOrder;
                }
            }
            else
            {
                // ����Ҳ���Ψһ��������������Ϊ�գ������ȫ���ֶ�һ��ȷ���ܹ���ҳ
                if (String.IsNullOrEmpty(builder.OrderBy)) builder.Keys = Meta.FieldNames.ToArray();
            }
            return builder;
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
                    var field = this.GetType().GetFieldEx("_" + name);
                    if (field != null) return this.GetValue(field);
                }

                //����ƥ������
                var property = this.GetType().GetPropertyEx(name);
                if (property != null && property.CanRead) return this.GetValue(property);

                Object obj = null;
                if (Extends.TryGetValue(name, out obj)) return obj;

                //throw new ArgumentException("��[" + this.GetType().FullName + "]�в�����[" + name + "]����");

                return null;
            }
            set
            {
                //ƥ���ֶ�
                if (Meta.FieldNames.Contains(name))
                {
                    var field = this.GetType().GetFieldEx("_" + name);
                    if (field != null)
                    {
                        this.SetValue(field, value);
                        return;
                    }
                }

                //����ƥ������
                var property = this.GetType().GetPropertyEx(name);
                if (property != null && property.CanWrite)
                {
                    this.SetValue(property, value);
                    return;
                }

                if (Extends.ContainsKey(name))
                    Extends[name] = value;
                else
                    Extends.Add(name, value);

                //throw new ArgumentException("��[" + this.GetType().FullName + "]�в�����[" + name + "]����");
            }
        }
        #endregion

        #region ���뵼��XML
        /// <summary>����</summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static TEntity FromXml(String xml)
        {
            if (!String.IsNullOrEmpty(xml)) xml = xml.Trim();

            return xml.ToXmlEntity<TEntity>();
        }
        #endregion

        #region ���뵼��Json
        /// <summary>����</summary>
        /// <param name="json"></param>
        /// <returns></returns>
        //[Obsolete("�ó�Ա�ں����汾�н����ٱ�֧�֣�")]
        public static TEntity FromJson(String json)
        {
            return new Json().Deserialize<TEntity>(json);
        }
        #endregion

        #region ��¡
        /// <summary>������ǰ����Ŀ�¡���󣬽����������ֶ�</summary>
        /// <returns></returns>
        public override Object Clone() { return CloneEntity(); }

        /// <summary>��¡ʵ�塣������ǰ����Ŀ�¡���󣬽����������ֶ�</summary>
        /// <param name="setDirty">�Ƿ����������ݡ�Ĭ�ϲ�����</param>
        /// <returns></returns>
        public virtual TEntity CloneEntity(Boolean setDirty = false)
        {
            //var obj = CreateInstance();
            var obj = Meta.Factory.Create() as TEntity;
            foreach (var fi in Meta.Fields)
            {
                //obj[fi.Name] = this[fi.Name];
                if (setDirty)
                    obj.SetItem(fi.Name, this[fi.Name]);
                else
                    obj[fi.Name] = this[fi.Name];
            }
            if (Extends != null && Extends.Count > 0)
            {
                foreach (var item in Extends.Keys)
                {
                    obj.Extends[item] = Extends[item];
                }
            }
            return obj;
        }

        /// <summary>��¡ʵ��</summary>
        /// <param name="setDirty"></param>
        /// <returns></returns>
        internal protected override IEntity CloneEntityInternal(Boolean setDirty = true) { return CloneEntity(setDirty); }
        #endregion

        #region ����
        /// <summary>�����ء�</summary>
        /// <returns></returns>
        public override string ToString()
        {
            // ���Ȳ���ҵ��������Ҳ����Ψһ����
            var table = Meta.Table.DataTable;
            if (table.Indexes != null && table.Indexes.Count > 0)
            {
                IDataIndex di = null;
                foreach (var item in table.Indexes)
                {
                    if (!item.Unique) continue;
                    if (item.Columns == null || item.Columns.Length < 1) continue;

                    var columns = table.GetColumns(item.Columns);
                    if (columns == null || columns.Length < 1) continue;

                    di = item;

                    // ���ֻ��һ���������������������������ұ�ġ��������ʵ���Ҳ��������ٻ������������
                    if (!(columns.Length == 1 && columns[0].Identity)) break;
                }

                if (di != null)
                {
                    var columns = table.GetColumns(di.Columns);

                    // [v1,v2,...vn]
                    var sb = new StringBuilder();
                    foreach (var dc in columns)
                    {
                        if (sb.Length > 0) sb.Append(",");
                        if (Meta.FieldNames.Contains(dc.Name)) sb.Append(this[dc.Name]);
                    }
                    if (columns.Length > 1)
                        return String.Format("[{0}]", sb.ToString());
                    else
                        return sb.ToString();
                }
            }

            if (Meta.FieldNames.Contains("Name"))
                return this["Name"] == null ? null : this["Name"].ToString();
            else if (Meta.FieldNames.Contains("ID"))
                return this["ID"] == null ? null : this["ID"].ToString();
            else
                return "ʵ��" + Meta.ThisType.Name;
        }
        #endregion

        #region ������
        /// <summary>�����������ݵ�������</summary>
        /// <param name="isDirty">�ı������Ե����Ը���</param>
        /// <returns></returns>
        protected override Int32 SetDirty(Boolean isDirty)
        {
            var ds = Dirtys;
            if (ds == null || ds.Count < 1) return 0;

            var count = 0;
            foreach (var item in Meta.FieldNames)
            {
                var b = false;
                if (isDirty)
                {
                    if (!ds.TryGetValue(item, out b) || !b)
                    {
                        ds[item] = true;
                        count++;
                    }
                }
                else
                {
                    if (ds == null || ds.Count < 1) break;
                    if (ds.TryGetValue(item, out b) && b)
                    {
                        ds[item] = false;
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>�Ƿ��������ݡ������Ƿ����Update</summary>
        protected Boolean HasDirty
        {
            get
            {
                var ds = Dirtys;
                if (ds == null || ds.Count < 1) return false;

                foreach (var item in Meta.FieldNames)
                {
                    if (ds[item]) return true;
                }

                return false;
            }
        }

        /// <summary>����ֶδ���Ĭ��ֵ������Ҫ���������ݣ���Ϊ��Ȼ�û������ø��ֶΣ������ǲ������ݿ��Ĭ��ֵ</summary>
        /// <param name="fieldName"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        protected override bool OnPropertyChanging(string fieldName, object newValue)
        {
            // �������true����ʾ����ͬ�������Ѿ�������������
            if (base.OnPropertyChanging(fieldName, newValue)) return true;

            // ������ֶδ��ڣ��Ҵ���Ĭ��ֵ������Ҫ���������ݣ���Ϊ��Ȼ�û������ø��ֶΣ������ǲ������ݿ��Ĭ��ֵ
            FieldItem fi = Meta.Table.FindByName(fieldName);
            if (fi != null && !String.IsNullOrEmpty(fi.DefaultValue))
            {
                Dirtys[fieldName] = true;
                return true;
            }

            return false;
        }
        #endregion

        #region ��չ����
        /// <summary>��ȡ�����ڵ�ǰʵ�������չ����</summary>
        /// <typeparam name="TResult">��������</typeparam>
        /// <param name="key">��</param>
        /// <param name="func">�ص�</param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        protected TResult GetExtend<TResult>(String key, Func<String, Object> func) { return Extends.GetExtend<TEntity, TResult>(key, func); }

        /// <summary>��ȡ�����ڵ�ǰʵ�������չ����</summary>
        /// <typeparam name="TResult">��������</typeparam>
        /// <param name="key">��</param>
        /// <param name="func">�ص�</param>
        /// <param name="cacheDefault">�Ƿ񻺴�Ĭ��ֵ����ѡ������Ĭ�ϻ���</param>
        /// <returns></returns>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        protected TResult GetExtend<TResult>(String key, Func<String, Object> func, Boolean cacheDefault) { return Extends.GetExtend<TEntity, TResult>(key, func, cacheDefault); }

        /// <summary>���������ڵ�ǰʵ�������չ����</summary>
        /// <param name="key">��</param>
        /// <param name="value">ֵ</param>
        protected void SetExtend(String key, Object value) { Extends.SetExtend<TEntity>(key, value); }
        #endregion

        #region �ۼ�
        [NonSerialized]
        private static ICollection<String> _AdditionalFields;
        /// <summary>Ĭ���ۼ��ֶ�</summary>
        [XmlIgnore]
        protected static ICollection<String> AdditionalFields { get { return _AdditionalFields ?? (_AdditionalFields = new HashSet<String>(StringComparer.OrdinalIgnoreCase)); } }
        #endregion
    }
}