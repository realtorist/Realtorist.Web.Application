using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.WebEncoders;
using Realtorist.Extensions.Base;
using Realtorist.Extensions.Base.Manager;
using Realtorist.Models.Settings;
using Realtorist.RetsClient.Abstractions;
using Realtorist.RetsClient.Implementations.Composite;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Services.Abstractions.Upload;
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
        public int Priority => (int)ExtensionPriority.MainApplication;

        public void ConfigureServices(IServiceCollection services, IServiceProvider serviceProvider)
        {
            var env = serviceProvider.GetService<IWebHostEnvironment>();
            var extensionManager = serviceProvider.GetService<IExtensionManager>();
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

                foreach (var extension in extensionManager.GetInstances<IConfigureAutoMapperProfileExtension>().OrderBy(x => x.Priority))
                {
                    mc.AddProfiles(extension.GetAutoMapperProfiles());
                }
            });

            mapperConfig.AssertConfigurationIsValid();
            services.AddSingleton<IMapper>(mapperConfig.CreateMapper());

            var configuration = serviceProvider.GetService<IConfiguration>();

            services.AddScoped<ViewRenderService>();

            services.AddTransient<ILinkProvider, LinkProvider>();

            services.AddSingleton<IUpdateFlow, CompositeUpdateFlow>();

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
