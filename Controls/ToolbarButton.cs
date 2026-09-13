using Microsoft.Maui.Controls;

namespace Fast_smb.Controls;

/// <summary>
/// 顶栏文字按钮：点击效果与 Shell 导航栏 ToolbarItem 一致 ——
/// Pressed 状态保持透明背景（禁用 MAUI Button 默认按压变色），仅保留系统涟漪。
/// </summary>
public class ToolbarButton : Button
{
    public ToolbarButton()
    {
        BackgroundColor = Colors.Transparent;

        var group = new VisualStateGroup { Name = "CommonStates" };
        var normal = new VisualState { Name = "Normal" };
        var pressed = new VisualState { Name = "Pressed" };
        pressed.Setters.Add(new Setter { Property = BackgroundColorProperty, Value = Colors.Transparent });
        group.States.Add(normal);
        group.States.Add(pressed);

        var groups = new VisualStateGroupList { group };
        VisualStateManager.SetVisualStateGroups(this, groups);
    }
}
