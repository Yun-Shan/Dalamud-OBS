using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSPlugin.Objects;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;

namespace OBSPlugin
{
    public class BlurTab
    {
        private readonly Configuration _config;
        private readonly BlurManager _blurManager;
        private int _uiErrorCount = 0;

        public BlurTab(Configuration config, BlurManager blurManager)
        {
            _config = config;
            _blurManager = blurManager;
        }

        public void Draw()
        {
            if (ImGui.Checkbox("Enable Blur", ref _config.EnableBlur))
            {
                if (!_config.EnableBlur)
                {
                    foreach (var blur in _blurManager.BlurDict.Values)
                    {
                        _blurManager.BlurItemsToRemove.Add((Blur)blur.Clone());
                    }
                    _blurManager.BlurDict.Clear();
                }
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Master switch for all blur functionality.");
            ImGui.Separator();
            if (ImGui.Checkbox("UI Detection", ref _config.UIDetection))
            {
                _uiErrorCount = 0;
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Detect game UI elements together with game-rendering.\n" +
                    "May cause performance issues.\n" +
                    "If disabled, you need to manually call \"/obs update\" to update the blurs in obs.");
            if (ImGui.Checkbox("Asynchronous Update", ref _config.BlurAsync))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Asynchronizly update the blur filters in obs.\n" +
                    "Asynchronizly update will cause slight delays in updating the filters but has a better performance.");
            if (ImGui.Checkbox("Draw Blur Rect", ref _config.DrawBlurRect))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Draw the blurred content with a blue boundary.");
            if (ImGui.InputText("Source Name", ref _config.SourceName, 128))
            {
                _config.Save();
            }
            if (ImGui.DragInt("Blur Size", ref _config.BlurSize, 1, 1, 16))
            {
                foreach (var blur in _blurManager.BlurDict.Values)
                {
                    blur.Size = _config.BlurSize;
                    _blurManager.BlurItemsToAdd.Add((Blur)blur.Clone());
                }
                _config.Save();
            }
            ImGui.Separator();
            if (ImGui.Checkbox("ChatLog", ref _config.ChatLogBlur))
            {
                if (!_config.ChatLogBlur)
                {
                    Blur? chatLogBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("ChatLog", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (_blurManager.BlurDict.TryGetValue("ChatLogPanel_0", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (_blurManager.BlurDict.TryGetValue("ChatLogPanel_1", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (_blurManager.BlurDict.TryGetValue("ChatLogPanel_2", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (_blurManager.BlurDict.TryGetValue("ChatLogPanel_3", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                }
                _config.Save();
            }
            if (ImGui.Checkbox("PartyList", ref _config.PartyListBlur))
            {
                if (!_config.PartyListBlur)
                {
                    var blursToTurnOff = _blurManager.BlurDict.Values.Where(blur => blur.Name.StartsWith("PartyList"));
                    foreach (Blur blur in blursToTurnOff)
                    {
                        blur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", blur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)blur.Clone());
                    }
                }
                _config.Save();
            }
            if (ImGui.Checkbox("Target", ref _config.TargetBlur))
            {
                if (!_config.TargetBlur)
                {
                    Blur? targetBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("Target", out targetBlur))
                    {
                        targetBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", targetBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)targetBlur.Clone());
                    }
                }
                _config.Save();
            }

            if (ImGui.Checkbox("TargetTarget", ref _config.TargetTargetBlur))
            {
                if (!_config.TargetTargetBlur)
                {
                    Blur? targetTargetBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("TargetTarget", out targetTargetBlur))
                    {
                        targetTargetBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", targetTargetBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)targetTargetBlur.Clone());
                    }
                }
                _config.Save();
            }

            if (ImGui.Checkbox("FocusTarget", ref _config.FocusTargetBlur))
            {
                if (!_config.FocusTargetBlur)
                {
                    Blur? focusTargetBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("FocusTarget", out focusTargetBlur))
                    {
                        focusTargetBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", focusTargetBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)focusTargetBlur.Clone());
                    }
                }
                _config.Save();
            }
            if (ImGui.Checkbox("Character", ref _config.CharacterBlur))
            {
                if (!_config.CharacterBlur)
                {
                    Blur? characterBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("Character", out characterBlur))
                    {
                        characterBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", characterBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)characterBlur.Clone());
                    }
                }
                _config.Save();

            }
            if (ImGui.Checkbox("FriendList", ref _config.FriendListBlur))
            {
                if (!_config.FriendListBlur)
                {
                    Blur? friendListBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("FriendList", out friendListBlur))
                    {
                        friendListBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", friendListBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)friendListBlur.Clone());
                    }
                }
                _config.Save();
            }
            if (ImGui.Checkbox("Hotbar", ref _config.HotbarBlur))
            {
                if (!_config.HotbarBlur)
                {
                    var hotbars = _blurManager.BlurDict.Where(x => x.Key.Length >= 8 && x.Key[..6] == "Hotbar");
                    if (hotbars.Any())
                    {
                        Svc.PluginLog.Debug("Turn off HotbarBlur");
                        foreach (var i in hotbars)
                        {
                            i.Value.Enabled = false;
                            _blurManager.BlurItemsToAdd.Add((Blur)i.Value.Clone());
                        }
                    }
                }
                _config.Save();
            }
            if (_config.HotbarBlur)
            {
                ImGui.SameLine();
                var numbers = string.Join(",", _config.BlurredHotbars.Select(x => x.ToString()));
                ImGui.InputTextWithHint(string.Empty, "hotbar number splitted by comma", ref numbers, 32);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Adding hotbar will effect instantly, but remove need to re-enable hotbar blur to make it change.");
                try
                {
                    _config.BlurredHotbars = numbers.Trim().Replace("，", ",").Split(",").Select(x => int.Parse(x)).ToArray();
                }
                catch { }
            }
            if (ImGui.Checkbox("CastBar", ref _config.CastBarBlur))
            {
                if (!_config.CastBarBlur)
                {
                    Blur? castbarBlur = null;
                    if (_blurManager.BlurDict.TryGetValue("CastBar", out castbarBlur))
                    {
                        castbarBlur.Enabled = false;
                        Svc.PluginLog.Debug("Turn off {0}", castbarBlur.Name);
                        _blurManager.BlurItemsToAdd.Add((Blur)castbarBlur.Clone());
                    }
                }
                _config.Save();
            }
            if (_config.BlurAsync)
            {
                ImGui.Separator();
                ImGui.Text($"Current #BlurItemsToAdd: {_blurManager.BlurItemsToAdd.Count}");
                ImGui.Text($"Current #BlurItemsToRemove: {_blurManager.BlurItemsToRemove.Count}");
            }
        }
    }
}