using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OSX.WmiLib
{
    [DebuggerDisplay("{GetKey() ?? \"(N/A)\"}")]
    public abstract class WmiObject
    {
        protected ManagementBaseObject m_wmiObject;
        private WmiContext m_wmiContext;
        protected Dictionary<Type, IEnumerable<WmiObject>> m_Associators = new Dictionary<Type, IEnumerable<WmiObject>>();

        //public object Id => GetKey();

        public static TResult CreateObject<TResult>(WmiContext context, ManagementBaseObject wmiObject)
            where TResult : WmiObject
        {
            return (TResult)CreateObject(typeof(TResult), context, wmiObject);
        }

        public static object CreateObject(Type type, WmiContext context, ManagementBaseObject wmiObject)
        {
            if (context == null || wmiObject == null) return null;

            var p = wmiObject.Properties.Cast<PropertyData>().ToList();

            var result = (WmiObject)Activator.CreateInstance(type);
            result.m_wmiObject = wmiObject;
            result.m_wmiContext = context;

            var key = result.GetKey();
            if (key != null)
                context.GetCache(type)[result.GetKey()] = result;

            result.OnCreated();
            return result;
        }

        public WmiContext GetContext() => m_wmiContext;

        #region Get
        // instance based
        protected object Get(string property)
        {
            return m_wmiObject == null ? null : Get(m_wmiObject, property);
        }

        protected object Get(Type type, string property)
        {
            var o = Get(property);
            if (type.IsEnum)
                return Enum.ToObject(type, o);
            return o;
        }

        protected TResult Get<TResult>(string property)
        {
            return (TResult)Get(typeof(TResult), property);
        }

        // static access
        protected static object Get(ManagementBaseObject wmiObject, Type type, string property)
        {
            var o = Get(wmiObject, property);
            if (type.IsEnum)
                return Enum.ToObject(type, o);
            return o;
        }

        protected static object Get(ManagementBaseObject wmiObject, string property)
        {
            return wmiObject[property];
        }

        protected static TResult Get<TResult>(ManagementBaseObject wmiObject, string property)
        {
            return (TResult)Get(wmiObject, typeof(TResult), property);
        }
        #endregion

        protected ManagementBaseObject CallEx(string methodName, ManagementBaseObject parameters)
        {
            if (!(m_wmiObject is ManagementObject)) return null;
            return ((ManagementObject)m_wmiObject).InvokeMethod(methodName, parameters, null);
        }

        protected ManagementBaseObject CreateMethodParameters(string methodName, params object[] parameters)
        {
            if (!(m_wmiObject is ManagementObject)) return null;
            var result = ((ManagementObject)m_wmiObject).GetMethodParameters(methodName);

            if (result != null && parameters != null)
                foreach (PropertyData property in result.Properties)
                {
                    var ID = (int)property.Qualifiers["ID"].Value;
                    if (ID < parameters.Length && parameters[ID] != null)
                        property.Value = parameters[ID];
                }
            return result;
        }

        protected object Call(string methodName, params object[] parameters)
        {
            var param = CreateMethodParameters(methodName, parameters);
            var result = ((ManagementObject)m_wmiObject).InvokeMethod(methodName, param, null);
            return result["ReturnValue"];
        }

        protected TResult Call<TResult>(string methodName, params object[] parameters)
        {
            return (TResult)Call(methodName, parameters);
        }

        public virtual object GetKey()
        {
            var a = WmiInfo.GetKeyProperty(GetType());
            return a != null ? Get(a) : null;
        }

        public string GetClassName()
        {
            return WmiInfo.GetClassName(GetType());
        }

        protected virtual void OnCreated() { }

        //protected virtual void OnLoadProperties()
        //{
        //    LoadProperties();
        //}

        //private void LoadProperties()
        //{
        //    foreach (var pi in WmiInfo.GetPropertyInfos(GetType()))
        //    {
        //        pi.Key.SetValue(this, Get(pi.Key.PropertyType, pi.Value));
        //    }
        //}

        public override string ToString()
        {
            return GetKey().ToString();
        }

        protected dynamic _caller(params object[] parameters)
        {
            StackFrame caller = new StackFrame(1);
            MethodInfo method = (MethodInfo)caller.GetMethod();
            return Call(method.Name, parameters);
        }

        //protected ManagementBaseObject _callerex(params object[] parameters)
        //{
        //    StackFrame caller = new StackFrame(1);
        //    MethodInfo method = (MethodInfo)caller.GetMethod();
        //    var param = CreateMethodParameters(method.Name);
        //    return CallEx(method.Name, param);
        //}

        protected dynamic _getter([CallerMemberName] string name = null) => Get(name);

        protected void AddAssociators<TResult>()
            where TResult : WmiObject, new()
        {
            m_Associators.Add(typeof(TResult), new List<WmiObject>(this.Associators<TResult>()));
        }

        public IEnumerable<TResult> GetAssociators<TResult>()
            where TResult : WmiObject, new()
        {
            if (!m_Associators.ContainsKey(typeof(TResult)))
                AddAssociators<TResult>();

            return m_Associators[typeof(TResult)].Cast<TResult>();
        }

        //public TResult Find<TResult>(WmiContext context, string key)
        //    where TResult : WmiObject 
        //{
        //    TResult obj = context.Find<TResult>(key);
        //    if (obj == null)
        //        obj = LoadObject(context, key);
        //    return obj;
        //}
    }

    //internal abstract class WmiObject<T> : WmiObject
    //    where T : WmiObject<T>
    //{
    //private static IEnumerable<T> LoadAllObjects(WmiContext context)
    //{
    //    return LoadObjects(context, null);
    //}

    //private static T LoadObject(WmiContext context, object ID)
    //{
    //    var a = typeof(T).GetCustomAttribute<WmiClassAttribute>();
    //    if (a == null) throw new ArgumentNullException("Attribute missing");
    //    return LoadObjects(context, $"{a.KeyProperty}=\"{WmiInfo.GetWmiQueryValue(ID)}\"").FirstOrDefault();
    //}

    //private static IEnumerable<T> LoadObjects(WmiContext context, string condition)
    //{
    //    var className = WmiInfo.GetClassName(typeof(T));
    //    if (className == null)
    //        throw new ArgumentNullException("Attribute missing");

    //    string query = string.Format("SELECT * FROM {0}", className);
    //    if (!string.IsNullOrEmpty(condition))
    //        query += " WHERE " + condition;

    //    var searcher = new ManagementObjectSearcher();
    //    searcher.Query = new ObjectQuery(query);
    //    searcher.Options.ReturnImmediately = false;
    //    searcher.Options.DirectRead = true;

    //    var result = new List<T>();
    //    foreach (ManagementObject wmiObject in searcher.Get())
    //    {
    //        T obj = CreateObject<T>(context, wmiObject);
    //        result.Add(obj);
    //    }
    //    return result;
    //}

    //}
}
