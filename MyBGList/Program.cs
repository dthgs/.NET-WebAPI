using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyBGList.Attributes;
using MyBGList.Constants;
using MyBGList.Models;
using MyBGList.Swagger;
using Serilog;
using Serilog.Sinks.MSSqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders() // Remove all pre-configured logging providers, then add what to keep after
    .AddSimpleConsole()
    .AddDebug()
    .AddApplicationInsights(builder.Configuration["Azure:ApplicationInsights:InstrumentationKey"]);


builder.Host.UseSerilog((ctx, lc) => {
    lc.WriteTo.MSSqlServer(
      connectionString: ctx.Configuration.GetConnectionString("DefaultConnection"),
      sinkOptions: new MSSqlServerSinkOptions
      {
          TableName = "LogEvents",
          AutoCreateSqlTable = true
      },
      columnOptions: new ColumnOptions()
      {
          AdditionalColumns = new SqlColumn[]
            {
                new SqlColumn()
                {
                    ColumnName = "SourceContext",
                    PropertyName = "SourceContext",
                    DataType = System.Data.SqlDbType.NVarChar
                }
            }
      });
    lc.Enrich.WithMachineName();
    lc.Enrich.WithThreadId();
    lc.WriteTo.File("Logs/log.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
            "{Timestamp:HH:mm:ss} [{Level:u3}] " +
            "[{MachineName} #{ThreadId}] " +
            "{Message:lj}{NewLine}{Exception}");
},
    writeToProviders: true); // Make Serilog pass the log events not only to its sinks, but also to other logging providers

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(cfg =>
    {
        cfg.WithOrigins(builder.Configuration["AllowedOrigins"]);
        cfg.AllowAnyHeader();
        cfg.AllowAnyMethod();
    });
    options.AddPolicy(
        name: "AnyOrigin",
        cfg =>
        {
            cfg.AllowAnyOrigin();
            cfg.AllowAnyHeader();
            cfg.AllowAnyMethod();
        }
    );
    options.AddPolicy(
        name: "AnyOrigin_GetOnly",
        cfg =>
        {
            cfg.AllowAnyOrigin();
            cfg.AllowAnyHeader();
            cfg.WithMethods("GET");
        }
    );
});

builder.Services.AddControllers(options =>
{
    // Customize Model Binding errors
    options.ModelBindingMessageProvider.SetValueIsInvalidAccessor(
        (x) => $"The value '{x}' is invalid.");
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(
        (x) => $"The field {x} must be a number.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor(
        (x, y) => $"The value '{x}' is not valid for {y}.");
    options.ModelBindingMessageProvider.SetMissingKeyOrValueAccessor(
        () => $"A value is required.");

    options.CacheProfiles.Add("NoCache", new CacheProfile() { NoStore = true });
    options.CacheProfiles.Add("Any-60",
        new CacheProfile()
        {
            Location = ResponseCacheLocation.Any,
            Duration = 60
        });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.ResolveConflictingActions(apiDesc => apiDesc.First()); //  Deal with routing conflict situations (not encouraged, keeping it for now)
    opts.ParameterFilter<SortColumnFilter>(); // Add sortColumn params to swagger/v1/swagger.json
    opts.ParameterFilter<SortOrderFilter>();
}
);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApiUser, IdentityRole>(options => // Add the Identity service
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 12;
}).AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthentication(options => { // Add the Authentication service
    options.DefaultAuthenticateScheme =
    options.DefaultChallengeScheme =
    options.DefaultForbidScheme =
    options.DefaultScheme =
    options.DefaultSignInScheme =
    options.DefaultSignOutScheme =
        JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options => {
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["JWT:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["JWT:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
          System.Text.Encoding.UTF8.GetBytes(
              builder.Configuration["JWT:SigningKey"]))
    };
});

// Configure ApiController's behavior
//builder.Services.Configure<ApiBehaviorOptions>(options => // Replaced by the [ManualValidationFilter]...
//    options.SuppressModelStateInvalidFilter = true);  // Execute action methods even if some of their input parameters are not valid

builder.Services.AddResponseCaching(options => // Response Caching Middleware, halve default caching size limits
{
    options.MaximumBodySize = 32 * 1024 * 1024; // Maximum cacheable size for the response body, default 64mb
    options.SizeLimit = 50 * 1024 * 1024; // Size limit for the response cache middleware, default 100mb
});

builder.Services.AddMemoryCache();

/* builder.Services.AddDistributedSqlServerCache(options => // Sql distributed cache
{
    options.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.SchemaName = "dbo";
    options.TableName = "AppCache";
}); */

builder.Services.AddStackExchangeRedisCache(options => // Redis distributed cache
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Configuration.GetValue<bool>("UseSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("UseDeveloperExceptionPage"))
    app.UseDeveloperExceptionPage(); // Captures exceptions from the HTTP pipeline and generate an HTML error page
else
    app.UseExceptionHandler("/error"); // Sends relevant error info to a customizable handler

app.UseHttpsRedirection();

app.UseCors();

app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.Use((context, next) => // Custom middleware, add a default cache-control fallback behaviour
{
    context.Response.GetTypedHeaders().CacheControl =
            new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
            {
                NoCache = true,
                NoStore = true
            };
    return next.Invoke();
});

/** Minimal API **/
app.MapGet("/error",
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)] (HttpContext context) =>
    {
        var exceptionHandler = context.Features.Get<IExceptionHandlerPathFeature>();
       
        // TODO: logging, sending notifications, and more
       
        var details = new ProblemDetails();
        details.Detail = exceptionHandler?.Error.Message;
        details.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;
        details.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
        details.Status = StatusCodes.Status500InternalServerError;

        app.Logger.LogError(CustomLogEvents.Error_Get, exceptionHandler?.Error, "An unhandled exception occurred.");

        return Results.Problem(details);
    });

app.MapGet("/error/test",
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)] () =>
    {
        throw new Exception("test");
    });

app.MapGet("/cod/test",
    [EnableCors("AnyOrigin_GetOnly")]
    [ResponseCache(NoStore = true)] () => {
        return Results.Text(
            "<script>"
                + "window.alert('Your client supports JavaScript!"
                + "\\r\\n\\r\\n"
                + $"Server time (UTC): {DateTime.UtcNow.ToString("o")}"
                + "\\r\\n"
                + "Client time (UTC): ' + new Date().toISOString());"
                + "</script>"
                + "<noscript>Your client does not support JavaScript</noscript>",
            "text/html"
        );
    });

app.MapGet("/cache/test/1",
    [EnableCors("AnyOrigin")]
    (HttpContext context) => {
        context.Response.Headers["cache-control"] = "no-cache, no-store";
        return Results.Ok();
    });

app.MapGet("/cache/test/2",
    [EnableCors("AnyOrigin")]
    (HttpContext context) =>
    {
        return Results.Ok();
    });

app.MapControllers();

app.Run();
