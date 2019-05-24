using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace OSX.WmiLib.Infrastructure
{
    internal class WmiQueryBuilder : ExpressionVisitor
    {
        private WmiContext m_Context;
        private bool m_FetchAllProperties;
        private StringBuilder sb;

        public WmiQueryBuilder(WmiContext context)
        {
            m_Context = context;
        }

        public string Translate(IWmiQueryable query, bool fetchAllProperties = false)
        {
            m_FetchAllProperties = fetchAllProperties;
            var a = query.ElementType.GetCustomAttribute<WmiClassAttribute>(false);
            if (a == null) throw new InvalidOperationException();

            sb = new StringBuilder();
            var e = Evaluator.PartialEval(query.Expression);
            Visit(e);
            return sb.ToString();
        }

        private static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
                e = ((UnaryExpression)e).Operand;
            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            switch (m.Method.Name)
            {
                case "Where":
                    Visit(m.Arguments[0]);
                    sb.Append(" WHERE ");
                    LambdaExpression lambda = (LambdaExpression)StripQuotes(m.Arguments[1]);
                    Visit(lambda.Body);
                    return m;

                case "Associators":
                    var arg = (ConstantExpression)m.Arguments[0];
                    sb.Append("ASSOCIATORS OF {");
                    sb.Append(WmiInfo.GetClassName(arg.Value.GetType()));
                    sb.Append("=\"");
                    sb.Append(WmiInfo.GetWmiQueryValue(((WmiObject)arg.Value).GetKey()));
                    sb.Append("\"} WHERE ResultClass=");
                    sb.Append(WmiInfo.GetClassName(m.Method.GetGenericArguments()[0]));
                    return m;

                case "Contains":
                    Visit(m.Object);
                    sb.Append(" LIKE \"%");
                    var oldSB = sb;
                    sb = new StringBuilder();
                    Visit(m.Arguments[0]);
                    var s = sb.ToString();
                    sb = oldSB;
                    sb.Append(s.Substring(1, s.Length - 2));
                    sb.Append("%\"");
                    return m;
            }
            throw new NotSupportedException($"The method '{m.Method.Name}' is not supported");
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            sb.Append("(");
            Visit(b.Left);
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    sb.Append(" AND ");
                    break;
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    sb.Append(" OR");
                    break;
                case ExpressionType.Equal:
                    sb.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    sb.Append(" <> ");
                    break;
                case ExpressionType.LessThan:
                    sb.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    sb.Append(" <= ");
                    break;
                case ExpressionType.GreaterThan:
                    sb.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    sb.Append(" >= ");
                    break;
                default:
                    throw new NotSupportedException($"The binary operator '{b.NodeType}' is not supported");
            }
            Visit(b.Right);
            sb.Append(")");
            return b;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    sb.Append(" NOT ");
                    Visit(u.Operand);
                    break;
                case ExpressionType.Convert:
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException($"The unary operator '{u.NodeType}' is not supported");
            }
            return u;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            IWmiQueryable q = c.Value as IWmiQueryable;
            if (q != null)
            {
                // assume constant nodes w/ IQueryables are table references
                if (sb.Length == 0)
                {
                    sb.Append("SELECT ");
                    if (m_FetchAllProperties)
                        sb.Append("*");
                    else
                        sb.Append(string.Join(",", WmiInfo.GetWmiPropertyNames(q.ElementType)));
                    sb.Append(" FROM ");
                }
                sb.Append(WmiInfo.GetClassName(q.ElementType));
            }
            else if (c.Value == null)
            {
                sb.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        sb.Append(((bool)c.Value) ? 1 : 0);
                        break;
                    case TypeCode.String:
                        sb.AppendFormat("\"{0}\"", WmiInfo.GetWmiQueryValue(c.Value));
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException($"The constant for '{c.Value}' is not supported");
                    default:
                        sb.Append(c.Value);
                        break;
                }
            }
            return c;
        }

        protected override Expression VisitMember(MemberExpression m)
        {
            if (m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
            {
                if (m.Member.Name == "GetKey" && m.Member.MemberType == MemberTypes.Method)
                    sb.Append(WmiInfo.GetKeyProperty(m.Expression.Type));
                else
                {
                    var a = m.Member.GetCustomAttribute<WmiPropertyAttribute>();
                    var property = m.Member as PropertyInfo;
                    if (property == null)
                        throw new MemberAccessException($"'{m.Member.Name} is not a property of class '{m.Type.Name}'");

                    //if (a == null)
                    //    throw new MemberAccessException($"'{m.Member.Name}' is not a WMI property of class '{m.Type.Name}'");

                    var propName = a?.Property ?? m.Member.Name;
                    if (property.PropertyType == typeof(bool))
                        sb.Append($"({propName} <> 0)");
                    else
                        sb.Append(propName);
                }
                return m;
            }
            throw new NotSupportedException($"The member '{m.Member.Name}' is not supported");
        }

    }
}
