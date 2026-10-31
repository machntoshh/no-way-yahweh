using NoWayYahweh.Core;

// Started by Chrome/Edge. The browser passes the calling extension's origin as the first
// argument; anything else is refused before a single byte is read.
if (args.Length == 0 || !HostRegistration.AllowedOrigins.Contains(args[0]))
{
    Console.Error.WriteLine("Origem não autorizada.");
    return 2;
}

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
var handler = MessageHandler.Default;

try
{
    while (NativeMessaging.Read(input) is { } body)
        NativeMessaging.Write(output, handler.Handle(body));
    return 0;
}
catch (InvalidDataException e)
{
    // Broken framing: the stream cannot be trusted any more, so answer once and stop.
    NativeMessaging.Write(output, MessageHandler.Error(e.Message));
    return 3;
}
