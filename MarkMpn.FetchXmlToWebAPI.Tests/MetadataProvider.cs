using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using System;
using System.ServiceModel;
using JetBrains.Annotations;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    [UsedImplicitly]
    public class FollowUpPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Extract the tracing service for use in debugging sandboxed plug-ins.
            ITracingService? tracingService =
                (ITracingService?)serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext? context = (IPluginExecutionContext?)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.
            if (context != null && 
                context.InputParameters.Contains("Target") &&
                // Obtain the target entity from the input parameters.
                context.InputParameters["Target"] is Entity entity)
            {
                // Verify that the target entity represents an account.
                // If not, this plug-in was not registered correctly.
                if (entity.LogicalName != "contact")
                    return;

                try
                {
                    // Create a task activity to follow up with the account customer in 7 days.
                    Entity followup = new("task");

                    followup["subject"] = "Send e-mail to the new customer.";
                    followup["description"] =
                        "Follow up with the customer. Check if there are any new issues that need resolution.";
                    followup["scheduledstart"] = DateTime.Now.AddDays(7);
                    followup["scheduledend"] = DateTime.Now.AddDays(7);
                    followup["category"] = context.PrimaryEntityName;

                    // Refer to the contact in the task activity.
                    if (context.OutputParameters.Contains("id"))
                    {
                        Guid regardingobjectid = new(context.OutputParameters["id"]?.ToString() ?? "invalid_guid");
                        string regardingobjectidType = "contact";

                        followup["regardingobjectid"] =
                        new EntityReference(regardingobjectidType, regardingobjectid);
                    }

                    // Obtain the organization service reference.
                    if (serviceProvider.GetService(typeof(IOrganizationServiceFactory)) is IOrganizationServiceFactory serviceFactory) {

                        IOrganizationService? service = serviceFactory.CreateOrganizationService(context.UserId);

                        // Create the task in Microsoft Dynamics CRM.
                        tracingService?.Trace("FollowupPlugin: Creating the task activity.");
                        service.Create(followup);
                    }
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("An error occurred in the FollowUpPlugin plug-in.", ex);
                }
                catch (Exception ex)
                {
                    tracingService?.Trace("FollowupPlugin: {0}", ex.ToString());
                    throw;
                }
            }
        }
    }

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
