using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

const int MaxFrameSize = 1024;

if (args.Length == 0 || args[0] is not ("receiver" or "sender"))
{
    PrintUsage();
    return 1;
}

var mode = args[0];
var pipeName = GetOption(args, "--pipe") ?? "quizmvp-lab";
var process = Process.GetCurrentProcess();

Console.WriteLine($"Роль: {mode}; PID: {Environment.ProcessId}; имя канала: {pipeName}; открытых дескрипторов: {GetHandleCount(process)}");

try
{
    return mode == "receiver"
        ? await ReceiveAsync(args, pipeName)
        : await SendAsync(args, pipeName);
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Операция отменена.");
    return 3;
}
catch (EndOfStreamException exception)
{
    Console.Error.WriteLine($"Приёмник обнаружил обрыв внутри кадра: {exception.Message}");
    return 5;
}

static async Task<int> ReceiveAsync(string[] arguments, string pipeName)
{
    var readChunk = ParsePositiveOption(arguments, "--read-chunk", 2);
    var closeAfter = ParsePositiveOption(arguments, "--close-after", int.MaxValue);

    await using var pipe = new NamedPipeServerStream(
        pipeName,
        PipeDirection.InOut,
        maxNumberOfServerInstances: 1,
        PipeTransmissionMode.Byte,
        PipeOptions.Asynchronous);

    Console.WriteLine("Приёмник ожидает подключение...");
    await pipe.WaitForConnectionAsync();
    Console.WriteLine($"Передатчик подключён; дескриптор канала: 0x{pipe.SafePipeHandle.DangerousGetHandle():X}.");

    var received = 0;
    while (true)
    {
        var header = new byte[sizeof(int)];
        var hasHeader = await ReadExactlyAsync(pipe, header, readChunk, allowEofAtStart: true);
        if (!hasHeader)
        {
            Console.WriteLine($"EOF между кадрами; принято кадров: {received}.");
            return 0;
        }

        var frameLength = BinaryPrimitives.ReadInt32BigEndian(header);
        if (frameLength is < 1 or > MaxFrameSize)
        {
            Console.Error.WriteLine($"Кадр отклонён: размер {frameLength}, допустимый диапазон 1–{MaxFrameSize} байт.");
            return 4;
        }

        var payload = new byte[frameLength];
        if (!await ReadExactlyAsync(pipe, payload, readChunk, allowEofAtStart: false))
        {
            Console.Error.WriteLine("Ошибка: соединение оборвалось внутри кадра.");
            return 5;
        }

        received++;
        Console.WriteLine($"Кадр {received}: {Encoding.UTF8.GetString(payload)} (байт: {payload.Length}).");
        if (received >= closeAfter)
        {
            Console.WriteLine("Приёмник закрывает канал по сценарию --close-after.");
            return 0;
        }
    }
}

static async Task<int> SendAsync(string[] arguments, string pipeName)
{
    var pauseMilliseconds = ParseNonNegativeOption(arguments, "--pause-ms", 150);
    var splitWrites = arguments.Contains("--split", StringComparer.Ordinal);
    var messages = new[] { "quiz-кадр-1", "quiz-кадр-2", "quiz-кадр-3" };

    await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await pipe.ConnectAsync(10_000);
    Console.WriteLine($"Подключение установлено; дескриптор канала: 0x{pipe.SafePipeHandle.DangerousGetHandle():X}.");

    try
    {
        for (var index = 0; index < messages.Length; index++)
        {
            var payload = Encoding.UTF8.GetBytes(messages[index]);
            if (payload.Length > MaxFrameSize)
            {
                throw new InvalidOperationException("Размер кадра превышает установленный предел.");
            }

            var frame = new byte[sizeof(int) + payload.Length];
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(0, sizeof(int)), payload.Length);
            payload.CopyTo(frame.AsSpan(sizeof(int)));

            if (splitWrites)
            {
                for (var offset = 0; offset < frame.Length; offset += 2)
                {
                    var count = Math.Min(2, frame.Length - offset);
                    await pipe.WriteAsync(frame.AsMemory(offset, count));
                    await pipe.FlushAsync();
                    await Task.Delay(35);
                }
            }
            else
            {
                await pipe.WriteAsync(frame);
                await pipe.FlushAsync();
            }

            Console.WriteLine($"Отправлен кадр {index + 1}: {messages[index]}.");
            if (pauseMilliseconds > 0)
            {
                await Task.Delay(pauseMilliseconds);
            }
        }
    }
    catch (IOException exception)
    {
        Console.Error.WriteLine($"Канал закрыт принимающей стороной; передача прервана: {exception.Message}");
        return 2;
    }

    Console.WriteLine("Все кадры отправлены; передатчик закрывает канал.");
    return 0;
}

static async Task<bool> ReadExactlyAsync(
    Stream stream,
    Memory<byte> buffer,
    int maxReadChunk,
    bool allowEofAtStart)
{
    var offset = 0;
    while (offset < buffer.Length)
    {
        var count = Math.Min(maxReadChunk, buffer.Length - offset);
        var read = await stream.ReadAsync(buffer.Slice(offset, count));
        if (read == 0)
        {
            if (offset == 0 && allowEofAtStart)
            {
                return false;
            }

            throw new EndOfStreamException("Поток завершился посреди кадра.");
        }

        offset += read;
    }

    return true;
}

static string? GetOption(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static int ParsePositiveOption(string[] arguments, string name, int fallback)
{
    var value = GetOption(arguments, name);
    return value is not null && int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}

static int ParseNonNegativeOption(string[] arguments, string name, int fallback)
{
    var value = GetOption(arguments, name);
    return value is not null && int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
}

static int GetHandleCount(Process process) => OperatingSystem.IsWindows() ? process.HandleCount : -1;

static void PrintUsage()
{
    Console.WriteLine("Использование:");
    Console.WriteLine("  dotnet run --project tools/QuizMvp.IpcDemo -- receiver --pipe <имя> [--read-chunk 2] [--close-after 1]");
    Console.WriteLine("  dotnet run --project tools/QuizMvp.IpcDemo -- sender --pipe <имя> [--split] [--pause-ms 150]");
}
