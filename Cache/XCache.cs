using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Web;
using NewLife.Configuration;

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

        /// <summary>
        /// ���������Ч�ڡ�
        /// -2	�رջ���
        /// -1	�Ƕ�ռ���ݿ⣬���ⲿϵͳ�������ݿ⣬ʹ�����󼶻��棻
        ///  0	���þ�̬���棻
        /// >0	��̬����ʱ�䣬��λ���룻
        /// </summary>
        public static Int32 Expiration = -1;

        /// <summary>���ݻ�������</summary>
        public static XCacheType CacheType
        {
            get
            {
                if (Expiration > 0) return XCacheType.Period;
                return (XCacheType)Expiration;
            }
        }

        /// <summary>
        /// ��ʼ�����á�
        /// ��ȡ���ã�
        /// </summary>
        static XCache()
        {
            //��ȡ������Ч��
            Expiration = Config.GetMutilConfig<Int32>(-2, "XCode.Cache.Expiration", "XCacheExpiration");
            //��ȡ�������
            CheckPeriod = Config.GetMutilConfig<Int32>(5, "XCode.Cache.CheckPeriod", "XCacheCheckPeriod");

            if (Expiration < -2) Expiration = -2;
            if (CheckPeriod <= 0) CheckPeriod = 5;
        }
        #endregion

        #region ����ά��
        /// <summary>
        /// ����ά����ʱ��
        /// </summary>
        private static Timer AutoCheckCacheTimer;

        /// <summary>
        /// ά����ʱ���ļ�����ڣ�Ĭ��5��
        /// </summary>
        public static Int32 CheckPeriod = 5;

        /// <summary>
        /// ά��
        /// </summary>
        /// <param name="obj"></param>
        private static void Check(Object obj)
        {
            //�رջ��桢���þ�̬��������󼶻���ʱ������Ҫ���
            if (CacheType != XCacheType.Period) return;

            if (_TableCache.Count > 0)
            {
                lock (_TableCache)
                {
                    if (_TableCache.Count > 0)
                    {
                        List<String> toDel = null;
                        foreach (String sql in _TableCache.Keys)
                            if (_TableCache[sql].ExpireTime < DateTime.Now)
                            {
                                if (toDel == null) toDel = new List<String>();
                                toDel.Add(sql);
                            }
                        if (toDel != null && toDel.Count > 0)
                            foreach (String sql in toDel)
                                _TableCache.Remove(sql);
                    }
                }
            }
            if (_IntCache.Count > 0)
            {
                lock (_IntCache)
                {
                    if (_IntCache.Count > 0)
                    {
                        List<String> toDel = null;
                        foreach (String sql in _IntCache.Keys)
                            if (_IntCache[sql].ExpireTime < DateTime.Now)
                            {
                                if (toDel == null) toDel = new List<String>();
                                toDel.Add(sql);
                            }
                        if (toDel != null && toDel.Count > 0)
                            foreach (String sql in toDel)
                                _IntCache.Remove(sql);
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
            if (AutoCheckCacheTimer != null) return;

            // ������ʱ���������ӳ�ʱ�䣬ʵ���ϲ�����
            AutoCheckCacheTimer = new Timer(new TimerCallback(Check), null, Timeout.Infinite, Timeout.Infinite);
            // �ı䶨ʱ��Ϊ5��󴥷�һ�Ρ�
            AutoCheckCacheTimer.Change(CheckPeriod * 1000, CheckPeriod * 1000);
        }
        #endregion

        #region ��ӻ���
        /// <summary>������ݱ��档</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="ds">�������¼��</param>
        /// <param name="tableNames">��������</param>
        public static void Add(String sql, DataSet ds, String[] tableNames)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return;

            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return;
                HttpContext.Current.Items.Add(_dst + sql, new CacheItem<DataSet>(tableNames, ds));
                return;
            }

            //��̬����
            if (_TableCache.ContainsKey(sql)) return;
            lock (_TableCache)
            {
                if (_TableCache.ContainsKey(sql)) return;

                _TableCache.Add(sql, new CacheItem<DataSet>(tableNames, ds, Expiration));
            }

            //����Ч��
            if (CacheType == XCacheType.Period) CreateTimer();
        }

        /// <summary>���Int32���档</summary>
        /// <param name="sql">SQL���</param>
        /// <param name="n">����������</param>
        /// <param name="tableNames">��������</param>
        public static void Add(String sql, Int32 n, String[] tableNames)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return;

            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return;
                HttpContext.Current.Items.Add(_int + sql, new CacheItem<Int32>(tableNames, n));
                return;
            }

            //��̬����
            if (_IntCache.ContainsKey(sql)) return;
            lock (_IntCache)
            {
                if (_IntCache.ContainsKey(sql)) return;

                _IntCache.Add(sql, new CacheItem<Int32>(tableNames, n, Expiration));
            }

            //����Ч��
            if (CacheType == XCacheType.Period) CreateTimer();
        }
        #endregion

        #region ɾ������
        /// <summary>�Ƴ�������ĳ�����ݱ�Ļ���</summary>
        /// <param name="tableName">���ݱ�</param>
        public static void Remove(String tableName)
        {
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return;

                var cs = HttpContext.Current.Items;
                List<Object> toDel = new List<Object>();
                foreach (Object obj in cs.Keys)
                {
                    String str = obj as String;
                    if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int)))
                    {
                        CacheItem ci = cs[obj] as CacheItem;
                        if (ci != null && ci.IsDependOn(tableName)) toDel.Add(obj);
                    }
                }
                foreach (Object obj in toDel)
                    cs.Remove(obj);
                return;
            }

            //��̬����
            lock (_TableCache)
            {
                // 2011-03-11 ��ʯͷ �����Ѿ���Ϊ����ƿ����������Ҫ�Ż���ƿ������_TableCache[sql]
                // 2011-11-22 ��ʯͷ ��Ϊ�������ϣ������Ǽ�ֵ������ÿ��ȡֵ��ʱ��Ҫ���²���
                List<String> toDel = new List<String>();
                foreach (var item in _TableCache)
                    if (item.Value.IsDependOn(tableName)) toDel.Add(item.Key);

                foreach (String sql in toDel)
                    _TableCache.Remove(sql);
            }
            lock (_IntCache)
            {
                List<String> toDel = new List<String>();
                foreach (var item in _IntCache)
                    if (item.Value.IsDependOn(tableName)) toDel.Add(item.Key);

                foreach (String sql in toDel)
                    _IntCache.Remove(sql);
            }
        }

        /// <summary>�Ƴ�������һ�����ݱ�Ļ���</summary>
        /// <param name="tableNames"></param>
        public static void Remove(String[] tableNames)
        {
            foreach (String tn in tableNames) Remove(tn);
        }

        /// <summary>��ջ���</summary>
        public static void RemoveAll()
        {
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return;

                var cs = HttpContext.Current.Items;
                List<Object> toDel = new List<Object>();
                foreach (Object obj in cs.Keys)
                {
                    String str = obj as String;
                    if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int))) toDel.Add(obj);
                }
                foreach (Object obj in toDel)
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
        /// <summary>���һ������Ƿ����ĳһ��</summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public static Boolean Contain(String sql)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return false;
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return false;
                return HttpContext.Current.Items.Contains(_dst + sql);
            }
            return _TableCache.ContainsKey(sql);
        }

        /// <summary>
        /// ��ȡDataSet����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public static DataSet Item(String sql)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return null;
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return null;
                CacheItem<DataSet> ci = HttpContext.Current.Items[_dst + sql] as CacheItem<DataSet>;
                if (ci == null) return null;
                return ci.Value;
            }
            return _TableCache[sql].Value;
        }

        /// <summary>
        /// ����Int32�������Ƿ����ĳһ��
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public static Boolean IntContain(String sql)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return false;
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return false;
                return HttpContext.Current.Items.Contains(_int + sql);
            }
            return _IntCache.ContainsKey(sql);
        }

        /// <summary>
        /// ��ȡInt32����
        /// </summary>
        /// <param name="sql">SQL���</param>
        /// <returns></returns>
        public static Int32 IntItem(String sql)
        {
            //�رջ���
            if (CacheType == XCacheType.Close) return -1;
            //���󼶻���
            if (CacheType == XCacheType.RequestCache)
            {
                if (HttpContext.Current == null) return -1;
                CacheItem<Int32> ci = HttpContext.Current.Items[_int + sql] as CacheItem<Int32>;
                if (ci == null) return -1;
                return ci.Value;
            }
            return _IntCache[sql].Value;
        }
        #endregion

        #region ����
        /// <summary>
        /// �������
        /// </summary>
        internal static Int32 Count
        {
            get
            {
                //�رջ���
                if (CacheType == XCacheType.Close) return 0;
                //���󼶻���
                if (CacheType == XCacheType.RequestCache)
                {
                    if (HttpContext.Current == null) return 0;
                    Int32 k = 0;
                    foreach (Object obj in HttpContext.Current.Items.Keys)
                    {
                        String str = obj as String;
                        if (!String.IsNullOrEmpty(str) && (str.StartsWith(_dst) || str.StartsWith(_int))) k++;
                    }
                    return k;
                }
                return _TableCache.Count + _IntCache.Count;
            }
        }

        //private static Boolean? _Debug;
        ///// <summary>�Ƿ����</summary>
        //public static Boolean Debug
        //{
        //    get
        //    {
        //        if (_Debug == null) _Debug = Config.GetConfig<Boolean>("XCode.Cache.Debug", false);
        //        return _Debug.Value;
        //    }
        //    set { _Debug = value; }
        //}
        #endregion
    }

    /// <summary>���ݻ�������</summary>
    internal enum XCacheType
    {
        /// <summary>�رջ���</summary>
        Close = -2,

        /// <summary>���󼶻���</summary>
        RequestCache = -1,

        /// <summary>���þ�̬����</summary>
        Infinite = 0,

        /// <summary>����Ч�ڻ���</summary>
        Period = 1
    }
}