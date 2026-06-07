using System;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;

namespace OBSPlugin
{
    public class RecordCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly Func<RecordingStatus> _getRecordStatus;

        public RecordCommandHandler(IOBSWebsocket obs, Func<RecordingStatus> getRecordStatus)
        {
            _obs = obs;
            _getRecordStatus = getRecordStatus;
        }

        public void HandleRecordCommand(string args)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Svc.Chat.PrintError("[OBSPlugin] Record command requires a subcommand: 'start', 'stop', 'pause', or 'resume'.");
                return;
            }

            switch (args.ToLowerInvariant())
            {
                case "start":
                    if (!_getRecordStatus().IsRecording)
                    {
                        _obs.StartRecord();
                        Svc.Chat.Print("[OBSPlugin] Started recording.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Recording is already active.");
                    }
                    break;

                case "stop":
                    if (_getRecordStatus().IsRecording)
                    {
                        _obs.StopRecord();
                        Svc.Chat.Print("[OBSPlugin] Stopped recording.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Recording is not active.");
                    }
                    break;

                case "pause":
                    if (!_getRecordStatus().IsRecordingPaused && _getRecordStatus().IsRecording)
                    {
                        _obs.PauseRecord();
                        Svc.Chat.Print("[OBSPlugin] Paused recording.");
                    }
                    else if (_getRecordStatus().IsRecordingPaused)
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Recording is already paused.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Cannot pause as recording is not active.");
                    }
                    break;

                case "resume":
                    if (_getRecordStatus().IsRecordingPaused)
                    {
                        _obs.ResumeRecord();
                        Svc.Chat.Print("[OBSPlugin] Resumed recording.");
                    }
                    else if (!_getRecordStatus().IsRecording)
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Cannot resume as recording is not active.");
                    }
                    else
                    {
                        Svc.Chat.PrintError("[OBSPlugin] Recording is not paused.");
                    }
                    break;

                default:
                    Svc.Chat.PrintError($"[OBSPlugin] '{args}' is not a valid subcommand. Valid subcommands are 'start', 'stop', 'pause', or 'resume'.");
                    break;
            }
        }
    }
}
