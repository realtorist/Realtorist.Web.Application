using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Realtorist.Models.Events;
using Realtorist.Models.Exceptions;
using Realtorist.Services.Abstractions.Events;
using Realtorist.Web.Helpers;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly LinkGenerator _linkGenerator;
        private readonly IEventLogger _eventLogger;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, LinkGenerator linkGenerator, ILogger<ExceptionHandlingMiddleware> logger, IEventLogger eventLogger)
        {
            _next = next;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventLogger = eventLogger ?? throw new ArgumentNullException(nameof(eventLogger));
            _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
        }

        public async Task Invoke(HttpContext httpContext)
        {
            var start = Stopwatch.GetTimestamp();
            Exception exception = null;

            var request = $"{httpContext.Request.Path}{httpContext.Request.QueryString.ToUriComponent()}";

            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                //if (httpContext.Response.HasStarted) throw;

                int statusCode = 0;
                string logMessage = "";

                exception = ex;

                switch (ex)
                {
                    case NotFoundException nfEx:
                        statusCode = StatusCodes.Status404NotFound;
                        logMessage = $"Not Found exception :: {nfEx.Message}. StackTrace: {nfEx.StackTrace}";
                        break;
                    default:
                        statusCode = StatusCodes.Status500InternalServerError;
                        logMessage = $"Unhandled exception :: {ex.Message}. StackTrace: {ex.StackTrace}";
                        break;
                }

                _logger.LogError(ex, logMessage);

                var userAgent = httpContext.Request.Headers.ContainsKey("User-Agent") ? httpContext.Request.Headers["User-Agent"].ToString() : "N/A";

                if (statusCode == StatusCodes.Status404NotFound)
                {
                    await _eventLogger.CreateEventAsync(EventLevel.Warning, EventTypes.UrlNotFound, "URL not found", $"Requested page wasn't found.\nRequest: {request}\nStatus code: {statusCode}\nUser-Agent: {userAgent}");
                }
                else 
                {
                    await _eventLogger.CreateEventAsync(EventTypes.Generic, "An error has occured", $"An exception occured on the website.\nRequest: {request}\nStatus code: {statusCode}\nUser-Agent: {userAgent}", ex);
                }

                httpContext.Response.StatusCode = statusCode;

                if (httpContext.Request.Path.ToString().StartsWith("/api") && !httpContext.Response.HasStarted)
                {
                    var messageResponse = new
                    {
                        statusCode,
                        message = logMessage
                    };

                    var jsonMessage = JsonConvert.SerializeObject(messageResponse);

                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(jsonMessage, Encoding.UTF8);

                    return;

                }

                if (statusCode == StatusCodes.Status404NotFound && httpContext.Request.Path.ToString().StartsWith("/property/") && !httpContext.Response.HasStarted)
                {
                    var viewResult = new ViewResult {
                        ViewName = "ListingNotFound"
                    };

                    await httpContext.WriteResultAsync(viewResult);

                    return;
                }

                throw;
            }
            finally
            {
                var elapsedMs = (Stopwatch.GetTimestamp() - start) * 1000 / (double)Stopwatch.Frequency;

                var logEntry = string.Format("{0} {1} {2} Completed in {3}ms {4}",
                    httpContext.Request.Protocol,
                    httpContext.Request.Method,
                    httpContext.Request.Path.ToString() + httpContext.Request.QueryString,
                    elapsedMs.ToString(),
                    httpContext.Response.StatusCode);

                _logger.Log(httpContext.Response.StatusCode < 400 ? LogLevel.Information : LogLevel.Error,
                    exception,
                    logEntry);
            }
        }
    }
}
