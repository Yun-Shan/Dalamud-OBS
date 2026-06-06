using System;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class StreamCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly IChatGui _chat;
        private readonly Func<OutputStatus> _getStreamStatus;

        public StreamCommandHandler(IOBSWebsocket obs, IChatGui chat, Func<OutputStatus> getStreamStatus)
        {
            _obs = obs;
            _chat = chat;
            _getStreamStatus = getStreamStatus;
        }

        public void HandleStreamCommand(string args, IChatGui chat)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                chat.PrintError("[OBSPlugin] Stream command requires a subcommand: 'start' or 'stop'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (!_getStreamStatus().IsActive)
                    {
                        _obs.StartStream();
                        chat.Print("[OBSPlugin] Started stream.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] The stream is already active.");
                    }
                    break;

                case "stop":
                    if (_getStreamStatus().IsActive)
                    {
                        _obs.StopStream();
                        chat.Print("[OBSPlugin] Stopped stream.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] The stream is not active.");
                    }
                    break;

                default:
                    chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start' or 'stop'.");
                    break;
            }
        }
    }
}
