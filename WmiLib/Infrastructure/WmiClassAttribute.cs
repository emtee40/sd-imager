using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace OSX.WmiLib.Infrastructure
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal class WmiClassAttribute : Attribute
    {
        public string Path { get; private set; }
        public string ClassName { get; private set; }
        public string KeyProperty { get; private set; }

        public WmiClassAttribute(string className) : this(className, null) { }

        public WmiClassAttribute(string className, string keyProperty)
        {
            Debug.WriteLine(className);

            var p = new ManagementPath(className);
            p.SetAsClass();
            Path = p.Path;
            ClassName = p.ClassName;
            KeyProperty = keyProperty;
        }
    }
}
