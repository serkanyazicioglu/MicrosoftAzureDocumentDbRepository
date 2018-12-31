using Microsoft.Azure.Documents.Client;
using Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleApp.Repositories
{
    public class BaseDocDbRepository<T> : BaseDocumentDbRepository<T> where T : Microsoft.Azure.Documents.Resource, new()
    {
        private static DocumentClient currentDocumentClient = null;
        public override DocumentClient CurrentDocumentClient
        {
            get
            {
                if (currentDocumentClient == null)
                {
                    currentDocumentClient = new DocumentClient(new Uri(ConfigurationManager.AppSettings["docdb.endpoint"]), ConfigurationManager.AppSettings["docdb.authKey"], new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Tcp, RetryOptions = new RetryOptions { MaxRetryAttemptsOnThrottledRequests = 60, MaxRetryWaitTimeInSeconds = 12 }, MaxConnectionLimit = 1000 });
                }

                return currentDocumentClient;
            }
        }

        public override string DatabaseId => ConfigurationManager.AppSettings["docdb.databaseId"];

        public override string CollectionId => ConfigurationManager.AppSettings["docdb.collectionId"];
    }
}
