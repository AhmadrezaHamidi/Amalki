using System.Data;
using AhmadBase.Config.Containers;
using AhmadBase.External.Service;
using AhmadBase.External.Service.Sms;
using AhmadBase.IOC;
using Amlak.Application.Contract.Services;
using Amlak.Application.Validators;
using Amlak.Config.Extensions.Swagger;
using Amlak.Config.Middlewares;
using Amlak.Domain.Identity.Repositories;
using Amlak.Domain.Lead.Repositories;
using Amlak.Domain.Listing.Repositories;
using Amlak.Domain.Publishing.Repositories;
using Amlak.Persistence.EF.Repositories;
using Amlak.Persistence.EF.Services;
using Amlak.Read.Dapper;
using Amlak.Read.Dapper.Lead;
using Amlak.Read.Dapper.Listing;
using Amlak.Read.Dapper.Publishing;
using Autofac;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using AhmadSmsOptions = AhmadBase.External.Service.SmsOptions;
using SmsContract     = AhmadBase.External.Service.ISmsService;

namespace Amlak.Config;

public class AutofacModule : Autofac.Module
{
    private readonly IConfiguration _configuration;
    private readonly bool           _useFakeServices;

    public AutofacModule(IConfiguration configuration)
    {
        _configuration   = configuration;
        _useFakeServices = bool.TryParse(configuration["UseFakeServices"], out var b) && b;
    }

    protected override void Load(ContainerBuilder builder)
    {
        // ── ۱. ثبت خودکار فریم‌ورک ──────────────────────────────────────────
        Container.Setup(builder, _configuration)
            .RegisterDatabaseModels()
            .RegisterMainServices()
            .RegisterHandlers()
            .RegisterAuxiliaryServices();

        // ── ۲. Repository های سمت نوشتن (EF) ────────────────────────────────
        builder.RegisterType<UserRepository>()         .As<IUserRepository>()         .InstancePerLifetimeScope();
        builder.RegisterType<OtpRepository>()          .As<IOtpRepository>()          .InstancePerLifetimeScope();
        builder.RegisterType<RefreshTokenRepository>() .As<IRefreshTokenRepository>() .InstancePerLifetimeScope();
        builder.RegisterType<PropertyRepository>()     .As<IPropertyRepository>()     .InstancePerLifetimeScope();
        builder.RegisterType<ChannelPostRepository>()  .As<IChannelPostRepository>()  .InstancePerLifetimeScope();
        builder.RegisterType<LeadRepository>()         .As<ILeadRepository>()         .InstancePerLifetimeScope();

        // ── ۳. اتصال Dapper برای سمت خواندن ─────────────────────────────────
        var connStr = _configuration.GetConnectionString("QueryConnection") ?? string.Empty;
        builder.Register<IDbConnection>(_ => new SqlConnection(connStr))
               .As<IDbConnection>()
               .InstancePerLifetimeScope();

        builder.RegisterType<PhotoUrls>()              .SingleInstance();
        builder.RegisterType<PropertyReadRepository>() .InstancePerLifetimeScope();
        builder.RegisterType<ChannelReadRepository>()  .InstancePerLifetimeScope();
        builder.RegisterType<LeadReadRepository>()     .InstancePerLifetimeScope();

        // ── ۴. پیامک (با Circuit Breaker) ───────────────────────────────────
        builder.Register(c =>
        {
            var opts = c.Resolve<IConfiguration>().GetSection(AhmadSmsOptions.Section).Get<AhmadSmsOptions>() ?? new AhmadSmsOptions();
            return Microsoft.Extensions.Options.Options.Create(opts);
        }).As<IOptions<AhmadSmsOptions>>().SingleInstance();

        if (_useFakeServices)
            builder.RegisterType<FakeSmsSender>().As<SmsContract>().InstancePerLifetimeScope();
        else
            builder.RegisterType<SmsSender>().As<SmsContract>().InstancePerLifetimeScope();

        // ── ۵. سرویس‌های سامانه ─────────────────────────────────────────────
        builder.RegisterType<JwtService>()        .As<IJwtService>()     .InstancePerLifetimeScope();
        builder.RegisterType<LocalPhotoStorage>() .As<IPhotoStorage>()   .SingleInstance();
        builder.RegisterType<SmsAdminNotifier>()  .As<IAdminNotifier>()  .InstancePerLifetimeScope();

        // HttpClient اختصاصی کانال‌ها: بدون IHttpClientFactory، پس هیچ handler لاگی URL حاوی توکن را ثبت نمی‌کند
        builder.Register(c => new BotChannelGateway(
                    c.Resolve<IConfiguration>(),
                    new HttpClient { Timeout = TimeSpan.FromSeconds(30) }))
               .As<IChannelGateway>()
               .SingleInstance();

        // ── ۶. Middleware ها ────────────────────────────────────────────────
        builder.RegisterType<CorrelationIdStartupFilter>().As<IStartupFilter>().SingleInstance();

        // ── ۷. FluentValidation ─────────────────────────────────────────────
        builder.RegisterAssemblyTypes(typeof(ValidationBehavior<,>).Assembly)
               .AsClosedTypesOf(typeof(IValidator<>))
               .InstancePerLifetimeScope();

        // ترتیب ثبت = ترتیب اجرا؛ اعتبارسنجی بیرونی‌ترین لایه است تا زودتر fail کند
        builder.RegisterGeneric(typeof(ValidationBehavior<,>))
               .As(typeof(IPipelineBehavior<,>))
               .InstancePerLifetimeScope();

        // ── ۷b. انتشار رویدادهای دامین پس از هر Command ─────────────────────
        builder.RegisterGeneric(typeof(DomainEventDispatchBehavior<,>))
               .As(typeof(IPipelineBehavior<,>))
               .InstancePerLifetimeScope();

        // ── ۸. Swagger ──────────────────────────────────────────────────────
        builder.Register<IConfigureOptions<SwaggerGenOptions>>(_ =>
            new ConfigureOptions<SwaggerGenOptions>(opts =>
            {
                opts.SchemaGeneratorOptions.SchemaIdSelector = t => t.FullName ?? t.Name;
                opts.OperationFilterDescriptors.Add(new FilterDescriptor
                {
                    Type      = typeof(ErrorResponseOperationFilter),
                    Arguments = Array.Empty<object>()
                });
            }))
            .As<IConfigureOptions<SwaggerGenOptions>>()
            .SingleInstance();
    }
}
