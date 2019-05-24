using OSX.WmiLib.Infrastructure;
using OSX.WmiLib.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace OSX.WmiLib
{
    public class WmiContext
    {
        private Dictionary<string, ManagementScope> m_Scopes = new Dictionary<string, ManagementScope>();
        private WmiQueryBuilder m_QueryBuilder;
        private bool m_FetchAllProperties = true;
        private Dictionary<Type, Dictionary<object, WmiObject>> m_Cache;

        public WmiContext()
        {
            CreateCache();
        }

        private void CreateCache()
        {
            m_Cache = new Dictionary<Type, Dictionary<object, WmiObject>>();
        }

        public IWmiQueryable<T> Instances<T>()
            where T : WmiObject, new()
        {
            return new WmiQuery<T>(this);
        }

        public bool FetchAllProperties { get { return m_FetchAllProperties; } set { m_FetchAllProperties = value; } }

        public IEnumerator<TResult> Execute<TResult>(IWmiQueryable<TResult> query)
            where TResult : WmiObject
        {
            if (m_QueryBuilder == null)
                m_QueryBuilder = new WmiQueryBuilder(this);

            var querystring = m_QueryBuilder.Translate(query, m_FetchAllProperties);
            var objQuery = new ObjectQuery(querystring);
            var enumOptions = new EnumerationOptions()
            {
                ReturnImmediately = false,
                Rewindable = false,
                DirectRead = true,
            };
            var wmiSearcher = new ManagementObjectSearcher(GetScope(typeof(TResult)), objQuery, enumOptions);
            Debug.WriteLine(querystring);

            foreach (ManagementObject o in wmiSearcher.Get())
                yield return WmiObject.CreateObject<TResult>(this, o);
            wmiSearcher.Dispose();
        }

        private TResult LoadObject<TResult>(object key)
            where TResult : WmiObject 
        {
            return new WmiQuery<TResult>(this).Where(z => z.GetKey() == key).SingleOrDefault();
        }

        private ManagementScope GetScope(Type type)
        {
            ManagementScope scope = null;
            var path = new ManagementPath(WmiInfo.GetPath(type));
            if (!m_Scopes.TryGetValue(path.NamespacePath, out scope))
            {
                scope = new ManagementScope(path.NamespacePath);
                m_Scopes[path.NamespacePath] = scope;
            }
            return scope;
        }

        internal Dictionary<object, WmiObject> GetCache(Type type)
        {
            Dictionary<object, WmiObject> entry = null;
            if (!m_Cache.TryGetValue(type, out entry))
            {
                entry = new Dictionary<object, WmiObject>();
                m_Cache[type] = entry;
            }
            return entry;
        }

        public void Reset()
        {
            CreateCache();
        }

        public TResult Find<TResult>(string key)
            where TResult : WmiObject
        {
            WmiObject obj = null;
            if (!GetCache(typeof(TResult)).TryGetValue(key, out obj))
                obj = LoadObject<TResult>(key);
            return (TResult)obj;
        }

        public IWmiQueryable<DiskDrive> DiskDrives { get { return Instances<DiskDrive>(); } }
        public IWmiQueryable<DiskPartition> DiskPartitions { get { return Instances<DiskPartition>(); } }
        public IWmiQueryable<LogicalDisk> LogicalDisks { get { return Instances<LogicalDisk>(); } }
        public IWmiQueryable<Volume> Volumes { get { return Instances<Volume>(); } }
        public IWmiQueryable<PhysicalMedia> PhysicalMedias => Instances<PhysicalMedia>();

        public IEnumerable<DiskDrive> GetRemovableDiskDrives()
        {
            return DiskDrives.Where(z => z.MediaType.Contains("removable"));
        }
    }
}
