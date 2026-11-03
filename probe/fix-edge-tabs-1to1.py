# -*- coding: utf-8 -*-
# 1:1 Edge tabs: selected = band-colored, no seam line (flares extend below the seam,
# cream-on-cream covers the seam shading); unselected = BARE TEXT, no frame at all.
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

# ---- TabRaised: bare text only ----
start = s.index('            <!-- ============ tabs:')
end = s.index('            <!-- selected: pure white')
new_raised = '''            <!-- ============ tabs: Edge browser style ============ -->
            <!-- unselected: bare text, no frame at all -->
            <Style x:Key="TabRaised" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="34,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="15"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                              Margin="{TemplateBinding Padding}"/>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_raised + s[end:]

# ---- TabMerged: flares extend past the seam (invisible cream-on-cream) to kill the line ----
old = '''                            <Grid>
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
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>'''
new = '''                            <Grid>
                                <!-- shoulders slide down to the seam, then extend invisibly
                                     (band-colored) past it to cover the seam shading -->
                                <Path HorizontalAlignment="Left" VerticalAlignment="Top" Width="9" Height="54"
                                      Stretch="Fill" Fill="{DynamicResource WellBrush}"
                                      Data="M 9,0 C 8,15 5,30 0,42 L 0,54 L 9,54 Z"/>
                                <Path HorizontalAlignment="Right" VerticalAlignment="Top" Width="9" Height="54"
                                      Stretch="Fill" Fill="{DynamicResource WellBrush}"
                                      Data="M 0,0 C 1,15 4,30 9,42 L 9,54 L 0,54 Z"/>
                                <!-- body IS part of the band below: same color, no seam -->
                                <Border Background="{DynamicResource WellBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0"
                                        VerticalAlignment="Top" Height="58"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>'''
assert old in s
s = s.replace(old, new)

open(p, 'w', encoding='utf-8').write(s)
print('Edge 1:1 tabs applied')
