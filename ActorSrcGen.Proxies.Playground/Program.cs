// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using DataflowSrcGen.Abstractions.Playground;

Console.WriteLine("Playground is starting");

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

Settings? settings = config.GetRequiredSection("Services:Calc").Get<Settings>();
if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out var url))
    throw new InvalidDataException("BaseUrl");
var services = new ServiceCollection();

//services.AddHttpClient<ICalcProxy, CalcProxy>("calc", client => client.BaseAddress = url);

//var sp = services.BuildServiceProvider();
//ICalcProxy proxy = sp.GetRequiredService<ICalcProxy>();

//var payload = new Payload(5, 8);
//var addResult = await proxy.AppendAsync(payload);
//Console.WriteLine($"{payload.A} + {payload.B} = {addResult}");

//payload = new Payload(15, 8);
//var subResult = await proxy.AppendAsync(payload);
//Console.WriteLine($"{payload.A} - {payload.B} = {subResult}");

var wf = new MyWorkflow();
Console.WriteLine(await wf.Post(10));

public record Settings(string BaseUrl);
