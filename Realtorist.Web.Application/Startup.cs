using AutoMapper;
using ExtCore.Infrastructure.Actions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.WebEncoders;
using Realtorist.DataAccess.Implementations.Mongo;
using Realtorist.GeoCoding.Implementations.Here;
using Realtorist.Models.Settings;
using Realtorist.RetsClient.Abstractions;
using Realtorist.RetsClient.Implementations.Composite;
using Realtorist.RetsClient.Implementations.Crea;
using Realtorist.Services.Abstractions.Providers;
using Realtorist.Services.Abstractions.Upload;
using Realtorist.Services.Implementations.Default;
using Realtorist.Services.Implementations.Default.Upload;
using Realtorist.Web.Application.Jobs;
using Realtorist.Web.Application.Jobs.Background;
using Realtorist.Web.Application.Middleware;
using Realtorist.Web.Application.Services.Providers;
using Realtorist.Web.Helpers;
using System;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace Realtorist.Web.Application
{
    public class Startup : IConfigureServicesAction, IConfigureAction
    {
        public int Priority => 1;

        void IConfigureServicesAction.Execute(IServiceCollection services, IServiceProvider serviceProvider)
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
            var settings = settingsProvider.GetSettingAsync<WebsiteSettings>(SettingTypes.Website).Result;
            var profile = settingsProvider.GetSettingAsync<ProfileSettings>(SettingTypes.Profile).Result;

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

            services.RegisterJobs(settings);

            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddHostedService<BackgroundQueueHostedService>();
        }

        void IConfigureAction.Execute(IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            var env = serviceProvider.GetService<IWebHostEnvironment>();
            if (env.IsDevelopment())
            {
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseResponseCompression();

            app.UseExceptionHandler("/oh-no");
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseCors();
            app.UseAuthorization();

            app.UseMiddleware<AuthMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "admin",
                    pattern: "{area:exists}/{controller=AdminHome}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            if (env.IsDevelopment())
            {
                app.UseStaticFiles();
            }
            else
            {
                app.UseStaticFiles(new StaticFileOptions()
                {
                    HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
                    OnPrepareResponse = (context) =>
                    {
                        var headers = context.Context.Response.GetTypedHeaders();
                        headers.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                        {
                            Public = true,
                            MaxAge = TimeSpan.FromDays(30)
                        };
                    }
                });
            }
        }
    }
}
