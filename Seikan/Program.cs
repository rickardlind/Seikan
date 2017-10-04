using System;
using System.Net;
using System.Threading;

namespace Seikan
{
    class Param
    {
        public IPEndPoint AcceptEP { get; set; }
        public IPEndPoint ConnectEP { get; set; }
        public int Timeout { get; set; }
        public int MaxConnections { get; set; }
    }

    class Usage : Exception
    {
        public Usage(string msg) : base(msg) { }
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var param = ParseArgs(args);
                var forwarder = new TcpForwarder(param.AcceptEP,
                                                 param.ConnectEP,
                                                 param.MaxConnections);
                var cts = new CancellationTokenSource();

                // Cancel on Ctrl+C
                Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                var task = forwarder.RunAsync(cts.Token, param.Timeout);
                task.Wait();
            }
            catch (Usage usage)
            {
                Console.WriteLine("{0}", usage.Message);
                Console.WriteLine("Usage: Seikan.exe /accept <addr>:<port> /connect <addr>:<port> [/timeout <ms>] [/maxconn <num>]");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: {0}", ex.Message);
            }
        }

        static Param ParseArgs(string[] args)
        {
            var param = new Param() { Timeout = Timeout.Infinite, MaxConnections = 10 };

            try
            {
                var i = 0;
                while (i < args.Length)
                {
                    var option = args[i++];

                    switch (option)
                    {
                        case "/accept":
                            param.AcceptEP = ParseEndpoint(args[i++]);
                            break;

                        case "/connect":
                            param.ConnectEP = ParseEndpoint(args[i++]);
                            break;

                        case "/timeout":
                            param.Timeout = int.Parse(args[i++]);
                            break;

                        case "/maxconn":
                            param.MaxConnections = int.Parse(args[i++]);
                            break;

                        default:
                            throw new Usage("unknown option: " + option);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Usage(ex.Message);
            }

            if (param.AcceptEP == null || param.ConnectEP == null)
            {
                throw new Usage("missing parameter");
            }

            return param;
        }

        static IPEndPoint ParseEndpoint(string arg)
        {
            var fields = arg.Split(new[] { ':' });
            return new IPEndPoint(IPAddress.Parse(fields[0]), Int32.Parse(fields[1]));
        }
    }
}
