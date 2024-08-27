# Welcome To ActorSrcGen 
 
ActorSrcGen is a C# Source Generator allowing the conversion of simple C#
classes into Dataflow compatible pipelines.

ActorSrcGen simplifies the process of working with TPL Dataflow by generating 
the boilerplate needed to safely trap and handle errors without interrupting 
the operation of the pipeline.  It's normally based on the assumption that 
the pipeline will be a long lived process with '*ingesters*' that continually 
pump incoming messages into the pipeline.

If you encounter any issues or have any questions, please don't hesitate to
submit an issue report.  This helps me understand any problems or limitations of
the project and allows me to address them promptly.

If you have an idea for a new feature or enhancement, I encourage you to submit
a feature request.  Your input will shape the future direction of ActorSrcGen
and help make it even better.

If you have any code changes or improvements you'd like to contribute, I welcome
pull requests (PRs).  I will review your changes and
provide feedback, helping you ensure a smooth integration process.


## How Do You Use It?

1. Get the latest version of the package into your project:

    ```shell
    dotnet add package ActorSrcGen
    ```

    1. From there, development follows a simple process.  First declare the pipeline class.

    ```csharp
    [Actor]
    public partial class MyPipeline
    {
    }
    ```

    The class must be `partial`, since the boilerplate code is added to another part 
    of the class by the ActorSrcGen Source Generator.

    If you are using Visual Studio, you can see the generated part of the code under the
    ActorSrcGen analyzer:

    ![File1](doc/file1.png)

1.  Next, you create some '*ingester*' functions.  Ingesters are functions that are able 
    to receive incoming work from somewhere.  This could be requests coming in on a queue or 
    other async source, or be generated in situ.

    ```csharp
    [Ingest(1)]
    [NextStep(nameof(DoSomethingWithRequest))]
    public async Task<string> ReceivePollRequest(CancellationToken cancellationToken)
    {
        return await GetTheNextRequest();
    }
    ```

    Each ingester defines a `Priority`, and the ingesters are visited in priority order.  
    The ingestion message pump will preferentially consume from the highest priority ingester 
    until it no longer yields any messages, at which point it will fall through to the next 
    highest priority ingester.  If nothing comes from any of the ingesters then it will sleep 
    for a second and them repeat the cycle.

    You can define as many ingesters as you like, all feeding into the pipeline, but 
    remember that the lowest priority ones only get a chance to run if there was nothing 
    available through any other channel.  If you need to implement a more sophisticated load 
    balancing scheme to pull incoming work from multiple sources, you can do it from outside 
    of the pipeline instead.

1. The next step is to implement the pipeline functions themselves.  These are the steps 
    in the pipeline that get the TPL Dataflow wrapper generated to link them together and 
    buffer all their incoming and outgoing data.

    The first pipeline step to implement has the `[FirstStep]` attribute adornment.  The 
    description is not used at present, but will be used in future for logging purposes.

    ```csharp
    [FirstStep("decode incoming poll request")]
    [NextStep(nameof(ActOnTheRequest))]
    public PollRequest DecodeRequest(string json)
    {
        Console.WriteLine(nameof(DecodeRequest));
        var pollRequest = JsonSerializer.Deserialize<PollRequest>(json);
        return pollRequest;
    }
    ```

    The first step is used to control how the interface to the pipeline looks from the 
    outside world.  The pipeline can implement interfaces like `IDataflow<TIn, TOut>` depending
    the parameter and return types of the first and last steps.  This makes it easy to 
    treat your pipeline class as just another TPL Dataflow block to be inserted into other 
    pipelines, as needed.

1. Now implement whatever other steps are needed in the pipeline.  The outputs and input types 
    of successive steps need to match.

    ```csharp
    [Step]
    [NextStep(nameof(DeliverResults))]
    public PollResults ActOnTheRequest(PollRequest req)
    {
        Console.WriteLine(nameof(ActOnTheRequest));
        var result = SomeApiClient.GetTheResults(req.Id);
        return result;
    }
    ```

    Again, you can have as many of these as you need, with branching done using multiple 
    `[NextStep]` attributes.

1. Finally, you define a last step, using the `[LastStep]` attribute:

    ```csharp
    [LastStep]
    public bool DeliverResults(PollResults res)
    {
        return myQueue.TryPush(res);
    }
    ```

    As mentioned in the first step method, the return type of this function is used
    to influence the interface types.  It also helps in creating an *accepter* function that 
    can be used to get results out of the pipeline.

1. These functions are enough information for ActorSrcGen to be able to generate the 
    boilerplate around the pipeline connecting the steps using TPL Dataflow.

    Here's what will be generated from the above

    ```csharp
    using System.Threading.Tasks.Dataflow;
    using Gridsum.DataflowEx;

    public partial class MyActor : Dataflow<string, bool>, IActor< string >
    {

	    public MyActor(DataflowOptions dataflowOptions = null) : base(DataflowOptions.Default)
	    {
            _DeliverResults = new TransformBlock<PollResults,bool>(         (PollResults x) => {
                try
                {
                    return DeliverResults(x);
                }
                catch
                {
                    return default;
                }
            },
                new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1
            });
            RegisterChild(_DeliverResults);

            _ActOnTheRequest = new TransformBlock<PollRequest,PollResults>(         (PollRequest x) => {
                try
                {
                    return ActOnTheRequest(x);
                }
                catch
                {
                    return default;
                }
            },
                new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1
            });
            RegisterChild(_ActOnTheRequest);

            _DecodeRequest = new TransformBlock<string,PollRequest>(         (string x) => {
                try
                {
                    return DecodeRequest(x);
                }
                catch
                {
                    return default;
                }
            },
                new ExecutionDataflowBlockOptions() {
                    BoundedCapacity = 1,
                    MaxDegreeOfParallelism = 1
            });
            RegisterChild(_DecodeRequest);

            _ActOnTheRequest.LinkTo(_DeliverResults, new DataflowLinkOptions { PropagateCompletion = true });
            _DecodeRequest.LinkTo(_ActOnTheRequest, new DataflowLinkOptions { PropagateCompletion = true });
	        }

            TransformBlock<PollResults,bool> _DeliverResults;
            TransformBlock<PollRequest,PollResults> _ActOnTheRequest;
            TransformBlock<string,PollRequest> _DecodeRequest;
            public override ITargetBlock<string > InputBlock { get => _DecodeRequest ; }
            public override ISourceBlock< bool > OutputBlock { get => _DeliverResults; }
            public bool Call(string input) => InputBlock.Post(input);
            public async Task<bool> Cast(string input) => await InputBlock.SendAsync(input);
    
            public async Task<bool> AcceptAsync(CancellationToken cancellationToken)
            {
                try
                {
                    var result = await _DeliverResults.ReceiveAsync(cancellationToken);
                    return result;
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    return await Task.FromCanceled<bool>(cancellationToken);
                }
            }

          public async Task Ingest(CancellationToken ct)
          {
            // start the message pump
            while (!ct.IsCancellationRequested)
            {
              var foundSomething = false;
              try
              {
                // cycle through ingesters IN PRIORITY ORDER.
                {
                    var msg = await ReceivePollRequest(ct);
                    if (msg != null)
                    {
                        Call(msg);
                        foundSomething = true;
                        // then jump back to the start of the pump
                        continue;
                    }
                }

                if (!foundSomething) 
                    await Task.Delay(1000, ct);
              }
              catch (TaskCanceledException)
              {
                // if nothing was found on any of the receivers, then sleep for a while.
                continue;
              }
              catch (Exception e)
              {
                // _logger.LogError(e, "Exception suppressed");
              }
            }
          }
        }
    ```


1. To use the pipeline, you can insert messages directly, using the `Call` or `Cast` methods,
    or you can invoke the receiver message pump:

    ```csharp
    var actor = new MyActor();

    try
    {
        if (actor.Call("""
                       { "something": "here" }
                       """))
            Console.WriteLine("Called Synchronously");

        // stop the pipeline after 10 secs
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // kick off an endless process to keep ingesting input into the pipeline
        var t = Task.Run(async () => await actor.Ingest(cts.Token), cts.Token);

        // consume results from the last step via the AcceptAsync method
        while (!cts.Token.IsCancellationRequested)
        {
            var result = await actor.AcceptAsync(cts.Token);
            Console.WriteLine($"Result: {result}");
        }

        await t; // cancel the message pump task
        await actor.SignalAndWaitForCompletionAsync(); // wait for all pipeline tasks to complete
    }
    catch (OperationCanceledException _)
    {
        Console.WriteLine("All Done!");
    }
    ```


## What It Does

Its purpose is to simplify the
usage of TPL Dataflow, a library that helps with writing robust and performant
asynchronous and concurrent code in .NET.  In this case, the source
generator takes a regular C# class and extends it by generating the necessary
boilerplate code to use TPL Dataflow.  The generated code creates a pipeline of
dataflow components that support the actor model.  The code that you need to write is
simpler, and therefore much easier to test, since they are generally just pure 
functions taking a value and returning a response object.

The generated code includes the necessary wiring to connect the methods of
your class together using the TPL Dataflow.  This allows the
methods to be executed in a coordinated and concurrent manner.

Overall, the source generator simplifies the process of using TPL Dataflow by
automatically generating the code that would otherwise need to be written
manually.  It saves developers from writing a lot of boilerplate code and allows
them to focus on the core logic of their application.


## Why Bother?

You might be wondering what the architectural benefits of using a model like
this might be.

Writing robust and performant asynchronous and concurrent code in .NET is a
laborious process.  TPL Dataflow makes it easier - it "*provides dataflow
components to help increase the robustness of concurrency-enabled applications.
This dataflow model promotes actor-based programming by providing in-process
message passing for coarse-grained dataflow and pipelining tasks*" (see
[docs](https://learn.microsoft.com/en-us/dotnet/standard/parallel-programming/dataflow-task-parallel-library)).

ActorSrcGen allows you to take advantage of that model without needing to write
a lot of the necessary boilerplate code.


### The Actor Model
The Actor Model is a programming paradigm that is based on the concept of
actors, which are autonomous units of computation.  It has several benefits in
programming:

1. **Concurrency**: Actors can be executed concurrently, allowing for efficient
   use of multiple CPU cores.  This can lead to significant performance
   improvements in systems that require concurrent execution.
1. **Fault tolerance**: Actors can be designed to be fault-tolerant, meaning
   that if an actor fails or crashes, it can be restarted without affecting the
   rest of the system.  This can improve the reliability and availability of the
   system.
1. **Encapsulation**: Actors encapsulate their state and behavior, making it
   easier to reason about and test the code.  This can lead to better code
   quality and maintainability.

### TPL Dataflow

The Task Parallel Library (TPL) Dataflow in .NET provides a powerful framework
for building high-throughput systems.  Here are some benefits of using TPL
Dataflow for high-throughput systems:

1. **Efficiency**: TPL Dataflow is designed to optimize the execution of tasks
   and dataflows.  It automatically manages the execution of tasks based on
   available resources, reducing unnecessary overhead and maximizing throughput.
1. **Scalability**: TPL Dataflow allows you to easily scale your system by
   adding or removing processing blocks.  You can dynamically adjust the number
   of processing blocks based on the workload, ensuring that your system can
   handle varying levels of throughput.
1. **Flexibility**: TPL Dataflow provides a variety of processing blocks, such
   as buffers, transform blocks, and action blocks, which can be combined and
   customized to fit your specific requirements.  This flexibility allows you to
   build complex dataflows that can handle different types of data and
   processing logic.


## Acknowledgements

The generated source builds atop
[DataflowEx](https://github.com/gridsum/DataflowEx) for a clean stateful
object-oriented wrapper around your pipeline.

With thanks to:

- Gridsum [DataflowEx](https://github.com/gridsum/DataflowEx)
- [Bnaya.SourceGenerator.Template](https://github.com/bnayae/Bnaya.SourceGenerator.Template) (see [article](https://blog.stackademic.com/source-code-generators-diy-f04229c59e1a))
