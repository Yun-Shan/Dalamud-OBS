using System;
using OBSWebsocketDotNet;
using Dalamud.Plugin.Services;

namespace OBSPlugin
{
    public class AudioCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly IChatGui _chat;

        public AudioCommandHandler(IOBSWebsocket obs, IChatGui chat)
        {
            _obs = obs;
            _chat = chat;
        }

        public void HandleAudioCommand(string args, IChatGui chat)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                chat.PrintError("[OBSPlugin] Command requires a subcommand followed by an audio device name: 'mute <device_name>' or 'unmute <device_name>'.");
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
                    chat.PrintError("[OBSPlugin] Audio commands need an audio device name to function.");
                    return;
                }

                switch (command)
                {
                    case "mute":
                        _obs.SetInputMute(systemName, true);
                        chat.Print($"[OBSPlugin] Muted {systemName}.");
                        break;

                    case "unmute":
                        _obs.SetInputMute(systemName, false);
                        chat.Print($"[OBSPlugin] Unmuted {systemName}.");
                        break;
                }
            }
            else
            {
                chat.PrintError("[OBSPlugin] Valid commands are 'mute <device_name>' and 'unmute <device_name>'.");
            }
        }
    }
}
