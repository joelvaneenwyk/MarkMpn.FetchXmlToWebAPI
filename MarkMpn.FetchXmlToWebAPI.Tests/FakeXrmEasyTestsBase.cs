using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Enums;
using FakeXrmEasy.FakeMessageExecutors;
using FakeXrmEasy.Middleware;
using FakeXrmEasy.Middleware.Crud;
using FakeXrmEasy.Middleware.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[SuppressMessage("Design", "CA1051:Do not declare visible instance fields")]
public class FakeXrmEasyTestsBase
{
    private readonly Lazy<FetchXmlConversionEntities> _entities = new(
        () => new FetchXmlConversionEntities(), LazyThreadSafetyMode.ExecutionAndPublication);

    protected readonly IXrmFakedContext Context;
    protected readonly IOrganizationServiceAsync2 Service;
    
    protected FakeXrmEasyTestsBase()
    {
        Context = MiddlewareBuilder
            .New()
            .AddCrud()
            .AddFakeMessageExecutors(Assembly.GetAssembly(typeof(AddListMembersListRequestExecutor)))
            .AddFakeMessageExecutors(Assembly.GetAssembly(typeof(RetrieveAllEntitiesRequestExecutor)))
            .UseCrud()
            .UseMessages()
            .SetLicense(FakeXrmEasyLicense.RPL_1_5)
            .Build();

        Service = Context.GetAsyncOrganizationService2();
    }

    protected string ConvertFetchToOData(string fetch) =>
        _entities.Value.Convert(fetch, Context);
}


public sealed class FetchXmlConversionEntities
{
    private readonly List<OneToManyRelationshipMetadata> _relationships = new();
    private readonly List<EntityMetadata> _entities = new();
    private readonly Dictionary<string, AttributeMetadata[]> _attributes;


    public EntityMetadata[] Entities => _entities.ToArray();

    public FetchXmlConversionEntities()
    {
        // Add basic metadata
        this._relationships.AddRange(new[]
        {
                new OneToManyRelationshipMetadata
                {
                    SchemaName = "contact_customer_accounts",
                    ReferencedEntity = "account",
                    ReferencedAttribute = "accountid",
                    ReferencingEntity = "contact",
                    ReferencingAttribute = "parentcustomerid"
                },
                new OneToManyRelationshipMetadata
                {
                    SchemaName = "account_primarycontact",
                    ReferencedEntity = "contact",
                    ReferencedAttribute = "contactid",
                    ReferencingEntity = "account",
                    ReferencingAttribute = "primarycontactid"
                }
            });

        this._entities.AddRange(new[]
        {
                new EntityMetadata
                {
                    LogicalName = "account",
                    EntitySetName = "accounts"
                },
                new EntityMetadata
                {
                    LogicalName = "contact",
                    EntitySetName = "contacts"
                },
                new EntityMetadata
                {
                    LogicalName = "connection",
                    EntitySetName = "connections"
                },
                new EntityMetadata
                {
                    LogicalName = "webresource",
                    EntitySetName = "webresourceset"
                },
                new EntityMetadata
                {
                    LogicalName = "stringmap",
                    EntitySetName = "stringmaps"
                },
                new EntityMetadata
                {
                    LogicalName = "incident",
                    EntitySetName = "incidents"
                }
            });

        _attributes = new Dictionary<string, AttributeMetadata[]>
        {
            ["account"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "accountid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "name"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "websiteurl"
                    },
                    new LookupAttributeMetadata
                    {
                        LogicalName = "primarycontactid",
                        Targets = new[] { "contact" }
                    }
            },
            ["contact"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "contactid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "firstname"
                    },
                    new LookupAttributeMetadata
                    {
                        LogicalName = "parentcustomerid",
                        Targets = new[] { "account", "contact" }
                    },
                    new DateTimeAttributeMetadata
                    {
                        LogicalName = "createdon"
                    }
            },
            ["connection"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "connectionid"
                    },
                    new PicklistAttributeMetadata
                    {
                        LogicalName = "record1objecttypecode"
                    }
            },
            ["incident"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "incidentid"
                    }
            },
            ["stringmap"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "stringmapid"
                    },
                    new EntityNameAttributeMetadata
                    {
                        LogicalName = "objecttypecode"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "attributename"
                    },
                    new IntegerAttributeMetadata
                    {
                        LogicalName = "attributevalue"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "value"
                    }
            },
            ["webresource"] = new AttributeMetadata[]
            {
                    new UniqueIdentifierAttributeMetadata
                    {
                        LogicalName = "webresourceid"
                    },
                    new StringAttributeMetadata
                    {
                        LogicalName = "name"
                    },
                    new ManagedPropertyAttributeMetadata
                    {
                        LogicalName = "iscustomizable"
                    }
            }
        };

        SetSealedProperty(
            _attributes["webresource"].First(a => a.LogicalName == "iscustomizable"),
            nameof(ManagedPropertyAttributeMetadata.ValueAttributeTypeCode),
            AttributeTypeCode.Boolean);
        SetRelationships(this._entities.ToArray(), this._relationships.ToArray());
        SetAttributes(this._entities.ToArray(), _attributes);

        var incidentEntityMetadata = this._entities.First(e => e.LogicalName == "incident");
        SetSealedProperty(
            incidentEntityMetadata,
            nameof(EntityMetadata.ObjectTypeCode),
            112);
    }

    public string Convert(
        string fetch,
        IXrmFakedContext context,
        string orgUrl = "https://example.crm.dynamics.com/api/data/v9.0")
    {
        foreach (var entity in this._entities)
            context.SetEntityMetadata(entity);

        var org = context.GetOrganizationService();
        var converter = new FetchXmlToWebAPIConverter(
            new MetadataProvider(org),
            orgUrl);
        return converter.ConvertFetchXmlToWebAPI(fetch);
    }

    private static void SetAttributes(EntityMetadata[] entities, Dictionary<string, AttributeMetadata[]> attributes)
    {
        foreach (var entity in entities)
        {
            SetSealedProperty(entity, nameof(EntityMetadata.PrimaryIdAttribute),
                attributes[entity.LogicalName].OfType<UniqueIdentifierAttributeMetadata>().First().LogicalName);
            SetSealedProperty(entity, nameof(EntityMetadata.Attributes), attributes[entity.LogicalName]);
        }
    }

    private static void SetRelationships(EntityMetadata[] entities, OneToManyRelationshipMetadata[] relationships)
    {
        foreach (var relationship in relationships)
        {
            relationship.ReferencingEntityNavigationPropertyName = relationship.ReferencingAttribute;
            relationship.ReferencedEntityNavigationPropertyName = relationship.SchemaName;
        }

        foreach (var entity in entities)
        {
            var oneToMany = relationships.Where(r => r.ReferencedEntity == entity.LogicalName).ToArray();
            var manyToOne = relationships.Where(r => r.ReferencingEntity == entity.LogicalName).ToArray();

            SetSealedProperty(entity, nameof(EntityMetadata.OneToManyRelationships), oneToMany);
            SetSealedProperty(entity, nameof(EntityMetadata.ManyToOneRelationships), manyToOne);
        }
    }

    private static void SetSealedProperty(object? target, string name, object value)
    {
        PropertyInfo? prop = target?.GetType()?.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value, null);
        }
    }
}
