using OSX.WmiLib.Infrastructure;
using System;
using System.Collections.Generic;
using System.Management;
using System.Reflection;

namespace OSX.WmiLib
{
    internal static class WmiInfo
    {
        private class WmiClassInfo
        {
            public string className;
            public string path;
            public string keyProperty;
            public Dictionary<PropertyInfo, string> propertyInfo;
        }

        private static Dictionary<Type, WmiClassInfo> m_classInfo = new Dictionary<Type, WmiClassInfo>();

        public static string GetClassName(Type type) => GetClassInfo(type)?.className;

        public static string GetPath(Type type) => GetClassInfo(type)?.path;

        public static string GetKeyProperty(Type type) => GetClassInfo(type)?.keyProperty;

        public static Dictionary<PropertyInfo, string> GetPropertyInfos(Type type) => GetClassInfo(type)?.propertyInfo;

        public static string GetWmiQueryValue(object value)
        {
            if (value is string)
                return ((string)value).Replace(@"\", @"\\").Replace("\"", "\\\"");
            else
                return value.ToString();
        }

        public static IEnumerable<string> GetWmiPropertyNames(Type type)
        {
            if (!type.IsSubclassOf(typeof(WmiObject)))
                return null;

            var result = new List<string>();
            result.Add(GetKeyProperty(type));
            foreach (var pi in GetPropertyInfos(type))
            {
                if (!result.Contains(pi.Value))
                    result.Add(pi.Value);
            }
            return result;
        }

        private static string FindKeyProperty(string wmiPath)
        {
            var c = new ManagementClass(wmiPath);
            var p = c.Properties;

            foreach (var prop in p)
                foreach (var q in prop.Qualifiers)
                    if (q.Name == "key")
                        return prop.Name;
            return null;
        }

        private static WmiClassInfo GetClassInfo(Type type)
        {
            WmiClassInfo result = null;
            if (!m_classInfo.TryGetValue(type, out result))
            {
                result = GenerateClassInfo(type);
                m_classInfo[type] = result;
            }
            return result;
        }

        private static WmiClassInfo GenerateClassInfo(Type type)
        {
            var a = type.GetCustomAttribute<WmiClassAttribute>();
            if (a == null)
                throw new Exception($"WmiClassAttribute missing on class '{type.Name}'");

            var result = new WmiClassInfo();
            result.className = a.ClassName;
            result.path = a.Path;
            result.keyProperty = a.KeyProperty ?? FindKeyProperty(a.Path);

            //result.propertyInfo = new Dictionary<PropertyInfo, string>();
            //foreach (var propertyInfo in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
            //{
            //    var a = propertyInfo.GetCustomAttribute<WmiPropertyAttribute>();
            //    if (a != null)
            //        result.Add(propertyInfo, a.Property ?? propertyInfo.Name);
            //}

            return result;
        }
    }
}
