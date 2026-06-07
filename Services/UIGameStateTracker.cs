using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using OBSPlugin.Objects;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OBSPlugin
{
    public class UIGameStateTracker
    {
        private readonly Configuration _config;
        private readonly BlurManager _blurManager;

        Blur[] PartyMemberBlurList = new Blur[8];

        public UIGameStateTracker(Configuration config, BlurManager blurManager)
        {
            _config = config;
            _blurManager = blurManager;
        }

        public unsafe void UpdateGameUI()
        {
            if (!_config.Enabled) return;
            if (!_config.EnableBlur) return;
            if (Svc.ObjectTable.LocalPlayer == null) return;
            try
            {
                UpdateChatLog();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating ChatLog UI: {0}", e);
                _config.ChatLogBlur = false;
                _config.Save();
            }
            try
            {
                UpdatePartyList();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating PartyList UI: {0}", e);
                _config.PartyListBlur = false;
                _config.Save();
            }
            try
            {
                UpdateTarget();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating Target UI: {0}", e);
                _config.TargetBlur = false;
                _config.TargetTargetBlur = false;
                _config.Save();
            }
            try
            {
                UpdateFocusTarget();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating FocusTarget UI: {0}", e);
                _config.FocusTargetBlur = false;
                _config.Save();
            }
            try
            {
                UpdateNamePlate();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating NamePlate UI: {0}", e);
                _config.NamePlateBlur = false;
                _config.Save();
            }
            try
            {
                UpdateCharacter();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating Character UI: {0}", e);
                _config.CharacterBlur = false;
                _config.Save();
            }
            try
            {
                UpdateFridendList();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating FriendList UI: {0}", e);
                _config.FriendListBlur = false;
                _config.Save();
            }
            try
            {
                UpdateHotbar();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating Hotbar UI: {0}", e);
                _config.HotbarBlur = false;
                _config.Save();
            }
            try
            {
                UpdateCastBar();
            }
            catch (Exception e)
            {
                Svc.PluginLog.Error("Error Updating CastBar UI: {0}", e);
                _config.CastBarBlur = false;
                _config.Save();
            }
        }

        private unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }

            return pos;
        }

        private unsafe Vector2 GetFloatingNodePosition(AtkResNode* node)
        {
            if (node == null) return new Vector2(0, 0);
            var scaledPosX = node->X - (node->Width * ((node->ScaleX - 1) / 2));
            var scaledPosY = node->Y - (node->Height * ((node->ScaleY - 1)));
            var pos = new Vector2(scaledPosX, scaledPosY);
            var par = node->ParentNode;
            if (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += this.GetFloatingNodePosition(node->ParentNode);
            }

            return pos;
        }

        private unsafe Vector2 GetNodeScale(AtkResNode* node)
        {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null)
            {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }

            return scale;
        }

        private unsafe bool GetNodeVisible(AtkResNode* node)
        {
            if (node == null) return false;

            while (node != null)
            {
                if (!node->IsVisible()) return false;
                node = node->ParentNode;
            }

            return true;
        }

        private unsafe (float, float, float, float) GetUIRect(float X, float Y, float W, float H)
        {
            var size = ImGui.GetIO().DisplaySize;
            var width = size.X;
            var height = size.Y;
            var top = Y / height * 100;
            var left = X / width * 100;
            var bottom = (height - Y - H) / height * 100;
            var right = (width - X - W) / width * 100;
            return (top, bottom, left, right);
        }

        private unsafe Blur GetBlurFromNode(AtkResNode* node, string name, bool floating = false, bool? enabled = null)
        {
            var position = floating ? GetFloatingNodePosition(node) : GetNodePosition(node);
            var scale = GetNodeScale(node);
            var nodeVisible = GetNodeVisible(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            if (_config.DrawBlurRect && nodeVisible
                && _blurManager.BlurDict.TryGetValue(name, out Blur? existingBlur)
                && existingBlur.Enabled)
                ImGui.GetForegroundDrawList(ImGui.GetMainViewport()).AddRect(position, position + size, 0xFFFF0000);
            var (top, bottom, left, right) = GetUIRect(position.X,
                position.Y,
                size.X,
                size.Y);
            Blur blur = new(name, top, bottom, left, right, _config.BlurSize);
            blur.Enabled = enabled == null ? nodeVisible : (bool)enabled;
            return blur;
        }

        private unsafe void UpdateChatLog()
        {
            if (!_config.ChatLogBlur) return;

            try
            {
                var panel = GetChatLogPanelVisiblity();

                UpdateChatLogPanel("ChatLog");
                UpdateChatLogPanel("ChatLogPanel_1", panel["ChatLogPanel_1"]);
                UpdateChatLogPanel("ChatLogPanel_2", panel["ChatLogPanel_2"]);
                UpdateChatLogPanel("ChatLogPanel_3", panel["ChatLogPanel_3"]);
            }
            catch (Exception)
            {
                return;
            }

        }

        private unsafe void UpdateChatLogPanel(string ChatLogWindowName, bool followUI = true)
        {
            var chatLog = Svc.GameGui.GetAddonByName(ChatLogWindowName, 1);
            if (chatLog.IsNull) return;
            unsafe
            {
                var chatLogPtr = (AtkUnitBase*)chatLog.Address;
                if (chatLogPtr->UldManager.NodeListCount <= 0) return;
                var chatLogNode = chatLogPtr->UldManager.NodeList[0];
                bool? visiblity = followUI ? null : false;
                UpdateBlur(GetBlurFromNode(chatLogNode, ChatLogWindowName, false, visiblity));
            }
        }

        private unsafe Dictionary<string, bool> GetChatLogPanelVisiblity()
        {

            var chatLog = Svc.GameGui.GetAddonByName("ChatLog", 1);
            if (chatLog.IsNull) throw new Exception("ChatLog get faild!");
            unsafe
            {
                var chatLogPtr = (AtkUnitBase*)chatLog.Address;
                if (chatLogPtr->UldManager.NodeListCount <= 0) throw new Exception("ChatLog's children is empty!");

                return new Dictionary<string, bool>
                {
                    { "ChatLogPanel_1", !GetNodeVisible(chatLogPtr->UldManager.NodeList[13]) },
                    { "ChatLogPanel_2", !GetNodeVisible(chatLogPtr->UldManager.NodeList[12]) },
                    { "ChatLogPanel_3", !GetNodeVisible(chatLogPtr->UldManager.NodeList[11]) }
                };
            }

        }

        private unsafe void UpdatePartyList()
        {
            if (!_config.PartyListBlur) return;
            HashSet<string> existingBlur = new();
            uint partyMemberCount = 0;
            var partyList = Svc.GameGui.GetAddonByName("_PartyList", 1);
            if (partyList.IsNull) return;
            unsafe
            {
                var partyListPtr = (AtkUnitBase*)partyList.Address;
                for (var i = 0; i < partyListPtr->UldManager.NodeListCount; i++)
                {
                    var childNode = partyListPtr->UldManager.NodeList[i];
                    var IsVisible = GetNodeVisible(childNode);
                    if (childNode != null && (int)childNode->Type == 1006 && IsVisible)
                    {
                        for (var j = 0; j < childNode->GetAsAtkComponentNode()->Component->UldManager.NodeListCount; j++)
                        {
                            var childChildNode = childNode->GetAsAtkComponentNode()->Component->UldManager.NodeList[j];
                            var childChildIsVisible = GetNodeVisible(childChildNode);
                            if (childChildNode != null && childChildNode->Type == NodeType.Text)
                            {
                                if (childChildNode->NodeId == 17 && childChildIsVisible)
                                {
                                    PartyMemberBlurList[partyMemberCount] = GetBlurFromNode(childChildNode, $"PartyList_{partyMemberCount}");
                                    existingBlur.Add($"PartyList_{partyMemberCount}");
                                    partyMemberCount++;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            var blursToRemove = _blurManager.BlurDict.Values.Where(blur => blur.Name.StartsWith("PartyList") && !existingBlur.Contains(blur.Name));
            if (blursToRemove.Any())
            {
                blursToRemove.ToList().ForEach(blur =>
                {
                    _blurManager.BlurItemsToRemove.Add(blur);
                    _blurManager.BlurDict.Remove(blur.Name);
                });
            }
            for (int i = 0; i < partyMemberCount; i++)
            {
                UpdateBlur(PartyMemberBlurList[i]);
            }
        }

        private unsafe void UpdateNamePlate()
        {
            if (!_config.NamePlateBlur) return;
            Dictionary<string, Blur> namePlateBlurMap = new();
            HashSet<string> existingBlur = new();
            var namePlate = Svc.GameGui.GetAddonByName("NamePlate", 1);
            if (namePlate.IsNull) return;
            unsafe
            {
                var namePlatePtr = (AtkUnitBase*)namePlate.Address;
                for (var i = 0; i < namePlatePtr->UldManager.NodeListCount; i++)
                {
                    var childNode = namePlatePtr->UldManager.NodeList[i];
                    var IsVisible = GetNodeVisible(childNode);
                    if (childNode != null && (int)childNode->Type == 1001 && IsVisible)
                    {
                        var collisionNode = childNode->GetAsAtkComponentNode()->Component->UldManager.NodeList[0];
                        var collisionNodeIsVisible = GetNodeVisible(collisionNode);
                        if (collisionNode != null && collisionNode->Type == NodeType.Collision && collisionNodeIsVisible)
                        {
                            string blurName = $"NamePlate_{(ulong)collisionNode:X}";
                            namePlateBlurMap[blurName] = GetBlurFromNode(collisionNode, blurName, true);
                        }
                    }
                }
            }
            var namePlateBlurList = namePlateBlurMap.OrderBy(
                    pair => Math.Pow((pair.Value.Top - pair.Value.Bottom) / 2, 2) +
                            Math.Pow((pair.Value.Left - pair.Value.Right) / 2, 2)
                ).Take(_config.MaxNamePlateCount);
            foreach (KeyValuePair<string, Blur> pair in namePlateBlurList)
            {
                UpdateBlur(pair.Value);
                existingBlur.Add(pair.Value.Name);
            }

            var blursToRemove = _blurManager.BlurDict.Values.Where(blur => blur.Name.StartsWith("NamePlate") && !existingBlur.Contains(blur.Name));
            if (blursToRemove.Any())
            {
                blursToRemove.ToList().ForEach(blur =>
                {
                    _blurManager.BlurItemsToRemove.Add(blur);
                    _blurManager.BlurDict.Remove(blur.Name);
                });
            }
        }


        private unsafe void UpdateTarget()
        {
            if (!_config.TargetBlur && !_config.TargetTargetBlur) return;
            Blur? targetBlur = null;
            Blur? targetTargetBlur = null;
            var targetInfo = Svc.GameGui.GetAddonByName("_TargetInfo", 1);
            if (targetInfo.IsNull) return;
            unsafe
            {
                var targetInfoPtr = (AtkUnitBase*)targetInfo.Address;
                if (!GetNodeVisible(targetInfoPtr->UldManager.NodeList[0]))
                {
                    targetInfo = Svc.GameGui.GetAddonByName("_TargetInfoMainTarget", 1);
                    if (!targetInfo.IsNull)
                    {
                        targetInfoPtr = (AtkUnitBase*)targetInfo.Address;
                    }
                }
                int textIndex = 0;
                int totalText = 0;
                for (var i = 0; i < targetInfoPtr->UldManager.NodeListCount; i++)
                {
                    var childNode = targetInfoPtr->UldManager.NodeList[i];
                    if (childNode != null && childNode->Type == NodeType.Text)
                    {
                        totalText++;
                    }
                }
                for (var i = 0; i < targetInfoPtr->UldManager.NodeListCount; i++)
                {
                    var childNode = targetInfoPtr->UldManager.NodeList[i];
                    var IsVisible = GetNodeVisible(childNode);
                    if (childNode != null && childNode->Type == NodeType.Text)
                    {
                        if (IsVisible)
                        {
                            if (textIndex == 2)
                            {
                                targetBlur = GetBlurFromNode(childNode, "Target");
                            }
                            else if (textIndex == totalText - 1)
                            {
                                targetTargetBlur = GetBlurFromNode(childNode, "TargetTarget");
                            }
                        }
                        textIndex++;
                    }
                }
            }
            if (_config.TargetBlur)
            {
                if (targetBlur == null)
                {
                    if (_blurManager.BlurDict.TryGetValue("Target", out Blur? blurToRemove))
                    {
                        _blurManager.BlurItemsToRemove.Add(blurToRemove);
                        _blurManager.BlurDict.Remove(blurToRemove.Name);
                    }
                }
                else
                {
                    UpdateBlur(targetBlur);
                }
            }

            if (_config.TargetTargetBlur)
            {
                if (targetTargetBlur == null)
                {
                    if (_blurManager.BlurDict.TryGetValue("TargetTarget", out Blur? blurToRemove))
                    {
                        _blurManager.BlurItemsToRemove.Add(blurToRemove);
                        _blurManager.BlurDict.Remove(blurToRemove.Name);
                    }
                }
                else
                {
                    UpdateBlur(targetTargetBlur);
                }
            }
        }

        private unsafe void UpdateFocusTarget()
        {
            if (!_config.FocusTargetBlur) return;
            Blur? focusTargetBlur = null;
            var focusTargetInfo = Svc.GameGui.GetAddonByName("_FocusTargetInfo", 1);
            if (focusTargetInfo.IsNull) return;
            unsafe
            {
                var focusTargetInfoPtr = (AtkUnitBase*)focusTargetInfo.Address;
                for (var i = 0; i < focusTargetInfoPtr->UldManager.NodeListCount; i++)
                {
                    var childNode = focusTargetInfoPtr->UldManager.NodeList[i];
                    var IsVisible = GetNodeVisible(childNode);
                    if (childNode != null && childNode->Type == NodeType.Text && IsVisible)
                    {
                        focusTargetBlur = GetBlurFromNode(childNode, "FocusTarget");
                        break;
                    }
                }
            }
            if (focusTargetBlur == null)
            {
                if (_blurManager.BlurDict.TryGetValue("FocusTarget", out Blur? blurToRemove))
                {
                    _blurManager.BlurItemsToRemove.Add(blurToRemove);
                    _blurManager.BlurDict.Remove(blurToRemove.Name);
                }
            }
            else
            {
                UpdateBlur(focusTargetBlur);
            }
        }

        private unsafe void UpdateCharacter()
        {
            if (!_config.CharacterBlur) return;
            var character = Svc.GameGui.GetAddonByName("Character", 1);
            var characterProfile = Svc.GameGui.GetAddonByName("CharacterProfile", 1);
            if (!character.IsNull)
            {
                unsafe
                {
                    var characterPtr = (AtkUnitBase*)character.Address;
                    if (characterPtr->UldManager.NodeListCount > 0)
                    {
                        var childNode = characterPtr->UldManager.NodeList[82];
                        UpdateBlur(GetBlurFromNode(childNode, "Character"));
                    }
                }
            }
            if (!characterProfile.IsNull)
            {
                unsafe
                {
                    var characterProfilePtr = (AtkUnitBase*)characterProfile.Address;
                    if (characterProfilePtr->UldManager.NodeListCount > 0)
                    {
                        var childNodeProfile = characterProfilePtr->UldManager.NodeList[30];
                        UpdateBlur(GetBlurFromNode(childNodeProfile, "CharacterProfile"));
                    }
                }
            }
        }

        private unsafe void UpdateFridendList()
        {
            if (!_config.FriendListBlur) return;
            var friendList = Svc.GameGui.GetAddonByName("FriendList", 1);
            if (friendList.IsNull) return;
            unsafe
            {
                var friendListPtr = (AtkUnitBase*)friendList.Address;
                if (friendListPtr->UldManager.NodeListCount <= 0) return;
                var childNode = friendListPtr->UldManager.NodeList[8];
                UpdateBlur(GetBlurFromNode(childNode, "FriendList"));
            }
        }

        private unsafe void UpdateHotbar()
        {
            if (!_config.HotbarBlur || !_config.BlurredHotbars.Any()) return;
            foreach (var i in _config.BlurredHotbars)
            {
                var suffix = (i - 1).ToString("00");
                var hotbar = Svc.GameGui.GetAddonByName($"_ActionBar{(suffix == "00" ? string.Empty : suffix)}", 1);
                if (hotbar.IsNull) return;
                unsafe
                {
                    var hotbarPtr = (AtkUnitBase*)hotbar.Address;
                    var childNode = hotbarPtr->UldManager.NodeList[0];
                    UpdateBlur(GetBlurFromNode(childNode, $"Hotbar{suffix}"));
                }
            }
        }

        private unsafe void UpdateCastBar()
        {
            if (!_config.CastBarBlur) return;
            var castbar = Svc.GameGui.GetAddonByName("_CastBar", 1);
            if (castbar.IsNull) return;
            unsafe
            {
                var castbarPtr = (AtkUnitBase*)castbar.Address;
                var childNode = castbarPtr->UldManager.NodeList[1];
                UpdateBlur(GetBlurFromNode(childNode, "CastBar"));
            }
        }

        private unsafe void UpdateCustomAddon(String addonName)
        {
            var addon = Svc.GameGui.GetAddonByName(addonName, 1);
            if (addon.IsNull) return;
            unsafe
            {
                var addonPtr = (AtkUnitBase*)addon.Address;
                if (addonPtr->UldManager.NodeListCount <= 0) return;
                var childNode = addonPtr->UldManager.NodeList[0];
                UpdateBlur(GetBlurFromNode(childNode, addonName));
            }
        }

        private void UpdateBlur(Blur blur)
        {
            _blurManager.UpdateBlur(blur);
        }
    }
}