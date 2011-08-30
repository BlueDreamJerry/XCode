﻿using System;
using System.Collections.Generic;
using System.Text;

namespace XCode.DataAccessLayer
{
    /// <summary>
    /// 数据表
    /// </summary>
    public interface IDataTable : ICloneable
    {
        #region 属性
        /// <summary>
        /// 编号
        /// </summary>
        Int32 ID { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        String Name { get; set; }

        /// <summary>
        /// 别名
        /// </summary>
        String Alias { get; set; }

        /// <summary>
        /// 所有者
        /// </summary>
        String Owner { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        DatabaseType DbType { get; set; }

        /// <summary>
        /// 是否视图
        /// </summary>
        Boolean IsView { get; set; }

        /// <summary>
        /// 说明
        /// </summary>
        String Description { get; set; }
        #endregion

        #region 扩展属性
        /// <summary>数据列集合</summary>
        List<IDataColumn> Columns { get; }

        /// <summary>数据关系集合</summary>
        List<IDataRelation> Relations { get; }

        /// <summary>数据索引集合</summary>
        List<IDataIndex> Indexes { get; }

        /// <summary>主键集合</summary>
        IDataColumn[] PrimaryKeys { get; }
        #endregion

        #region 方法
        /// <summary>
        /// 创建数据列
        /// </summary>
        /// <returns></returns>
        IDataColumn CreateColumn();

        /// <summary>
        /// 创建数据关系
        /// </summary>
        /// <returns></returns>
        IDataRelation CreateRelation();

        /// <summary>
        /// 创建数据索引
        /// </summary>
        /// <returns></returns>
        IDataIndex CreateIndex();

        /// <summary>
        /// 根据字段名获取字段
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IDataColumn GetColumn(String name);

        /// <summary>
        /// 根据字段名数组获取字段数组
        /// </summary>
        /// <param name="names"></param>
        /// <returns></returns>
        IDataColumn[] GetColumns(String[] names);

        /// <summary>
        /// 连接另一个表，处理两表间关系
        /// </summary>
        /// <param name="table"></param>
        void Connect(IDataTable table);

        /// <summary>
        /// 修正数据
        /// </summary>
        void Fix();
        #endregion
    }
}