using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[TestClass]
public class CreateContactTests : FakeXrmEasyTestsBase
{
    [TestMethod]
    public async Task ShouldCreateContact()
    {
        var result = await CreateContactFn.CreateContact(Service, "Joe", "joe@satriani.com");
        Assert.IsTrue(result.Succeeded);

        var contacts = Context.CreateQuery("contact").ToList();
        Assert.AreEqual(1, contacts.Count);

        Assert.AreEqual("Joe", contacts[0]["firstname"]);
        Assert.AreEqual("joe@satriani.com", contacts[0]["emailaddress1"]);
    }
}