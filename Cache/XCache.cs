using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Threading;
using System.Web;
using NewLife.Configuration;
using NewLife.Log;
using NewLife.Reflection;
using NewLife.Threading;
using XCode.DataAccessLayer;

namespace XCode.Cache
{
    /// <summary>���ݻ�����</summary>
    /// <remarks>
    /// ��SQLΪ���Բ�ѯ���л��棬ͬʱ������ִ��SQLʱ�����ݹ�����ɾ�����档
    /// </remarks>
    static class XCache
    {
        #region ��ʼ��
        private static Dictionary<String, CacheItem<DataSet>> _TableCache = new Dictionary<String, CacheItem<DataSet>>();
        private static Dictionary<String, CacheItem<Int32>> _IntCache = new Dictionary<String, CacheItem<Int32>>();

        static readonly String _dst = "XCache_DataSet_";
        static readonly String _int = "XCache_Int32_";

        /// <summary>���������Ч�ڡ�
        /// -2	�رջ���
        /// -1	�Ƕ�ռ���ݿ⣬���ⲿϵͳ�������ݿ⣬ʹ�����󼶻��棻
        ///  0	���þ�̬���棻
        /// >0	��̬����ʱ�䣬��λ���룻
        /// </summary>
        //public static Int32 Expiration = -1;
        static Int32 Expiration { get { return CacheSetting.CacheExpiration; } }

        /// <summary>���ݻ�������</summary>
        internal static CacheKinds Kind { get { return Expiration > 0 ? CacheKinds.��Ч�ڻ��� : (CacheKinds)Expiration; } }

        /// <summary>��ʼ�����á���ȡ����</summary>
        static XCache()
        {
            //��ȡ������Ч��
            //Expiration = Config.GetMutilConfig<Int32>(-2, "XCode.Cache.Expiration", "XCacheExpiration");
            //��ȡ�������
            //CheckPeriod = Config.GetMutilConfig<Int32>(5, "XCode.Cache.CheckPeriod", "XCacheCheckPeriod");
            CheckPeriod = CacheSetting.CheckPeriod;

            //if (Expiration < -2) Expiration = -2;
            if (CheckPeriod <= 0) CheckPeriod = 5;

            if (DAL.Debug)
            {
                // ��Ҫ����һ�£�������ֱ����Kindת���������ַ��������������Ϊö�ٱ���������޷���ʾ��ȷ������
                String name = null;
                switch (Kind)
                {
                    case CacheKinds.�رջ���:
                        name = "�رջ���";
                        break;
                    case CacheKinds.���󼶻���:
                        name = "���󼶻���";
                        break;
                    case CacheKinds.���þ�̬����:
                        name = "���þ�̬����";
                        break;
                    case CacheKinds.��Ч�ڻ���:
                        name = "��Ч�ڻ���";
                        break;
                    default:
                        break;
                }
                if (Kind < CacheKinds.��Ч�ڻ���)
                    DAL.WriteLog("һ�����棺{0}", name);
                else
                    DAL.WriteLog("һ�����棺{0}��{1}", Expiration, name);
            }
        }
        #endregion

        #region ����ά��
        /// <summary>����ά����ʱ��</summary>
        private static TimerX AutoCheckCacheTimer;

        /// <summary>ά����ʱ���ļ�����ڣ�Ĭ��5��</summary>
        public static Int32 CheckPeriod = 5;

        /// <summary>ά��</summary>
        /// <param name="obj"></param>
        private static void Check(Object obj)
        {
            //�رջ��桢���þ�̬��������󼶻���ʱ������Ҫ���
            if (Kind != CacheKinds.��Ч�ڻ���) return;

            if (_TableCache.Count > 0)
            {
                lock (_TableCache)
                {
                    if (_TableCache.Count > 0)
                    {
                        var list = new List<String>();
                        foreach (var sql in _TableCache.Keys)
                        {
                            if (_TableCache[sql].ExpireTime < DateTime.Now)
                            {
                                list.Add(sql);
                            }
                        }
                        if (list != null && list.Count > 0)
                        {
                            foreach (var sql in list)
                                _TableCache.Remove(sql);
                        }
                    }
                }
            }
            if (_IntCache.Count > 0)
            {
                lock (_IntCache)
                {
                    if (_IntCache.Count > 0)
                    {
                        var list = new List<String>();
                        foreach (var sql in _IntCache.Keys)
                        {
                            if (_IntCache[sql].ExpireTime < DateTime.Now)
                            {
                                list.Add(sql);
                            }
                        }
                        if (list != null && list.Count > 0)
                        {
                            foreach (var sql in list)
                                _IntCache.Remove(sql);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// ������ʱ����
        /// ��Ϊ��ʱ����ԭ��ʵ�ʻ���ʱ�����Ҫ��ExpirationҪ��
        /// </summary>
        private static void CreateTimer()
        {
            //�رջ��桢���þ�̬��������󼶻���ʱ������Ҫ���
            if (Kind != CacheKinds.��Ч�ڻ���) return;

            if (AutoCheckCacheTimer != null) return;

            AutoCheckCacheTimer = new TimerX(Check, null, CheckPeriod * 1000, CheckPeriod * 1000);
            //// ������ʱ���������ӳ�ʱ�䣬ʵ���ϲ�����
            //AutoCheckCacheTimer = new Timer(new TimerCallback(Check), null, Timeout.Infinite, Timeout.Infinite);
            //// �ı䶨ʱ��Ϊ5��󴥷�һ�Ρ�
            //AutoCheckCacheTimer.Change(CheckPeriod * 1000, CheckPeriod * 1000);
        }
        #endregion

        #region ��ӻ���
        /// <summary>������ݱ��档</summary>
        /// <param name="cache">�������</param>
        /// <param name="prefix">ǰ׺</param>
        /// <param name="sql">SQL���</param>
        /// <param name="value">�������¼��</param>
        /// <param name="tableNames">��������</param>
        static void Add<T>(Dictionary<String, CacheItem<T>> cache, String prefix, String sql, T value, String[] tableNames)
        {
            //�رջ���
            if (Kind == CacheKinds.�رջ���) return;

            //���󼶻���
            if (Kind == CacheKinds.���󼶻���)
            {
                if (Items == null) return;

                Items.Add(prefix + sql, new CacheItem<T>(tableNames, value));
                return;
            }

            //��̬����
            if (cache.ContainsKey(sql)) return;
            lock (cache)
            {
                if (cache.ContainsKey(sql)) return;

                cache.Add(sql, new CacheItem<T>(tableNames, value, Expiration));
            }

            //����Ч��
            if (Kind == CacheKinds.��Ч�ڻ���) CreateTimer();
        }

        /// <summary>������ݱ��档</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="value">�������¼��</param>
        /// <param name="tableNames">��������</param>
        public static void Add(String sql, DataSet value, String[] tableNames) { Add(_TableCache, _dst, sql, value, tableNames); }

        /// <summary>���Int32���档</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="value">����������</param>
        /// <param name="tableNames">��������</param>
        public static void Add(String sql, Int32 value, String[] tableNames) { Add(_IntCache, _int, sql, value, tableNames); }
        #endregion

        #region ɾ������
        /// <summary>�Ƴ�������ĳ�����ݱ�Ļ���</summary>
        /// <param name="tableName">���ݱ�</param>
        public static void Remove(String tableName)
        {
            //���󼶻���
            if (Kind == CacheKinds.���󼶻���)
            {
                var cs = Items;
                if (cs == null) return;

                var toDel = new List<Object>();
                foreach (var obj in cs.Keys)
                {
                    var str = obj as String;
                    if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int)))
                    {
                        var ci = cs[obj] as CacheItem;
                        if (ci != null && ci.IsDependOn(tableName)) toDel.Add(obj);
                    }
                }
                foreach (var obj in toDel)
                    cs.Remove(obj);
                return;
            }

            //��̬����
            lock (_TableCache)
            {
                // 2011-03-11 ��ʯͷ �����Ѿ���Ϊ����ƿ����������Ҫ�Ż���ƿ������_TableCache[sql]
                // 2011-11-22 ��ʯͷ ��Ϊ�������ϣ������Ǽ�ֵ������ÿ��ȡֵ��ʱ��Ҫ���²���
                var list = new List<String>();
                foreach (var item in _TableCache)
                    if (item.Value.IsDependOn(tableName)) list.Add(item.Key);

                foreach (var sql in list)
                    _TableCache.Remove(sql);
            }
            lock (_IntCache)
            {
                var list = new List<String>();
                foreach (var item in _IntCache)
                    if (item.Value.IsDependOn(tableName)) list.Add(item.Key);

                foreach (var sql in list)
                    _IntCache.Remove(sql);
            }
        }

        /// <summary>�Ƴ�������һ�����ݱ�Ļ���</summary>
        /// <param name="tableNames"></param>
        public static void Remove(String[] tableNames)
        {
            foreach (var tn in tableNames)
                Remove(tn);
        }

        /// <summary>��ջ���</summary>
        public static void RemoveAll()
        {
            //���󼶻���
            if (Kind == CacheKinds.���󼶻���)
            {
                var cs = Items;
                if (cs == null) return;

                var toDel = new List<Object>();
                foreach (var obj in cs.Keys)
                {
                    var str = obj as String;
                    if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int))) toDel.Add(obj);
                }
                foreach (var obj in toDel)
                    cs.Remove(obj);
                return;
            }
            //��̬����
            lock (_TableCache)
            {
                _TableCache.Clear();
            }
            lock (_IntCache)
            {
                _IntCache.Clear();
            }
        }
        #endregion

        #region ���һ���
        /// <summary>��ȡDataSet����</summary>
        /// <param name="cache">�������</param>
        /// <param name="sql">SQL���</param>
        /// <param name="value">���</param>
        /// <returns></returns>
        static Boolean TryGetItem<T>(Dictionary<String, CacheItem<T>> cache, String sql, out T value)
        {
            value = default(T);

            //�رջ���
            if (Kind == CacheKinds.�رջ���) return false;

            CheckShowStatics(ref NextShow, ref Total, ShowStatics);

            //���󼶻���
            if (Kind == CacheKinds.���󼶻���)
            {
                if (Items == null) return false;

                var prefix = String.Format("XCache_{0}_", typeof(T).Name);
                var ci = Items[prefix + sql] as CacheItem<T>;
                if (ci == null) return false;

                value = ci.Value;
            }
            else
            {
                CacheItem<T> ci = null;
                if (!cache.TryGetValue(sql, out ci) || ci == null) return false;
                value = ci.Value;
            }

            Interlocked.Increment(ref Shoot);

            return true;
        }

        /// <summary>��ȡDataSet����</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="ds">���</param>
        /// <returns></returns>
        public static Boolean TryGetItem(String sql, out DataSet ds) { return TryGetItem(_TableCache, sql, out ds); }

        /// <summary>��ȡInt32����</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="count">���</param>
        /// <returns></returns>
        public static Boolean TryGetItem(String sql, out Int32 count) { return TryGetItem(_IntCache, sql, out count); }
        #endregion

        #region ����
        /// <summary>�������</summary>
        internal static Int32 Count
        {
            get
            {
                //�رջ���
                if (Kind == CacheKinds.�رջ���) return 0;
                //���󼶻���
                if (Kind == CacheKinds.���󼶻���)
                {
                    if (Items == null) return 0;
                    var k = 0;
                    foreach (var obj in Items.Keys)
                    {
                        var str = obj as String;
                        if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int))) k++;
                    }
                    return k;
                }
                return _TableCache.Count + _IntCache.Count;
            }
        }

        /// <summary>���󼶻�����</summary>
        static IDictionary Items { get { return HttpContext.Current != null ? HttpContext.Current.Items : null; } }
        #endregion

        #region ͳ��
        /// <summary>�ܴ���</summary>
        public static Int32 Total;

        /// <summary>����</summary>
        public static Int32 Shoot;

        /// <summary>��һ����ʾʱ��</summary>
        public static DateTime NextShow;

        /// <summary>��鲢��ʾͳ����Ϣ</summary>
        /// <param name="next"></param>
        /// <param name="total"></param>
        /// <param name="show"></param>
        public static void CheckShowStatics(ref DateTime next, ref Int32 total, Func show)
        {
            if (next < DateTime.Now)
            {
                var isfirst = next == DateTime.MinValue;
                next = DAL.Debug ? DateTime.Now.AddMinutes(10) : DateTime.Now.AddHours(24);

                if (!isfirst) show();
            }

            Interlocked.Increment(ref total);
        }

        /// <summary>��ʾͳ����Ϣ</summary>
        public static void ShowStatics()
        {
            if (Total > 0)
            {
                var sb = new StringBuilder();
                // �Ű���Ҫ��һ������ռ�����ַ�λ��
                var str = Kind.ToString();
                sb.AppendFormat("һ������<{0,-" + (20 - str.Length) + "}>", str);
                sb.AppendFormat("�ܴ���{0,7:n0}", Total);
                if (Shoot > 0) sb.AppendFormat("������{0,7:n0}��{1,6:P02}��", Shoot, (Double)Shoot / Total);

                XTrace.WriteLine(sb.ToString());
            }
        }
        #endregion

        #region ��������
        /// <summary>���ݻ�������</summary>
        internal enum CacheKinds
        {
            /// <summary>�رջ���</summary>
            �رջ��� = -2,

            /// <summary>���󼶻���</summary>
            ���󼶻��� = -1,

            /// <summary>���þ�̬����</summary>
            ���þ�̬���� = 0,

            /// <summary>����Ч�ڻ���</summary>
            ��Ч�ڻ��� = 1
        }
        #endregion
    }
}