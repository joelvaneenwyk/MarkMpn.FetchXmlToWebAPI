using System;
using System.Collections.Generic;
using System.Linq;
using FakeXrmEasy.Plugins.PluginSteps.InvalidRegistrationExceptions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
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

            return _organizationServices?.Execute(
                    new RetrieveEntityRequest
                    {
                        LogicalName = logicalName,
                        EntityFilters = EntityFilters.Entity |
                                    EntityFilters.Attributes |
                                    EntityFilters.Relationships
                    }) is RetrieveEntityResponse response
                ? response.EntityMetadata
                : throw new InvalidPrimaryEntityNameException(logicalName);
        }

        public EntityMetadata GetEntity(int? otc)
        {
            return !otc.HasValue
                ? throw new ArgumentNullException(nameof(otc))
                : _organizationServices?.Execute(
                    new RetrieveAllEntitiesRequest
                    {
                        EntityFilters = EntityFilters.Entity |
                                        EntityFilters.Attributes |
                                        EntityFilters.Relationships
                    }) is RetrieveAllEntitiesResponse response
                ? response.EntityMetadata.First(e => e.ObjectTypeCode == otc)
                : throw new KeyNotFoundException();
        }
    }
}
