using AutoMapper;
using ExtCore.Infrastructure;
using ExtCore.Infrastructure.Actions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.WebEncoders;
using Realtorist.DataAccess.Implementations.Mongo;
using Realtorist.Extensions.Base;
using Realtorist.GeoCoding.Implementations.Here;
using Realtorist.Models.Settings;
using Realtorist.RetsClient.Abstractions;
using Realtorist.RetsClient.Implementations.Composite;
using Realtorist.RetsClient.Implementations.Crea;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Services.Abstractions.Upload;
using Realtorist.Services.Implementations.Default;
using Realtorist.Services.Implementations.Default.Upload;
using Realtorist.Web.Admin.Application.Middleware;
using Realtorist.Web.Application.Jobs;
using Realtorist.Web.Application.Jobs.Background;
using Realtorist.Web.Application.Middleware;
using Realtorist.Web.Application.Services.Providers;
using Realtorist.Web.Helpers;
using Realtorist.Web.Models.Abstractions.Jobs.Background;
using System;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Realtorist.Web.Application
{
    public class Startup : IConfigureServicesExtension, IConfigureApplicationExtension
    {
        public int Priority => 1;

        public void ConfigureServices(IServiceCollection services, IServiceProvider serviceProvider)
        {
            var env = serviceProvider.GetService<IWebHostEnvironment>();
            services.AddControllersWithViews().AddNewtonsoftJson();

            services.AddCors(options =>
                {
                    options.AddDefaultPolicy(
                        builder =>
                        {
                            builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                        });
                });

            services.AddTransient<IHttpContextAccessor, HttpContextAccessor>();
            services.AddTransient<IActionContextAccessor, ActionContextAccessor>();

            var mapperConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new Realtorist.Web.Models.AutoMapperProfile());
                mc.AddProfile(new CreaAutoMapperProfile());

                foreach (var extension in ExtensionManager.GetInstances<IConfigureAutoMapperProfileExtension>().OrderBy(x => x.Priority))
                {
                    mc.AddProfiles(extension.GetAutoMapperProfiles());
                }
            });

            mapperConfig.AssertConfigurationIsValid();
            services.AddSingleton<IMapper>(mapperConfig.CreateMapper());

            var configuration = serviceProvider.GetService<IConfiguration>();

            services.ConfigureDefaultServices();
            services.ConfigureMongoDataAccessServices(configuration);
            services.ConfigureCreaServices();
            services.ConfigureHereGeoCoding();
            services.AddLogging(loggingBuilder =>
            {
                var loggingSection = configuration.GetSection("Logging");
                loggingBuilder.AddFile(loggingSection);
            });

            services.AddScoped<ViewRenderService>();

            services.AddTransient<IUploadService, FileSystemUploadService>();
            services.AddTransient<ILinkProvider, LinkProvider>();

            services.AddSingleton<IUpdateFlow, CompositeUpdateFlow>();
            services.AddSingleton<IUpdateFlowFactory, DefaultUpdateFlowFactory>();

            var settingsProvider = services.BuildServiceProvider().GetService<ISettingsProvider>();

            services.Configure<WebEncoderOptions>(options =>
            {
                options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All);
            });

            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.MimeTypes = new[] { "application/javascript", "text/css", "text/javascript" };
            });

            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddHostedService<BackgroundQueueHostedService>();

            var settings = settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website).Result;
            if (settings is null)
            {
                return;
            }

            var profile = settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile).Result;
            services.RegisterJobs(settings);
        }

        public void ConfigureApplication(IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            app.UseResponseCompression();

            app.UseExceptionHandler("/oh-no");
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseRouting();
            app.UseCors();
            app.UseAuthorization();
        }
    }
}
