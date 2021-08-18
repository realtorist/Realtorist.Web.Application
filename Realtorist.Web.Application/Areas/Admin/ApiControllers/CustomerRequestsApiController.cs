using System;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.CustomerRequests;
using Realtorist.Models.Helpers;
using Realtorist.Models.Pagination;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Communication;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models.Admin.CustomerRequests;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations for customer requests
    /// </summary>
    [Route("api/admin/requests")]
    [Area("Admin")]
    [RequireAuthorization]
    public class CustomerRequestsApiController : Controller
    {
        private readonly ICustomerRequestsDataAccess _requestsDataAccess;
        private readonly ISettingsProvider _settingsProvider;
        private readonly IEmailClient _emailClient;

        public CustomerRequestsApiController(
            ICustomerRequestsDataAccess requestsDataAccess,
            ISettingsProvider settingsProvider, 
            IEmailClient emailClient)
        {
            _requestsDataAccess = requestsDataAccess ?? throw new ArgumentNullException(nameof(requestsDataAccess));
            _emailClient = emailClient ?? throw new ArgumentNullException(nameof(emailClient));
            _settingsProvider = settingsProvider  ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        /// <summary>
        /// Gets customer requests
        /// </summary>
        /// <param name="request">Pagination request</param>
        /// <returns>Customer requests</returns>
        [Route("")]
        [HttpGet]
        public async Task<PaginationResult<CustomerRequestListModel>> GetRequestsAsync([FromQuery] PaginationRequest request)
        {
            var requests = await _requestsDataAccess.GetCustomerRequestsAsync<CustomerRequestListModel>(request);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            foreach (var req in requests.Results)
            {
                req.DateTimeUtc = websiteSettings.GetDateTimeInTimeZoneFromUtc(req.DateTimeUtc);
            }

            return requests;
        }

        /// <summary>
        /// Gets customer request by id
        /// </summary>
        /// <param name="requestId">Id of the request</param>
        /// <returns>Customer request</returns>
        [Route("{requestId}")]
        [HttpGet]
        public async Task<CustomerRequest> GetRequestAsync([FromRoute] Guid requestId)
        {
            var request = await _requestsDataAccess.GetCustomerRequestAsync(requestId);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);
            
            request.DateTimeUtc = websiteSettings.GetDateTimeInTimeZoneFromUtc(request.DateTimeUtc);
            foreach (var req in request.Replies)
            {
                req.DateTimeUtc = websiteSettings.GetDateTimeInTimeZoneFromUtc(req.DateTimeUtc);
            }

            return request;
        }

        /// <summary>
        /// Send reply to customer and saves it as reply
        /// </summary>
        /// <param name="requestId">Id of the customer request to reply to</param>
        /// <param name="reply">Reply</param>
        /// <returns>Status code</returns>
        [Route("{requestId}/reply")]
        [HttpPut]
        public async Task<IActionResult> ReplyAsync([FromRoute] Guid requestId, [FromBody] Reply reply)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState.GetModelStateValidationErrors());
            }

            var replyModel = new CustomerRequestReply
            {
                DateTimeUtc = DateTime.UtcNow,
                FromCustomer = false,
                Read = true,
                Message = reply.Message
            };

            var request = await _requestsDataAccess.GetCustomerRequestAsync(requestId);

            var smtpSettings = await _settingsProvider.GetSettingAsync<SmtpSettings>(SettingTypes.Smtp);
            var profile = await _settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);
            var websiteSettings = await _settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);

            var mail = new MailMessage(
                new MailAddress(smtpSettings.Email ?? smtpSettings.Username, profile.FirstName + " " + profile.LastName),
                new MailAddress(request.Request.Email))
            {
                Subject = $"RE: {request.Request.Type.GetLookupDisplayTextFromObject()}",
                Body = BuildEmailReply(profile.FullName, request, replyModel, websiteSettings),
                IsBodyHtml = true
            };

            await _emailClient.SendEmailAsync(mail, smtpSettings);

            await _requestsDataAccess.ReplyAsync(requestId, replyModel);

            return NoContent();
        }

        /// <summary>
        /// Deletes customer request
        /// </summary>
        /// <param name="requestId">Id of the request</param>
        /// <returns>No content</returns>
        [Route("{requestId}")]
        [HttpDelete]
        public async Task<NoContentResult> DeleteRequestAsync([FromRoute] Guid requestId)
        {
            await _requestsDataAccess.DeleteCustomerRequestAsync(requestId);
            return NoContent();
        }

        /// <summary>
        /// Marks customer request as read on not read
        /// </summary>
        /// <param name="requestId">Id of the request</param>
        /// <param name="read">Should be request marked as read or not-read</param>
        /// <returns>No content</returns>
        [Route("{requestId}/read")]
        [HttpPost]
        public async Task<NoContentResult> ToggleRequestReadStatusAsync([FromRoute] Guid requestId, [FromQuery] bool read = true)
        {
            await _requestsDataAccess.MarkRequestAsReadAsync(requestId, read);

            return NoContent();
        }

        /// <summary>
        /// Marks all customer requests as read
        /// </summary>
        /// <returns>No Content</returns>
        [Route("read")]
        [HttpPost]
        public async Task<NoContentResult> MarkAllAsReadAsync()
        {
            await _requestsDataAccess.MarkAllRequestsAsReadAsync();

            return NoContent();
        }

        private static string BuildEmailReply(string myName, CustomerRequest request, CustomerRequestReply reply, WebsiteSettings settings)
        {
            var message = reply.Message;
            foreach (var r in request.Replies.OrderByDescending(x => x.DateTimeUtc))
            {
                message += $@"

<hr>

<h3>On {settings.GetDateTimeInTimeZoneFromUtc(r.DateTimeUtc).ToString("f")} {(r.FromCustomer ? request.Request.Name : myName)} wrote:</hr>

<p>{r.Message}</p>";
            }

            message += $@"

<hr>

<h3>On {settings.GetDateTimeInTimeZoneFromUtc(request.DateTimeUtc).ToString("f")} {request.Request.Name} wrote:</h3>

<p>{request.Request.Message}</p>";

            return message;

        }
    }
}