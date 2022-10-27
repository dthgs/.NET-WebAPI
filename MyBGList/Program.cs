using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyBGList.Attributes;
using MyBGList.Models;
using MyBGList.Swagger;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts => {
        opts.ResolveConflictingActions(apiDesc => apiDesc.First()); //  Deal with routing conflict situations (not encouraged, keeping it for now)
        opts.ParameterFilter<SortColumnFilter>(); // Add sortColumn params to swagger/v1/swagger.json
        opts.ParameterFilter<SortOrderFilter>();
    }
);
 

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"))
    );

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
app.MapGet(
    "/error",
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    () => Results.Problem()
);

app.MapGet(
    "/error/test",
    [EnableCors("AnyOrigin")]
    [ResponseCache(NoStore = true)]
    () =>
    {
        throw new Exception("test");
    }
);

app.MapGet(
    "/cod/test",
    [EnableCors("AnyOrigin_GetOnly")]
    [ResponseCache(NoStore = true)]
    () =>
        Results.Text(
            "<script>"
                + "window.alert('Your client supports JavaScript!"
                + "\\r\\n\\r\\n"
                + $"Server time (UTC): {DateTime.UtcNow.ToString("o")}"
                + "\\r\\n"
                + "Client time (UTC): ' + new Date().toISOString());"
                + "</script>"
                + "<noscript>Your client does not support JavaScript</noscript>",
            "text/html"
        )
);

app.MapControllers();

app.Run();
