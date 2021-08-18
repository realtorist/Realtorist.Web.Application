using System;
using System.Dynamic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Helpers;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to settings
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/settings")]
    [RequireAuthorization]
    public class SettingsApiController : Controller
    {
        private readonly ISettingsDataAccess _settingsDataAccess;
        private readonly ICachedSettingsProvider _cachedSettingsProvider;

        public SettingsApiController(ISettingsDataAccess settingsDataAccess,ICachedSettingsProvider cachedSettingsProvider = null)
        {
            _settingsDataAccess = settingsDataAccess ?? throw new ArgumentNullException(nameof(settingsDataAccess));
            _cachedSettingsProvider = cachedSettingsProvider;
        }

        /// <summary>
        /// Gets setting
        /// </summary>
        /// <param name="type">Setting type</param>
        /// <returns>Setting</returns>
        [HttpGet]
        [Route("{type}")]
        public async Task<JsonResult> GetSetting([FromRoute] string type)
        {
            var setting = await _settingsDataAccess.GetSettingAsync(type);

            return Json(setting, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
        }

        /// <summary>
        /// Updates setting
        /// </summary>
        /// <param name="type">Setting type</param>
        /// <param name="value">New setting value</param>
        /// <returns>OK</returns>
        [HttpPost]
        [Route("{type}")]
        public async Task<IActionResult> UpdateSetting([FromRoute] string type, [FromBody] JToken value)
        {
            if (value is null) return BadRequest();

            var convertType = typeof(ExpandoObject);
            if (value.Type == JTokenType.Array)
            {
                convertType = typeof(ExpandoObject[]);
            }
            
            if (SettingTypes.SettingTypeMap.ContainsKey(type))
            {
                ModelState.Clear();
                if (!TryValidateModel(value.ToObject(SettingTypes.SettingTypeMap[type])))
                {
                    return BadRequest(ModelState.GetModelStateValidationErrors());
                }
            }

            var data = value.ToObject(convertType);

            await _settingsDataAccess.UpdateSettingsAsync(type, data);

            _cachedSettingsProvider?.ResetSettingCache(type);

            return NoContent();
        }
    }
}