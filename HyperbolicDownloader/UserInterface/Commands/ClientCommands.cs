using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

namespace HyperbolicDownloader.UserInterface.Commands;

internal class ClientCommands
{
    public void ShowInfo(string _)
    {
        if (Program.PublicIpAddress is not null)
        {
            ConsoleExt.WriteLine($"The public IP address is: {Program.PublicIpAddress}", ConsoleColor.Green);
            ConsoleExt.WriteLine($"The public port is: {Program.PublicPort}", ConsoleColor.Green);
            Console.WriteLine();
        }

        ConsoleExt.WriteLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()}", ConsoleColor.Green);
        ConsoleExt.WriteLine($"The private port is: {Program.PrivatePort}", ConsoleColor.Green);
    }
}