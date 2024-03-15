using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using FakeXrmEasy.Plugins.PluginSteps.InvalidRegistrationExceptions;
using JetBrains.Annotations;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests;







































































internal sealed class MetadataProvider : IMetadataProvider
{
    private readonly IOrganizationService? _organizationServices;

    public MetadataProvider(IOrganizationService? organizationServices)
    {
        this._organizationServices = organizationServices;
    }

    public bool IsConnected => true;

    public EntityMetadata GetEntity(string? logicalName)
    {
        ArgumentNullException.ThrowIfNull(logicalName);

        if (_organizationServices?.Execute(
                new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity |
                                EntityFilters.Attributes |
                                EntityFilters.Relationships
            }) is RetrieveEntityResponse response)
        {
            return response.EntityMetadata;
        }

        throw new InvalidPrimaryEntityNameException(logicalName);
    }

    public EntityMetadata GetEntity(int? otc)
    {
        ArgumentNullException.ThrowIfNull(otc);

        if (_organizationServices?.Execute(
                new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity |
                                    EntityFilters.Attributes |
                                    EntityFilters.Relationships
                }) is RetrieveAllEntitiesResponse response)
        {
            return response.EntityMetadata.First(e => e.ObjectTypeCode == otc);
        }

        throw new KeyNotFoundException();
    }
}