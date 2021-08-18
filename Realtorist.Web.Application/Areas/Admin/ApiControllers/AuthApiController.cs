using System;
using System.Dynamic;
using System.Net.Mail;
using System.Threading.Tasks;using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Realtorist.DataAccess.Abstractions;
using Realtorist.Models.Helpers;
using Realtorist.Models.Settings;
using Realtorist.Services.Abstractions.Communication;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Web.Application.Attributes;
using Realtorist.Web.Models.Admin.Auth;

namespace Realtorist.Web.Application.Areas.Admin.ApiControllers
{
    /// <summary>
    /// Provides operations related to auth
    /// </summary>
    [Area("Admin")]
    [Route("api/admin/auth")]
    public class AuthApiController : Controller
    {
        private readonly ISettingsDataAccess _settingsDataAccess;
        private readonly ICachedSettingsProvider _cachedSettingsProvider;
        private readonly IEncryptionProvider _encryptionProvider;
        private readonly IEmailClient _emailClient;
        private readonly ILogger _logger;

        public AuthApiController(
            ISettingsDataAccess settingsDataAccess,
            IEncryptionProvider encryptionProvider, 
            ICachedSettingsProvider cachedSettingsProvider,
            IEmailClient emailClient,
            ILogger<AuthApiController> logger)
        {
            _settingsDataAccess = settingsDataAccess ?? throw new ArgumentNullException(nameof(settingsDataAccess));
            _encryptionProvider = encryptionProvider ?? throw new ArgumentNullException(nameof(encryptionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emailClient = emailClient ?? throw new ArgumentNullException(nameof(emailClient));
            _cachedSettingsProvider = cachedSettingsProvider;
        }

        /// <summary>
        /// Gets user's profile
        /// </summary>
        /// <returns>User profile</returns>
        [Route("login")]
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (!ModelState.IsValid) return Unauthorized();

            var profileSettings = await _settingsDataAccess.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);
            var password = await _settingsDataAccess.GetSettingAsync<PasswordSettings>(SettingTypes.Password);
            
            var encryptedPassword = _encryptionProvider.EncryptOneWay(model.Password);

            if (model.Email != profileSettings.Email || encryptedPassword != password.Password) return Unauthorized(
                new {
                    Errors = new[] {
                        "Wrong email/password"
                    }
                }
            );

            var tokenContainer = new TokenContainer
            {
                ExpirationTimeUtc = DateTime.UtcNow.AddDays(1),
                Guid = password.Guid,
                Email = profileSettings.Email
            };

            var token = _encryptionProvider.EncryptTwoWay(tokenContainer.ToJson());

            return Ok(new { Token = token });
        }

        /// <summary>
        /// Gets user's profile
        /// </summary>
        /// <returns>User profile</returns>
        [Route("request-password")]
        [HttpPost]
        public async Task<IActionResult> RequestPassword([FromBody] RequestPasswordModel model)
        {
            if (!ModelState.IsValid) return Unauthorized();

            var profileSettings = await _settingsDataAccess.GetSettingAsync<ProfileSettings>(SettingTypes.Profile);
            if (model.Email != profileSettings.Email) return BadRequest(
                new
                {
                    Errors = new[] {
                        "Wrong email"
                    }
                });

            var password = await _settingsDataAccess.GetSettingAsync<PasswordSettings>(SettingTypes.Password);

            password.ResetSettings = new PasswordSettings.PasswordReset
            {
                Guid = Guid.NewGuid(),
                ExpirationDateUtc = DateTime.UtcNow.AddHours(1)
            };

            await _settingsDataAccess.UpdateSettingsAsync(SettingTypes.Password, password.ToJson().FromJson<ExpandoObject>());
            _cachedSettingsProvider?.ResetSettingCache(SettingTypes.Password);

            var smtpSettings = await _settingsDataAccess.GetSettingAsync<SmtpSettings>(SettingTypes.Smtp);
            var websiteSettings = await _settingsDataAccess.GetSettingAsync<WebsiteSettings>(SettingTypes.Website);

            var mail = new MailMessage(
                new MailAddress(smtpSettings.Email ?? smtpSettings.Username, profileSettings.FirstName + " " + profileSettings.LastName),
                new MailAddress(profileSettings.Email))
            {
                Subject = $"Password reset request",
                Body = BuildEmail(profileSettings.FirstName,websiteSettings.WebsiteAddress, password.ResetSettings.Guid),
                IsBodyHtml = true
            };

            await _emailClient.SendEmailAsync(mail, smtpSettings);

            return Ok();
        }

        /// <summary>
        /// Gets user's profile
        /// </summary>
        /// <returns>User profile</returns>
        [Route("reset-password")]
        [HttpPost]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordModel model)
        {
            if (!ModelState.IsValid) return Unauthorized();

            var password = await _settingsDataAccess.GetSettingAsync<PasswordSettings>(SettingTypes.Password);

            if (password.ResetSettings == null 
                || password.ResetSettings.Guid != model.Id 
                || password.ResetSettings.ExpirationDateUtc < DateTime.UtcNow) 
            {
                return Unauthorized(
                new
                {
                    Errors = new[] {
                        "Your password reset link has expired"
                    }
                });
            }

            var encryptedPassword = _encryptionProvider.EncryptOneWay(model.Password);
            password.Password = encryptedPassword;
            password.Guid = Guid.NewGuid();
            password.ResetSettings = null;

            await _settingsDataAccess.UpdateSettingsAsync(SettingTypes.Password, password.ToJson().FromJson<ExpandoObject>());
            _cachedSettingsProvider?.ResetSettingCache(SettingTypes.Password);

            return Ok();
        }

        /// <summary>
        /// Gets user's profile
        /// </summary>
        /// <returns>User profile</returns>
        [Route("change-password")]
        [HttpPost]
        [RequireAuthorization]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            if (!ModelState.IsValid) return BadRequest();

            var password = await _settingsDataAccess.GetSettingAsync<PasswordSettings>(SettingTypes.Password);
            
            var encrypedOldPassword = _encryptionProvider.EncryptOneWay(model.OldPassword);

            if (password.Password != encrypedOldPassword)
            {
                return BadRequest("Wrong password");
            }

            var encryptedPassword = _encryptionProvider.EncryptOneWay(model.Password);
            password.Password = encryptedPassword;
            password.Guid = Guid.NewGuid();
            password.ResetSettings = null;

            await _settingsDataAccess.UpdateSettingsAsync(SettingTypes.Password, password.ToJson().FromJson<ExpandoObject>());
            _cachedSettingsProvider?.ResetSettingCache(SettingTypes.Password);

            return Ok();
        }

        private static string BuildEmail(string myName, string websiteAddress, Guid guid)
        {
            var link = $"https://{websiteAddress}/admin/auth/reset-password?id={guid}";
            var message = $@"
<p><h2>Trouble signing in?</h2></p>
<p></p>
<p>{myName}, resetting your password is easy.</p>

<p>Just follow the link for resetting your password:<a href=""{link}"">{link}</a></p>

<p>If you did not make this request then please ignore this email.</p>
";
            return message;
        }
    }
}