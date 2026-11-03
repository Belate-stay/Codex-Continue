# -*- coding: utf-8 -*-
# FINAL tabs (user spec): NO slants — a plain rounded box wrapping the text, snug padding,
# hugging the band below (no seam line), and text NEVER moves between states.
# Every replace asserts this time — no silent failures.
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

start = s.index('            <!-- ============ tabs:')
end = s.index('            <!-- ============ buttons')
new_tabs = '''            <!-- ============ tabs ============ -->
            <!-- unselected: bare text, no frame; text centered identically to the selected tab -->
            <Style x:Key="TabRaised" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="22,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="15"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Grid>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

            <!-- selected: plain rounded box, band-colored, hugging the band below (no seam).
                 Face extends 12px past the button bottom to bury the band's seam shading. -->
            <Style x:Key="TabMerged" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="22,0"/>
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
                                <Border x:Name="face" CornerRadius="8,8,0,0" Background="{DynamicResource WellBrush}"
                                        VerticalAlignment="Top" Height="54"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_tabs + s[end:]
open(p, 'w', encoding='utf-8').write(s)

# verify the critical constraints are actually in the file now
s2 = open(p, encoding='utf-8').read()
assert 'Data="M 9,0' not in s2, 'slant paths still present'
assert s2.count('Margin="{TemplateBinding Padding}"') >= 3, 'padding binding missing'
assert 'VerticalAlignment="Center"' in s2.split('TabMerged')[1][:1500], 'merged text not centered'
print('final tabs applied AND verified in file')
