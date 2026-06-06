using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json.Linq;
using OBSPlugin.Objects;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using System.IO;
using System.Threading;

namespace OBSPlugin
{
    public class PluginUI
    {
        private readonly Plugin Plugin;
        private bool isThreadRunning = true;
        internal BlockingCollection<Blur> BlurItemsToAdd = new(10000);
        internal BlockingCollection<Blur> BlurItemsToRemove = new(10000);
        public Dictionary<string, Blur> BlurDict = new();
        public Configuration Config => Plugin.config;
        private int UIErrorCount = 0;
        Blur[] PartyMemberBlurList = new Blur[8];

        private string lastDutyEvent = "";
        private DateTime lastDutyEventTime = DateTime.MinValue;
        private OrderedDictionary<string, OrderedDictionary<string, List<string>>> debugDutyTree = new();
        private bool debugDutyTreeCached = false;

        public bool IsVisible { get; set; }
        public PluginUI(Plugin plugin)
        {
            Plugin = plugin;
            InitAddConsuming();
            InitRemoveConsuming();
            InitDutyEventHandlers();
        }

        private void InitDutyEventHandlers()
        {
            var dutyState = Plugin.DutyState;
            dutyState.DutyStarted += _ =>
            {
                lastDutyEvent = "副本已开始";
                lastDutyEventTime = DateTime.Now;
            };
            dutyState.DutyWiped += _ =>
            {
                lastDutyEvent = "副本已灭团";
                lastDutyEventTime = DateTime.Now;
            };
            dutyState.DutyRecommenced += _ =>
            {
                lastDutyEvent = "副本已重开";
                lastDutyEventTime = DateTime.Now;
            };
            dutyState.DutyCompleted += _ =>
            {
                lastDutyEvent = "副本已完成";
                lastDutyEventTime = DateTime.Now;
            };
        }

        private void CacheDutyTree()
        {
            if (debugDutyTreeCached) return;
            debugDutyTree.Clear();

            var sheet = Plugin.Data.GetExcelSheet<ContentFinderCondition>();
            if (sheet == null) return;

            var sortedRows = sheet
                .OrderBy(row => row.ContentType.Value.RowId)
                .ThenBy(row => row.ContentUICategory.Value.RowId)
                .ThenBy(row => row.RowId);

            foreach (var row in sortedRows)
            {
                var contentType = string.IsNullOrEmpty(row.ContentType.Value.Name.ToString()) ? "未知" : row.ContentType.Value.Name.ToString();
                var uiCategory = string.IsNullOrEmpty(row.ContentUICategory.Value.Name.ToString()) ? "未知" : row.ContentUICategory.Value.Name.ToString();
                var name = string.IsNullOrEmpty(row.Name.ToString()) ? "未知" : row.Name.ToString();

                if (!debugDutyTree.ContainsKey(contentType))
                    debugDutyTree[contentType] = new OrderedDictionary<string, List<string>>();

                var uiCategoryDict = (OrderedDictionary<string, List<string>>)debugDutyTree[contentType]!;

                if (!uiCategoryDict.ContainsKey(uiCategory))
                    uiCategoryDict[uiCategory] = new List<string>();

                var nameList = uiCategoryDict[uiCategory]!;

                if (!nameList.Contains(name))
                    nameList.Add(name);
            }

            debugDutyTreeCached = true;
        }

        private void InitAddConsuming()
        {
            Task.Run(() =>
            {
                while (!BlurItemsToAdd.IsCompleted && isThreadRunning)
                {
                    Blur? blur = null;
                    try
                    {
                        blur = BlurItemsToAdd.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (blur != null)
                    {
                        if (BlurDict.TryGetValue(blur.Name, out Blur? latestBlur))
                        {
                            if (blur.LastEdit.CompareTo(latestBlur.LastEdit) < 0)
                            {
                                continue;
                            }

                        }
                        OBSAddOrUpdateBlur(blur);
                    }
                }
                Plugin.PluginLog.Information("No more OBS blurs to add.");
            });

        }
        private void InitRemoveConsuming()
        {
            Task.Run(() =>
            {
                while (!BlurItemsToRemove.IsCompleted && isThreadRunning)
                {
                    Blur? blur = null;
                    try
                    {
                        blur = BlurItemsToRemove.Take();
                    }
                    catch (InvalidOperationException) { }

                    if (blur != null)
                    {
                        OBSRemoveBlur(blur);
                    }
                }
                Plugin.PluginLog.Information("No more OBS blurs to add.");
            });
        }

        public void Draw()
        {
            try
            {
                Plugin.stopWatchHook?.Update();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error at updating stopwatch: {0}", e);
            }

            if (Config.UIDetection)
                UpdateGameUI();

            if (!IsVisible)
                return;

            ImGui.SetNextWindowSize(new Vector2(530, 450), ImGuiCond.FirstUseEver);
            bool configOpen = IsVisible;
            if (ImGui.Begin("OBS 插件配置", ref configOpen))
            {
                IsVisible = configOpen;
                if (ImGui.BeginTabBar("TabBar"))
                {
                    if (ImGui.BeginTabItem("连接##Tab"))
                    {
                        if (ImGui.BeginChild("Connection##SettingsRegion"))
                        {
                            DrawConnectionSettings();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("直播##Tab"))
                    {
                        if (ImGui.BeginChild("Stream##SettingsRegion"))
                        {
                            DrawStream();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("录制##Tab"))
                    {
                        if (ImGui.BeginChild("Record##SettingsRegion"))
                        {
                            DrawRecord();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("回放##Tab"))
                    {
                        if (ImGui.BeginChild("Replay##SettingsRegion"))
                        {
                            DrawReplay();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("模糊##Tab"))
                    {
                        if (ImGui.BeginChild("Blur##SettingsRegion"))
                        {
                            DrawBlurSettings();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("关于##Tab"))
                    {
                        if (ImGui.BeginChild("Blur##SettingsRegion"))
                        {
                            DrawAbout();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    if (ImGui.BeginTabItem("调试##Tab"))
                    {
                        if (ImGui.BeginChild("Debug##SettingsRegion"))
                        {
                            DrawDebug();
                            ImGui.EndChild();
                        }
                        ImGui.EndTabItem();
                    }
                    ImGui.EndTabBar();
                }
                ImGui.End();
            }

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

        private bool OBSAddOrUpdateBlur(Blur blur)
        {
            if (!Plugin.Connected) return false;

            string sourceName = Config.SourceName;
            try
            {
                FilterSettings? filter = null;
                var settings = new JObject();
                bool created = false;
                try
                {
                    filter = Plugin.obs.GetSourceFilter(sourceName, blur.Name);
                    settings = filter.Settings;
                }
                catch
                {
                    created = true;
                }

                /* StreamFX settings*/
                //settings["Filter.Blur.Mask"] = true;
                //settings["Filter.Blur.Mask.Region.Top"] = blur.Top;
                //settings["Filter.Blur.Mask.Region.Bottom"] = blur.Bottom;
                //settings["Filter.Blur.Mask.Region.Left"] = blur.Left;
                //settings["Filter.Blur.Mask.Region.Right"] = blur.Right;
                //settings["Filter.Blur.Mask.Type"] = 0;
                //settings["Filter.Blur.Type"] = "dual_filtering";
                //settings["Filter.Blur.Size"] = blur.Size;

                /* obs-composite-blur settings*/
                settings["blur_algorithm"] = 3; //ALGO_DUAL_KAWASE
                settings["blur_type"] = 1;
                settings["effect_mask"] = 1; //EFFECT_MASK_TYPE_CROP
                settings["effect_mask_crop_top"] = blur.Top;
                settings["effect_mask_crop_bottom"] = blur.Bottom;
                settings["effect_mask_crop_left"] = blur.Left;
                settings["effect_mask_crop_right"] = blur.Right;
                settings["kawase_passes"] = blur.Size;


                if (created)
                {
                    Plugin.obs.CreateSourceFilter(sourceName, blur.Name, "obs_composite_blur", settings);
                }
                else
                {
                    Plugin.obs.SetSourceFilterSettings(sourceName, blur.Name, settings);
                }
                Plugin.obs.SetSourceFilterEnabled(sourceName, blur.Name, blur.Enabled);
            }
            catch (ErrorResponseException e)
            {
                if (e.ToString().Contains("specified source doesn't exist"))
                {
                    Config.UIDetection = false;
                    var errMsg = $"Cannot find source \"{Config.SourceName}\", please check.";
                    Plugin.PluginLog.Error(errMsg);
                    Plugin.Chat.PrintError($"[OBSPlugin] {errMsg}");
                    Config.Save();
                }
                return false;
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Failed updating blur: {0}", e);
                return false;
            }
            Plugin.PluginLog.Debug("Updated blur: {0} {1} ({2}, {3}, {4}, {5})", blur.Name, blur.Enabled, blur.Top, blur.Bottom, blur.Left, blur.Right);
            return true;
        }

        private bool OBSRemoveBlur(Blur blur)
        {
            if (!Plugin.Connected) return false;
            bool removed;
            try
            {
                removed = Plugin.obs.RemoveSourceFilter(Config.SourceName, blur.Name);
                Plugin.PluginLog.Debug("Deleted blur: {0}", blur.Name);
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Failed deleting blur: {0}", e);
                return false;
            }
            return removed;
        }

        private bool OBSRemoveBlurs(string blurNamePrefix)
        {
            if (!Plugin.Connected) return false;
            try
            {
                var filters = Plugin.obs.GetSourceFilterList(Config.SourceName);
                foreach (var filter in filters)
                {
                    if (filter.Name.StartsWith(blurNamePrefix))
                    {
                        Plugin.obs.RemoveSourceFilter(Config.SourceName, filter.Name);
                    }
                }
                Plugin.PluginLog.Debug("Deleted all blurs starting with {0}", blurNamePrefix);
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Failed deleting blurs: {0}", e);
                return false;
            }
            return true;
        }

        internal unsafe void UpdateGameUI()
        {
            if (!Config.Enabled) return;
            if (!Config.EnableBlur) return;
            if (!Plugin.Connected) return;
            if (Plugin.ObjectTable.LocalPlayer == null) return;
            try
            {
                UpdateChatLog();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating ChatLog UI: {0}", e);
                Config.ChatLogBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdatePartyList();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating PartyList UI: {0}", e);
                Config.PartyListBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateTarget();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating Target UI: {0}", e);
                Config.TargetBlur = false;
                Config.TargetTargetBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateFocusTarget();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating FocusTarget UI: {0}", e);
                Config.FocusTargetBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateNamePlate();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating NamePlate UI: {0}", e);
                Config.NamePlateBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateCharacter();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating Character UI: {0}", e);
                Config.CharacterBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateFridendList();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating FriendList UI: {0}", e);
                Config.FriendListBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateHotbar();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating Hotbar UI: {0}", e);
                Config.HotbarBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            try
            {
                UpdateCastBar();
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error("Error Updating CastBar UI: {0}", e);
                Config.CastBarBlur = false;
                UIErrorCount++;
                Config.Save();
            }
            if (UIErrorCount > 1000)
            {
                var errMsg = "More than 1000 UI errors encountered, UI detection is turned off. " +
                    "Please open /xllog for more details.";
                Plugin.PluginLog.Error(errMsg);
                Plugin.Chat.PrintError($"[OBSPlugin] {errMsg}");
                Config.UIDetection = false;
                Config.Save();
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

        private unsafe Blur GetBlurFromNode(AtkResNode* node, string name, bool floating = false, bool? enabled = null)
        {
            var position = floating ? GetFloatingNodePosition(node) : GetNodePosition(node);
            var scale = GetNodeScale(node);
            var nodeVisible = GetNodeVisible(node);
            var size = new Vector2(node->Width, node->Height) * scale;
            if (Config.DrawBlurRect && nodeVisible
                && BlurDict.TryGetValue(name, out Blur? existingBlur)
                && existingBlur.Enabled)
                ImGui.GetForegroundDrawList(ImGui.GetMainViewport()).AddRect(position, position + size, 0xFFFF0000);
            var (top, bottom, left, right) = GetUIRect(position.X,
                position.Y,
                size.X,
                size.Y);
            Blur blur = new(name, top, bottom, left, right, Config.BlurSize);
            blur.Enabled = enabled == null ? nodeVisible : (bool)enabled;
            return blur;
        }

        private unsafe void UpdateBlur(Blur blur)
        {
            blur.LastEdit = DateTime.Now;
            if (BlurDict.TryGetValue(blur.Name, out Blur? existingBlur))
            {
                if (!blur.Equals(existingBlur))
                {
                    BlurDict[blur.Name] = blur;
                    if (Config.BlurAsync)
                    {
                        BlurItemsToAdd.Add(blur);
                    }
                    else
                    {
                        OBSAddOrUpdateBlur(blur);
                    }
                }
            }
            else
            {
                BlurDict[blur.Name] = blur;
                BlurItemsToAdd.Add((Blur)blur.Clone());
            }
        }

        private unsafe void UpdateChatLog()
        {
            if (!Config.ChatLogBlur) return;

            try
            {
                var panel = GetChatLogPanelVisiblity();

                UpdateChatLogPanel("ChatLog");
                //UpdateChatLogPanel("ChatLogPanel_0"); // Panel_0 always in main panel
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
            var chatLog = Plugin.GameGui.GetAddonByName(ChatLogWindowName, 1);
            if (chatLog.IsNull) return;
            unsafe
            {
                var chatLogPtr = (AtkUnitBase*)chatLog.Address;
                if (chatLogPtr->UldManager.NodeListCount <= 0) return;
                var chatLogNode = chatLogPtr->UldManager.NodeList[0];
                bool? visiblity = followUI ? null : false; // null is auto
                UpdateBlur(GetBlurFromNode(chatLogNode, ChatLogWindowName, false, visiblity));
            }
        }

        private unsafe Dictionary<string, bool> GetChatLogPanelVisiblity()
        {

            var chatLog = Plugin.GameGui.GetAddonByName("ChatLog", 1);
            if (chatLog.IsNull) throw new Exception("ChatLog get faild!");
            unsafe
            {
                var chatLogPtr = (AtkUnitBase*)chatLog.Address;
                if (chatLogPtr->UldManager.NodeListCount <= 0) throw new Exception("ChatLog's children is empty!");

                // when panel tag invisiblity, it means the sub panel is a standalone panel.
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
            if (!Config.PartyListBlur) return;
            HashSet<string> existingBlur = new();
            uint partyMemberCount = 0;
            var partyList = Plugin.GameGui.GetAddonByName("_PartyList", 1);
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
            var blursToRemove = BlurDict.Values.Where(blur => blur.Name.StartsWith("PartyList") && !existingBlur.Contains(blur.Name));
            if (blursToRemove.Any())
            {
                blursToRemove.ToList().ForEach(blur =>
                {
                    BlurItemsToRemove.Add(blur);
                    BlurDict.Remove(blur.Name);
                });
            }
            for (int i = 0; i < partyMemberCount; i++)
            {
                UpdateBlur(PartyMemberBlurList[i]);
            }
        }

        private unsafe void UpdateNamePlate()
        {
            if (!Config.NamePlateBlur) return;
            Dictionary<string, Blur> namePlateBlurMap = new();
            HashSet<string> existingBlur = new();
            var namePlate = Plugin.GameGui.GetAddonByName("NamePlate", 1);
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
                ).Take(Config.MaxNamePlateCount);
            foreach (KeyValuePair<string, Blur> pair in namePlateBlurList)
            {
                UpdateBlur(pair.Value);
                existingBlur.Add(pair.Value.Name);
            }

            var blursToRemove = BlurDict.Values.Where(blur => blur.Name.StartsWith("NamePlate") && !existingBlur.Contains(blur.Name));
            if (blursToRemove.Any())
            {
                blursToRemove.ToList().ForEach(blur =>
                {
                    BlurItemsToRemove.Add(blur);
                    BlurDict.Remove(blur.Name);
                });
            }
        }


        private unsafe void UpdateTarget()
        {
            if (!Config.TargetBlur && !Config.TargetTargetBlur) return;
            Blur? targetBlur = null;
            Blur? targetTargetBlur = null;
            // uint partyMemberCount = 0;
            var targetInfo = Plugin.GameGui.GetAddonByName("_TargetInfo", 1);
            if (targetInfo.IsNull) return;
            unsafe
            {
                var targetInfoPtr = (AtkUnitBase*)targetInfo.Address;
                if (!GetNodeVisible(targetInfoPtr->UldManager.NodeList[0]))
                {
                    targetInfo = Plugin.GameGui.GetAddonByName("_TargetInfoMainTarget", 1);
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
            if (Config.TargetBlur)
            {
                if (targetBlur == null)
                {
                    if (BlurDict.TryGetValue("Target", out Blur? blurToRemove))
                    {
                        BlurItemsToRemove.Add(blurToRemove);
                        BlurDict.Remove(blurToRemove.Name);
                    }
                }
                else
                {
                    UpdateBlur(targetBlur);
                }
            }

            if (Config.TargetTargetBlur)
            {
                if (targetTargetBlur == null)
                {
                    if (BlurDict.TryGetValue("TargetTarget", out Blur? blurToRemove))
                    {
                        BlurItemsToRemove.Add(blurToRemove);
                        BlurDict.Remove(blurToRemove.Name);
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
            if (!Config.FocusTargetBlur) return;
            Blur? focusTargetBlur = null;
            var focusTargetInfo = Plugin.GameGui.GetAddonByName("_FocusTargetInfo", 1);
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
                if (BlurDict.TryGetValue("FocusTarget", out Blur? blurToRemove))
                {
                    BlurItemsToRemove.Add(blurToRemove);
                    BlurDict.Remove(blurToRemove.Name);
                }
            }
            else
            {
                UpdateBlur(focusTargetBlur);
            }
        }

        private unsafe void UpdateCharacter()
        {
            if (!Config.CharacterBlur) return;
            var character = Plugin.GameGui.GetAddonByName("Character", 1);
            var characterProfile = Plugin.GameGui.GetAddonByName("CharacterProfile", 1);
            // character
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
            // characterProfile, may separate but i think it is fun
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
            if (!Config.FriendListBlur) return;
            var friendList = Plugin.GameGui.GetAddonByName("FriendList", 1);
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
            if (!Config.HotbarBlur || !Config.BlurredHotbars.Any()) return;
            foreach (var i in Config.BlurredHotbars)
            {
                var suffix = (i - 1).ToString("00");
                var hotbar = Plugin.GameGui.GetAddonByName($"_ActionBar{(suffix == "00" ? string.Empty : suffix)}", 1);
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
            if (!Config.CastBarBlur) return;
            var castbar = Plugin.GameGui.GetAddonByName("_CastBar", 1);
            if (castbar.IsNull) return;
            unsafe
            {
                var castbarPtr = (AtkUnitBase*)castbar.Address;
                var childNode = castbarPtr->UldManager.NodeList[1];
                UpdateBlur(GetBlurFromNode(childNode, "CastBar"));
            }
        }

        // TODO IDK how to create the list in UI, maybe i can write a command
        private unsafe void UpdateCustomAddon(String addonName)
        {
            var addon = Plugin.GameGui.GetAddonByName(addonName, 1);
            if (addon.IsNull) return;
            unsafe
            {
                var addonPtr = (AtkUnitBase*)addon.Address;
                if (addonPtr->UldManager.NodeListCount <= 0) return;
                var childNode = addonPtr->UldManager.NodeList[0];
                UpdateBlur(GetBlurFromNode(childNode, addonName));
            }
        }

        private void DrawConnectionSettings()
        {
            if (ImGui.Checkbox("启用", ref Config.Enabled))
            {
                Config.Save();
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(Plugin.Connected ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                Plugin.Connected ? "已连接" : "未连接");
            var address = Config.Address;
            if (ImGui.InputText("服务器地址", ref address, 128, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (int.TryParse(address, out int port))
                {
                    address = $"127.0.0.1:{port}";
                }
                if(!(address.StartsWith("ws://") || address.StartsWith("wss://")))
                {
                    address = "ws://" + address;
                }
                Config.Address = address;
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("按回车确认");

            if (ImGui.InputText("密码", ref Config.Password, 128, ImGuiInputTextFlags.Password))
            {
                Config.Save();
            }
            string connectionButtonText = Plugin.Connected ? "断开连接" : "连接";
            if (ImGui.Button(connectionButtonText))
            {
                if (Plugin.Connected)
                {
                    Plugin.obs.Disconnect();
                }
                else
                {
                    Plugin.TryConnect(Config.Address, Config.Password);
                }
            }
            if (Plugin.ConnectionFailed)
            {
                ImGui.SameLine();
                ImGui.Text("认证失败，请检查地址和密码！");
            }
            if (Plugin.Connected)
            {
                ImGui.Separator();
                ImGui.Text("OBS 插件版本：" + Plugin.versionInfo.PluginVersion);
                ImGui.Text("OBS 版本：" + Plugin.versionInfo.OBSStudioVersion);
            }
        }

        private void DrawBlurSettings()
        {
            if (ImGui.Checkbox("Enable Blur", ref Config.EnableBlur))
            {
                if (!Config.EnableBlur)
                {
                    foreach (var blur in BlurDict.Values)
                    {
                        BlurItemsToRemove.Add((Blur)blur.Clone());
                    }
                    BlurDict.Clear();
                }
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Master switch for all blur functionality.");
            ImGui.Separator();
            if (ImGui.Checkbox("UI Detection", ref Config.UIDetection))
            {
                UIErrorCount = 0;
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Detect game UI elements together with game-rendering.\n" +
                    "May cause performance issues.\n" +
                    "If disabled, you need to manually call \"/obs update\" to update the blurs in obs.");
            if (ImGui.Checkbox("Asynchronous Update", ref Config.BlurAsync))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Asynchronizly update the blur filters in obs.\n" +
                    "Asynchronizly update will cause slight delays in updating the filters but has a better performance.");
            if (ImGui.Checkbox("Draw Blur Rect", ref Config.DrawBlurRect))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Draw the blurred content with a blue boundary.");
            if (ImGui.InputText("Source Name", ref Config.SourceName, 128))
            {
                Config.Save();
            }
            if (ImGui.DragInt("Blur Size", ref Config.BlurSize, 1, 1, 16))
            {
                foreach (var blur in BlurDict.Values)
                {
                    blur.Size = Config.BlurSize;
                    BlurItemsToAdd.Add((Blur)blur.Clone());
                }
                Config.Save();
            }
            ImGui.Separator();
            if (ImGui.Checkbox("ChatLog", ref Config.ChatLogBlur))
            {
                if (!Config.ChatLogBlur)
                {
                    Blur? chatLogBlur = null;
                    if (BlurDict.TryGetValue("ChatLog", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (BlurDict.TryGetValue("ChatLogPanel_0", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (BlurDict.TryGetValue("ChatLogPanel_1", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (BlurDict.TryGetValue("ChatLogPanel_2", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                    if (BlurDict.TryGetValue("ChatLogPanel_3", out chatLogBlur))
                    {
                        chatLogBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", chatLogBlur.Name);
                        BlurItemsToAdd.Add((Blur)chatLogBlur.Clone());
                    }
                }
                Config.Save();
            }
            if (ImGui.Checkbox("PartyList", ref Config.PartyListBlur))
            {
                if (!Config.PartyListBlur)
                {
                    var blursToTurnOff = BlurDict.Values.Where(blur => blur.Name.StartsWith("PartyList"));
                    foreach (Blur blur in blursToTurnOff)
                    {
                        blur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", blur.Name);
                        BlurItemsToAdd.Add((Blur)blur.Clone());
                    }
                }
                Config.Save();
            }
            if (ImGui.Checkbox("Target", ref Config.TargetBlur))
            {
                if (!Config.TargetBlur)
                {
                    Blur? targetBlur = null;
                    if (BlurDict.TryGetValue("Target", out targetBlur))
                    {
                        targetBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", targetBlur.Name);
                        BlurItemsToAdd.Add((Blur)targetBlur.Clone());
                    }
                }
                Config.Save();
            }

            if (ImGui.Checkbox("TargetTarget", ref Config.TargetTargetBlur))
            {
                if (!Config.TargetTargetBlur)
                {
                    Blur? targetTargetBlur = null;
                    if (BlurDict.TryGetValue("TargetTarget", out targetTargetBlur))
                    {
                        targetTargetBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", targetTargetBlur.Name);
                        BlurItemsToAdd.Add((Blur)targetTargetBlur.Clone());
                    }
                }
                Config.Save();
            }

            if (ImGui.Checkbox("FocusTarget", ref Config.FocusTargetBlur))
            {
                if (!Config.FocusTargetBlur)
                {
                    Blur? focusTargetBlur = null;
                    if (BlurDict.TryGetValue("FocusTarget", out focusTargetBlur))
                    {
                        focusTargetBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", focusTargetBlur.Name);
                        BlurItemsToAdd.Add((Blur)focusTargetBlur.Clone());
                    }
                }
                Config.Save();
            }
            if (ImGui.Checkbox("Character", ref Config.CharacterBlur))
            {
                if (!Config.CharacterBlur)
                {
                    Blur? characterBlur = null;
                    if (BlurDict.TryGetValue("Character", out characterBlur))
                    {
                        characterBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", characterBlur.Name);
                        BlurItemsToAdd.Add((Blur)characterBlur.Clone());
                    }
                }
                Config.Save();

            }
            if (ImGui.Checkbox("FriendList", ref Config.FriendListBlur))
            {
                if (!Config.FriendListBlur)
                {
                    Blur? friendListBlur = null;
                    if (BlurDict.TryGetValue("FriendList", out friendListBlur))
                    {
                        friendListBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", friendListBlur.Name);
                        BlurItemsToAdd.Add((Blur)friendListBlur.Clone());
                    }
                }
                Config.Save();
            }
            if (ImGui.Checkbox("Hotbar", ref Config.HotbarBlur))
            {
                if (!Config.HotbarBlur)
                {
                    var hotbars = BlurDict.Where(x => x.Key.Length >= 8 && x.Key[..6] == "Hotbar");
                    if (hotbars.Any())
                    {
                        Plugin.PluginLog.Debug("Turn off HotbarBlur");
                        foreach (var i in hotbars)
                        {
                            i.Value.Enabled = false;
                            BlurItemsToAdd.Add((Blur)i.Value.Clone());
                        }
                    }
                }
                Config.Save();
            }
            if (Config.HotbarBlur)
            {
                ImGui.SameLine();
                var numbers = string.Join(",", Config.BlurredHotbars.Select(x => x.ToString()));
                ImGui.InputTextWithHint(string.Empty, "hotbar number splitted by comma", ref numbers, 32);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Adding hotbar will effect instantly, but remove need to re-enable hotbar blur to make it change.");
                try
                {
                    Config.BlurredHotbars = numbers.Trim().Replace("，", ",").Split(",").Select(x => int.Parse(x)).ToArray();
                }
                catch { }
            }
            if (ImGui.Checkbox("CastBar", ref Config.CastBarBlur))
            {
                if (!Config.CastBarBlur)
                {
                    Blur? castbarBlur = null;
                    if (BlurDict.TryGetValue("CastBar", out castbarBlur))
                    {
                        castbarBlur.Enabled = false;
                        Plugin.PluginLog.Debug("Turn off {0}", castbarBlur.Name);
                        BlurItemsToAdd.Add((Blur)castbarBlur.Clone());
                    }
                }
                Config.Save();
            }
            /*
            if (ImGui.Checkbox("NamePlate", ref Config.NamePlateBlur))
            {
                if (!Config.NamePlateBlur)
                {
                    OBSRemoveBlurs("NamePlate");
                }
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Blurring NamePlate is not recommended.\n" +
                    "This may cause severe performance issues.\n" +
                    "It's better to turn off name plate in game or limit the number of blurs to <= 8.");
            ImGui.SameLine();
            if (ImGui.DragInt("Max", ref Config.MaxNamePlateCount, 1, 1, 50))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Max number of nameplates to be blurred, sorted by the distance to the center.");
            */
            if (Config.BlurAsync)
            {
                ImGui.Separator();
                ImGui.Text($"Current #BlurItemsToAdd: {BlurItemsToAdd.Count}");
                ImGui.Text($"Current #BlurItemsToRemove: {BlurItemsToRemove.Count}");
            }

        }

        private void DrawAbout()
        {
            ImGui.Text("你需要在 OBS 中安装模糊插件才能使模糊滤镜正常工作。");

            ImGui.Separator();

            ImGui.Text("对于 OBS v30+：");
            ImGui.BulletText("");
            ImGui.SameLine();
            if (ImGui.Button("OBS Composite Blur"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://github.com/FiniteSingularity/obs-composite-blur/releases/tag/v1.0.5",
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error(ex, "Could not open OBS Composite Blur url");
                }
            }
            ImGui.SameLine();
            ImGui.Text("下载并安装即可。");

            ImGui.BulletText("");
            ImGui.SameLine();
            ImGui.Text("OBS-websocket 5.3.0");
            ImGui.SameLine();
            ImGui.TextWrapped("这是 OBS v30 内置插件，但你仍需要在 OBS 中设置密码并启用它（工具 -> OBS Websocket Server Settings），" +
                "然后在连接标签页中提供端口和密码。");

            ImGui.NewLine();
            ImGui.Text("如果遇到任何 bug，请在以下位置提交问题：");
            ImGui.SameLine();
            if (ImGui.Button("Github"))
            {
                try
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = "https://github.com/Yun-Shan/Dalamud-OBS",
                        UseShellExecute = true,
                    });
                }
                catch (Exception ex)
                {
                    Plugin.PluginLog.Error(ex, "Could not open OBS-websocket url");
                }
            }



        }

        private void DrawDebug()
        {
            if (ImGui.Checkbox("启用调试", ref Config.EnableDebug))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("调试总开关。");

            if (!Config.EnableDebug)
            {
                ImGui.EndChild();
                return;
            }

            CacheDutyTree();

            ImGui.Separator();

            var dutyState = Plugin.DutyState;
            ImGui.Text($"副本已开始：{dutyState.IsDutyStarted}");

            var cfc = dutyState.ContentFinderCondition;
            if (cfc.IsValid)
            {
                ImGui.Text($"副本类型：{cfc.Value.ContentType.Value.Name}");
                ImGui.Text($"副本界面分类：{cfc.Value.ContentUICategory.Value.Name}");
                ImGui.Text($"副本名称：{cfc.Value.Name}");
            }
            else
            {
                ImGui.Text("副本内容：无效");
            }

            ImGui.Separator();

            var elapsed = DateTime.Now - lastDutyEventTime;
            if (elapsed.TotalSeconds < 5 && !string.IsNullOrEmpty(lastDutyEvent))
            {
                ImGui.Text($"{lastDutyEvent} (已过 {elapsed.TotalSeconds:F1} 秒)");
            }
            else
            {
                lastDutyEvent = "";
            }

            ImGui.Separator();

            foreach (var contentType in debugDutyTree)
            {
                if (ImGui.TreeNode(contentType.Key))
                {
                    var onlyUnknownCategory = contentType.Value.Count == 1 && contentType.Value.ContainsKey("未知");
                    if (onlyUnknownCategory)
                    {
                        foreach (var name in contentType.Value["未知"])
                        {
                            ImGui.Text(name);
                        }
                    }
                    else
                    {
                        foreach (var uiCategory in contentType.Value)
                        {
                            if (ImGui.TreeNode(uiCategory.Key))
                            {
                                foreach (var name in uiCategory.Value)
                                {
                                    ImGui.Text(name);
                                }
                                ImGui.TreePop();
                            }
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }

        private void DrawStream()
        {
            string obsButtonText;

            switch (Plugin.obsStreamStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "直播开始中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止直播";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "直播停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "开始直播";
                    break;

                default:
                    obsButtonText = "状态未知";
                    break;
            }

            if (ImGui.Button(obsButtonText))
            {
                if (!Plugin.Connected) return;
                try
                {
                    Plugin.obs.ToggleStream();
                }
                catch (Exception e)
                {
                    Plugin.PluginLog.Error("Error on toggle streaming: {0}", e);
                    Plugin.Chat.PrintError("[OBSPlugin] Error on toggle streaming, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(Plugin.obsStreamStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                Plugin.obsStreamStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "直播中" : "已停止");

            if (Plugin.streamStats != null && Plugin.streamStats.IsActive)
            {
                ImGui.Text($"直播中：{Plugin.streamStats.IsActive}");
                ImGui.Text($"重新连接中：{Plugin.streamStats.IsReconnecting}");
                ImGui.Text($"直播时间：{Plugin.streamStats.TimeCode}");
                ImGui.Text($"拥塞：{Plugin.streamStats.Congestion}");
                ImGui.Text($"总帧数：{Plugin.streamStats.TotalFrames}");
                ImGui.Text($"丢帧：{Plugin.streamStats.SkippedFrames}");
                ImGui.Text($"已发送字节：{Plugin.streamStats.BytesSent}");
            }
        }

        internal void SetRecordingDir()
        {
            // SetFilenameFormatting();
            if (Config.RecordDir == null || Config.RecordDir.Length == 0) return;
            if (Plugin.ClientState == null || Plugin.ClientState.TerritoryType == 0) return;

            var curDir = Config.RecordDir;
            if (Config.IncludeTerritory && Plugin.obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
            {
                var terriIdx = Plugin.ClientState.TerritoryType;
                string folderName;

                if (Config.UseDutyName)
                {
                    // Try to get duty name from ContentFinderCondition
                    var territory = Plugin.Data.GetExcelSheet<TerritoryType>().GetRow(terriIdx);
                    var cfcId = territory.ContentFinderCondition.RowId;
                    if (cfcId > 0)
                    {
                        var cfc = Plugin.Data.GetExcelSheet<ContentFinderCondition>().GetRow(cfcId);
                        folderName = cfc.Name.ToString();
                    }
                    else
                    {
                        // Fall back to zone name if not in a duty
                        folderName = territory.Map.Value.PlaceName.Value.Name.ToString();
                    }
                }
                else
                {
                    var terriName = Plugin.Data.GetExcelSheet<TerritoryType>().GetRow(terriIdx).Map.Value.PlaceName.Value.Name;
                    folderName = terriName.ToString();
                }

                curDir = Path.Combine(curDir, folderName);
            }

            if (!Directory.Exists(curDir))
            {
                Directory.CreateDirectory(curDir);
            }

            Plugin.obs.SetRecordDirectory(curDir);
        }

        internal void ResetReplayBufferRecordingDir()
        {
            if (!Plugin.config.Enabled) return;
            bool needToResume = false;
            if (Plugin.obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                needToResume = true;
                Plugin.obs.StopReplayBuffer();
            }
            SetRecordingDir();
            if (needToResume)
            {
                new Task(() =>
                {
                    var leftTimes = 5;
                    while (leftTimes > 0)
                    {
                        if (Plugin.obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
                        {
                            break;
                        }
                        try
                        {
                            Plugin.obs.StartReplayBuffer();
                        }
                        catch (ErrorResponseException err)
                        {
                            Plugin.PluginLog.Warning("Start replay buffer error: {0}", err);
                        }
                        leftTimes -= 1;
                        Thread.Sleep(1000);
                    }
                    if (leftTimes == 0)
                    {
                        Plugin.PluginLog.Error("Cannot resume replay buffer...");
                    }
                }).Start();
            }
        }

        /*
        internal void SetFilenameFormatting()
        {
            if (Config.FilenameFormat == null || Config.FilenameFormat.Length == 0) return;
            if (Plugin.ClientState == null || Plugin.ClientState.TerritoryType == 0) return;

            var filenameFormat = Config.FilenameFormat;
            if (Config.ZoneAsSuffix && Plugin.obsRecordStatus == OutputState.Stopped)
            {
                var terriIdx = Plugin.ClientState.TerritoryType;
                var terriName = Plugin.Data.GetExcelSheet<TerritoryType>().GetRow(terriIdx).Map.Value.PlaceName.Value.Name;
                filenameFormat += "_" + terriName;
            }

            Plugin.obs.SetFilenameFormatting(filenameFormat);
        }
        */

        private void DrawRecord()
        {

            string obsButtonText;

            switch (Plugin.obsRecordStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "录制开始中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止录制";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "录制停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "开始录制";
                    break;

                default:
                    obsButtonText = "状态未知";
                    break;
            }

            if (ImGui.Button(obsButtonText))
            {
                if (!Plugin.Connected) return;
                try
                {
                    if (Plugin.obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED)
                    {
                        SetRecordingDir();
                    }
                    Plugin.obs.ToggleRecord();
                }
                catch (Exception e)
                {
                    Plugin.PluginLog.Error("Error on toggle recording: {0}", e);
                    Plugin.Chat.PrintError("[OBSPlugin] Error on toggle recording, check log for details.");
                }
            }

            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(Plugin.obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                Plugin.obsRecordStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "录制中" : "已停止");

            if (ImGui.InputText("录制目录", ref Config.RecordDir, 256, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                Config.Save();
                if (Plugin.Connected)
                {
                    Plugin.obs.SetRecordDirectory(Config.RecordDir);
                    Plugin.PluginLog.Information("Recording directory set to {0}", Config.RecordDir);
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("按回车保存");
            if (Config.UseDutyName)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Checkbox("区域作为子文件夹", ref Config.IncludeTerritory))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，录制将保存到以当前区域名命名的子文件夹。");
            if (Config.UseDutyName)
            {
                ImGui.EndDisabled();
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 400);
            if (ImGui.Checkbox("副本名称作为子文件夹", ref Config.UseDutyName))
            {
                if (Config.UseDutyName)
                {
                    // Set Config.IncludeTerritory by default
                    Config.IncludeTerritory = true;
                }
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果在副本中，使用副本名称代替区域名作为子文件夹。");

            if (ImGui.Checkbox("区域作为后缀", ref Config.ZoneAsSuffix))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，将以当前区域名作为录制文件的后缀。");

            if (ImGui.Checkbox("战斗开始时自动录制", ref Config.StartRecordOnCombat))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗开始时将自动开始录制。");

            if (ImGui.Checkbox("倒计时开始时自动录制", ref Config.StartRecordOnCountDown))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，倒计时开始时将自动开始录制。");

            ImGui.SameLine(ImGui.GetColumnWidth() - 350);
            if (ImGui.Checkbox("倒计时取消时停止录制", ref Config.StopRecordOnCountDownCancel))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，倒计时取消时将自动停止录制。");

            if (ImGui.Checkbox("战斗结束后停止录制", ref Config.StopRecordOnCombat))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗结束后将自动停止录制。");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("", ref Config.StopRecordOnCombatDelay, 1, 0, 300, "%d 秒"))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("战斗结束后停止录制的延迟时间（秒）。");

            if (ImGui.Checkbox("离开区域时停止录制", ref Config.StopRecordOnZoneExit))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，离开区域时将自动停止录制。");

            if (Config.StopRecordOnCombat && ImGui.Checkbox("过场时不要停止录制", ref Config.DontStopInCutscene))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，观看过场动画时不会停止录制。");
            if (Config.StopRecordOnCombat && ImGui.Checkbox("战斗恢复时取消停止录制", ref Config.CancelStopRecordOnResume))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，在停止倒计时前有新的战斗则不停止录制。");
        }

        private void DrawReplay()
        {

            string obsButtonText;

            switch (Plugin.obsReplayBufferStatus)
            {
                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTING:
                    obsButtonText = "回放缓存启动中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
                    obsButtonText = "停止回放缓存";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPING:
                    obsButtonText = "回放缓存停止中...";
                    break;

                case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                    obsButtonText = "启动回放缓存";
                    break;

                default:
                    obsButtonText = "状态未知";
                    break;
            }

            if (ImGui.Button(obsButtonText))
            {
                if (!Plugin.Connected) return;
                try
                {
                    Plugin.obs.ToggleReplayBuffer();
                }
                catch (Exception e)
                {
                    Plugin.PluginLog.Error("Error on toggle replay buffer: {0}", e);
                    Plugin.Chat.PrintError("[OBSPlugin] Error on toggle replay buffer, check log for details.");
                }
            }
            ImGui.SameLine(ImGui.GetColumnWidth() - 80);
            ImGui.TextColored(Plugin.obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                Plugin.obsReplayBufferStatus == OutputState.OBS_WEBSOCKET_OUTPUT_STARTED ? "回放中" : "已停止");

            if (ImGui.Checkbox("自动录制时启动回放缓存", ref Config.StartReplayBufferOnRecord))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，自动录制开始时将自动启动回放缓存。");

            if (ImGui.Checkbox("区域作为子文件夹", ref Config.ResetReplayBufferDirByTerritory))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，将自动为不同区域设置回放缓存目录。\n" +
                    "这会使回放缓存在切换区域时自动停止和重启。\n" +
                    "否则所有回放缓存将保存到它启动时的文件夹。");

            if (ImGui.Checkbox("战斗结束后保存回放缓存", ref Config.SaveReplayBufferOnCombat))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("如果选择，战斗结束后将自动保存回放缓存。");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragInt("", ref Config.SaveReplayBufferOnCombatDelay, 1, 0, 300, "%d 秒"))
            {
                Config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("战斗结束后保存回放缓存的延迟时间（秒）。");

            if (Plugin.obsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                ImGui.BeginDisabled();
            }
            if (ImGui.Button("保存回放"))
            {
                if (!Plugin.Connected) return;
                try
                {
                    Plugin.obs.SaveReplayBuffer();
                }
                catch (Exception e)
                {
                    Plugin.PluginLog.Error("Error on save replay buffer: {0}", e);
                    Plugin.Chat.PrintError("[OBSPlugin] Error on save replay buffer, check log for details.");
                }
            }
            if (Plugin.obsReplayBufferStatus != OutputState.OBS_WEBSOCKET_OUTPUT_STARTED)
            {
                ImGui.EndDisabled();
            }
        }

        internal void Dispose()
        {
            if (Plugin.Connected)
            {
                foreach (Blur blur in BlurDict.Values)
                {
                    blur.Enabled = false;
                    Plugin.PluginLog.Debug("Turn off {0}", blur.Name);
                    Plugin.obs.RemoveSourceFilter(Config.SourceName, blur.Name);
                }
            }
            isThreadRunning = false;
        }

    }
}
