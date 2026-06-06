using System;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class RecordCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly IChatGui _chat;
        private readonly Func<RecordingStatus> _getRecordStatus;

        public RecordCommandHandler(IOBSWebsocket obs, IChatGui chat, Func<RecordingStatus> getRecordStatus)
        {
            _obs = obs;
            _chat = chat;
            _getRecordStatus = getRecordStatus;
        }

        public void HandleRecordCommand(string args, IChatGui chat)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                chat.PrintError("[OBSPlugin] Record command requires a subcommand: 'start', 'stop', 'pause', or 'resume'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (!_getRecordStatus().IsRecording)
                    {
                        _obs.StartRecord();
                        chat.Print("[OBSPlugin] Started recording.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] Recording is already active.");
                    }
                    break;

                case "stop":
                    if (_getRecordStatus().IsRecording)
                    {
                        _obs.StopRecord();
                        chat.Print("[OBSPlugin] Stopped recording.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] Recording is not active.");
                    }
                    break;

                case "pause":
                    if (!_getRecordStatus().IsRecordingPaused && _getRecordStatus().IsRecording)
                    {
                        _obs.PauseRecord();
                        chat.Print("[OBSPlugin] Paused recording.");
                    }
                    else if (_getRecordStatus().IsRecordingPaused)
                    {
                        chat.PrintError("[OBSPlugin] Recording is already paused.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] Cannot pause as recording is not active.");
                    }
                    break;

                case "resume":
                    if (_getRecordStatus().IsRecordingPaused)
                    {
                        _obs.ResumeRecord();
                        chat.Print("[OBSPlugin] Resumed recording.");
                    }
                    else if (!_getRecordStatus().IsRecording)
                    {
                        chat.PrintError("[OBSPlugin] Cannot resume as recording is not active.");
                    }
                    else
                    {
                        chat.PrintError("[OBSPlugin] Recording is not paused.");
                    }
                    break;

                default:
                    chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start', 'stop', 'pause', or 'resume'.");
                    break;
            }
        }
    }
}
