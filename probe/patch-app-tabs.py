# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

# 1) NeuInput: same-surface (canvas fill), inset strokes must match the fill
s = s.replace('''            <Style x:Key="NeuInput" TargetType="TextBox">
                <Setter Property="Background" Value="{DynamicResource WellBrush}"/>''',
'''            <Style x:Key="NeuInput" TargetType="TextBox">
                <Setter Property="Background" Value="{DynamicResource CanvasBrush}"/>''')
s = s.replace('BorderBrush="{DynamicResource WellBrush}">\n                                        <Border.Effect>\n                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"\n                                                              BlurRadius="7" Opacity="0.95"/>',
              'BorderBrush="{DynamicResource CanvasBrush}">\n                                        <Border.Effect>\n                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"\n                                                              BlurRadius="7" Opacity="0.95"/>')
# the two inner-shadow strokes inside NeuInput template use WellBrush (2 places)
s = s.replace('''                                    <Border BorderThickness="2,2,0,0" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource WellBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                              BlurRadius="7" Opacity="0.95"/>
                                        </Border.Effect>
                                    </Border>
                                    <!-- inner highlight: light bleeding from bottom-right -->
                                    <Border BorderThickness="0,0,2,2" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource WellBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuLight}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.9"/>
                                        </Border.Effect>
                                    </Border>''',
'''                                    <Border BorderThickness="2,2,0,0" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource CanvasBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                              BlurRadius="7" Opacity="0.95"/>
                                        </Border.Effect>
                                    </Border>
                                    <!-- inner highlight: light bleeding from bottom-right -->
                                    <Border BorderThickness="0,0,2,2" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource CanvasBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuLight}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.9"/>
                                        </Border.Effect>
                                    </Border>''')

# 2) NeuCard: same-surface raised (pill == canvas now)
s = s.replace('''            <Style x:Key="NeuCard" TargetType="ContentControl">
                <Setter Property="Background" Value="{DynamicResource WellBrush}"/>''',
'''            <Style x:Key="NeuCard" TargetType="ContentControl">
                <Setter Property="Background" Value="{DynamicResource PillBrush}"/>''')

# 3) Tab styles: generous browser tabs
start = s.index('            <!-- ============ tabs ============ -->')
end = s.index('            <!-- ============ buttons')
new_tabs = '''            <!-- ============ tabs: browser-tab metaphor, generous type ============ -->
            <!-- unselected: one step lower (darker lavender), same folder-tab silhouette -->
            <Style x:Key="TabRaised" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="26,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="15"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Grid>
                                <Border x:Name="dark" CornerRadius="9,9,0,0" Background="{DynamicResource SurfaceHoverBrush}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.7"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="light" CornerRadius="9,9,0,0" Background="{DynamicResource SurfaceHoverBrush}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.75"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="bd" Background="{DynamicResource SurfaceHoverBrush}" CornerRadius="9,9,0,0"/>
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

            <!-- selected: white, merges seamlessly into the white recessed band below -->
            <Style x:Key="TabMerged" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="26,0"/>
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
                                <Border x:Name="face" CornerRadius="9,9,0,0" Background="{DynamicResource WellBrush}"
                                        VerticalAlignment="Top" Height="58"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_tabs + s[end:]

open(p, 'w', encoding='utf-8').write(s)
print('app patched: same-surface input/card + browser tabs')
