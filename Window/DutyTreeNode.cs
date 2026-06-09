using FFXIVClientStructs.FFXIV.Common.Lua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace OBSPlugin.Window
{
    public enum DutyTreeNodeType
    {
        Root,
        ContentType,
        ContentUICategory,
        DutyEntry
    }

    public class DutyTreeNode(DutyTreeNodeType nodeType, string name, uint rowId = 0, DutyTreeNode? parent = null)
    {
        public DutyTreeNodeType NodeType { get; set; } = nodeType;
        public string Name { get; set; } = name;
        public uint RowId { get; set; } = rowId;
        public DutyTreeNode? Parent { get; set; } = parent;
        public List<DutyTreeNode> Children { get; set; } = new();
        public CheckboxStatus CheckStatus { get; private set; }

        public bool IsLeaf => Children.Count == 0;

        public void UpdateCheckStatus(bool check)
        {
            CheckStatus = check ? CheckboxStatus.Checked : CheckboxStatus.Unchecked;
            PropagateCheckStatusToChildren(CheckStatus);
            Parent?.UpdateCheckStatusFromChildren();
        }

        // 向上传播计算
        private void UpdateCheckStatusFromChildren()
        {
            CheckStatus = CalcStatusFromChildren(Children);
            Parent?.UpdateCheckStatusFromChildren();
        }

        // 向下传播计算
        private void PropagateCheckStatusToChildren(CheckboxStatus status)
        {
            foreach (var child in Children)
            {
                child.CheckStatus = status;
                child.PropagateCheckStatusToChildren(status);
            }
        }

        private static CheckboxStatus CalcStatusFromChildren(List<DutyTreeNode> list)
        {
            if (list == null || list.Count == 0)
                return CheckboxStatus.Unchecked;

            // 获取底层数组的 Span，避免迭代器分配
            var span = CollectionsMarshal.AsSpan(list);

            var first = span[0];

            // 如果第一个元素是 Indeterminate，直接返回
            if (first.CheckStatus == CheckboxStatus.Indeterminate)
                return CheckboxStatus.Indeterminate;

            // 检查其余元素是否与第一个相同
            for (int i = 1; i < span.Length; i++)
            {
                if (span[i].CheckStatus != first.CheckStatus)
                {
                    return CheckboxStatus.Indeterminate;
                }
            }

            return first.CheckStatus;
        }
    }
}