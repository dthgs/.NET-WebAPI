using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBGList.Attributes;
using MyBGList.Models;
using MyBGList.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders() // Remove all pre-configured logging providers, then add what to keep after
    .AddSimpleConsole()
    .AddDebug();

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

// Configure ApiController's behavior
//builder.Services.Configure<ApiBehaviorOptions>(options => // Replaced by the [ManualValidationFilter]...
//    options.SuppressModelStateInvalidFilter = true);  // Execute action methods even if some of their input parameters are not valid

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

app.UseAuthorization();

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

app.MapControllers();

app.Run();
