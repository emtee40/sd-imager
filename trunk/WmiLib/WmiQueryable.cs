using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace OSX.WmiLib
{
    public interface IWmiQueryable<T> : IEnumerable<T>, IWmiQueryable { }

    public interface IWmiQueryable : IEnumerable
    {
        Type ElementType { get; }
        Expression Expression { get; }
        WmiContext Context { get; }
    }

    public static class WmiQueryable
    {
        public static IWmiQueryable<TSource> Where<TSource>(this IWmiQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
            where TSource : WmiObject
        {
            return new WmiQuery<TSource>(source.Context, Expression.Call(((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(typeof(TSource)),
                source.Expression, Expression.Quote(predicate)));
        }

        public static IWmiQueryable<TResult> Associators<TResult>(this WmiObject source)
            where TResult : WmiObject
        {
            var q = new WmiQuery<TResult>(source.GetContext(), Expression.Call(((MethodInfo)MethodBase.GetCurrentMethod()).MakeGenericMethod(
                typeof(TResult)), Expression.Constant(source, typeof(WmiObject))));
            return q;
        }
    }
}
