using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Dto;
using Realtorist.Models.Helpers;
using Realtorist.Services.Abstractions.Communication;
using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Services.Abstractions.Events;
using Realtorist.Models.Events;

namespace Realtorist.Web.Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : ControllerBase
    {
        private readonly ICustomerRequestsDataAccess _customerRequestsDataAccess;
        private readonly IEventLogger _eventLogger;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IEmailClient _emailClient;
        private readonly ILogger _logger;

        public RequestController(
            ICustomerRequestsDataAccess customerRequestsDataAccess,
            ISettingsProvider settingsProvider,
            IEmailClient emailClient,
            IEventLogger eventLogger,
            ILogger<RequestController> logger)
        {
            _customerRequestsDataAccess = customerRequestsDataAccess ?? throw new ArgumentNullException(nameof(customerRequestsDataAccess));
            _emailClient = emailClient ?? throw new ArgumentNullException(nameof(emailClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
        }

        [HttpPost("")]
        public async Task<IActionResult> PostRequestAsync([FromBody] RequestInformationModel request)
        {
            if (request is null) return BadRequest("Model is empty");

            _logger.LogInformation($"Received customer request: {request.ToJson()}");
            if (!TryValidateModel(request)) return BadRequest("Model is not valid");

            await _customerRequestsDataAccess.AddCustomerRequestAsync(request);

            var settings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            var profile = await _settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);
            var smtpSettings = await _settingsProvider.GetSettingAsync<SmtpSettings>(SettingTypes.Smtp);

            var mail = new MailMessage(new MailAddress(smtpSettings.Email ?? smtpSettings.Username, request.Name), new MailAddress(profile.Email))
            {
                Subject = "New request from the website",

                Body = $@"Hello {profile.FirstName}!
You have a new request from the website:
Name: {request.Name}
Phone:{request.Phone}
Email:{request.Email}
Request type:{request.Type.GetLookupDisplayText()}
I am: {request.Iam.GetLookupDisplayText()}
Listing ID: {request.ListingId}
Listing Address: {request.Address}
Message:
    {request.Message}"
            };

            await _emailClient.SendEmailAsync(mail, smtpSettings);

            var eventType = EventTypes.CustomerRequest;
            var title = $"New request from '{request.Name}'";
            var message = $"{request.Name} made a request of type '{request.Type.GetLookupDisplayText()}'";

            if (request.Type == Realtorist.Models.Enums.RequestType.CreateAccount)
            {
                eventType = EventTypes.AccountCreated;
                title = "New account created";
                message = $"{request.Name} created a new account";
            }

            await _eventLogger.CreateEventAsync(EventLevel.Info, eventType, title, message);

            return Ok();
        }
    }
}
