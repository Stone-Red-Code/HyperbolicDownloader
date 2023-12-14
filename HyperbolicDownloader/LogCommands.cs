using HyperbolicDownloaderApi.Managment;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Net;

namespace HyperbolicDownloader;

internal class LogCommands
{
    private bool debug = false;

    public void Log(bool debug)
    {
        this.debug = debug;

        Console.Clear();

        if (debug)
        {
            Console.WriteLine("Debug log opened");
        }
        else
        {
            Console.WriteLine("Log opened");
        }

        ApiManager.OnNotificationMessageRecived += OnNotificationMessageRecived;

        char c = ' ';

        do
        {
            if (c != '\0')
            {
                Console.WriteLine("Press 'q' to exit");
            }
            c = Console.ReadKey(true).KeyChar;
        } while (c != 'q');

        ApiManager.OnNotificationMessageRecived -= OnNotificationMessageRecived;

        Console.Clear();

        Console.WriteLine("Log closed");
    }

    private void OnNotificationMessageRecived(object? sender, NotificationMessageEventArgs e)
    {
        if (e.NotificationMessageType == NotificationMessageType.Log || (debug && e.NotificationMessageType == NotificationMessageType.Debug))
        {
            lock (Console.Out)
            {
                if (e.Message?.Contains('>') == true && IPAddress.TryParse(e.Message[..e.Message.IndexOf('>')].Trim(), out IPAddress? ipAddress))
                {
                    ConsoleExt.Write($"[{DateTime.Now}] ", ConsoleColor.DarkGray);

                    byte[] ipAddressBytes = ipAddress.GetAddressBytes();

                    Array.Resize(ref ipAddressBytes, 8);

                    ConsoleColor consoleColor = (BitConverter.ToInt64(ipAddressBytes) % 12) switch
                    {
                        0 => ConsoleColor.Red,
                        1 => ConsoleColor.Green,
                        2 => ConsoleColor.Yellow,
                        3 => ConsoleColor.Blue,
                        4 => ConsoleColor.Magenta,
                        5 => ConsoleColor.Cyan,
                        6 => ConsoleColor.DarkRed,
                        7 => ConsoleColor.DarkGreen,
                        8 => ConsoleColor.DarkYellow,
                        9 => ConsoleColor.DarkBlue,
                        10 => ConsoleColor.DarkMagenta,
                        11 => ConsoleColor.DarkCyan,
                        _ => ConsoleColor.White
                    };

                    ConsoleExt.Write(e.Message[..e.Message.IndexOf('>')], consoleColor);
                    ConsoleExt.Write(e.Message[e.Message.IndexOf('>')..], ConsoleColor.White);
                }
                else
                {
                    ConsoleExt.Write($"[{DateTime.Now}] ", ConsoleColor.DarkGray);
                    ConsoleExt.Write(e.Message, ConsoleColor.White);
                }
            }
        }
    }
}