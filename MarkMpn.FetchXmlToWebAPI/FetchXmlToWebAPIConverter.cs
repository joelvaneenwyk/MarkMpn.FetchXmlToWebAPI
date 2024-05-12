using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using JetBrains.Annotations;
using Microsoft.Xrm.Sdk.Metadata;

namespace MarkMpn.FetchXmlToWebAPI
{
    /// <summary>
    ///     Converts a FetchXML query to Web API format
    /// </summary>
    public class FetchXmlToWebAPIConverter
    {
        private class LinkEntityOData
        {
            public LinkEntityOData(string? propertyName = null)
            {
                PropertyName = propertyName;
            }

            protected virtual string Separator => ";";

            protected string? PropertyName { get; }

            public List<string> Select { get; } = new List<string>();

            public List<LinkEntityOData> Expand { get; } = new List<LinkEntityOData>();

            public readonly List<FilterOData> Filter = new List<FilterOData>();

            protected virtual IEnumerable<string> GetParts()
            {
                if (Select.Count != 0)
                {
                    yield return "$select=" + string.Join(",", Select);
                }

                if (Expand.Count != 0)
                {
                    yield return "$expand=" + string.Join(",", Expand.Select(e => $"{e.PropertyName}({e})"));
                }

                if (Filter.Count != 0)
                {
                    yield return "$filter=" + string.Join(" and ", Filter);
                }
            }

            public override string ToString()
            {
                return string.Join(Separator, GetParts());
            }
        }

        private sealed class FilterOData
        {
            public bool And { get; set; }

            public readonly List<string> Conditions = new List<string>();

            public FilterOData(List<FilterOData>? filters = null)
            {
                Filters = filters ?? new List<FilterOData>();
            }

            public List<FilterOData> Filters { get; }

            public override string? ToString()
            {
                if (Conditions.Count == 0 && Filters.Count == 0)
                {
                    return null;
                }

                var items = Conditions.Select(c => c.ToString())
                    .Concat(Filters.Select(f => f.ToString()))
                    .Where(c => !string.IsNullOrEmpty(c));

                var logicalOperator = And ? " and " : " or ";

                return "(" + string.Join(logicalOperator, items) + ")";
            }
        }

        private sealed class EntityOData : LinkEntityOData
        {
            public EntityOData(string? propertyName = null) : base(propertyName)
            {
            }

            public int? Top { get; set; }

            public int? PageSize { get; }

            public List<OrderOData> OrderBy { get; } = new List<OrderOData>();

            public List<string> Groups { get; } = new List<string>();

            public List<string> Aggregates { get; } = new List<string>();

            protected override string Separator => "&";

            protected override IEnumerable<string> GetParts()
            {
                if (Aggregates.Count != 0)
                {
                    var aggregate = $"aggregate({string.Join(",", Aggregates)})";

                    if (Groups.Count != 0)
                    {
                        aggregate = $"groupby(({string.Join(",", Groups)}),{aggregate})";
                    }

                    if (Filter.Count != 0)
                    {
                        aggregate = $"filter({string.Join(" and ", Filter)})/{aggregate}";
                    }

                    yield return "$apply=" + aggregate;
                    yield break;
                }

                foreach (var part in base.GetParts())
                {
                    yield return part;
                }

                if (OrderBy.Count != 0)
                {
                    yield return "$orderby=" + string.Join(",", OrderBy);
                }

                if (Top != null)
                {
                    yield return "$top=" + Top;
                }
            }

            public override string ToString()
            {
                var query = base.ToString();

                return "/" + PropertyName + (string.IsNullOrEmpty(query) ? "" : "?" + query);
            }
        }

        private sealed class OrderOData
        {
            public OrderOData(string? propertyName = null)
            {
                PropertyName = propertyName ?? string.Empty;
            }

            public string PropertyName { get; set; }

            public bool Descending { get; set; }

            public override string ToString()
            {
                return PropertyName + (Descending ? " desc" : " asc");
            }
        }

        private readonly IMetadataProvider _metadata;
        private readonly string _orgUrl;

        /// <summary>
        ///     Creates a new <see cref="FetchXmlToWebAPIConverter" />
        /// </summary>
        /// <param name="metadata">The source of metadata for the conversion</param>
        /// <param name="orgUrl">
        ///     The base URL of the organization Web API service, e.g.
        ///     https://example.crm.dynamics.com/api/data/v9.0
        /// </param>
        public FetchXmlToWebAPIConverter(IMetadataProvider metadata, string orgUrl)
        {
            _metadata = metadata;
            _orgUrl = orgUrl;
        }

        /// <summary>
        ///     Converts a FetchXML query to Web API format
        /// </summary>
        /// <param name="fetch">The FetchXML query to convert</param>
        /// <returns>The equivalent Web API format query</returns>
        public string? ConvertFetchXmlToWebAPI(string fetch)
        {
            var url = ConvertFetchXmlToWebAPI(fetch, out var preferHeaders);

            if (preferHeaders != null && preferHeaders.Length > 0)
            {
                throw new NotSupportedException("A Prefer header is required in addition to the URL");
            }

            return url;
        }

        /// <summary>
        ///     Converts a FetchXML query to Web API format
        /// </summary>
        /// <param name="fetch">The FetchXML query to convert</param>
        /// <param name="preferHeaders">The value to set the Prefer header to</param>
        /// <returns>The equivalent Web API format query</returns>
        [PublicAPI]
        public string? ConvertFetchXmlToWebAPI(string fetch, out string[] preferHeaders)
        {
            if (!_metadata.IsConnected)
            {
                throw new InvalidOperationException("Must have an active connection to CRM to compose OData query.");
            }

            EntityOData? converted = null;
            string? url = null;
            using (var reader = new StringReader(fetch))
            {
                var serializer = new XmlSerializer(typeof(FetchType));
                try
                {
                    if (serializer.Deserialize(XmlReader.Create(reader)) is FetchType parsed)
                    {
                        converted = ConvertOData(parsed);
                        url = _orgUrl + converted;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException("Error parsing FetchXML", ex);
                }

                preferHeaders = (converted?.PageSize != null
                    ? new[] { $"odata.maxpagesize={converted.PageSize}" }
                    : null) ?? Array.Empty<string>();
            }

            return url;
        }

        private EntityOData ConvertOData(FetchType fetch)
        {
            if (!string.IsNullOrEmpty(fetch.datasource))
            {
                throw new NotSupportedException("Only live data is supported in Web API");
            }

            if (!string.IsNullOrEmpty(fetch.page) && fetch.page != "1")
            {
                // Should be able to use $skip to move to subsequent pages, but this generates an error from Web API:
                // {"error":{"code":"0x80060888","message":"Skip Clause is not supported in CRM"}}
                throw new NotSupportedException(
                    "Skipping to subsequent pages is not supported in Web API. Load the first page and follow the @odata.nextLink URLs to get to subsequent pages");
            }

            if (!(fetch.Items.FirstOrDefault(i => i is FetchEntityType) is FetchEntityType entity))
            {
                throw new NotSupportedException("Fetch must contain entity definition");
            }

            EntityOData odata = new EntityOData(LogicalToCollectionName(entity.name));

            if (!string.IsNullOrEmpty(fetch.top))
            {
                odata.Top = int.Parse(fetch.top, CultureInfo.InvariantCulture);
            }

            if (entity.Items == null)
            {
                return odata;
            }

            if (fetch.aggregate)
            {
                odata.Groups.AddRange(ConvertGroups(entity.Items));
                odata.Aggregates.AddRange(ConvertAggregates(entity.Items));
            }

            odata.Select.AddRange(ConvertSelect(entity.name, entity.Items));
            odata.OrderBy.AddRange(ConvertOrder(entity.name, entity.Items));
            odata.Filter.AddRange(ConvertFilters(entity.name, entity.Items, entity.Items));
            odata.Expand.AddRange(ConvertJoins(entity.name, entity.Items, entity.Items));

            // Add extra filters to simulate inner joins
            var count = 1;
            odata.Filter.AddRange(ConvertInnerJoinFilters(entity.name, entity.Items, entity.Items, "", ref count));

            return odata;
        }

        private static IEnumerable<string> ConvertGroups(object[] items)
        {
            return items.OfType<FetchAttributeType>()
                .Where(a => a.groupbySpecified)
                .Select(a => a.name);
        }

        private static IEnumerable<string> ConvertAggregates(object[] items)
        {
            return items.OfType<FetchAttributeType>()
                .Where(a => a.aggregateSpecified)
                .Select(a =>
                    a.aggregate == AggregateType.count
                        ? $"$count as {a.alias}"
                        : $"{a.name} with {GetAggregateType(a.aggregate)} as {a.alias}");
        }

        private static string GetAggregateType(AggregateType aggregate)
        {
            switch (aggregate)
            {
                case AggregateType.avg:
                    return "average";

                case AggregateType.countcolumn:
                    return "countdistinct";

                case AggregateType.max:
                case AggregateType.min:
                case AggregateType.sum:
                    return aggregate.ToString();
                case AggregateType.count:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(aggregate), aggregate, null);
            }

            throw new NotSupportedException("Unknown aggregate type " + aggregate);
        }

        private List<FilterOData> ConvertInnerJoinFilters(string entityName, object[] items, object[] rootEntityItems,
            string path, ref int count)
        {
            var filters = new List<FilterOData>();

            foreach (var linkEntity in items.OfType<FetchLinkEntityType>()
                         .Where(l => l.linktype == "inner" || string.IsNullOrEmpty(l.linktype)))
            {
                var currentLinkEntity = linkEntity;
                var propertyName = path + LinkItemToNavigationProperty(entityName, currentLinkEntity, out var child,
                    out var manyToManyNextLink);

                currentLinkEntity = manyToManyNextLink ?? currentLinkEntity;

                if (!child)
                {
                    var childFilter = currentLinkEntity.Items == null
                        ? new List<FilterOData>()
                        : ConvertFilters(currentLinkEntity.name, currentLinkEntity.Items, rootEntityItems,
                            propertyName + "/").ToList();

                    if (childFilter.Count == 0)
                    {
                        if (propertyName.Split('/').Length >= 2)
                        {
                            // Filtering on nested link-entities is not currently supported
                            // See https://github.com/rappen/FetchXMLBuilder/issues/415
                            throw new NotSupportedException(
                                $"Cannot include inner join on nested link-entity {propertyName}. Try rearranging your query to have inner joins on first-level link-entities only");
                        }

                        filters.Add(new FilterOData
                        {
                            Conditions =
                            {
                                $"{propertyName}/{_metadata.GetEntity(currentLinkEntity.name).PrimaryIdAttribute} ne null"
                            }
                        });
                    }

                    if (currentLinkEntity.Items != null)
                    {
                        childFilter.AddRange(ConvertInnerJoinFilters(currentLinkEntity.name, currentLinkEntity.Items,
                            rootEntityItems, path + propertyName + "/", ref count));
                    }

                    filters.AddRange(childFilter);
                }
                else
                {
                    var rangeVariable = "o" + count++;
                    var childFilter = currentLinkEntity.Items == null
                        ? new List<FilterOData>()
                        : ConvertFilters(currentLinkEntity.name, currentLinkEntity.Items, rootEntityItems,
                            $"{rangeVariable}/").ToList();

                    if (childFilter.Count == 0)
                    {
                        childFilter.Add(new FilterOData
                        {
                            Conditions =
                            {
                                $"{rangeVariable}/{_metadata.GetEntity(currentLinkEntity.name).PrimaryIdAttribute} ne null"
                            }
                        });
                    }

                    if (currentLinkEntity.Items != null)
                    {
                        childFilter.AddRange(ConvertInnerJoinFilters(currentLinkEntity.name, currentLinkEntity.Items,
                            rootEntityItems, path + rangeVariable + "/", ref count));
                    }

                    var condition = propertyName + $"/any({rangeVariable}:{string.Join(" and ", childFilter)})";
                    filters.Add(new FilterOData { Conditions = { condition } });
                }
            }

            return filters;
        }

        private IEnumerable<LinkEntityOData> ConvertJoins(string entityName, object[] items, object[] rootEntityItems)
        {
            foreach (var linkEntity in items
                         .OfType<FetchLinkEntityType>()
                         .Where(l => l.Items != null && l.Items.Length > 0))
            {
                var currentLinkEntity = linkEntity;
                LinkEntityOData expand = new LinkEntityOData(LinkItemToNavigationProperty(
                    entityName, currentLinkEntity, out var child, out var manyToManyNextLink));
                currentLinkEntity = manyToManyNextLink ?? currentLinkEntity;
                expand.Select.AddRange(ConvertSelect(currentLinkEntity.name, currentLinkEntity.Items));

                if (linkEntity.linktype == "outer" || child)
                {
                    // Don't need to add filters at this point for single-valued properties in inner joins, they'll be added separately later
                    expand.Filter.AddRange(ConvertFilters(currentLinkEntity.name, currentLinkEntity.Items,
                        rootEntityItems));
                }

                // Recurse into child joins
                expand.Expand.AddRange(ConvertJoins(currentLinkEntity.name, currentLinkEntity.Items, rootEntityItems));

                yield return expand;
            }
        }

        private IEnumerable<string> ConvertSelect(string entityName, object[] items)
        {
            var attributeitems = items
                .OfType<FetchAttributeType>()
                .Where(i => i.name != null);

            return GetAttributeNames(entityName, attributeitems);
        }

        private IEnumerable<string> GetAttributeNames(string entityName, IEnumerable<FetchAttributeType> attributeitems)
        {
            var entityMeta = _metadata.GetEntity(entityName);

            foreach (FetchAttributeType attributeitem in attributeitems)
            {
                var attrMeta = entityMeta
                                   .Attributes
                                   .FirstOrDefault(a => a.LogicalName == attributeitem.name)
                               ?? throw new NotSupportedException(
                                   $"Unknown attribute {entityName}.{attributeitem.name}");

                yield return GetPropertyName(attrMeta);
            }
        }

        private IEnumerable<FilterOData> ConvertFilters(string entityName, object[] items, object[] rootEntityItems,
            string navigationProperty = "")
        {
            return items
                .OfType<filter>()
                .Where(f => f.Items != null && f.Items.Length != 0)
                .Select(f =>
                {
                    var filterOData = new FilterOData { And = f.type == filterType.and };
                    filterOData.Conditions.AddRange(ConvertConditions(entityName, f.Items, rootEntityItems,
                        navigationProperty));
                    filterOData.Filters.AddRange(ConvertFilters(entityName, f.Items, rootEntityItems,
                        navigationProperty));
                    return filterOData;
                });
        }

        private IEnumerable<string> ConvertConditions(string entityName, object[] items, object[] rootEntityItems,
            string navigationProperty = "")
        {
            return items
                .OfType<condition>()
                .Select(c => GetCondition(entityName, c, rootEntityItems, navigationProperty));
        }

        private string GetCondition(string entityName, condition condition, object[] rootEntityItems,
            string navigationProperty = "")
        {
            var result = "";
            if (!string.IsNullOrEmpty(condition.attribute))
            {
                if (!string.IsNullOrEmpty(condition.entityname))
                {
                    var linkEntity = FindLinkEntity(entityName, rootEntityItems, condition.entityname, "",
                                         out navigationProperty,
                                         out var child)
                                     ?? throw new NotSupportedException("Cannot find filter entity " +
                                                                        condition.entityname);

                    if (child)
                    {
                        // Filtering a child collection separately has different semantics in OData vs. FetchXML, e.g.:
                        //
                        // <fetch top="50" >
                        //   <entity name="account" >
                        //     <attribute name="name" />
                        //     <filter type="or" >
                        //       <condition attribute="name" operator="eq" value="fxb" />
                        //       <condition entityname="contact" attribute="firstname" operator="eq" value="jonas" />
                        //     </filter>
                        //     <link-entity name="contact" from="parentcustomerid" to="accountid" link-type="inner" >
                        //       <attribute name="fullname" />
                        //       <filter>
                        //         <condition attribute="lastname" operator="eq" value="rapp" />
                        //       </filter>
                        //     </link-entity>
                        //   </entity>
                        // </fetch>
                        //
                        // gives a result only where a contact matches both the firstname and lastname filters, unless the account name is "fxb"
                        // in which case only the lastname filter needs to match. By comparison, the similar OData query
                        //
                        // /accounts?$select=name&$expand=contact_customer_accounts($select=fullname;$filter=lastname eq 'rapp')&$filter=(name eq 'fxb' or contact_customer_accounts/any(o1:(o1/firstname eq 'jonas'))) and (contact_customer_accounts/any(o1:(o1/lastname eq 'rapp')))&$top=50
                        //
                        // applies the firstname and lastname filters separately on the full list of contacts, so as long as one contact matches the firstname
                        // filter it doesn't matter if it is the same record that matches the lastname filter.
                        throw new NotSupportedException("Cannot apply filter to child collection " +
                                                        navigationProperty);
                    }

                    entityName = linkEntity.name;
                }

                if (navigationProperty.Split('/').Length >= 3)
                {
                    // Filtering on nested link-entities is not currently supported
                    // See https://github.com/rappen/FetchXMLBuilder/issues/415
                    throw new NotSupportedException(
                        $"Cannot filter on nested link-entity {navigationProperty}. Try rearranging your query to have filters in first-level link-entities only");
                }

                var entity = _metadata.GetEntity(entityName);
                var attrMeta = entity
                                   .Attributes.FirstOrDefault(a => a.LogicalName == condition.attribute)
                               ?? throw new NotSupportedException(
                                   $"No metadata for attribute: {entityName}.{condition.attribute}");

                result = navigationProperty + GetPropertyName(attrMeta);

                if (attrMeta is ManagedPropertyAttributeMetadata)
                {
                    result += "/Value";
                }

                string? function = null;
                var functionParameters = 1;
                var functionParameterType = typeof(string);
                var value = condition.value;

                switch (condition.@operator)
                {
                    case @operator.eq:
                    case @operator.ne:
                    case @operator.lt:
                    case @operator.le:
                    case @operator.gt:
                    case @operator.ge:
                        result += $" {condition.@operator} ";
                        break;
                    case @operator.neq:
                        result += " ne ";
                        break;
                    case @operator.@null:
                        result += " eq null";
                        break;
                    case @operator.notnull:
                        result += " ne null";
                        break;
                    case @operator.like:
                    case @operator.notlike:
                        var hasInitialWildcard = value.StartsWith("%", StringComparison.OrdinalIgnoreCase);
                        if (hasInitialWildcard)
                        {
                            value = value.Take(1).ToString();
                        }

                        var hasTerminalWildcard = value?.EndsWith("%", StringComparison.OrdinalIgnoreCase) ?? false;
                        if (hasTerminalWildcard)
                        {
                            value = value?[..^1];
                        }

                        if (value != null && !AreAllLikeWildcardsEscaped(value))
                        {
                            throw new NotSupportedException(
                                "OData queries do not support complex LIKE wildcards. Only % at the start or end of the value is supported");
                        }

                        value = value != null ? UnescapeLikeWildcards(value) : value;

                        if (!hasInitialWildcard && !hasTerminalWildcard)
                        {
                            result += " eq ";
                        }
                        else
                        {
                            string func;

                            if (hasInitialWildcard && hasTerminalWildcard)
                            {
                                func = "contains";
                            }
                            else if (hasInitialWildcard)
                            {
                                func = "endswith";
                            }
                            else
                            {
                                func = "startswith";
                            }

                            if (value != null)
                            {
                                result =
                                    $"{func}({HttpUtility.UrlEncode(navigationProperty + attrMeta.LogicalName)}, {FormatValue(typeof(string), value)})";
                            }
                        }

                        if (condition.@operator == @operator.notlike)
                        {
                            result = "not " + result;
                        }

                        break;
                    case @operator.beginswith:
                    case @operator.notbeginwith:
                        result =
                            $"startswith({HttpUtility.UrlEncode(navigationProperty + attrMeta.LogicalName)}, {FormatValue(typeof(string), condition.value)})";

                        if (condition.@operator == @operator.notbeginwith)
                        {
                            result = "not " + result;
                        }

                        break;
                    case @operator.endswith:
                    case @operator.notendwith:
                        result =
                            $"endswith({HttpUtility.UrlEncode(navigationProperty + attrMeta.LogicalName)}, {FormatValue(typeof(string), condition.value)})";

                        if (condition.@operator == @operator.notendwith)
                        {
                            result = "not " + result;
                        }

                        break;
                    case @operator.above:
                        function = "Above";
                        break;
                    case @operator.eqorabove:
                        function = "AboveOrEqual";
                        break;
                    case @operator.between:
                        function = "Between";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.containvalues:
                        function = "ContainValues";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.notcontainvalues:
                        function = "DoesNotContainValues";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.eqbusinessid:
                        function = "EqualBusinessId";
                        functionParameters = 0;
                        break;
                    case @operator.equserid:
                        function = "EqualUserId";
                        functionParameters = 0;
                        break;
                    case @operator.equserlanguage:
                        function = "EqualUserLanguage";
                        functionParameters = 0;
                        break;
                    case @operator.equseroruserhierarchy:
                        function = "EqualUserOrUserHierarchy";
                        functionParameters = 0;
                        break;
                    case @operator.equseroruserhierarchyandteams:
                        function = "EqualUserOrUserHierarchyAndTeams";
                        functionParameters = 0;
                        break;
                    case @operator.equseroruserteams:
                        function = "EqualUserOrUserTeams";
                        functionParameters = 0;
                        break;
                    case @operator.equserteams:
                        function = "EqualUserTeams";
                        functionParameters = 0;
                        break;
                    case @operator.@in:
                        function = "In";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.infiscalperiod:
                        function = "InFiscalPeriod";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.infiscalperiodandyear:
                        function = "InFiscalPeriodAndYear";
                        functionParameters = 2;
                        functionParameterType = typeof(long);
                        break;
                    case @operator.infiscalyear:
                        function = "InFiscalYear";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.inorafterfiscalperiodandyear:
                        function = "InOrAfterFiscalPeriodAndYear";
                        functionParameters = 2;
                        functionParameterType = typeof(long);
                        break;
                    case @operator.inorbeforefiscalperiodandyear:
                        function = "InOrBeforeFiscalPeriodAndYear";
                        functionParameters = 2;
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastsevendays:
                        function = "Last7Days";
                        functionParameters = 0;
                        break;
                    case @operator.lastfiscalperiod:
                        function = "LastFiscalPeriod";
                        functionParameters = 0;
                        break;
                    case @operator.lastfiscalyear:
                        function = "LastFiscalYear";
                        functionParameters = 0;
                        break;
                    case @operator.lastmonth:
                        function = "LastMonth";
                        functionParameters = 0;
                        break;
                    case @operator.lastweek:
                        function = "LastWeek";
                        functionParameters = 0;
                        break;
                    case @operator.lastxdays:
                        function = "LastXDays";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxfiscalperiods:
                        function = "LastXFiscalPeriods";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxfiscalyears:
                        function = "LastXFiscalYears";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxhours:
                        function = "LastXHours";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxmonths:
                        function = "LastXMonths";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxweeks:
                        function = "LastXWeeks";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastxyears:
                        function = "LastXYears";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.lastyear:
                        function = "LastYear";
                        functionParameters = 0;
                        break;
                    case @operator.nextsevendays:
                        function = "Next7Days";
                        functionParameters = 0;
                        break;
                    case @operator.nextfiscalperiod:
                        function = "NextFiscalPeriod";
                        functionParameters = 0;
                        break;
                    case @operator.nextfiscalyear:
                        function = "NextFiscalYear";
                        functionParameters = 0;
                        break;
                    case @operator.nextmonth:
                        function = "NextMonth";
                        functionParameters = 0;
                        break;
                    case @operator.nextweek:
                        function = "NextWeek";
                        functionParameters = 0;
                        break;
                    case @operator.nextxdays:
                        function = "NextXDays";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxfiscalperiods:
                        function = "NextXFiscalPeriods";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxfiscalyears:
                        function = "NextXFiscalYears";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxhours:
                        function = "NextXHours";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxmonths:
                        function = "NextXMonths";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxweeks:
                        function = "NextXWeeks";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextxyears:
                        function = "NextXYears";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.nextyear:
                        function = "NextYear";
                        functionParameters = 0;
                        break;
                    case @operator.notbetween:
                        function = "NotBetween";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.nebusinessid:
                        function = "NotEqualBusinessId";
                        functionParameters = 0;
                        break;
                    case @operator.neuserid:
                        function = "NotEqualUserId";
                        functionParameters = 0;
                        break;
                    case @operator.notin:
                        function = "NotIn";
                        functionParameters = int.MaxValue;
                        break;
                    case @operator.notunder:
                        function = "NotUnder";
                        break;
                    case @operator.olderthanxdays:
                        function = "OlderThanXDays";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.olderthanxhours:
                        function = "OlderThanXHours";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.olderthanxminutes:
                        function = "OlderThanXMinutes";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.olderthanxmonths:
                        function = "OlderThanXMonths";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.olderthanxweeks:
                        function = "OlderThanXWeeks";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.olderthanxyears:
                        function = "OlderThanXYears";
                        functionParameterType = typeof(long);
                        break;
                    case @operator.on:
                        function = "On";
                        break;
                    case @operator.onorafter:
                        function = "OnOrAfter";
                        break;
                    case @operator.onorbefore:
                        function = "OnOrBefore";
                        break;
                    case @operator.thisfiscalperiod:
                        function = "ThisFiscalPeriod";
                        functionParameters = 0;
                        break;
                    case @operator.thisfiscalyear:
                        function = "ThisFiscalYear";
                        functionParameters = 0;
                        break;
                    case @operator.thismonth:
                        function = "ThisMonth";
                        functionParameters = 0;
                        break;
                    case @operator.thisweek:
                        function = "ThisWeek";
                        functionParameters = 0;
                        break;
                    case @operator.thisyear:
                        function = "ThisYear";
                        functionParameters = 0;
                        break;
                    case @operator.today:
                        function = "Today";
                        functionParameters = 0;
                        break;
                    case @operator.tomorrow:
                        function = "Tomorrow";
                        functionParameters = 0;
                        break;
                    case @operator.under:
                        function = "Under";
                        break;
                    case @operator.eqorunder:
                        function = "UnderOrEqual";
                        break;
                    case @operator.yesterday:
                        function = "Yesterday";
                        functionParameters = 0;
                        break;
                    default:
                        throw new NotSupportedException(
                            $"Unsupported OData condition operator '{condition.@operator}'");
                }

                if (!string.IsNullOrEmpty(function))
                {
                    if (functionParameters == int.MaxValue)
                    {
                        return
                            $"{navigationProperty}Microsoft.Dynamics.CRM.{HttpUtility.UrlEncode(function)}(PropertyName='{HttpUtility.UrlEncode(attrMeta.LogicalName)}',PropertyValues=[{string.Join(",", condition.Items.Select(i => FormatValue(functionParameterType, i.Value)))}])";
                    }

                    if (functionParameters == 0)
                    {
                        return
                            $"{navigationProperty}Microsoft.Dynamics.CRM.{HttpUtility.UrlEncode(function)}(PropertyName='{HttpUtility.UrlEncode(attrMeta.LogicalName)}')";
                    }

                    if (functionParameters == 1)
                    {
                        return
                            $"{navigationProperty}Microsoft.Dynamics.CRM.{HttpUtility.UrlEncode(function)}(PropertyName='{HttpUtility.UrlEncode(attrMeta.LogicalName)}',PropertyValue={FormatValue(functionParameterType, condition.value)})";
                    }

                    return
                        $"{navigationProperty}Microsoft.Dynamics.CRM.{HttpUtility.UrlEncode(function)}(PropertyName='{HttpUtility.UrlEncode(attrMeta.LogicalName)}',{string.Join(",", condition.Items.Select((i, idx) => $"Property{idx + 1}={FormatValue(functionParameterType, i.Value)}"))})";
                }

                if (!string.IsNullOrEmpty(value) && !result.Contains('('))
                {
                    var valueType = typeof(string);
                    var typeCode = attrMeta.AttributeType;

                    if (attrMeta is ManagedPropertyAttributeMetadata managedPropAttr)
                    {
                        typeCode = managedPropAttr.ValueAttributeTypeCode;
                    }

                    switch (typeCode)
                    {
                        case AttributeTypeCode.Money:
                        case AttributeTypeCode.Decimal:
                            valueType = typeof(decimal);
                            break;

                        case AttributeTypeCode.BigInt:
                            valueType = typeof(long);
                            break;

                        case AttributeTypeCode.Boolean:
                            valueType = typeof(bool);
                            break;

                        case AttributeTypeCode.Double:
                            valueType = typeof(double);
                            break;

                        case AttributeTypeCode.Integer:
                        case AttributeTypeCode.State:
                        case AttributeTypeCode.Status:
                        case AttributeTypeCode.Picklist:
                            valueType = typeof(int);
                            break;

                        case AttributeTypeCode.Uniqueidentifier:
                        case AttributeTypeCode.Lookup:
                        case AttributeTypeCode.Customer:
                        case AttributeTypeCode.Owner:
                            valueType = typeof(Guid);
                            break;

                        case AttributeTypeCode.DateTime:
                            valueType = typeof(DateTime);
                            break;

                        case AttributeTypeCode.EntityName:
                            valueType = typeof(string);

                            if (int.TryParse(value, out var otc))
                            {
                                value = _metadata.GetEntity(otc).LogicalName;
                            }

                            break;
                    }

                    result += FormatValue(valueType, value);
                }
                else if (!string.IsNullOrEmpty(condition.valueof))
                {
                    result += condition.valueof;
                }
            }

            return result;
        }

        private static bool AreAllLikeWildcardsEscaped(string value)
        {
            var bracketStart = -1;

            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];

                if (ch != '%' && ch != '_' && ch != '[' && ch != ']')
                {
                    if (bracketStart != -1)
                    {
                        // We've got a non-wildcard character in brackets - it's not an escaped wildcard
                        return false;
                    }

                    continue;
                }

                if (bracketStart == -1)
                {
                    if (ch == '[')
                    {
                        bracketStart = i;
                    }
                    else
                    {
                        // We've got a wildcard character outside of brackets - it's not escaped
                        return false;
                    }
                }

                if (ch == ']')
                {
                    if (i > bracketStart + 2)
                    {
                        // We've got more than a single character in the brackets - it's not a single escaped wildcard
                        return false;
                    }

                    bracketStart = -1;
                }
            }

            return true;
        }

        private static string UnescapeLikeWildcards(string value) => value
                .Replace("[_]", "_")
                .Replace("[%]", "%")
                .Replace("[[]", "[");

        private FetchLinkEntityType? FindLinkEntity(string entityName, object[] items, string alias, string path,
            out string navigationProperty, out bool child)
        {
            child = false;
            navigationProperty = path;

            foreach (var linkItem in items.OfType<FetchLinkEntityType>())
            {
                var propertyName = LinkItemToNavigationProperty(entityName, linkItem, out child, out var manyToManyNextLink);
                var currentLinkItem = manyToManyNextLink ?? linkItem;

                navigationProperty = path + propertyName + "/";

                if (currentLinkItem.alias == alias || (string.IsNullOrEmpty(currentLinkItem.alias) && currentLinkItem.name == alias))
                {
                    return currentLinkItem;
                }

                var childMatch = FindLinkEntity(currentLinkItem.name, currentLinkItem.Items, alias, navigationProperty,
                    out navigationProperty, out child);

                if (childMatch != null)
                {
                    return childMatch;
                }
            }

            return null;
        }

        private static string GetPropertyName(AttributeMetadata attr)
        {
            if (attr is LookupAttributeMetadata)
            {
                return $"_{attr.LogicalName}_value";
            }

            return attr.LogicalName;
        }

        private static string FormatValue(Type type, string s, CultureInfo? cultureInfo = null)
        {
            var culture = cultureInfo ?? CultureInfo.CurrentCulture;

            if (type == typeof(string))
            {
                return "'" + HttpUtility.UrlEncode(s.Replace("'", "''")) + "'";
            }

            if (type == typeof(DateTime))
            {
                var date = DateTimeOffset.Parse(s, culture);
                return date.Equals(date.Date)
                    ? date.ToString("yyyy-MM-dd", culture)
                    : date.ToString("u").Replace(' ', 'T');
            }

            if (type == typeof(bool))
            {
                return s == "1" ? "true" : "false";
            }

            if (type == typeof(Guid))
            {
                return $"'{Guid.Parse(s)}'";
            }

            return HttpUtility.UrlEncode(Convert.ChangeType(s, type, culture).ToString())
                   ?? string.Empty;
        }

        private IEnumerable<OrderOData> ConvertOrder(string entityName, object[] items)
        {
            return items
                .OfType<FetchOrderType>()
                .Where(o => o.attribute != null)
                .Select(o => ConvertOrder(entityName, o));
        }

        private OrderOData ConvertOrder(string entityName, FetchOrderType orderitem)
        {
            if (!string.IsNullOrEmpty(orderitem.alias))
            {
                throw new NotSupportedException(
                    $"OData queries do not support ordering on link entities. Please remove the sort on {orderitem.alias}.{orderitem.attribute}");
            }

            var attrMetadata = _metadata
                                   .GetEntity(entityName)
                                   .Attributes
                                   .FirstOrDefault(a => a.LogicalName == orderitem.attribute)
                               ?? throw new NotSupportedException(
                                   $"No metadata for attribute {entityName}.{orderitem.attribute}");

            var odata = new OrderOData
            {
                PropertyName = GetPropertyName(attrMetadata),
                Descending = orderitem.descending
            };

            return odata;
        }

        private string LogicalToCollectionName(string entity)
        {
            var entityMeta = _metadata.GetEntity(entity);
            return entityMeta.EntitySetName ?? entityMeta.LogicalCollectionName;
        }

        private string LinkItemToNavigationProperty(string entityName, FetchLinkEntityType linkItem, out bool child,
            out FetchLinkEntityType? manyToManyNextLink)
        {
            manyToManyNextLink = null;
            var entity = _metadata.GetEntity(entityName);
            foreach (var relation in entity.OneToManyRelationships
                         .Where(r =>
                             r.ReferencedEntity == entityName &&
                             r.ReferencedAttribute == linkItem.to &&
                             r.ReferencingEntity == linkItem.name &&
                             r.ReferencingAttribute == linkItem.from))
            {
                child = true;
                return relation.ReferencedEntityNavigationPropertyName;
            }

            foreach (var relation in entity.ManyToOneRelationships
                         .Where(r =>
                             r.ReferencingEntity == entityName &&
                             r.ReferencingAttribute == linkItem.to &&
                             r.ReferencedEntity == linkItem.name &&
                             r.ReferencedAttribute == linkItem.from))
            {
                child = false;
                return relation.ReferencingEntityNavigationPropertyName;
            }

            foreach (var relation in entity.ManyToManyRelationships
                         .Where(r =>
                             r.Entity1LogicalName == entityName &&
                             r.Entity1IntersectAttribute == linkItem.from))
            {
                var linkItems = linkItem.Items.Where(i => i is FetchLinkEntityType).ToList();
                if (linkItems.Count > 1)
                {
                    throw new NotSupportedException("Invalid M:M-relation definition for OData");
                }

                if (linkItems.Count == 1)
                {
                    var nextLink = (FetchLinkEntityType)linkItems[0];
                    if (relation.Entity2LogicalName == nextLink.name &&
                        relation.Entity2IntersectAttribute == nextLink.to)
                    {
                        child = true;
                        manyToManyNextLink = nextLink;
                        return relation.Entity1NavigationPropertyName;
                    }
                }
            }

            foreach (var relation in entity.ManyToManyRelationships
                         .Where(r =>
                             r.Entity2LogicalName == entityName &&
                             r.Entity2IntersectAttribute == linkItem.from))
            {
                var linkItems = linkItem.Items.Where(i => i is FetchLinkEntityType).ToList();
                if (linkItems.Count > 1)
                {
                    throw new NotSupportedException("Invalid M:M-relation definition for OData");
                }

                if (linkItems.Count == 1)
                {
                    var nextLink = (FetchLinkEntityType)linkItems[0];
                    if (relation.Entity1LogicalName == nextLink.name &&
                        relation.Entity1IntersectAttribute == nextLink.from)
                    {
                        child = true;
                        manyToManyNextLink = nextLink;
                        return relation.Entity2NavigationPropertyName;
                    }
                }
            }

            throw new NotSupportedException(
                $"Cannot find metadata for relation {entityName}.{linkItem.to} => {linkItem.name}.{linkItem.from}");
        }
    }
}