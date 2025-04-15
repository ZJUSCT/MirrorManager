using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Orchestrator.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var dbPath = builder.Configuration["DataPath"]!;
builder.Services.AddDbContext<OrchDbContext>(
    opt => opt.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddSingleton<IStateStore, StateStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddMvc(opt =>
    opt.OutputFormatters.OfType<StringOutputFormatter>().Single().SupportedMediaTypes.Add("text/html"));
builder.Services.AddOutputCache();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrchDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.UseOutputCache();

app.Run();
