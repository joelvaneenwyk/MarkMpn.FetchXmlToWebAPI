using System.Xml.Serialization;

namespace MarkMpn.FetchXmlToWebAPI
{
    public partial class FetchType
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        [XmlAttribute]
        public string datasource;
#pragma warning restore CA1051 // Do not declare visible instance fields
    }
}
