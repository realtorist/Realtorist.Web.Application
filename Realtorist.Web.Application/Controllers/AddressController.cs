using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.GeoCoding.Abstractions;
using Realtorist.Models.Helpers;
using System;
using System.Threading.Tasks;

namespace Realtorist.Web.Application.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AddressController : ControllerBase
    {
        private readonly IGeoCoder _geoCoder;
        private readonly ILogger _logger;

        public AddressController(IGeoCoder geoCoder, ILogger<AddressController> logger)
        {
            _geoCoder = geoCoder ?? throw new ArgumentNullException(nameof(geoCoder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("")]
        public async Task<IActionResult> GetCoordinatesAsync([FromQuery] string address) 
        {
            if (address.IsNullOrEmpty()) return BadRequest();
            var coordinates = await _geoCoder.GetCoordinatesAsync(address);

            return new JsonResult(coordinates);
        }

        [HttpGet("Autocomplete")]
        public async Task<IActionResult> AutocompleteAsync([FromQuery] string query)
        {
            if (query.IsNullOrEmpty()) return new JsonResult(new object[0]);

            var autocomplete = await _geoCoder.GetAddressSuggestionsAsync(query);
            return new JsonResult(autocomplete);
        }
    }
}
