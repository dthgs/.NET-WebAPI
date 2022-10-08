using Microsoft.AspNetCore.Cors;
using MyBGList;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => {
    options.AddDefaultPolicy(cfg => {
        cfg.WithOrigins(builder.Configuration["AllowedOrigins"]);
        cfg.AllowAnyHeader();
        cfg.AllowAnyMethod();
    });
    options.AddPolicy(name: "AnyOrigin",
        cfg => {
            cfg.AllowAnyOrigin();
            cfg.AllowAnyHeader();
            cfg.AllowAnyMethod();
        });
});

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts => 
    opts.ResolveConflictingActions(apiDesc =>  apiDesc.First()) //  Deal with routing conflict situations (not encouraged, keeping it for now)
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
app.MapGet("/error", [EnableCors("AnyOrigin")] () => Results.Problem());
app.MapGet("/error/test", [EnableCors("AnyOrigin")] () => { throw new Exception("test"); });

app.MapControllers();

app.Run();
