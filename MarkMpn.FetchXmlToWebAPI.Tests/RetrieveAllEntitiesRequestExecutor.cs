using System;
using System.Collections.Generic;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.FakeMessageExecutors;
using JetBrains.Annotations;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    [UsedImplicitly]
    internal sealed class RetrieveAllEntitiesRequestExecutor : IFakeMessageExecutor
    {
        private readonly Func<IEnumerable<EntityMetadata>> _getEntities;

        public RetrieveAllEntitiesRequestExecutor(Func<IEnumerable<EntityMetadata>> getEntities)
        {
            _getEntities = getEntities;
        }

        private IEnumerable<EntityMetadata> Entities => _getEntities();

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
                    ["EntityMetadata"] = Entities
                }
            };
        }

        public Type GetResponsibleRequestType()
        {
            return typeof(RetrieveAllEntitiesRequest);
        }
    }
}
