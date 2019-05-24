using System;

namespace OSX.WmiLib.Infrastructure
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    internal class WmiPropertyAttribute : Attribute
    {
        public string Property { get; private set; }
        public WmiPropertyAttribute(string Property)
        {
            this.Property = Property;
        }
        public WmiPropertyAttribute() : this(null) { }
    }
}
