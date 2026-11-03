# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

# 1) body font -> Noto Sans CJK DemiLight (light, not bold)
s = s.replace('''            <FontFamily x:Key="BodyFont">./Fonts/#Source Han Sans CN</FontFamily>
            <FontFamily x:Key="UiFont">./Fonts/#Source Han Sans CN</FontFamily>''',
'''            <FontFamily x:Key="BodyFont">./Fonts/#Noto Sans CJK</FontFamily>
            <FontFamily x:Key="UiFont">Microsoft YaHei UI, Microsoft YaHei</FontFamily>''')

# 2) Edge-style tabs: white selected with sliding shoulder lines, flat bg-colored unselected
start = s.index('            <!-- ============ tabs:')
end = s.index('            <!-- ============ buttons')
new_tabs = '''            <!-- ============ tabs: Edge browser style ============ -->
            <!-- unselected: flat, same-family gray, shoulders slide down to the bar -->
            <Style x:Key="TabRaised" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="30,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="15"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Grid>
                                <Path HorizontalAlignment="Left" VerticalAlignment="Top" Width="9" Height="42"
                                      Stretch="Fill" Fill="{DynamicResource SurfaceHoverBrush}"
                                      Data="M 9,0 C 8,15 5,29 0,42 L 9,42 Z"/>
                                <Path HorizontalAlignment="Right" VerticalAlignment="Top" Width="9" Height="42"
                                      Stretch="Fill" Fill="{DynamicResource SurfaceHoverBrush}"
                                      Data="M 0,0 C 1,15 4,29 9,42 L 0,42 Z"/>
                                <Border x:Name="bd" Background="{DynamicResource SurfaceHoverBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0" Height="42" VerticalAlignment="Top"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                    <Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfacePressedBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

            <!-- selected: pure white, shoulders slide into the white band below -->
            <Style x:Key="TabMerged" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="30,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="15"/>
                <Setter Property="FontWeight" Value="SemiBold"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Panel.ZIndex" Value="3"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Grid>
                                <Path HorizontalAlignment="Left" VerticalAlignment="Top" Width="9" Height="42"
                                      Stretch="Fill" Fill="{DynamicResource WellBrush}"
                                      Data="M 9,0 C 8,15 5,29 0,42 L 9,42 Z"/>
                                <Path HorizontalAlignment="Right" VerticalAlignment="Top" Width="9" Height="42"
                                      Stretch="Fill" Fill="{DynamicResource WellBrush}"
                                      Data="M 0,0 C 1,15 4,29 9,42 L 0,42 Z"/>
                                <!-- body extends past the seam into the white band -->
                                <Border Background="{DynamicResource WellBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0"
                                        VerticalAlignment="Top" Height="56"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_tabs + s[end:]
open(p, 'w', encoding='utf-8').write(s)
print('fonts lightened + Edge tabs in')
