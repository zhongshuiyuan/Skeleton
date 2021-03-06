using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Ataoge.Data;
using Ataoge.Data.Metadata;

namespace Ataoge.Linq
{
    public static class ExpressionHelper
    {

        public static Expression<Func<TEntity, object>> BuildPropertyExpression<TEntity>(string propertyName)
        {
            //1.创建表达式参数（指定参数或变量的类型:p）
            ParameterExpression param = Expression.Parameter(typeof(TEntity));
            //2.构建表达式体(类型包含指定的属性:p.Name)  
            MemberExpression body = Expression.Property(param, propertyName);
            //3.根据参数和表达式体构造一个lambda表达式  
            return Expression.Lambda<Func<TEntity, object>>(body, param);
        }

        public static Expression<Func<TEntity, bool>> BuildPrediateExpression<TEntity, TValue>(string propertyName, string op, TValue value)
        {
            ParameterExpression param = Expression.Parameter(typeof(TEntity), "t");

            var left = Expression.Property(param, propertyName);
            var right = value.GetVarName(v => value);

            Expression body;
            switch (op.ToLower())
            {
                case "gt":
                    body = Expression.GreaterThan(left, right);
                    break;
                case "eq":
                default:
                    body = Expression.Equal(left, right);
                    break;
            }
            return Expression.Lambda<Func<TEntity, bool>>(body, param);
        }

        public static ParameterExpression BuildParameterExpression<TEntity>(string paramName = null)
        {
            if (string.IsNullOrEmpty(paramName))
                return Expression.Parameter(typeof(TEntity), "t");
            return Expression.Parameter(typeof(TEntity), paramName);
        }

        public static ParameterExpression BuildParameterExpression(Type type, string paramName = null)
        {
            if (string.IsNullOrEmpty(paramName))
                return Expression.Parameter(type, "t");
            return Expression.Parameter(type, paramName);
        }

        public static Expression BuildBinaryExpression<TEntity>(ParameterExpression parameterExpression, string propertyName, string op, Expression valueExpression)
        {
            //待完善
            if (propertyName.IndexOf('.') > 0)
            {
                string[] properties = propertyName.Split('.');
                if (properties.Length > 2)
                    throw new NotSupportedException();

                var classType = typeof(TEntity).GetProperty(properties[0]).PropertyType;
                var paramExpression = BuildParameterExpression(classType, "st");
                return null;
            }

            var left = Expression.Property(parameterExpression, propertyName);
            var right = valueExpression;

            Expression body;
            switch (op.ToLower())
            {
                case "gt":
                    body = Expression.GreaterThan(left, right);
                    break;
                case "eq":
                default:
                    body = Expression.Equal(left, right);
                    break;
            }

            return body;
        }

        private static Expression GetVarName<T>(Expression<Func<T, T>> exp)
        {
            return exp.Body;
        }

        private static Expression GetVarName<T>(this T t, Expression<Func<T, T>> exp)
        {
            return exp.Body;
        }

        public static Expression BuildValueExpression(string valueType, string value)
        {
            switch (valueType)
            {
                case "Int32":
                    int intValue = int.Parse(value);
                    return GetVarName<int>(t => intValue);
                case "Int64":
                    long longValue = long.Parse(value);
                    return GetVarName<long>(t => longValue);
                case "Single":
                case "Decimal":
                case "Double":
                    double doubleValue = double.Parse(value);
                    return GetVarName<double>(t => doubleValue);
                case "DateTime":
                    DateTime dateTime = DateTime.Parse(value);
                    return GetVarName<DateTime>(t => dateTime);
                case "String":
                default:
                    return GetVarName<string>(t => value);
            }
        }


        internal static Expression BuildColumnConditionExpression(ParameterExpression parameterExpression, Type entityType, string propertyName, string valueType, SearchMode searchMode, FilterMode filterMode, List<Condition> conditions, bool bodyOnly = true)
        {
            Expression bodyExpression = null;
            //var propertyName = propertyName;
            MemberExpression prpertyExpression = null;

            if (propertyName.IndexOf(".") > 0)
            {
                var propertyNames = propertyName.Split('.');
                if (propertyNames.Length > 2)
                    throw new NotSupportedException();

                var property1 = Expression.Property(parameterExpression, propertyNames[0]);
                var classType = entityType.GetProperty(propertyNames[0]).PropertyType;
                if (classType.IsGenericType) //ICollection<> 集合
                {
                    classType = classType.GenericTypeArguments[0];
                    var paramExpression =ExpressionHelper.BuildParameterExpression(classType, "m");

                    var subbodyExpression = BuildColumnConditionExpression(paramExpression, classType, propertyNames[1], valueType, searchMode, filterMode, conditions, false);
                    bodyExpression = Expression.Call(typeof(Enumerable), "Any", new Type[] { classType }, property1, subbodyExpression);
                    if (bodyOnly)
                        return bodyExpression;
                    return Expression.Lambda(bodyExpression, parameterExpression);
                }
                else
                {
                     prpertyExpression = Expression.Property(property1, propertyNames[1]);
                }
  
            }

            if (prpertyExpression == null)
                prpertyExpression = Expression.Property(parameterExpression, propertyName);
                
            foreach (var condition in conditions)
            {
                Expression valueExpression = null;
                Expression left = null;
                switch (condition.Key)
                {
                    case "eq":
                        switch (searchMode)
                        {
                            case SearchMode.FlagOr:
                            case SearchMode.Flag:
                                valueExpression = BuildValueExpression(valueType, condition.Value);
                                left = Expression.And(prpertyExpression, valueExpression);
                                if (bodyExpression == null)
                                {
                                    bodyExpression = Expression.Equal(left, valueExpression);
                                }
                                else
                                {
                                    switch (filterMode)
                                    {
                                        case FilterMode.Or:
                                            bodyExpression = Expression.OrElse(bodyExpression, Expression.Equal(left, valueExpression));
                                            break;
                                        case FilterMode.And:
                                        default:
                                            bodyExpression = Expression.AndAlso(bodyExpression, Expression.Equal(left, valueExpression));
                                            break;
                                    }

                                }
                                
                                break;
                           
                            case SearchMode.DataTimeDay:
                            case SearchMode.DateTimeMonth:
                            case SearchMode.DateTimeYear:
                                var dateTimeTuple = GetDateTimeRange(searchMode, condition.Value);
                                valueExpression = GetVarName<DateTime>(t => dateTimeTuple.Item1);
                                var ll = Expression.GreaterThanOrEqual(prpertyExpression, valueExpression);
                                valueExpression = GetVarName<DateTime>(t => dateTimeTuple.Item2);
                                var rr = Expression.LessThan(prpertyExpression, valueExpression);
                             
                                if (bodyExpression == null)
                                {
                                    bodyExpression = Expression.AndAlso(ll, rr);
                                }
                                else
                                {
                                    var ae = Expression.AndAlso(ll, rr);
                                    switch(filterMode)
                                    {
                                        case FilterMode.Or:
                                             bodyExpression = Expression.OrElse(bodyExpression, ae);
                                            break;
                                        case FilterMode.And:
                                        default:
                                            bodyExpression = Expression.AndAlso(bodyExpression, ae);
                                            break;
                                    }
                                }
                                break;
                            case SearchMode.StringValue:
                                if (condition.Value == "NULL" || condition.Value == "*")
                                {
                                    valueExpression = Expression.Constant(null);
                                }
                                else
                                {
                                    valueExpression = BuildValueExpression(valueType, condition.Value);
                                }
                                Expression be = null;
                                if (condition.Value == "*") 
                                    be = Expression.NotEqual(prpertyExpression, valueExpression);
                                else
                                    be = Expression.Equal(prpertyExpression, valueExpression);

                                if (bodyExpression == null)
                                {
                                    bodyExpression = be;
                                }
                                else
                                {
                                    
                                    switch(filterMode)
                                    {
                                        case FilterMode.Or:
                                            bodyExpression = Expression.OrElse(bodyExpression, be);
                                            break;
                                        case FilterMode.And:
                                        default:
                                            bodyExpression = Expression.AndAlso(bodyExpression, be);
                                            break;
                                    }
                                }
                                break;
                            case SearchMode.Contains:
                            case SearchMode.ContainsOr:
                                break;
                            case SearchMode.NormalOr:
                            case SearchMode.Normal:
                            default:
                                valueExpression = BuildValueExpression(valueType, condition.Value);
                                if (bodyExpression == null)
                                {
                                    bodyExpression = Expression.Equal(prpertyExpression, valueExpression);
                                }
                                else
                                {
                                    switch(filterMode)
                                    {
                                        case FilterMode.Or:
                                             bodyExpression = Expression.OrElse(bodyExpression, Expression.Equal(prpertyExpression, valueExpression));
                                             break;
                                        case FilterMode.And:
                                        default:
                                             bodyExpression = Expression.AndAlso(bodyExpression, Expression.Equal(prpertyExpression, valueExpression));
                                             break;
                                    }
                                }
                                break;
                        }
                        break;
                    case "gt":
                        switch (searchMode)
                        {
                           
                            case SearchMode.Contains:  
                           
                            case SearchMode.ContainsOr:
                                break;
                            case SearchMode.NormalOr:
                            case SearchMode.Normal:
                            default:
                                valueExpression = BuildValueExpression(valueType, condition.Value);
                                if (bodyExpression == null)
                                {
                                    bodyExpression = Expression.GreaterThan(prpertyExpression, valueExpression);
                                }
                                else
                                {
                                    switch(filterMode)
                                    {
                                        case FilterMode.Or:
                                             bodyExpression = Expression.OrElse(bodyExpression, Expression.GreaterThan(prpertyExpression, valueExpression));
                                             break;
                                        case FilterMode.And:
                                        default:
                                             bodyExpression = Expression.AndAlso(bodyExpression, Expression.GreaterThan(prpertyExpression, valueExpression));
                                             break;
                                    }
                                }
                                break;

                        }
                        break;
                    case "lt":
                        switch (searchMode)
                        {
                           
                            case SearchMode.Contains:  
                           
                            case SearchMode.ContainsOr:
                                break;
                            case SearchMode.NormalOr:
                            case SearchMode.Normal:
                            default:
                                valueExpression = BuildValueExpression(valueType, condition.Value);
                                if (bodyExpression == null)
                                {
                                    bodyExpression = Expression.LessThan(prpertyExpression, valueExpression);
                                }
                                else
                                {
                                    switch(filterMode)
                                    {
                                        case FilterMode.Or:
                                             bodyExpression = Expression.OrElse(bodyExpression, Expression.LessThan(prpertyExpression, valueExpression));
                                             break;
                                        case FilterMode.And:
                                        default:
                                             bodyExpression = Expression.AndAlso(bodyExpression, Expression.LessThan(prpertyExpression, valueExpression));
                                             break;
                                    }
                                }
                                break;

                        }
                        break;
                    default:
                        break;
                }
            }
            if (bodyOnly)
                return bodyExpression;

            if (bodyExpression != null)
                return Expression.Lambda(bodyExpression, parameterExpression);

            return null;
        }


        static Tuple<DateTime, DateTime> GetDateTimeRange(SearchMode searchMode, string value)
        {
            DateTime startTime;
            DateTime endTime;
            switch(searchMode)
            {
                case SearchMode.DateTimeYear:
                    startTime = DateTime.ParseExact(value, "yyyy", null);
                    endTime = startTime.AddYears(1);
                    break;
                case SearchMode.DateTimeMonth:
                    startTime = DateTime.ParseExact(value, "yyyyMM", null);
                    endTime = startTime.AddMonths(1);
                    break;
                case SearchMode.DataTimeDay:
                    startTime = DateTime.ParseExact(value, "yyyyMMdd", null);
                    endTime = startTime.AddDays(1);
                    break;
                default:
                    startTime = DateTime.Now;
                    endTime = DateTime.Now;
                    break;
            }
            return Tuple.Create(startTime, endTime);
        }


        ///<summary>
        ///根据模型生成 Filter 表达式
        ///</summary>
        public static List<Expression<Func<TEntity, bool>>> BuildFilterInfo<TEntity>(IViewModel vm, IFilterInfo filterInfo)
        {
            //将条件按照列组合起来
            if (filterInfo.Filters != null && filterInfo.Filters.Count() > 0)
            {
                return BuildWhereExpression<TEntity>(vm, filterInfo.Filters);
            }
            return null;
        }

        public static List<Expression<Func<TEntity, bool>>> BuildWhereExpression<TEntity>(IViewModel viewModel, IList<string> where)
        {
            //将条件按照列组合起来
            Dictionary<UiColumnInfo, List<Condition>> dictConditions = new Dictionary<UiColumnInfo, List<Condition>>();

            foreach (var filter in where)
            {
                var ss = filter.Split(' ');
                if (ss.Length != 3)
                    continue;
                var columnInfo = viewModel.GetColumnInfo(ss[0]);
                var condition = new Condition() { Key = ss[1], Value = ss[2] };
                if (columnInfo != null)
                {
                    if (dictConditions.ContainsKey(columnInfo))
                    {
                        List<Condition> conditions = dictConditions[columnInfo];
                        conditions.Add(condition);
                    }
                    else
                    {
                        List<Condition> conditions = new List<Condition>();
                        conditions.Add(condition);
                        dictConditions[columnInfo] = conditions;
                    }
                }
            }


            List<Expression<Func<TEntity, bool>>> expressions = new List<Expression<Func<TEntity, bool>>>();

            foreach (var columnCondition in dictConditions)
            {
                ParameterExpression parameterExpression = BuildParameterExpression(typeof(TEntity));
                UiColumnInfo columnInfo = columnCondition.Key;
                Expression<Func<TEntity, bool>> prediateExpression = BuildColumnConditionExpression(parameterExpression, typeof(TEntity), columnInfo.PropertyName, columnInfo.PropertyValueType, columnInfo.SearchMode, columnInfo.FilterMode, columnCondition.Value, false)
                                                                        as Expression<Func<TEntity, bool>>;
                if (prediateExpression != null)
                    expressions.Add(prediateExpression);

            }
            return expressions;
        }

        public static IQueryable<TEntity> SetOrderByExpression<TEntity>(this IQueryable<TEntity> entityQueryable, IViewModel viewModel, string orderby)
        {
            var orderByList = BuildOrderByList(viewModel, orderby);
            bool first = true;
            foreach (var orderBy in orderByList)
            {
                var columnInfo = viewModel.GetColumnInfo(orderBy.Key);
                if (first)
                {
                    if (string.IsNullOrEmpty(orderBy.Value) || orderBy.Value == "asc")
                    {
                        switch (columnInfo.PropertyValueType)
                        {
                            case "Int32":
                                entityQueryable = entityQueryable.OrderBy(BuildOrderbyExpression<TEntity, int>(columnInfo.PropertyName));
                                break;
                            case "Int64":
                                entityQueryable = entityQueryable.OrderBy(BuildOrderbyExpression<TEntity, long>(columnInfo.PropertyName));
                                break;
                            case "Double":
                                entityQueryable = entityQueryable.OrderBy(BuildOrderbyExpression<TEntity, double>(columnInfo.PropertyName));
                                break;
                            case "DateTime":
                                entityQueryable = entityQueryable.OrderBy(BuildOrderbyExpression<TEntity, DateTime>(columnInfo.PropertyName));
                                break;
                            default:
                                entityQueryable = entityQueryable.OrderBy(BuildOrderbyExpression<TEntity, object>(columnInfo.PropertyName));
                                break;
                        }
                    }
                    else
                    {
                        //spots = spots.OrderByDescending(orderByExpression.Value);
                        switch (columnInfo.PropertyValueType)
                        {
                            case "Int32":
                                entityQueryable = entityQueryable.OrderByDescending(BuildOrderbyExpression<TEntity, int>(columnInfo.PropertyName));
                                break;
                            case "Int64":
                                entityQueryable = entityQueryable.OrderByDescending(BuildOrderbyExpression<TEntity, long>(columnInfo.PropertyName));
                                break;
                            case "Double":
                                entityQueryable = entityQueryable.OrderByDescending(BuildOrderbyExpression<TEntity, double>(columnInfo.PropertyName));
                                break;
                            case "DateTime":
                                entityQueryable = entityQueryable.OrderByDescending(BuildOrderbyExpression<TEntity, DateTime>(columnInfo.PropertyName));
                                break;
                            default:
                                entityQueryable = entityQueryable.OrderByDescending(BuildOrderbyExpression<TEntity, object>(columnInfo.PropertyName));
                                break;
                        }
                    }
                    first = false;
                }
                else
                {
                    if (string.IsNullOrEmpty(orderBy.Value) || orderBy.Value == "asc")
                    {
                        switch (columnInfo.PropertyValueType)
                        {
                            case "Int32":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenBy(BuildOrderbyExpression<TEntity, int>(columnInfo.PropertyName));
                                break;
                            case "Int64":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenBy(BuildOrderbyExpression<TEntity, long>(columnInfo.PropertyName));
                                break;
                            case "Double":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenBy(BuildOrderbyExpression<TEntity, double>(columnInfo.PropertyName));
                                break;
                            case "DateTime":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenBy(BuildOrderbyExpression<TEntity, DateTime>(columnInfo.PropertyName));
                                break;
                            default:
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenBy(BuildOrderbyExpression<TEntity, object>(columnInfo.PropertyName));
                                break;
                        }

                        //spots = (spots as IOrderedQueryable<ScenicSpot>).ThenBy(orderByExpression.Value);
                    }
                    else
                    {
                        switch (columnInfo.PropertyValueType)
                        {
                            case "Int32":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenByDescending(BuildOrderbyExpression<TEntity, int>(columnInfo.PropertyName));
                                break;
                            case "Int64":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenByDescending(BuildOrderbyExpression<TEntity, long>(columnInfo.PropertyName));
                                break;
                            case "Double":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenByDescending(BuildOrderbyExpression<TEntity, double>(columnInfo.PropertyName));
                                break;
                            case "DateTime":
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenByDescending(BuildOrderbyExpression<TEntity, DateTime>(columnInfo.PropertyName));
                                break;
                            default:
                                entityQueryable = (entityQueryable as IOrderedQueryable<TEntity>).ThenByDescending(BuildOrderbyExpression<TEntity, object>(columnInfo.PropertyName));
                                break;
                        }
                        //spots = (spots as IOrderedQueryable<ScenicSpot>).ThenByDescending(orderByExpression.Value);
                    }
                }
            }
            return entityQueryable;
        }

        public static IList<OrderBy> BuildOrderByList(IViewModel viewModel, string orderByString)
        {
            var orderBys = orderByString.Split(',');
            var list = new List<OrderBy>();
            foreach (var a in orderBys)
            {
                OrderBy orderBy = new OrderBy();
                var ab = a.Trim().Split(' ');
                if (ab.Length > 1)
                {
                    orderBy.Key = ab[0];
                    orderBy.Value = ab[ab.Length - 1].ToLower();
                }
                else
                {
                    orderBy.Key = ab[0];
                    orderBy.Value = "asc";
                }
                list.Add(orderBy);
            }
            return list;
        }


        public static Expression<Func<TEntity, TKey>> BuildOrderbyExpression<TEntity, TKey>(string propertyName)
        {
            ParameterExpression param = Expression.Parameter(typeof(TEntity));
            MemberExpression body = null;

            var propertyNames = propertyName.Split('.');

            foreach (var pName in propertyNames)
            {
                if (body == null)
                    body = Expression.Property(param, pName);
                else
                    body = Expression.Property(body, pName);
            }

            return Expression.Lambda<Func<TEntity, TKey>>(body, param);
        }

        public static Expression<Func<TEntity, TKey>> BuildPropertyExpression<TEntity, TKey>(string propertyName)
        {
            ParameterExpression param = Expression.Parameter(typeof(TEntity));
            MemberExpression body = null;

            var propertyNames = propertyName.Split('.');

            foreach (var pName in propertyNames)
            {
                if (body == null)
                    body = Expression.Property(param, pName);
                else
                    body = Expression.Property(body, pName);
            }


            return Expression.Lambda<Func<TEntity, TKey>>(body, param);
        }

        public static Expression BuildPropertyExpression(Type entityType, string propertyName)
        {
            //1.创建表达式参数（指定参数或变量的类型:p）
            ParameterExpression param = Expression.Parameter(entityType);
            //2.构建表达式体(类型包含指定的属性:p.Name)   
            MemberExpression body = null;

            var propertyNames = propertyName.Split('.');

            foreach (var pName in propertyNames)
            {
                if (body == null)
                    body = Expression.Property(param, pName);
                else
                    body = Expression.Property(body, pName);
            }

            //3.根据参数和表达式体构造一个lambda表达式  
            return Expression.Lambda(body, param);
        }

        public static Expression<Func<TEntity, bool>> BuildAuthorizationExpression<TEntity>(Expression<Func<IPermissionAssign, bool>> condition, bool forDeny = false,  string propertyName = "PermissionAssign")
            where TEntity : IEntity
        {
            Type type = typeof(TEntity);
            ParameterExpression paramExpression = Expression.Parameter(type, "t");
            var propertyExpression = Expression.Property(paramExpression, propertyName); 
            Type classType = type.GetProperty(propertyName).PropertyType;
            classType = classType.GenericTypeArguments[0];
            Expression anyExpression = Expression.Call(typeof(Enumerable), "Any", new Type[] { classType }, propertyExpression, condition);
            if (forDeny)
            {
                anyExpression = Expression.Not(anyExpression);
            }
            return Expression.Lambda<Func<TEntity, bool>>(anyExpression, paramExpression);
        }

        public static Expression<Func<TEntity, bool>> BuildAuthorizationExpression<TEntity>(IEnumerable<int> roles, int operation, bool forDeny = false,  bool bit = false, string propertyName = "PermissionAssign")
            where TEntity : IEntity
        {
            Expression<Func<IPermissionAssign, bool>> conditionExpression = BuildPermissionExpression(roles, operation, bit, forDeny);
            return BuildAuthorizationExpression<TEntity>(conditionExpression, forDeny, propertyName);
        }

        public static Expression BuildAuthorizationExpression(Type type, Expression<Func<IPermissionAssign, bool>> condition, bool forDeny = false, string propertyName = "PermissionAssign")
        {

            ParameterExpression paramExpression = Expression.Parameter(type, "t");
            var propertyExpression = Expression.Property(paramExpression, propertyName); 
            Type classType = type.GetProperty(propertyName).PropertyType;
            classType = classType.GenericTypeArguments[0];
            Expression anyExpression = Expression.Call(typeof(Enumerable), "Any", new Type[] { classType }, propertyExpression, condition);
            if (forDeny)
            {
                anyExpression = Expression.Not(anyExpression);
            }
            return Expression.Lambda(anyExpression, paramExpression);
        }

        public static Expression BuildAuthorizationExpression(Type type, IEnumerable<int> roles, int operation, bool forDeny = false, bool bit = false, string propertyName = "PermissionAssign")
        {
            Expression<Func<IPermissionAssign, bool>> conditionExpression = BuildPermissionExpression(roles, operation, bit, forDeny);
            return BuildAuthorizationExpression(type, conditionExpression, forDeny, propertyName);
        }

        private static Expression<Func<IPermissionAssign, bool>> BuildPermissionExpression(IEnumerable<int> roles, int operation, bool bit = false, bool forDeny = false)
        {
           if (forDeny)
              return BuildDenyPermissionExpression(roles, operation, bit);
            else
                return BuildAllowPermissionExpression(roles, operation, bit);
        }
        private static Expression<Func<IPermissionAssign, bool>> BuildAllowPermissionExpression(IEnumerable<int> roles, int operation, bool bit = false)
        {
            Expression<Func<IPermissionAssign, bool>> conditionExpression = null;
            if (bit)
            {
                conditionExpression = p => roles.Contains(p.RoleId)  && (p.Operation & operation) == operation; 
            }
            else
            {
                conditionExpression = p => roles.Contains(p.RoleId)  && p.Operation == operation; 
            }
            return conditionExpression;
        }

        private static Expression<Func<IPermissionAssign, bool>> BuildDenyPermissionExpression(IEnumerable<int> roles, int operation, bool bit = false)
        {
            Expression<Func<IPermissionAssign, bool>> conditionExpression = null;
            if (bit)
            {
                conditionExpression = p => roles.Contains(p.RoleId) && (p.IsRefused & operation) == operation; 
            }
            else
            {
                conditionExpression = p => roles.Contains(p.RoleId) && p.IsRefused == operation; 
            }
            return conditionExpression;
        }

    }

   
}