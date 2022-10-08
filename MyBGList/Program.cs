using MyBGList;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Executed at the start of the application to register and configure the required services and middlewares to handle the HTTP request & response pipeline

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts => 
    opts.ResolveConflictingActions(apiDesc =>  apiDesc.First()) //  deal with routing conflict situations (not encouraged, keeping it for now)
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Configuration.GetValue<bool>("UseSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Configuration.GetValue<bool>("UseDeveloperExceptionPage"))
    app.UseDeveloperExceptionPage(); // captures exceptions from the HTTP pipeline and generate an HTML error page
else
    app.UseExceptionHandler("/error"); // sends relevant error info to a customizable handler

app.UseHttpsRedirection();
app.UseAuthorization();

 /** Minimal API routes **/
app.MapGet("/error", () => Results.Problem());
app.MapGet("/error/test", () => { throw new Exception("test"); }); // Testing the DeveloperExceptionPageMiddleware

app.MapControllers();

app.Run();
