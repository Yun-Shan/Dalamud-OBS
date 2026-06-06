using System;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class ReplayCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly IChatGui _chat;
        private readonly OutputState _obsReplayBufferStatus;

        public ReplayCommandHandler(IOBSWebsocket obs, IChatGui chat, OutputState obsReplayBufferStatus)
        {
            _obs = obs;
            _chat = chat;
            _obsReplayBufferStatus = obsReplayBufferStatus;
        }

        public void HandleReplayCommand(string args, IChatGui chat)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                chat.PrintError("[OBSPlugin] Replay command requires a subcommand: 'start', 'save', or 'stop'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (_obsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.StartReplayBuffer();
                        chat.Print("[OBSPlugin] Started replay buffer.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] The replay buffer is already active.");
                    }
                    break;

                case "save":
                    if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.SaveReplayBuffer();
                        chat.Print("[OBSPlugin] Replay saved: " + _obs.GetLastReplayBufferReplay());
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] The replay buffer is not active.");
                    }
                    break;

                case "stop":
                    if (_obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                    {
                        _obs.StopReplayBuffer();
                        chat.Print("[OBSPlugin] Stopped replay buffer.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] The replay buffer is not active.");
                    }
                    break;

                default:
                    chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start', 'save', or 'stop'.");
                    break;
            }
        }
    }
}
