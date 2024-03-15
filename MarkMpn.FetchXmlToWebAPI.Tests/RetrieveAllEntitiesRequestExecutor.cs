using System;
using System.Collections.Generic;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.FakeMessageExecutors;
using JetBrains.Annotations;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[UsedImplicitly]
internal sealed class RetrieveAllEntitiesRequestExecutor : IFakeMessageExecutor
{
    private readonly FetchXmlConversionEntities _entities = new();
    
    public bool CanExecute(OrganizationRequest request)
    {
        return request is RetrieveAllEntitiesRequest;
    }

    public OrganizationResponse Execute(OrganizationRequest request, IXrmFakedContext ctx)
    {
        return new RetrieveAllEntitiesResponse
        {
            Results = new ParameterCollection
            {
                ["EntityMetadata"] = _entities.Entities
    }
        };
    }

    public Type GetResponsibleRequestType()
    {
        return typeof(RetrieveAllEntitiesRequest);
    }
}
