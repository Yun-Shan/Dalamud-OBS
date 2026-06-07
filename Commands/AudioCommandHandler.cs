using System;
using OBSWebsocketDotNet;

namespace OBSPlugin
{
    public class AudioCommandHandler
    {
        private readonly IOBSWebsocket _obs;

        public AudioCommandHandler(IOBSWebsocket obs)
        {
            _obs = obs;
        }

        public void HandleAudioCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Svc.Chat.PrintError("[OBSPlugin] Command requires a subcommand followed by an audio device name: 'mute <device_name>' or 'unmute <device_name>'.");
                return;
            }

            int firstSpaceIndex = args.IndexOf(' ');

            string command = args;
            string? systemName = null;

            if (firstSpaceIndex != -1)
            {
                command = args.Substring(0, firstSpaceIndex).ToLowerInvariant();
                systemName = args.Substring(firstSpaceIndex + 1).Trim();
            }

            if (command.Equals("mute") || command.Equals("unmute"))
            {
                if (string.IsNullOrWhiteSpace(systemName))
                {
                    Svc.Chat.PrintError("[OBSPlugin] Audio commands need an audio device name to function.");
                    return;
                }

                switch (command)
                {
                    case "mute":
                        _obs.SetInputMute(systemName, true);
                        Svc.Chat.Print($"[OBSPlugin] Muted {systemName}.");
                        break;

                    case "unmute":
                        _obs.SetInputMute(systemName, false);
                        Svc.Chat.Print($"[OBSPlugin] Unmuted {systemName}.");
                        break;
                }
            }
            else
            {
                Svc.Chat.PrintError("[OBSPlugin] Valid commands are 'mute <device_name>' and 'unmute <device_name>'.");
            }
        }
    }
}
