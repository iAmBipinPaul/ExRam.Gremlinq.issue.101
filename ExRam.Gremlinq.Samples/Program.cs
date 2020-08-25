#define GremlinServer
//#define CosmosDB
//#define AWSNeptune
//#define JanusGraph

using System;
using System.Linq;
using System.Threading.Tasks;
using ExRam.Gremlinq.Core;
using ExRam.Gremlinq.Providers.WebSocket;
using ExRam.Gremlinq.Samples.Model;
using Microsoft.Extensions.Logging;

// Put this into static scope to access the default GremlinQuerySource as "g". 
using static ExRam.Gremlinq.Core.GremlinQuerySource;

namespace ExRam.Gremlinq.Samples
{
    public class Program
    {
        private Person _marko;
        private Person _josh;
        private Person _peter;
        private Person _daniel;
        private Person _vadas;
        private readonly IGremlinQuerySource _g;

        public Program()
        {

            var logger = LoggerFactory
                .Create(builder => builder
                    .AddFilter(__ => true)
                    .AddConsole())
                .CreateLogger("Queries");


            _g = g
                .ConfigureEnvironment(env => env //We call ConfigureEnvironment twice so that the logger is set on the environment from now on.
                    .UseLogger(LoggerFactory
                        .Create(builder => builder
                            .AddFilter(__ => true)
                            .AddConsole())
                        .CreateLogger("Queries")))
                .ConfigureEnvironment(env => env
                    //Since the Vertex and Edge classes contained in this sample implement IVertex resp. IEdge,
                    //setting a model is actually not required as long as these classes are discoverable (i.e. they reside
                    //in a currently loaded assembly). We explicitly set a model here anyway.
                    .UseModel(GraphModel
                        .FromBaseTypes<Vertex, Edge>(lookup => lookup
                            .IncludeAssembliesOfBaseTypes())
                        //For CosmosDB, we exclude the 'PartitionKey' property from being included in updates.
                        .ConfigureProperties(model => model
                            .ConfigureElement<Vertex>(conf => conf
                                .IgnoreOnUpdate(x => x.PartitionKey))))
                   //Disable query logging for a noise free console output.
                   //Enable logging by setting the verbosity to anything but None.
                    .ConfigureOptions(options => options
                        .SetValue(WebSocketGremlinqOptions.QueryLogLogLevel, LogLevel.None))

                    .UseGremlinServer(builder => builder
                        .AtLocalhost()));
        }

        public async Task Run()
        {
            await Create_the_graph();
            await MarkKnowsAndDontKnows();
            Console.Write("Press any key...");
            Console.Read();
        }

        private async Task Create_the_graph()
        {
            // Create a graph very similar to the one
            // found at http://tinkerpop.apache.org/docs/current/reference/#graph-computing.

            // Uncomment to delete the whole graph on every run.
            await _g.V().Drop();

            _marko = await _g
                .AddV(new Person { Name = "Marko", Age = 29, ZipCode = "10001" })
                .FirstAsync();

            _vadas = await _g
                .AddV(new Person { Name = "Vadas", Age = 27, ZipCode = "10199" })
                .FirstAsync();

            _josh = await _g
               .AddV(new Person { Name = "Josh", Age = 32, ZipCode = "89002" })
               .FirstAsync();

            _peter = await _g
               .AddV(new Person { Name = "Peter", Age = 35, ZipCode = "10122" })
               .FirstAsync();

            _daniel = await _g
               .AddV(new Person
               {
                   Name = "Daniel",
                   Age = 37,
                   ZipCode = "88905",
                   PhoneNumbers = new[]
                   {
                        "+491234567",
                        "+492345678"
                   }
               })
               .FirstAsync();

            await _g
                .V(_marko.Id)
                .AddE<Knows>()
                .To(__ => __
                    .V(_vadas.Id))
                .FirstAsync();

            await _g
                .V(_marko.Id)
                .AddE<Knows>()
                .To(__ => __
                    .V(_josh.Id))
                .FirstAsync();
        }

        private async Task MarkKnowsAndDontKnows()
        {
            var nearByZipCodes = new string[] { "10001", "10199", "10121", "10122", "10123" };
            Console.WriteLine("Nearby Zip codes");
            Console.WriteLine(string.Join(",", nearByZipCodes));

            Console.WriteLine("\nWho does mark know =>");

            var whoDoesMarkKnow = await _g 
                .V<Person>(_marko.Id)
                .Both<Knows>()
                .OfType<Person>();
          

            foreach (var value in whoDoesMarkKnow)
            {
                Console.WriteLine($"Name: {value.Name.Value}, ZipCode: {value.ZipCode}");
            }

            Console.WriteLine("\nWho does mark know  from nearby Zip Code=>");
            
            var marksFriendsThatBelongToNearByZipCode
                = await _g
                .Inject(nearByZipCodes)
                .Fold()
                .As((_, injectedNearByZipCode) => _
                    .V<Person>(_marko.Id)
                    .Both<Knows>()
                    .OfType<Person>().Where(c => injectedNearByZipCode.Value.Contains(c.ZipCode)
                    ));

            foreach (var value in marksFriendsThatBelongToNearByZipCode)
            {
                Console.WriteLine($"Name: {value.Name.Value}, ZipCode: {value.ZipCode}");
            }

            Console.WriteLine($"\nAll the person that mark does not know from Nearby Zip Code =>");
            
            //This method query is throwing exception, It should return Peter
            var personWhoMarkDoesNotKnowAndBelongsToNearbyZipCode = await _g
                .Inject(nearByZipCodes)
                .Fold()
                .As((_, injectedNearByZipCode) => _
                    .V<Person>(_marko.Id)
                    .Both<Knows>() 
                    .OfType<Person>()
                    .Fold()
                    .As((__, knownPeople) => __
                        .V<Person>().Where(c =>
                            !knownPeople.Value.Contains(c) && injectedNearByZipCode.Value.Contains(c.ZipCode)))
                );

            foreach (var value in personWhoMarkDoesNotKnowAndBelongsToNearbyZipCode)
            {
                Console.WriteLine($"Name: {value.Name.Value}, ZipCode: {value.ZipCode}");
            }
        }

        private static async Task Main()
        {
            await new Program().Run();
        }
    }
}
