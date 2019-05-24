using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace OSX.WmiLib
{
    public class WmiQuery<T> : IWmiQueryable<T>
        where T : WmiObject
    {
        private Expression m_Expression;
        private WmiContext m_Context;

        public WmiQuery(WmiContext context)
        {
            m_Context = context;
            m_Expression = Expression.Constant(this);
        }

        public WmiQuery(WmiContext context, Expression expression)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (expression == null)
                throw new ArgumentNullException("expression");

            m_Context = context;
            m_Expression = expression;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_Context.Execute(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public Type ElementType
        {
            get { return typeof(T); }
        }

        public Expression Expression
        {
            get { return m_Expression; }
        }

        public WmiContext Context
        {
            get { return m_Context; }
        }
    }
}
