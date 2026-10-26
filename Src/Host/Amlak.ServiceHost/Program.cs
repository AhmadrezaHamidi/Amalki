using AhmadBase.Config.Applications;

var builder = WebApplication.CreateBuilder(args);

await new WebBuilder(builder)
    .PrintApplicationName()
    .ConfigureEnvironmentVariable()
    .ConfigureSerilog()
    .ConfigureAutofac()
    .ConfigureRedisCache()
    .ConfigureAuthentication()
    .ConfigureController()
    .ConfigureSwagger()
    .ConfigureEndPoints()
    .ConfigureApiVersion()
    .ConfigureCors()
    .RunAsync();
