using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;

namespace OBSPlugin.Services
{
    public struct ContentEntry
    {
        public string Name;
        public uint RowId;
    }

    public static class ContentFinderConditionExtensions
    {
        public static OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>> BuildDutyTree(IDataManager data)
        {
            var tree = new OrderedDictionary<string, OrderedDictionary<string, List<ContentEntry>>>();

            var sheet = data.GetExcelSheet<ContentFinderCondition>();
            if (sheet == null) return tree;

            var sortedRows = sheet
                .OrderBy(row => row.ContentType.Value.RowId)
                .ThenBy(row => row.ContentUICategory.Value.RowId)
                .ThenBy(row => row.RowId);

            foreach (var row in sortedRows)
            {
                var contentType = string.IsNullOrEmpty(row.ContentType.Value.Name.ToString()) ? "未知" : row.ContentType.Value.Name.ToString();
                var uiCategory = string.IsNullOrEmpty(row.ContentUICategory.Value.Name.ToString()) ? "未知" : row.ContentUICategory.Value.Name.ToString();
                var name = string.IsNullOrEmpty(row.Name.ToString()) ? "未知" : row.Name.ToString();

                if (!tree.ContainsKey(contentType))
                    tree[contentType] = new OrderedDictionary<string, List<ContentEntry>>();

                var uiCategoryDict = tree[contentType];

                if (!uiCategoryDict.ContainsKey(uiCategory))
                    uiCategoryDict[uiCategory] = new List<ContentEntry>();

                var nameList = uiCategoryDict[uiCategory];
                nameList.Add(new ContentEntry { Name = name, RowId = row.RowId });
            }

            return tree;
        }
    }
}