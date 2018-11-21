using Microsoft.Azure.CosmosDB.BulkExecutor;
using Microsoft.Azure.CosmosDB.BulkExecutor.BulkImport;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Newtonsoft.Json;
using Nhea.Helper;
using Nhea.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository
{
    public abstract class BaseDocumentDbRepository<T> : BaseRepository<T>, IDisposable where T : Microsoft.Azure.Documents.Resource, new()
    {
        public abstract DocumentClient CurrentDocumentClient { get; }

        public abstract string DatabaseId { get; }

        public abstract string CollectionId { get; }

        public Uri CollectionLink => UriFactory.CreateDocumentCollectionUri(DatabaseId, CollectionId);

        private IOrderedQueryable<T> CurrentDocumentQuery
        {
            get
            {
                var options = new FeedOptions { EnableCrossPartitionQuery = true };
                return CurrentDocumentClient.CreateDocumentQuery<T>(this.CollectionLink, options);
            }
        }

        private Document GetDocument(string id)
        {
            return CurrentDocumentClient.CreateDocumentQuery(this.CollectionLink)
                   .Where(e => e.Id == id)
                   .First();
        }

        public override T GetById(object id)
        {
            var entity = CurrentDocumentQuery.Where(query => query.Id == id.ToString()).SingleOrDefault();

            this.AddCore(entity);

            return entity;
        }

        private List<T> Items = new List<T>();

        public override T CreateNew()
        {
            var entity = new T();

            lock (lockObject)
            {
                Items.Add(entity);
            }

            return entity;
        }

        public override void Add(T entity)
        {
            this.AddCore(entity);
        }

        private object lockObject = new object();

        private void AddCore(T entity)
        {
            lock (lockObject)
            {
                if (entity != null && !Items.Contains(entity))
                {
                    if (Items.Any(query => query.Id == entity.Id))
                    {
                        Items.Remove(Items.Single(query => query.Id == entity.Id));
                    }

                    Items.Add(entity);
                }
            }
        }

        public void Remove(T entity)
        {
            lock (lockObject)
            {
                if (entity != null && Items.Contains(entity))
                {
                    if (Items.Any(query => query.Id == entity.Id))
                    {
                        Items.Remove(Items.Single(query => query.Id == entity.Id));
                    }
                }
            }
        }

        public override void Add(List<T> entities)
        {
            foreach (var entity in entities)
            {
                this.AddCore(entity);
            }
        }

        protected override bool AnyCore(Expression<Func<T, bool>> filter, bool getDefaultFilter)
        {
            if (getDefaultFilter && this.DefaultFilter != null)
            {
                filter = filter.And(this.DefaultFilter);
            }

            return CurrentDocumentQuery.Any(filter);
        }

        protected override int CountCore(Expression<Func<T, bool>> filter, bool getDefaultFilter)
        {
            if (getDefaultFilter && this.DefaultFilter != null)
            {
                filter = filter.And(this.DefaultFilter);
            }

            return CurrentDocumentQuery.Count(filter);
        }

        public override void Delete(Expression<Func<T, bool>> filter)
        {
            var entities = CurrentDocumentQuery.Where(filter).ToList();

            foreach (var entity in entities)
            {
                this.Delete(entity);
            }
        }

        public override void Delete(T entity)
        {
            CurrentDocumentClient.DeleteDocumentAsync(GetDocument(entity.Id).SelfLink);
        }

        protected override T GetSingleCore(Expression<Func<T, bool>> filter, bool getDefaultFilter)
        {
            if (DefaultFilter != null)
            {
                filter = filter.And(DefaultFilter);
            }

            var entity = CurrentDocumentQuery.Where(filter).ToList().SingleOrDefault();

            this.AddCore(entity);

            return entity;
        }

        protected override IQueryable<T> GetAllCore(Expression<Func<T, bool>> filter, bool getDefaultFilter, bool getDefaultSorter, string sortColumn, SortDirection? sortDirection, bool allowPaging, int pageSize, int pageIndex, ref int totalCount)
        {
            if (getDefaultFilter)
            {
                filter = filter.And(DefaultFilter);
            }

            if (filter == null)
            {
                filter = query => 1 == 1;
            }

            IQueryable<T> returnList = CurrentDocumentQuery.Where(filter).ToList().AsQueryable();

            if (!String.IsNullOrEmpty(sortColumn))
            {
                returnList = returnList.Sort(sortColumn, sortDirection);
            }
            else if (getDefaultSorter && DefaultSorter != null)
            {
                if (DefaultSortType == SortDirection.Ascending)
                {
                    returnList = returnList.OrderBy(DefaultSorter);
                }
                else
                {
                    returnList = returnList.OrderByDescending(DefaultSorter);
                }
            }

            if (allowPaging && pageSize > 0)
            {
                if (totalCount == 0)
                {
                    totalCount = returnList.Count();
                }

                int skipCount = pageSize * pageIndex;

                returnList = returnList.Skip<T>(skipCount).Take<T>(pageSize);
            }

            foreach (var entity in returnList)
            {
                this.AddCore(entity);
            }

            return returnList;
        }

        public override void Dispose()
        {
            this.Items = null;
        }

        public override bool IsNew(T entity)
        {
            return String.IsNullOrEmpty(entity.SelfLink);
        }

        public override void Refresh(T entity)
        {
            throw new NotImplementedException();
        }

        public override void Save()
        {
            CurrentDocumentClient.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 30;
            CurrentDocumentClient.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 9;

            if (this.Items.Count() < 5)
            {
                if (this.Items.Any(query => query.SelfLink != null))
                {
                    var items = this.Items.Where(query => query.SelfLink != null);

                    foreach (var entity in items)
                    {
                        SaveDocumentAsync(entity, ProcessType.Replace).Wait();
                    }
                }

                if (this.Items.Any(query => query.SelfLink == null))
                {
                    var items = this.Items.Where(query => query.SelfLink == null);

                    foreach (var entity in items)
                    {
                        var createResult = SaveDocumentAsync(entity, ProcessType.Create).Result;
                        var savedEntity = JsonConvert.DeserializeObject<T>(createResult.Resource.ToString().Replace(Environment.NewLine, String.Empty));

                        var oldItem = this.Items.Single(query => query.SelfLink == null && query.Id == savedEntity.Id);
                        var currentItem = this.Items[this.Items.IndexOf(oldItem)];
                        currentItem.ResourceId = savedEntity.ResourceId;
                        currentItem.SetPropertyValue("_self", savedEntity.SelfLink);
                        currentItem.SetPropertyValue("_etag", savedEntity.ETag);
                        currentItem.SetPropertyValue("_ts", savedEntity.Timestamp.ToUnixTime().ToString());
                    }
                }
            }
            else
            {
                BulkImport(this.Items);
            }
        }

        private enum ProcessType
        {
            Create,
            Replace
        }

        private void BulkImport(IEnumerable<T> items)
        {
            var bulkExecutor = new BulkExecutor(CurrentDocumentClient, GetCollection());
            bulkExecutor.InitializeAsync().Wait();

            CurrentDocumentClient.ConnectionPolicy.RetryOptions.MaxRetryWaitTimeInSeconds = 0;
            CurrentDocumentClient.ConnectionPolicy.RetryOptions.MaxRetryAttemptsOnThrottledRequests = 0;

            BulkImportResponse bulkImportResponse = null;

            do
            {
                try
                {
                    bulkImportResponse = bulkExecutor.BulkImportAsync(documents: items, enableUpsert: true).Result;
                }
                catch (DocumentClientException ex)
                {
                    Logger.Log(ex);
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    break;
                }

            } while (bulkImportResponse.NumberOfDocumentsImported < items.Count());
        }

        private async Task<ResourceResponse<Document>> SaveDocumentAsync(T entity, ProcessType processType)
        {
            ResourceResponse<Document> saveResult = null;
            var queryDone = false;

            int tryCount = 0;

            while (!queryDone)
            {
                try
                {
                    if (processType == ProcessType.Create)
                    {
                        saveResult = await CurrentDocumentClient.CreateDocumentAsync(this.CollectionLink, entity);
                    }
                    else if (processType == ProcessType.Replace)
                    {
                        saveResult = await CurrentDocumentClient.ReplaceDocumentAsync(entity.SelfLink, entity);
                    }

                    queryDone = true;
                }
                catch (DocumentClientException documentClientException)
                {
                    var statusCode = (int)documentClientException.StatusCode;

                    if (statusCode == 429 || statusCode == 503)
                    {
                        Thread.Sleep(documentClientException.RetryAfter);
                    }
                    else
                    {
                        tryCount++;

                        if (tryCount > 10)
                        {
                            throw;
                        }
                        else
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }
                catch (AggregateException aggregateException)
                {
                    if (aggregateException.InnerException != null && aggregateException.InnerException.GetType() == typeof(DocumentClientException))
                    {
                        var documentClientException = aggregateException.InnerException as DocumentClientException;
                        var statusCode = (int)documentClientException.StatusCode;

                        if (statusCode == 429 || statusCode == 503)
                        {
                            Thread.Sleep(documentClientException.RetryAfter);
                        }
                        else
                        {
                            tryCount++;

                            if (tryCount > 10)
                            {
                                throw;
                            }
                            else
                            {
                                Thread.Sleep(2000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    tryCount++;

                    Logger.Log(ex);

                    if (tryCount > 10)
                    {
                        throw;
                    }
                    else
                    {
                        Thread.Sleep(2000);
                    }
                }
            }

            return saveResult;
        }

        private DocumentCollection GetCollection()
        {
            return CurrentDocumentClient.CreateDocumentCollectionQuery(UriFactory.CreateDatabaseUri(this.DatabaseId)).Where(c => c.Id == this.CollectionId).AsEnumerable().FirstOrDefault();
        }
    }
}
