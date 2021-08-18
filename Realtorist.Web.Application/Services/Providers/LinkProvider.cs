using System;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Realtorist.Models.Helpers;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Helpers;

namespace Realtorist.Web.Application.Services.Providers
{
    /// <summary>
    /// Describes provider which helps to generate links
    /// </summary>
    public class LinkProvider : ILinkProvider
    {
        private readonly IUrlHelperFactory _urlHelperFactory;

        private readonly IActionContextAccessor _actionContextAccessor;

        public LinkProvider(IUrlHelperFactory urlHelperFactory, IActionContextAccessor actionContextAccessor)
        {
            _urlHelperFactory = urlHelperFactory ?? throw new ArgumentNullException(nameof(urlHelperFactory));
            _actionContextAccessor = actionContextAccessor ?? throw new ArgumentNullException(nameof(actionContextAccessor));
        }

        public string GetAbsoluteLink(string relativeLink)
        {
            if (relativeLink.IsNullOrEmpty()) throw new ArgumentException($"Parameter '{nameof(relativeLink)}' shouldn't be empty");
            
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);
            return urlHelper.ContentAbsolute(relativeLink);
        }
    }
}
