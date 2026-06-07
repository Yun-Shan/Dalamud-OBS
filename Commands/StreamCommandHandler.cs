using System;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class StreamCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly Func<OutputStatus> _getStreamStatus;

        public StreamCommandHandler(IOBSWebsocket obs, Func<OutputStatus> getStreamStatus)
        {
            _obs = obs;
            _getStreamStatus = getStreamStatus;
        }

        public void HandleStreamCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Svc.Chat.PrintError("[OBSPlugin] Stream command requires a subcommand: 'start' or 'stop'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (!_getStreamStatus().IsActive)
                    {
                        _obs.StartStream();
                        Svc.Chat.Print("[OBSPlugin] Started stream.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] The stream is already active.");
                    }
                    break;

                case "stop":
                    if (_getStreamStatus().IsActive)
                    {
                        _obs.StopStream();
                        Svc.Chat.Print("[OBSPlugin] Stopped stream.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] The stream is not active.");
                    }
                    break;

                default:
                    Svc.Chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start' or 'stop'.");
                    break;
            }
        }
    }
}
