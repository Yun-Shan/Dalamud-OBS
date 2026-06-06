using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OBSPlugin
{
    public class DebugTab
    {
        private readonly Configuration _config;
        private readonly IDutyState _dutyState;
        private readonly IDataManager _data;
        private readonly IPluginLog _log;
        private readonly IChatGui _chat;

        private string _lastDutyEvent = "";
        private DateTime _lastDutyEventTime = DateTime.MinValue;
        private OrderedDictionary<string, OrderedDictionary<string, List<string>>> _debugDutyTree = new();
        private bool _debugDutyTreeCached = false;

        public DebugTab(Configuration config, IDutyState dutyState, IDataManager data, IPluginLog log, IChatGui chat)
        {
            _config = config;
            _dutyState = dutyState;
            _data = data;
            _log = log;
            _chat = chat;
            InitDutyEventHandlers();
        }

        private void InitDutyEventHandlers()
        {
            _dutyState.DutyStarted += _ =>
            {
                _lastDutyEvent = "副本已开始";
                _lastDutyEventTime = DateTime.Now;
            };
            _dutyState.DutyWiped += _ =>
            {
                _lastDutyEvent = "副本已灭团";
                _lastDutyEventTime = DateTime.Now;
            };
            _dutyState.DutyRecommenced += _ =>
            {
                _lastDutyEvent = "副本已重开";
                _lastDutyEventTime = DateTime.Now;
            };
            _dutyState.DutyCompleted += _ =>
            {
                _lastDutyEvent = "副本已完成";
                _lastDutyEventTime = DateTime.Now;
            };
        }

        private void CacheDutyTree()
        {
            if (_debugDutyTreeCached) return;
            _debugDutyTree.Clear();

            var sheet = _data.GetExcelSheet<ContentFinderCondition>();
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

                if (!_debugDutyTree.ContainsKey(contentType))
                    _debugDutyTree[contentType] = new OrderedDictionary<string, List<string>>();

                var uiCategoryDict = (OrderedDictionary<string, List<string>>)_debugDutyTree[contentType]!;

                if (!uiCategoryDict.ContainsKey(uiCategory))
                    uiCategoryDict[uiCategory] = new List<string>();

                var nameList = uiCategoryDict[uiCategory]!;

                if (!nameList.Contains(name))
                    nameList.Add(name);
            }

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
                ImGui.EndChild();
                return;
            }

            CacheDutyTree();

            ImGui.Separator();

            ImGui.Text($"副本已开始：{_dutyState.IsDutyStarted}");

            var cfc = _dutyState.ContentFinderCondition;
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
    }
}