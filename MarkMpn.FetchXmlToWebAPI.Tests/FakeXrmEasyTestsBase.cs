using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.FakeMessageExecutors;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using FakeXrmEasy.Middleware.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[SuppressMessage("Design", "CA1051:Do not declare visible instance fields")]
public class FakeXrmEasyTestsBase
{
    protected readonly IXrmFakedContext Context;
    protected readonly IOrganizationServiceAsync2 Service;

    protected FakeXrmEasyTestsBase()
    {
        Context = MiddlewareBuilder
            .New()
            .AddCrud()
            .AddFakeMessageExecutors(Assembly.GetAssembly(typeof(AddListMembersListRequestExecutor)))
            .UseCrud()
            .UseMessages()
            .SetLicense(FakeXrmEasyLicense.RPL_1_5)
            .Build();

        Service = Context.GetAsyncOrganizationService2();
    }
}