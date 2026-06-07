using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSPlugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OBSPlugin
{
    public class DebugTab
    {
        private readonly Configuration _config;
        private readonly IChatGui _chat;

        private string _lastDutyEvent = "";
        private DateTime _lastDutyEventTime = DateTime.MinValue;
        private OrderedDictionary<string, OrderedDictionary<string, List<Services.ContentEntry>>> _debugDutyTree = new();
        private bool _debugDutyTreeCached = false;

        public DebugTab(Configuration config, IChatGui chat)
        {
            _config = config;
            _chat = chat;
            InitDutyEventHandlers();
        }

        private void InitDutyEventHandlers()
        {
            Svc.DutyState.DutyStarted += _ =>
            {
                _lastDutyEvent = "副本已开始";
                _lastDutyEventTime = DateTime.Now;
            };
            Svc.DutyState.DutyWiped += _ =>
            {
                _lastDutyEvent = "副本已灭团";
                _lastDutyEventTime = DateTime.Now;
            };
            Svc.DutyState.DutyRecommenced += _ =>
            {
                _lastDutyEvent = "副本已重开";
                _lastDutyEventTime = DateTime.Now;
            };
            Svc.DutyState.DutyCompleted += _ =>
            {
                _lastDutyEvent = "副本已完成";
                _lastDutyEventTime = DateTime.Now;
            };
        }

        private void CacheDutyTree()
        {
            if (_debugDutyTreeCached) return;
            _debugDutyTree = ContentFinderConditionExtensions.BuildDutyTree(Svc.DataManager);
            _debugDutyTreeCached = true;
        }

        public void Draw()
        {
            if (ImGui.Checkbox("启用调试", ref _config.EnableDebug))
            {
                _config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("调试总开关。");

            if (!_config.EnableDebug)
            {
                return;
            }

            CacheDutyTree();

            ImGui.Separator();

            ImGui.Text($"副本已开始：{Svc.DutyState.IsDutyStarted}");

            var cfc = Svc.DutyState.ContentFinderCondition;
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

            var elapsed = DateTime.Now - _lastDutyEventTime;
            if (elapsed.TotalSeconds < 5 && !string.IsNullOrEmpty(_lastDutyEvent))
            {
                ImGui.Text($"{_lastDutyEvent} (已过 {elapsed.TotalSeconds:F1} 秒)");
            }
            else
            {
                _lastDutyEvent = "";
            }

            ImGui.Separator();

            foreach (var contentType in _debugDutyTree)
            {
                if (ImGui.TreeNode(contentType.Key))
                {
                    var onlyUnknownCategory = contentType.Value.Count == 1 && contentType.Value.ContainsKey("未知");
                    if (onlyUnknownCategory)
                    {
                        foreach (var entry in contentType.Value["未知"])
                        {
                            ImGui.Text(entry.Name);
                        }
                    }
                    else
                    {
                        foreach (var uiCategory in contentType.Value)
                        {
                            if (ImGui.TreeNode(uiCategory.Key))
                            {
                                foreach (var entry in uiCategory.Value)
                                {
                                    ImGui.Text(entry.Name);
                                }
                                ImGui.TreePop();
                            }
                        }
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}