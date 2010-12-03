using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace XCode
{
	/// <summary>
	/// ����ָ�����������󶨵������ݱ�ı���
	/// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class BindTableAttribute : Attribute
	{
        private String _Name;
        /// <summary>����</summary>
        public String Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        private String _Description;
        /// <summary>����</summary>
        public String Description
        {
            get { return _Description; }
            set { _Description = value; }
        }

        private String _ConnName;
        /// <summary>������</summary>
        public String ConnName
        {
            get { return _ConnName; }
            set { _ConnName = value; }
        }

		/// <summary>
		/// ���캯��
		/// </summary>
        /// <param name="name">����</param>
		public BindTableAttribute(String name)
		{
			Name = name;
		}
		/// <summary>
		/// ���캯��
		/// </summary>
        /// <param name="name">����</param>
		/// <param name="description">����</param>
        public BindTableAttribute(String name, String description)
		{
            Name = name;
			Description = description;
		}

        /// <summary>
        /// ����Ӧ�������ͳ�Ա���Զ������ԡ�
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static BindTableAttribute GetCustomAttribute(MemberInfo element)
        {
            return GetCustomAttribute(element, typeof(BindTableAttribute)) as BindTableAttribute;
        }
    }
}