[![Build Status](https://dev.azure.com/serkanyazicioglu/serkanyazicioglu/_apis/build/status/serkanyazicioglu.MicrosoftAzureDocumentDbRepository?branchName=master)](https://dev.azure.com/serkanyazicioglu/serkanyazicioglu/_build/latest?definitionId=5&branchName=master)
[![NuGet](https://img.shields.io/nuget/v/Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository.svg)](https://www.nuget.org/packages/Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository/)

# Nhea Microsoft Azure DocumentDb Repository

Microsoft Azure DocumentDb base repository classes.


## Getting Started

Nhea is on NuGet. You may install Nhea Microsoft Azure DocumentDb Repository via NuGet Package manager.

https://www.nuget.org/packages/Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository/

```
Install-Package Nhea.Data.Repository.MicrosoftAzureDocumentDbRepository
```

### Prerequisites

Project is built with .NET Framework 4.6.1. 

This project references;

- Microsoft.Azure.DocumentDB > 1.22
- Microsoft.Azure.CosmosDB.BulkExecutor > 1.0.1

I highly suggest you to use Azure Storage Explorer. Click the link below to download.

https://azure.microsoft.com/en-us/features/storage-explorer/

### Configuration

First of all creating a base repository class is a good idea to set basic properties like connection string.

```
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
```
You may remove the abstract modifier if you want to use generic repositories or you may create individual repository classes for your documents if you want to set specific properties for that object.
```
public partial class Member : Microsoft.Azure.Documents.Resource
{
    public Guid Id { get; set; }

    public string Title { get; set; }

    public string UserName { get; set; }

    public string Password { get; set; }

    public int Status { get; set; }

    public string Email { get; set; }
}

public class MemberRepository : BaseDocDbRepository<Member>
{
    public override Member CreateNew()
    {
        var entity = base.CreateNew();
        entity.Id = Guid.NewGuid();
        entity.Status = (int)StatusType.Available;

        return entity;
    }

    //public override Expression<Func<Member, object>> DefaultSorter => query => new { query.Timestamp };

    //protected override SortDirection DefaultSortType => SortDirection.Descending;

    public override Expression<Func<Member, bool>> DefaultFilter => query => query.Status == (int)StatusType.Available;
}
```
Then in your code just initalize a new instance of your class and call appropriate methods for your needs.

```
Guid newMemberId = Guid.NewGuid();

using (MemberRepository memberRepository = new MemberRepository())
{
    var member = memberRepository.CreateNew();
    member.Id = newMemberId;
    member.Title = "Test Member";
    member.UserName = "username";
    member.Password = "password";
    member.Email = "test@test.com";
    memberRepository.Save();
}

using (MemberRepository memberRepository = new MemberRepository())
{
    var members = memberRepository.GetAll(query => query.Timestamp >= DateTime.Today).ToList();

    foreach (var member in members)
    {
        member.Title += " Lastname";
    }

    memberRepository.Save();
}

using (MemberRepository memberRepository = new MemberRepository())
{
    var member = memberRepository.GetSingle(query => query.Id == newMemberId);

    if (member != null)
    {
        member.Title = "Selected Member 2";
        memberRepository.Save();
    }
}

using (MemberRepository memberRepository = new MemberRepository())
{
    memberRepository.Delete(query => query.Title == "Selected Member 2");
    memberRepository.Save();
}

using (MemberRepository memberRepository = new MemberRepository())
{
    var member = memberRepository.CreateNew();
    bool isNew = memberRepository.IsNew(member);
}
```

### Bulk Import

When there are more than 5 or more items cached in the repository Nhea framework uses BulkImport library for faster saving processes.