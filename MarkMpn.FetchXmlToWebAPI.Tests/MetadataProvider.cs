using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    internal sealed class MetadataProvider : IMetadataProvider
    {
        private readonly IOrganizationService _org;

        public MetadataProvider(IOrganizationService org)
        {
            this._org = org;
        }

        public bool IsConnected => true;

        public EntityMetadata GetEntity(string logicalName)
        {
            var resp = (RetrieveEntityResponse)_org.Execute(
                new RetrieveEntityRequest
                {
                    LogicalName = logicalName,
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships
                });
            return resp.EntityMetadata;
        }

        public EntityMetadata GetEntity(int otc)
        {
            var resp = (RetrieveAllEntitiesResponse)_org.Execute(
                new RetrieveAllEntitiesRequest
                {
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships
                });
            return resp.EntityMetadata.First(e => e.ObjectTypeCode == otc);
        }
    }
}
