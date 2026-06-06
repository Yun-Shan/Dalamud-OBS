using System;
using Dalamud.Plugin.Services;
using OBSWebsocketDotNet;

namespace OBSPlugin
{
    public class SceneCommandHandler
    {
        private readonly IOBSWebsocket _obs;
        private readonly IChatGui _chat;

        public SceneCommandHandler(IOBSWebsocket obs, IChatGui chat)
        {
            _obs = obs;
            _chat = chat;
        }

        public void HandleSceneCommand(string args, IChatGui chat)
        {
            const string changeKeyword = "change";

            int firstSpaceIndex = args.IndexOf(' ');

            if (firstSpaceIndex == -1)
            {
                chat.PrintError("[OBSPlugin] Valid subcommand is 'change <scene_name>'");
                return;
            }

            string command = args.Substring(0, firstSpaceIndex);
            string sceneName = args.Substring(firstSpaceIndex + 1).Trim();

            if (!command.Equals(changeKeyword))
            {
                chat.PrintError("[OBSPlugin] Valid subcommand is 'change <scene_name>'");
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                chat.PrintError("[OBSPlugin] Please provide a scene name to change to.");
                return;
            }

            _obs.SetCurrentProgramScene(sceneName);
            chat.Print($"[OBSPlugin] Scene changed to {sceneName}.");
        }
    }
}
