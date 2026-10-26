using Scrabble.Core.Dictionary;
using Scrabble.Web.Hubs;
using Scrabble.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
var dictionaryPath = builder.Configuration["Dictionary:FilePath"];
var dictionaryLoad = WordDictionaryLoader.Load(dictionaryPath);
builder.Services.AddSingleton<IWordDictionary>(dictionaryLoad.Dictionary);
builder.Services.AddSingleton<GameManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<GameHub>("/hubs/game");

app.Run();

public partial class Program;
