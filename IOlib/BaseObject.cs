using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Collections;

namespace OSX.IOlib
{
    [DebuggerDisplay("{ID}")]
    internal abstract class BaseObject
    {
        protected ManagementObject m_wmiObject;
        protected Dictionary<Type, IEnumerable<object>> m_Associators;
        public object ID { get { return GetKey(); } }

        #region Get
        protected T Get<T>(ManagementBaseObject Object, string Property)
        {
            return (T)Get(Object, typeof(T), Property);
        }

        protected T Get<T>(string Property)
        {
            return (T)Get(typeof(T), Property);
        }

        protected object Get(Type type, string Property)
        {
            var o = Get(Property);
            if (type.IsEnum)
                return Enum.ToObject(type, o);
            return o;
        }

        protected object Get(ManagementBaseObject Object, Type type, string Property)
        {
            var o = Get(Object, Property);
            if (type.IsEnum)
                return Enum.ToObject(type, o);
            return o;
        }

        protected object Get(string Property)
        {
            return Get(m_wmiObject, Property);
        }

        protected object Get(ManagementBaseObject Object, string Property)
        {
            return Object[Property];
        }
        #endregion

        protected object Call(string MethodName, params object[] parameters)
        {
            return m_wmiObject.InvokeMethod(MethodName, parameters);
        }

        protected virtual object GetKey()
        {
            var a = GetKeyProperty();
            return a == null ? Get<object>("Name") : Get<object>(a);
        }

        public string GetKeyProperty()
        {
            var a = GetType().GetCustomAttribute<WmiClassAttribute>();
            return a == null ? null : a.KeyProperty;
        }

        public string GetClassName()
        {
            var a = GetType().GetCustomAttribute<WmiClassAttribute>();
            return a == null ? null : a.ClassName;
        }

        protected virtual void OnCreated() { }

        protected virtual void OnLoadProperties()
        {
            LoadProperties();
        }

        private void LoadProperties()
        {
            var t = this.GetType();
            foreach (var pi in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
            {
                var a = pi.GetCustomAttribute<WmiPropertyAttribute>();
                if (a != null)
                    pi.SetValue(this, Get(pi.PropertyType, a.Property ?? pi.Name));
            }
        }

        public override string ToString()
        {
            return ID.ToString();
        }

        protected static string GetWmiQueryValue(object value)
        {
            return value.ToString().Replace(@"\", @"\\");
        }
    }

    internal abstract class BaseObject<TObject> : BaseObject
        where TObject : BaseObject<TObject>
    {
        private static Dictionary<object, TObject> m_Cache;
        private static Dictionary<object, TObject> Cache { get { if (m_Cache == null) CreateCache(); return m_Cache; } }

        public static IEnumerable<TObject> AsEnumerable()
        {
            Reset();
            LoadAllObjects();
            return Cache.Values;
        }

        public static void FillCache()
        {
            Reset();
            LoadAllObjects();
        }

        private static void CreateCache()
        {
            m_Cache = new Dictionary<object, TObject>();
        }

        private static void LoadAllObjects()
        {
            LoadObjects(null);
        }

        private static void LoadObject(object ID)
        {
            var a = typeof(TObject).GetCustomAttribute<WmiClassAttribute>();
            if (a == null)
                throw new ArgumentNullException("Attribute missing");
            LoadObjects(string.Format("{0}=\"{1}\"", a.KeyProperty, GetWmiQueryValue(ID)));
        }

        private static void LoadObjects(string Condition)
        {
            var a = typeof(TObject).GetCustomAttribute<WmiClassAttribute>();
            if (a == null)
                throw new ArgumentNullException("Attribute missing");

            string q = string.Format("SELECT * FROM {0}", a.ClassName);
            if (!string.IsNullOrEmpty(Condition))
                q += " WHERE " + Condition;

            var os = new ManagementObjectSearcher();
            os.Query = new ObjectQuery(q);
            os.Options.ReturnImmediately = false;
            os.Options.DirectRead = true;

            foreach (ManagementObject o in os.Get().Cast<ManagementObject>())
            {
                TObject x = (TObject)Activator.CreateInstance(typeof(TObject));
                x.m_wmiObject = o;
                x.OnLoadProperties();
                x.OnCreated();
                if (Cache.ContainsKey(x.ID))
                    Cache.Remove(x.ID);
                Cache.Add(x.ID, x);
            }
        }

        protected void AddAssociators<T>()
            where T : BaseObject<T>
        {
            var a0 = GetType().GetCustomAttribute<WmiClassAttribute>();
            if (a0 == null)
                throw new ArgumentException("Attribute missing");

            var a1 = typeof(T).GetCustomAttribute<WmiClassAttribute>();
            if (a1 == null)
                throw new ArgumentException("Attribute missing");

            if (m_Associators == null)
                m_Associators = new Dictionary<Type, IEnumerable<object>>();

            var os = new ManagementObjectSearcher(string.Format("ASSOCIATORS OF {{{0}.{1}=\"{2}\"}} WHERE ResultClass={3}",
                a0.ClassName, a0.KeyProperty, GetWmiQueryValue(GetKey()), a1.ClassName));
            os.Options.DirectRead = true;
            os.Options.ReturnImmediately = false;

            var l = new List<object>();
            foreach (ManagementObject o in os.Get().Cast<ManagementObject>())
                l.Add(o[a1.KeyProperty]);

            m_Associators.Add(typeof(T), l);
        }

        public IEnumerable<T> GetAssociators<T>()
            where T : BaseObject<T>
        {
            if (m_Associators == null || !m_Associators.ContainsKey(typeof(T)))
                AddAssociators<T>();
            return m_Associators[typeof(T)].Select(z => BaseObject<T>.Find(z)).Where(z => z != null);
        }

        public static TObject Find(object Key)
        {
            TObject o = default(TObject);
            if (!Cache.ContainsKey(Key))
                LoadObject(Key);
            Cache.TryGetValue(Key, out o);
            return o;
        }

        public static void Reset()
        {
            m_Cache = null;
        }
    }
}
