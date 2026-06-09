using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace OBSPlugin.Window
{
    internal class UiHelper
    {
        private static readonly uint checkBoxIntermediateColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1f));
        public static bool Checkbox(ImU8String label, ref int val)
        {
            const int boxGap = 2;
            bool ret;
            if (val == -1)
            {
                bool b = false;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) val = 1;
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
                bool b = val != 0;
                ret = ImGui.Checkbox(label, ref b);
                if (ret) val = b ? 1 : 0;
            }
            return ret;
        }
    }
}
