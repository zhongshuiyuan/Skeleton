using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Ataoge.Data;
using Ataoge.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ataoge.EntityFrameworkCore.Repositories
{
    public class EfCoreTreeRepositoryBase<TDbContext, TEntity, TPrimaryKey> : EfCoreRepositoryBase<TDbContext, TEntity, TPrimaryKey>, IRepositoryOfTreeEntity<TEntity, TPrimaryKey>
        where TEntity : class, ITreeEntity<TPrimaryKey>
        where TDbContext : DbContext, IRepositoryContext
        where TPrimaryKey :struct,  IEquatable<TPrimaryKey>
    {
        public EfCoreTreeRepositoryBase(IDbContextProvider<TDbContext> dbContextProvider) : base(dbContextProvider)
        {
            _repositoryHelper = base.Context as IRepositoryHelper;
            
        }

        private IRepositoryHelper _repositoryHelper;

        public List<TEntity> GetChildrenList(TPrimaryKey id, bool recursion = false, Expression<Func<TEntity, bool>> where = null, string orderBySilbing = null)
        {
            return GetChildren(id, recursion, where, orderBySilbing).ToList();
        }

        public IQueryable<TEntity> GetChildren(TPrimaryKey id, bool recursion = false, Expression<Func<TEntity, bool>> where = null, string orderBySilbing = null)
        {
            if (recursion)
            {
                var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
                return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper.ProviderName, Table, entityType, ts => ts.Id.Equals(id), where, false, orderBySilbing);
            }
            
            IQueryable<TEntity> result = Table;
            if (where != null)
                result = result.Where(where);
            return result.Where(EfCoreRepositoryHelper.BuildEqualsPredicate<TEntity>(nameof(ITreeEntity.Pid), id));
           
        }

        public IQueryable<TEntity> GetSiblings(TPrimaryKey id, string orderBySilbing = null)
        {
            TEntity theEntity = Get(id); 
            return Table.Where(EfCoreRepositoryHelper.BuildEqualsPredicate<TEntity>(nameof(ITreeEntity.Pid), theEntity.Pid));//t => t.Pid == theEntity.Pid);
            // 构造表达式；
        }

        public IQueryable<TEntity> GetAllChildren(Expression<Func<TEntity, bool>> where, bool startQuery = false, string orderBySilbing = null)
        {
            
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
            if (startQuery)
                return  EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper.ProviderName, Table, entityType, where, null, false, orderBySilbing, 0, distinct:true);
            return  EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper.ProviderName, Table, entityType, t => t.Pid == null, where, false, orderBySilbing);
        }

        

        public List<TEntity> GetParentList(TPrimaryKey id)
        {
            return GetParents(id).ToList();
        }

        public IQueryable<TEntity> GetParents(TPrimaryKey id)
        {
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
            if (_repositoryHelper != null)
                return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper, Table, entityType, t => t.Id.Equals(id), null, true);
            return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper.ProviderName, Table, entityType, t => t.Id.Equals(id), null, true);
        }

         public IQueryable<TResult> GetParents<TResult>(Expression<Func<TEntity, bool>> startQuery, Expression<Func<TEntity, TResult>> selector)
            where TResult : class
         {
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);

             return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey, TResult>(_repositoryHelper, Table, entityType, startQuery, selector,  null, true, null, 0, true);
        }

         public IEnumerable<TPrimaryKey> GetParents(Expression<Func<TEntity, bool>> startQuery)
         {
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
            
             return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper, Table, entityType, startQuery, null, true, null, 0, true).Select(s => s.Id);
        }

        public IQueryable<TResult> GetChildrenRecursion<TResult>(Expression<Func<TEntity, bool>> startQuery, Expression<Func<TEntity, TResult>> selector)
            where TResult : class
        {
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
            return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey, TResult>(_repositoryHelper, Table, entityType, startQuery, selector,  null, false, null, 0, true);
        }

        public IEnumerable<TPrimaryKey> GetChildrenRecursion(Expression<Func<TEntity, bool>> startQuery)
        {
            var entityType = Context.Model.FindEntityType(typeof(TEntity).FullName);
            return EfCoreRepositoryHelper.TreeQuery<TEntity,TPrimaryKey>(_repositoryHelper, Table, entityType, startQuery, null, false, null, 0, true).Select(s => s.Id);
        }

        

        
    }
}