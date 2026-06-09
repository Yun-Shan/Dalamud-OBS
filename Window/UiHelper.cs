using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace OBSPlugin.Window
{
    internal enum CheckboxStatus
    {
        Unchecked = 0,
        Checked = 1,
        Indeterminate = -1
    }

    internal class UiHelper
    {
        private static readonly uint checkBoxIntermediateColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1f));

        public static bool Checkbox(ImU8String label, ref CheckboxStatus status)
        {
            const int boxGap = 2;
            bool ret;

            if (status == CheckboxStatus.Indeterminate)
            {
                bool b = false;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) status = CheckboxStatus.Checked;

                var itemMin = ImGui.GetItemRectMin();
                itemMin.X += boxGap;
                itemMin.Y += boxGap;
                var itemMax = ImGui.GetItemRectMax();
                itemMax.X -= boxGap;
                itemMax.Y -= boxGap;
                ImGui.GetWindowDrawList().AddRectFilled(itemMin, itemMax, checkBoxIntermediateColor, 2);
            }
            else
            {
                bool b = status == CheckboxStatus.Checked;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) status = b ? CheckboxStatus.Checked : CheckboxStatus.Unchecked;
            }

            return ret;
        }
    }
}
