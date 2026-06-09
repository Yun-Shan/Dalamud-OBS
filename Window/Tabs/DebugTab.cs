using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using OBSPlugin.Services;
using OBSPlugin.Window;
using System;
using System.Collections.Generic;

namespace OBSPlugin
{
    public class DebugTab
    {
        private readonly Configuration _config;

        private string _lastDutyEvent = "";
        private DateTime _lastDutyEventTime = DateTime.MinValue;
        private DutyTreeNode _debugDutyTreeRoot;
        private bool _debugDutyTreeCached = false;

        public DebugTab(Configuration config)
        {
            _config = config;
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
            _debugDutyTreeRoot = ContentFinderConditionExtensions.BuildDutyTree(Svc.DataManager);
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

            foreach (var contentTypeNode in _debugDutyTreeRoot.Children)
            {
                if (ImGui.TreeNode(contentTypeNode.Name))
                {
                    var onlyUnknownCategory = contentTypeNode.Children.Count == 1 && contentTypeNode.Children[0].Name == "未知";
                    if (onlyUnknownCategory)
                    {
                        foreach (var entryNode in contentTypeNode.Children[0].Children)
                        {
                            ImGui.Text(entryNode.Name);
                        }
                    }
                    else
                    {
                        foreach (var uiCategoryNode in contentTypeNode.Children)
                        {
                            if (ImGui.TreeNode(uiCategoryNode.Name))
                            {
                                foreach (var entryNode in uiCategoryNode.Children)
                                {
                                    ImGui.Text(entryNode.Name);
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