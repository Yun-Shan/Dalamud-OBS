using System;
using OBSWebsocketDotNet;

namespace OBSPlugin
{
    public class SceneCommandHandler
    {
        private readonly IOBSWebsocket _obs;

        public SceneCommandHandler(IOBSWebsocket obs)
        {
            _obs = obs;
        }

        public void HandleSceneCommand(string args)
        {
            const string changeKeyword = "change";

            int firstSpaceIndex = args.IndexOf(' ');

            if (firstSpaceIndex == -1)
            {
                Svc.Chat.PrintError("[OBSPlugin] Valid subcommand is 'change <scene_name>'");
                return;
            }

            string command = args.Substring(0, firstSpaceIndex);
            string sceneName = args.Substring(firstSpaceIndex + 1).Trim();

            if (!command.Equals(changeKeyword))
            {
                Svc.Chat.PrintError("[OBSPlugin] Valid subcommand is 'change <scene_name>'");
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Svc.Chat.PrintError("[OBSPlugin] Please provide a scene name to change to.");
                return;
            }

            _obs.SetCurrentProgramScene(sceneName);
            Svc.Chat.Print($"[OBSPlugin] Scene changed to {sceneName}.");
        }
    }
}
