using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

using NAudio.Utils;
using NAudio.Wave;

using Stone_Red_Utilities.StringExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Commands;

public class StreamingCommands(HostsManager hostsManager)
{
    public void GetWavStreamFrom(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            ApiManager.SendNotificationMessageNewLine("Path is empty!", NotificationMessageType.Error);
            return;
        }

        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            ApiManager.SendNotificationMessageNewLine("Invalid file path!", NotificationMessageType.Error);
            return;
        }

        string json = File.ReadAllText(fullPath);

        PublicHyperFileInfo? publicHyperFileInfo = JsonSerializer.Deserialize<PublicHyperFileInfo>(json);
        if (publicHyperFileInfo == null)
        {
            ApiManager.SendNotificationMessageNewLine("Parsing file failed!", NotificationMessageType.Error);
            return;
        }

        _ = hostsManager.AddRange(publicHyperFileInfo.Hosts);
        StreamWav(publicHyperFileInfo.Hash);
    }

    public void StreamWav(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            ApiManager.SendNotificationMessageNewLine("No hash value specified!", NotificationMessageType.Error);
            return;
        }

        hash = hash.Trim().ToLower();

        foreach (NetworkSocket host in hostsManager.ToList())
        {
            bool validIpAdress = IPAddress.TryParse(host.IPAddress, out IPAddress? ipAddress);

            if (!validIpAdress)
            {
                hostsManager.Remove(host, true);
                continue;
            }

            ApiManager.SendNotificationMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);

            Console.CursorLeft = 0;

            Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", hash);

            _ = sendTask.Wait(1000);

            if (!sendTask.IsCompletedSuccessfully)
            {
                Console.CursorLeft = 0;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);

                hostsManager.Remove(host);
                continue;
            }
            else if (!sendTask.Result)
            {
                host.LastActive = DateTime.Now;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", NotificationMessageType.Error);
                continue;
            }

            host.LastActive = DateTime.Now;

            ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Has the requested file", NotificationMessageType.Success);
            ApiManager.SendNotificationMessageNewLine("Requesting stream...");

            using TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(ipAddress!, host.Port);
            tcpClient.ReceiveBufferSize = 6400;

            NetworkStream nwStream = tcpClient.GetStream();
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            byte[] reciveBuffer = new byte[6400];

            byte[] bytesToSend = Encoding.ASCII.GetBytes($"StreamWav {hash}");
            nwStream.Write(bytesToSend);
            nwStream.ReadTimeout = 5000;

            int bytesRead;
            try
            {
                bytesRead = nwStream.Read(buffer, 0, 1000);
            }
            catch (IOException)
            {
                ApiManager.SendNotificationMessageNewLine(string.Empty);
                ApiManager.SendNotificationMessageNewLine("Lost connection to other host!", NotificationMessageType.Error);
                continue;
            }

            string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            string[] parts = dataReceived.Split('/');

            if (parts.Length != 5) //If received data does not contain 5 parts -> error
            {
                ApiManager.SendNotificationMessageNewLine(dataReceived, NotificationMessageType.Error);
                continue;
            }

            if (!int.TryParse(parts[0], out int dataLength) || dataLength <= 0)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid data length!", NotificationMessageType.Error);
                continue;
            }

            if (!int.TryParse(parts[2], out int sampleRate) || sampleRate <= 0)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid sample rate!", NotificationMessageType.Error);
                continue;
            }

            if (!int.TryParse(parts[3], out int bitsPerSample) || bitsPerSample <= 0)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid bits per sample!", NotificationMessageType.Error);
                continue;
            }

            if (!int.TryParse(parts[4], out int channels) || channels <= 0)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid channels!", NotificationMessageType.Error);
                continue;
            }

            string fileName = parts[1].ToFileName();

            ApiManager.SendNotificationMessageNewLine($"File name: {fileName}");
            ApiManager.SendNotificationMessageNewLine($"Starting stream...");
            ApiManager.SendNotificationMessageNewLine("Controls: [p]lay, [s]top");

            BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(sampleRate, bitsPerSample, channels));
            using WaveOutEvent player = new WaveOutEvent();
            player.Init(bufferedWaveProvider);
            player.Play();

            int totalBytesRead = 0;
            TimeSpan totalTime = TimeSpan.FromSeconds(dataLength / (double)sampleRate / channels / (bitsPerSample / 8));

            Task task = Task.Run(async () =>
            {
                while (player.GetPosition() < dataLength && player.PlaybackState != PlaybackState.Stopped)
                {
                    Console.Write($"\r[{player.PlaybackState,-7}] {player.GetPositionTimeSpan():hh\\:mm\\:ss}/{totalTime:hh\\:mm\\:ss}");

                    if (IsBufferNearlyFull(bufferedWaveProvider))
                    {
                        await Task.Delay(100);
                        continue;
                    }

                    try
                    {
                        bytesRead = nwStream.Read(reciveBuffer, 0, reciveBuffer.Length);

                        if (bytesRead == 0 && bufferedWaveProvider.BufferedBytes == 0)
                        {
                            player.Stop();
                            ApiManager.SendNotificationMessage($"\r[{player.PlaybackState,-7}]");
                        }
                    }
                    catch (IOException ex)
                    {
                        Debug.WriteLine(ex);
                        ApiManager.SendNotificationMessageNewLine(string.Empty);
                        ApiManager.SendNotificationMessageNewLine("Lost connection to other host!", NotificationMessageType.Error);
                        break;
                    }

                    bytesRead = Math.Min(bytesRead, dataLength - totalBytesRead);

                    bufferedWaveProvider.AddSamples(reciveBuffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                }

                ApiManager.SendNotificationMessageNewLine(string.Empty);
                ApiManager.SendNotificationMessageNewLine("Stream ended!", NotificationMessageType.Warning);
            });

            while (!task.IsCompleted)
            {
                ApiManager.SendNotificationMessage($"\r[{player.PlaybackState,-7}]");

                char c = Console.ReadKey(true).KeyChar;

                if (c == 'p')
                {
                    if (player.PlaybackState == PlaybackState.Playing)
                    {
                        player.Pause();
                    }
                    else
                    {
                        player.Play();
                    }
                }
                else if (c == 's')
                {
                    player.Stop();
                    ApiManager.SendNotificationMessage($"\r[{player.PlaybackState,-7}]");
                    break;
                }
            }

            hostsManager.SaveHosts();
            return;
        }
        ApiManager.SendNotificationMessageNewLine("None of the available hosts have the requested file!", NotificationMessageType.Error);
        hostsManager.SaveHosts();
    }

    private bool IsBufferNearlyFull(BufferedWaveProvider bufferedWaveProvider)
    {
        return bufferedWaveProvider != null &&
               bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
               < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
    }
}