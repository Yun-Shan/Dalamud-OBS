using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using OBSPlugin.Window;
using System.Linq;

namespace OBSPlugin.Services
{
    public static class ContentFinderConditionExtensions
    {
        public static DutyTreeNode BuildDutyTree(IDataManager data)
        {
            var root = new DutyTreeNode(DutyTreeNodeType.Root, "Root");

            var sheet = data.GetExcelSheet<ContentFinderCondition>();
            if (sheet == null) return root;

            var sortedRows = sheet
                .OrderBy(row => row.ContentType.Value.RowId)
                .ThenBy(row => row.ContentUICategory.Value.RowId)
                .ThenBy(row => row.RowId);

            foreach (var row in sortedRows)
            {
                var contentType = string.IsNullOrEmpty(row.ContentType.Value.Name.ToString()) ? "未知" : row.ContentType.Value.Name.ToString();
                var uiCategory = string.IsNullOrEmpty(row.ContentUICategory.Value.Name.ToString()) ? "未知" : row.ContentUICategory.Value.Name.ToString();
                var name = string.IsNullOrEmpty(row.Name.ToString()) ? "未知" : row.Name.ToString();

                var contentTypeNode = root.Children.FirstOrDefault(c => c.Name == contentType);
                if (contentTypeNode == null)
                {
                    contentTypeNode = new DutyTreeNode(DutyTreeNodeType.ContentType, contentType, row.ContentType.RowId, root);
                    root.Children.Add(contentTypeNode);
                }

                var uiCategoryNode = contentTypeNode.Children.FirstOrDefault(c => c.Name == uiCategory);
                if (uiCategoryNode == null)
                {
                    uiCategoryNode = new DutyTreeNode(DutyTreeNodeType.ContentUICategory, uiCategory, row.ContentUICategory.RowId, contentTypeNode);
                    contentTypeNode.Children.Add(uiCategoryNode);
                }

                uiCategoryNode.Children.Add(new DutyTreeNode(DutyTreeNodeType.DutyEntry, name, row.RowId, uiCategoryNode));
            }

            return root;
        }
    }
}