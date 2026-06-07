using System;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class ReplayCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly OutputState _obsReplayBufferStatus;

        public ReplayCommandHandler(IOBSWebsocket obs, OutputState obsReplayBufferStatus)
        {
            _obs = obs;
            _obsReplayBufferStatus = obsReplayBufferStatus;
        }

        public void HandleReplayCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Svc.Chat.PrintError("[OBSPlugin] Replay command requires a subcommand: 'start', 'save', or 'stop'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (_obsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.StartReplayBuffer();
                        Svc.Chat.Print("[OBSPlugin] Started replay buffer.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] The replay buffer is already active.");
                    }
                    break;

                case "save":
                    if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.SaveReplayBuffer();
                        Svc.Chat.Print("[OBSPlugin] Replay saved: " + _obs.GetLastReplayBufferReplay());
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] The replay buffer is not active.");
                    }
                    break;

                case "stop":
                    if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.StopReplayBuffer();
                        Svc.Chat.Print("[OBSPlugin] Stopped replay buffer.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] The replay buffer is not active.");
                    }
                    break;

                default:
                    Svc.Chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start', 'save', or 'stop'.");
                    break;
            }
        }
    }
}
