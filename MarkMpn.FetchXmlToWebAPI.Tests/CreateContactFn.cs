using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Newtonsoft.Json;
using DataverseEntities;

namespace MarkMpn.FetchXmlToWebAPI.Tests;

[PublicAPI]
public static class CreateContactFn
{
    [PublicAPI]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]
        HttpRequest req,
        ILogger log)
    {
        log.LogInformation("C# HTTP trigger function processed a request.");

        string? firstName = req.Query["firstname"];
        string? email = req.Query["email"];

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        dynamic? data = JsonConvert.DeserializeObject(requestBody);
        firstName ??= data?.firstName ?? "PersonName";
        email ??= data?.email ?? "person@place.com";

        var dataverseUrl = Environment.GetEnvironmentVariable("DataverseUrl") ?? "https://org.crm.dynamics.com";
        var clientId = Environment.GetEnvironmentVariable("ClientId") ?? "InvalidClientId";
        var clientSecret = Environment.GetEnvironmentVariable("ClientSecret") ?? "InvalidClientSecret";

        var client = new ServiceClient(new Uri(dataverseUrl), clientId, clientSecret, false);
        if (!client.IsReady) return new ObjectResult("Couldn't not connect to dataverse") { StatusCode = 401 };

        var result = await CreateContact(client, firstName, email);
        return new OkObjectResult(JsonConvert.SerializeObject(result));
    }

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