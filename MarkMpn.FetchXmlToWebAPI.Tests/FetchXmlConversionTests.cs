﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using FakeXrmEasy;
using JetBrains.Annotations;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI.Tests
{
    [TestClass]
    public class FetchXmlConversionTests : FakeXrmEasyTestsBase
    {
        [TestMethod]
        public void SimpleQuery()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name", odata);
        }

        [TestMethod]
        public void LeftOuterJoinParentLink()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)",
                odata);
        }

        [TestMethod]
        public void LeftOuterJoinChildLink()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='outer'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname)",
                odata);
        }

        [TestMethod]
        public void SimpleFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='FXB' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB')", odata);
        }

        [TestMethod]
        public void NestedFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='eq' value='FXB' />
                            <filter type='or'>
                                <condition attribute='websiteurl' operator='eq' value='xrmtoolbox.com' />
                                <condition attribute='websiteurl' operator='eq' value='fetchxmlbuilder.com' />
                            </filter>
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(name eq 'FXB' and (websiteurl eq 'xrmtoolbox.com' or websiteurl eq 'fetchxmlbuilder.com'))",
                odata);
        }

        [TestMethod]
        public void Sort()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <order attribute='name' />
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$orderby=name asc",
                odata);
        }

        [TestMethod]
        public void Top()
        {
            const string fetch = @"
                <fetch top='10'>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$top=10", odata);
        }

        [TestMethod]
        public void AggregateCount()
        {
            const string fetch = @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='accountid' aggregate='count' alias='count' />
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$apply=groupby((name),aggregate($count as count))",
                odata);
        }

        [TestMethod]
        public void AggregateMax()
        {
            const string fetch = @"
                <fetch aggregate='true'>
                    <entity name='account'>
                        <attribute name='name' groupby='true' alias='name' />
                        <attribute name='websiteurl' aggregate='max' alias='maxwebsite' />
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$apply=groupby((name),aggregate(websiteurl with max as maxwebsite))",
                odata);
        }

        [TestMethod]
        public void InnerJoinParentLink()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/contactid ne null)",
                odata);
        }

        [TestMethod]
        public void InnerJoinParentLinkWithFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='firstname' operator='eq' value='Mark' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/firstname eq 'Mark')",
                odata);
        }

        [TestMethod]
        public void InnerJoinParentLinkWithComplexFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='contactid' to='primarycontactid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='createdon' operator='on' value='2020-01-01' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=primarycontactid($select=firstname)&$filter=(primarycontactid/Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01'))",
                odata);
        }

        [TestMethod]
        public void InnerJoinChildLink()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname)&$filter=(contact_customer_accounts/any(o1:(o1/contactid ne null)))",
                odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='firstname' operator='eq' value='Mark' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(firstname eq 'Mark'))&$filter=(contact_customer_accounts/any(o1:(o1/firstname eq 'Mark')))",
                odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithComplexFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='createdon' operator='on' value='2020-01-01' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01')))&$filter=(contact_customer_accounts/any(o1:(o1/Microsoft.Dynamics.CRM.On(PropertyName='createdon',PropertyValue='2020-01-01'))))",
                odata);
        }

        [TestMethod]
        public void FilterPrefix()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='FXB%' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(startswith(name, 'FXB'))",
                odata);
        }

        [TestMethod]
        public void InnerJoinChildLinkWithPrefixFilter()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                            <attribute name='firstname' />
                            <filter>
                                <condition attribute='firstname' operator='like' value='FXB%' />
                            </filter>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$expand=contact_customer_accounts($select=firstname;$filter=(startswith(firstname, 'FXB')))&$filter=(contact_customer_accounts/any(o1:(startswith(o1%2ffirstname, 'FXB'))))",
                odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterSuffix()
        {
            const string fetch = @"
                 <fetch>
                     <entity name='account'>
                         <attribute name='name' />
                         <filter>
                             <condition attribute='name' operator='like' value='%FXB' />
                         </filter>
                     </entity>
                 </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(endswith(name, 'FXB'))",
                odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterContains()
        {
            const string fetch = @"
                 <fetch>
                     <entity name='account'>
                         <attribute name='name' />
                         <filter>
                             <condition attribute='name' operator='like' value='%FXB%' />
                         </filter>
                     </entity>
                 </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(contains(name, 'FXB'))",
                odata);
        }

        [TestMethod]
        public void FilterPrefixEscaped()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='[[]FXB%' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(startswith(name, '%5bFXB'))",
                odata);
        }

        [TestMethod]
        public void FilterComplexWildcard()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='name' operator='like' value='%F_XB%' />
                        </filter>
                    </entity>
                </fetch>";

            Assert.ThrowsException<NotSupportedException>(() => ConvertFetchToOData(fetch));
        }

        [TestMethod]
        public void FilterOnEntityName()
        {
            const string fetch = @"
                <fetch>
                    <entity name='stringmap'>
                        <attribute name='attributevalue' />
                        <attribute name='attributename' />
                        <attribute name='value' />
                        <filter>
                            <condition attribute='attributename' operator='eq' value='prioritycode' />
                            <condition attribute='objecttypecode' operator='eq' value='112' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/stringmaps?$select=attributevalue,attributename,value&$filter=(attributename eq 'prioritycode' and objecttypecode eq 'incident')",
                odata);
        }

        [TestMethod]
        public void FilterOnOptionSet()
        {
            const string fetch = @"
                <fetch>
                    <entity name='connection'>
                        <attribute name='connectionid' />
                        <filter>
                            <condition attribute='record1objecttypecode' operator='eq' value='8' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/connections?$select=connectionid&$filter=(record1objecttypecode eq 8)",
                odata);
        }

        [TestMethod]
        public void FilterOnManagedProperty()
        {
            const string fetch = @"
                <fetch>
                    <entity name='webresource'>
                        <attribute name='name' />
                        <attribute name='iscustomizable' />
                        <filter>
                            <condition attribute='iscustomizable' operator='eq' value='1' />
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/webresourceset?$select=name,iscustomizable&$filter=(iscustomizable/Value eq true)",
                odata);
        }

        [TestMethod]
        public void Skip()
        {
            const string fetch = @"
                <fetch count='10' page='3'>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            Assert.ThrowsException<NotSupportedException>(() => ConvertFetchToOData(fetch));
        }

        [TestMethod]
        public void Archive()
        {
            const string fetch = @"
                <fetch datasource='archive'>
                    <entity name='account'>
                        <attribute name='name' />
                    </entity>
                </fetch>";

            Assert.ThrowsException<NotSupportedException>(() => ConvertFetchToOData(fetch));
        }

        [Ignore]
        [TestMethod]
        public void FilterOnPrimaryKey()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='accountid' operator='eq' value='3fee3d59-68c9-ed11-b597-0022489b41c4' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(accountid eq 3fee3d59-68c9-ed11-b597-0022489b41c4)", odata);
        }

        [TestMethod]
        public void FilterOnLookup()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                            <condition attribute='primarycontactid' operator='eq' value='3fee3d59-68c9-ed11-b597-0022489b41c4' />
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(_primarycontactid_value eq 3fee3d59-68c9-ed11-b597-0022489b41c4)", odata);
        }

        [Ignore]
        [TestMethod]
        public void InnerJoinChildLinkWithNoChildren()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='inner'>
                        </link-entity>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual(
                "https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name&$filter=(contact_customer_accounts/any(o1:(o1/contactid ne null)))",
                odata);
        }


        [TestMethod]
        public void FilterWithNoChildren()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                        <attribute name='name' />
                        <filter>
                        </filter>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$select=name", odata);
        }

        [TestMethod]
        public void EntityWithNoChildren()
        {
            const string fetch = @"
                <fetch>
                    <entity name='account'>
                    </entity>
                </fetch>";

            string? odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts", odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterAll()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <filter>
                            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='all'>
                                <filter>
                                    <condition attribute='firstname' operator='eq' value='Mark' />
                                </filter>
                            </link-entity>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$filter=(contact_customer_accounts/all(x1:(x1/firstname eq 'Mark')))", odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterAny()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <filter>
                            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='any'>
                                <filter>
                                    <condition attribute='firstname' operator='eq' value='Mark' />
                                </filter>
                            </link-entity>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$filter=(contact_customer_accounts/any(x1:(x1/firstname eq 'Mark')))", odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterNotAny()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <filter>
                            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='not any'>
                                <filter>
                                    <condition attribute='firstname' operator='eq' value='Mark' />
                                </filter>
                            </link-entity>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$filter=(not contact_customer_accounts/any(x1:(x1/firstname ne 'Mark')))", odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterNotAll()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <filter>
                            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='not all'>
                                <filter>
                                    <condition attribute='firstname' operator='eq' value='Mark' />
                                </filter>
                            </link-entity>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$filter=(not contact_customer_accounts/all(x1:(x1/firstname ne 'Mark')))", odata);
        }

        [Ignore]
        [TestMethod]
        public void FilterNotAllNestedNotAny()
        {
            var fetch = @"
                <fetch>
                    <entity name='account'>
                        <filter>
                            <link-entity name='contact' from='parentcustomerid' to='accountid' link-type='not all'>
                                <filter>
                                    <link-entity name='account' from='primarycontactid' to='contactid' link-type='not any'>
                                        <filter>
                                            <condition attribute='name' operator='eq' value='Data8' />
                                        </filter>
                                    </link-entity>
                                </filter>
                            </link-entity>
                        </filter>
                    </entity>
                </fetch>";

            var odata = ConvertFetchToOData(fetch);

            Assert.AreEqual("https://example.crm.dynamics.com/api/data/v9.0/accounts?$filter=(not contact_customer_accounts/all(x1:(x1/account_primarycontact/any(x2:(x2/name eq 'Data8')))))", odata);
        }

        [UsedImplicitly]
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable IDE0051 // Remove unused private members
        private static string? ConvertFetchToODataAlt(string fetch)
        {
            var context = new XrmFakedContext();
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore IDE0051 // Remove unused private members

            // Add basic metadata
            var relationships = new[]
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
            };

            var entities = new[]
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
            };

            var attributes = new Dictionary<string, AttributeMetadata[]>
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

            SetSealedProperty(attributes["webresource"].Single(a => a.LogicalName == "iscustomizable"), nameof(ManagedPropertyAttributeMetadata.ValueAttributeTypeCode), AttributeTypeCode.Boolean);
            SetRelationships(entities, relationships);
            SetAttributes(entities, attributes);
            SetSealedProperty(entities.Single(e => e.LogicalName == "incident"), nameof(EntityMetadata.ObjectTypeCode), 112);

            foreach (var entity in entities)
                context.SetEntityMetadata(entity);

#pragma warning disable CS0618 // Type or member is obsolete
            context.AddFakeMessageExecutor<RetrieveAllEntitiesRequest>(new RetrieveAllEntitiesRequestExecutor());
#pragma warning restore CS0618 // Type or member is obsolete

            var org = context.GetOrganizationService();
            var converter = new FetchXmlToWebAPIConverter(new MetadataProvider(org), $"https://example.crm.dynamics.com/api/data/v9.0");
            return converter.ConvertFetchXmlToWebAPI(fetch);
        }

        private static void SetAttributes(EntityMetadata[] entities, Dictionary<string, AttributeMetadata[]> attributes)
        {
            foreach (var entity in entities)
            {
                SetSealedProperty(entity, nameof(EntityMetadata.PrimaryIdAttribute), attributes[entity.LogicalName].OfType<UniqueIdentifierAttributeMetadata>().Single().LogicalName);
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

        private static void SetSealedProperty(object target, string name, object value)
        {
            var prop = target.GetType().GetProperty(name);
            prop?.SetValue(target, value, null);
        }
    }
}
