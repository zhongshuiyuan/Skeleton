using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq.Parsing.Structure;

namespace System.Linq
{
    public static class AtaogeQueryableExtensions
    {
        static AtaogeQueryableExtensions()
        {
            
        }
        private static readonly TypeInfo QueryCompilerTypeInfo = typeof(QueryCompiler).GetTypeInfo();

        private static readonly FieldInfo QueryCompilerField = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");

        private static readonly FieldInfo QueryModelGeneratorField =  QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_queryModelGenerator");
        //private static readonly PropertyInfo NodeTypeProviderField = QueryCompilerTypeInfo.DeclaredProperties.Single(x => x.Name == "NodeTypeProvider");

        //private static readonly MethodInfo CreateQueryParserMethod = QueryCompilerTypeInfo.DeclaredMethods.First(x => x.Name == "CreateQueryParser");
        private static readonly FieldInfo LoggerField = QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_logger");

        //private static readonly MethodInfo ExtractParametersMethod = QueryCompilerTypeInfo.DeclaredMethods.First(x => x.Name == "ExtractParameters");
        private static readonly FieldInfo QueryContextFactoryField = QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_queryContextFactory");


        private static readonly FieldInfo DataBaseField = QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_database");

        private static readonly PropertyInfo Dependencies = typeof(Database).GetTypeInfo().DeclaredProperties.Single(x => x.Name == "Dependencies");
        //private static readonly FieldInfo QueryCompilationContextFactoryField = typeof(Database).GetTypeInfo().DeclaredFields.Single(x => x.Name == "_queryCompilationContextFactory");

        public static string ToSql<TEntity>(this IQueryable<TEntity> query, bool useParameters = false) where TEntity : class
        {
            if (useParameters)
                return GetSqlTextWithParement(query).Sql;
            var selectExpression = GetSelect(query, useParameters);
            return selectExpression?.ToString();
            //return sql;
        }

        public static SelectExpression GetSelect<TEntity>(this IQueryable<TEntity> query, bool useParameters = false) where TEntity : class
        {
            if (!(query is EntityQueryable<TEntity>) && !(query is InternalDbSet<TEntity>))
            {
                throw new ArgumentException("Invalid query");
            }
            
          
          
            var queryCompiler = (IQueryCompiler)QueryCompilerField.GetValue(query.Provider); //(IQueryCompiler)

            Expression expression = query.Expression;

            var queryModelGenerator = (IQueryModelGenerator)QueryModelGeneratorField.GetValue(queryCompiler);
            var queryModel = queryModelGenerator.ParseQuery(expression);

            if (useParameters)  //采用参数模式替换
            {
                var queryContextFactory=(IQueryContextFactory)QueryContextFactoryField.GetValue(queryCompiler);
                var queryContext = queryContextFactory.Create();
                //expression = (Expression) ExtractParametersMethod.Invoke(queryCompiler, new object[] {query.Expression, queryContext, true});

                var logger = (IDiagnosticsLogger<DbLoggerCategory.Query>) LoggerField.GetValue(queryCompiler);
                expression = (Expression) queryModelGenerator.ExtractParameters(logger, expression, queryContext);
            }

            //var nodeTypeProvider = (INodeTypeProvider)NodeTypeProviderField.GetValue(queryCompiler);
            //var parser = (IQueryParser)CreateQueryParserMethod.Invoke(queryCompiler, new object[] { nodeTypeProvider });
            
            //var queryModel = parser.GetParsedQuery(expression);//query.Expression);
            
            
            var database = DataBaseField.GetValue(queryCompiler);
            var dp = (DatabaseDependencies)Dependencies.GetValue(database);
            var queryCompilationContextFactory = dp.QueryCompilationContextFactory;// (IQueryCompilationContextFactory)QueryCompilationContextFactoryField.GetValue(database);
            var queryCompilationContext = queryCompilationContextFactory.Create(false);
            var modelVisitor = (RelationalQueryModelVisitor)queryCompilationContext.CreateQueryModelVisitor();
            modelVisitor.CreateQueryExecutor<TEntity>(queryModel);

            return modelVisitor.Queries.First();
        }

        internal static SqlWithParameters GetSqlTextWithParement<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            if (!(query is EntityQueryable<TEntity>) && !(query is InternalDbSet<TEntity>))
            {
                throw new ArgumentException("Invalid query");
            }

            var queryCompiler = (IQueryCompiler)QueryCompilerField.GetValue(query.Provider);
            Expression expression = query.Expression;
            
            var queryModelGenerator = (IQueryModelGenerator)QueryModelGeneratorField.GetValue(queryCompiler);
            var queryModel = queryModelGenerator.ParseQuery(expression);

            var queryContextFactory=(IQueryContextFactory)QueryContextFactoryField.GetValue(queryCompiler);
            var queryContext = queryContextFactory.Create();
            //Expression expression = (Expression) ExtractParametersMethod.Invoke(queryCompiler, new object[] {query.Expression, queryContext, true});
            var logger = (IDiagnosticsLogger<DbLoggerCategory.Query>) LoggerField.GetValue(queryCompiler);
            expression = (Expression) queryModelGenerator.ExtractParameters(logger, expression, queryContext);

            //var nodeTypeProvider = (INodeTypeProvider)NodeTypeProviderField.GetValue(queryCompiler);
            //var parser = (IQueryParser)CreateQueryParserMethod.Invoke(queryCompiler, new object[] { nodeTypeProvider });
            //var queryModel = parser.GetParsedQuery(expression);
            
            
            var database = DataBaseField.GetValue(queryCompiler);
            var dp = (DatabaseDependencies)Dependencies.GetValue(database);
            var queryCompilationContextFactory = dp.QueryCompilationContextFactory;// (IQueryCompilationContextFactory)QueryCompilationContextFactoryField.GetValue(database);
            var queryCompilationContext = queryCompilationContextFactory.Create(false);
            var modelVisitor = (RelationalQueryModelVisitor)queryCompilationContext.CreateQueryModelVisitor();
            modelVisitor.CreateQueryExecutor<TEntity>(queryModel);

            var selectExpression = modelVisitor.Queries.First();
            string sql  = selectExpression.CreateDefaultQuerySqlGenerator().GenerateSql(queryContext.ParameterValues).CommandText;
            //string sql =  modelVisitor.Queries.First().ToString();
            return new SqlWithParameters() {Sql = sql, Parameters = queryContext.ParameterValues?.ToDictionary(k => k.Key, v => v.Value)};
        }
    }

    internal class SqlWithParameters
    {
        public string Sql {get; set;}

        public IDictionary<string, object> Parameters {get; set;}
    }
}