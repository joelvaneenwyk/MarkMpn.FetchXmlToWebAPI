using System.Linq;
using System.Threading.Tasks;
using DataverseEntities;
using JetBrains.Annotations;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Task = System.Threading.Tasks.Task;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[TestClass]
public class CreateContactTests : FakeXrmEasyTestsBase
{
    public class GenericResult
    {
        public bool Succeeded { get; private init; }
        public string? ErrorMessage { get; set; }

        public static GenericResult Succeed()
        {
            return new GenericResult
            {
                Succeeded = true,
                ErrorMessage = ""
            };
        }
    }

    [PublicAPI]
    public static class CreateContactFn
    {
        [PublicAPI]
        public static async Task<GenericResult> CreateContact(IOrganizationServiceAsync2 service, string firstName,
            string email)
        {
            await service.CreateAsync(new Contact
            {
                ["firstname"] = firstName,
                ["emailaddress1"] = email
            });

            return GenericResult.Succeed();
        }

        [PublicAPI]
        public static GenericResult CreateContactSync(IOrganizationService service, string firstName, string email)
        {
            service.Execute(new CreateRequest
            {
                Target =
                    new Entity("contact")
                    {
                        ["firstname"] = firstName,
                        ["emailaddress1"] = email
                    }
            });
            return GenericResult.Succeed();
        }
    }

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
