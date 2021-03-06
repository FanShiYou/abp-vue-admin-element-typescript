using DotNetCore.CAP;
using LINGYUN.Abp.EventBus.CAP;
using LINGYUN.Abp.ExceptionHandling;
using LINGYUN.Abp.ExceptionHandling.Emailing;
using LINGYUN.Abp.MultiTenancy.DbFinder;
using LINGYUN.Abp.Sms.Aliyun;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Volo.Abp;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Auditing;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.MySQL;
using Volo.Abp.Identity.Localization;
using Volo.Abp.Json;
using Volo.Abp.Json.SystemTextJson;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.Security.Claims;
using Volo.Abp.Security.Encryption;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.VirtualFileSystem;

namespace LINGYUN.Abp.IdentityServer4
{
    [DependsOn(
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(LINGYUN.Abp.Account.AbpAccountApplicationModule),
        typeof(LINGYUN.Abp.Account.AbpAccountHttpApiModule),
        typeof(LINGYUN.Abp.Identity.AbpIdentityApplicationModule),
        typeof(LINGYUN.Abp.Identity.AbpIdentityHttpApiModule),
        typeof(LINGYUN.Abp.IdentityServer.AbpIdentityServerApplicationModule),
        typeof(LINGYUN.Abp.IdentityServer.AbpIdentityServerHttpApiModule),
        typeof(LINGYUN.Abp.Identity.EntityFrameworkCore.AbpIdentityEntityFrameworkCoreModule),
        typeof(LINGYUN.Abp.IdentityServer.EntityFrameworkCore.AbpIdentityServerEntityFrameworkCoreModule),
        typeof(AbpEntityFrameworkCoreMySQLModule),
        typeof(AbpAuditLoggingEntityFrameworkCoreModule),
        typeof(AbpTenantManagementEntityFrameworkCoreModule),
        typeof(AbpSettingManagementEntityFrameworkCoreModule),
        typeof(AbpPermissionManagementEntityFrameworkCoreModule),
        typeof(AbpAspNetCoreAuthenticationJwtBearerModule),
        typeof(AbpEmailingExceptionHandlingModule),
        typeof(AbpCAPEventBusModule),
        typeof(AbpAliyunSmsModule),
        typeof(AbpDbFinderMultiTenancyModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAutofacModule)
        )]
    public class AbpIdentityServerAdminHttpApiHostModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            var configuration = context.Services.GetConfiguration();

            PreConfigure<CapOptions>(options =>
            {
                options
                .UseMySql(configuration.GetConnectionString("Default"))
                .UseRabbitMQ(rabbitMQOptions =>
                {
                    configuration.GetSection("CAP:RabbitMQ").Bind(rabbitMQOptions);
                })
                .UseDashboard();
            });

            PreConfigure<IdentityBuilder>(builder =>
            {
                builder.AddDefaultTokenProviders();
            });
        }

        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            var configuration = hostingEnvironment.BuildConfiguration();

            // ??????????????????
            Configure<ForwardedHeadersOptions>(options =>
            {
                configuration.GetSection("App:Forwarded").Bind(options);
                // ??????????????????,???????????????????????????????????????????????????????????????
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            // ??????Ef
            Configure<AbpDbContextOptions>(options =>
            {
                options.UseMySQL();
                //if (hostingEnvironment.IsDevelopment())
                //{
                //    options.PreConfigure(ctx =>
                //    {
                //        ctx.DbContextOptions.EnableDetailedErrors();
                //        ctx.DbContextOptions.EnableSensitiveDataLogging();
                //    });
                //}
            });

            // ???????????????????????????????????????
            Configure<AbpJsonOptions>(options =>
            {
                options.UseHybridSerializer = true;
            });
            // ??????????????????????????????
            Configure<AbpSystemTextJsonSerializerOptions>(options =>
            {
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
            });

            // ?????????
            Configure<AbpStringEncryptionOptions>(options =>
            {
                var encryptionConfiguration = configuration.GetSection("Encryption");
                if (encryptionConfiguration.Exists())
                {
                    options.DefaultPassPhrase = encryptionConfiguration["PassPhrase"] ?? options.DefaultPassPhrase;
                    options.DefaultSalt = encryptionConfiguration.GetSection("Salt").Exists()
                        ? Encoding.ASCII.GetBytes(encryptionConfiguration["Salt"])
                        : options.DefaultSalt;
                    options.InitVectorBytes = encryptionConfiguration.GetSection("InitVector").Exists()
                        ? Encoding.ASCII.GetBytes(encryptionConfiguration["InitVector"])
                        : options.InitVectorBytes;
                }
            });

            Configure<PermissionManagementOptions>(options =>
            {
                // Rename IdentityServer.Client.ManagePermissions
                // See https://github.com/abpframework/abp/blob/dev/modules/identityserver/src/Volo.Abp.PermissionManagement.Domain.IdentityServer/Volo/Abp/PermissionManagement/IdentityServer/AbpPermissionManagementDomainIdentityServerModule.cs
                options.ProviderPolicies[ClientPermissionValueProvider.ProviderName] =
                    LINGYUN.Abp.IdentityServer.AbpIdentityServerPermissions.Clients.ManagePermissions;
            });

            // ??????????????????????????????
            Configure<AbpExceptionHandlingOptions>(options =>
            {
                //  ?????????????????????????????????
                options.Handlers.Add<Volo.Abp.Data.AbpDbConcurrencyException>();
                options.Handlers.Add<AbpInitializationException>();
                options.Handlers.Add<ObjectDisposedException>();
                options.Handlers.Add<StackOverflowException>();
                options.Handlers.Add<OutOfMemoryException>();
                options.Handlers.Add<System.Data.Common.DbException>();
                options.Handlers.Add<Microsoft.EntityFrameworkCore.DbUpdateException>();
                options.Handlers.Add<System.Data.DBConcurrencyException>();
            });
            // ????????????????????????????????????????????????
            Configure<AbpEmailExceptionHandlingOptions>(options =>
            {
                // ????????????????????????
                options.SendStackTrace = true;
            });

            Configure<AbpAuditingOptions>(options =>
            {
                options.ApplicationName = "Identity-Server-Admin";
                // ??????????????????????????????
                var entitiesChangedConfig = configuration.GetSection("App:TrackingEntitiesChanged");
                if (entitiesChangedConfig.Exists() && entitiesChangedConfig.Get<bool>())
                {
                    options
                    .EntityHistorySelectors
                    .AddAllEntities();
                }
            });

            Configure<AbpDistributedCacheOptions>(options =>
            {
                // ??????????????????,?????????????????????????????????????????????????????????
                options.KeyPrefix = "LINGYUN.Abp.Application";
                // ????????????30???
                options.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromDays(30);
                // ????????????60???
                options.GlobalCacheEntryOptions.AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(60);
            });

            Configure<AbpDistributedEntityEventOptions>(options =>
            {
                options.AutoEventSelectors.AddNamespace("Volo.Abp.Identity");
                options.AutoEventSelectors.AddNamespace("Volo.Abp.IdentityServer");
            });

            Configure<RedisCacheOptions>(options =>
            {
                var redisConfig = ConfigurationOptions.Parse(options.Configuration);
                options.ConfigurationOptions = redisConfig;
                options.InstanceName = configuration["Redis:InstanceName"];
            });

            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.AddEmbedded<AbpIdentityServerAdminHttpApiHostModule>("LINGYUN.Abp.IdentityServer4");
            });

            // ?????????
            Configure<AbpMultiTenancyOptions>(options =>
            {
                options.IsEnabled = true;
            });

            var tenantResolveCfg = configuration.GetSection("App:Domains");
            if (tenantResolveCfg.Exists())
            {
                Configure<AbpTenantResolveOptions>(options =>
                {
                    var domains = tenantResolveCfg.Get<string[]>();
                    foreach (var domain in domains)
                    {
                        options.AddDomainTenantResolver(domain);
                    }
                });
            }

            // Swagger
            context.Services.AddSwaggerGen(
                options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo { Title = "IdentityServer4 API", Version = "v1" });
                    options.DocInclusionPredicate((docName, description) => true);
                    options.CustomSchemaIds(type => type.FullName);
                    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Scheme = "bearer",
                        Type = SecuritySchemeType.Http,
                        BearerFormat = "JWT"
                    });
                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                            },
                            new string[] { }
                        }
                    });
                });

            // ???????????????????????????
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "????????????"));

                options.Resources
                       .Get<IdentityResource>()
                       .AddVirtualJson("/LINGYUN/Abp/IdentityServer4/Localization");
            });

            Configure<AbpClaimsMapOptions>(options =>
            {
                options.Maps.TryAdd("name", () => AbpClaimTypes.UserName);
            });

            context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["AuthServer:Authority"];
                    options.RequireHttpsMetadata = false;
                    options.Audience = configuration["AuthServer:ApiName"];
                });

            if (!hostingEnvironment.IsDevelopment())
            {
                var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                context.Services
                    .AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, "BackendAdmin-Protection-Keys");
            }
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();

            app.UseForwardedHeaders();
            // http?????????
            app.UseCorrelationId();
            // ??????????????????
            app.UseVirtualFiles();
            // ?????????
            app.UseAbpRequestLocalization();
            //??????
            app.UseRouting();
            // ??????
            app.UseAuthentication();
            app.UseAbpClaimsMap();
            // jwt
            app.UseJwtTokenMiddleware();
            // ?????????
            app.UseMultiTenancy();
            // Swagger
            app.UseSwagger();
            // Swagger???????????????
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support IdentityServer4 API");
            });
            // ????????????
            app.UseAuditing();
            // ??????
            app.UseConfiguredEndpoints();
        }
    }
}