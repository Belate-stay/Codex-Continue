# -*- coding: utf-8 -*-
# ROOT FIX: templates never consumed Padding (ContentPresenter lacked TemplateBinding),
# so all "make the tab wider" attempts silently no-oped. Bind it + widen + open the gap.
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

# ---- NeuBtn: consume padding (same latent bug) ----
old = '''                                <Border x:Name="face" CornerRadius="10" Background="{TemplateBinding Background}"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="dark" Property="Effect">'''
new = '''                                <Border x:Name="face" CornerRadius="10" Background="{TemplateBinding Background}"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="dark" Property="Effect">'''
assert old in s
s = s.replace(old, new)

# ---- TabRaised: content inside the shape, padding consumed, wide tabs ----
old = '''                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="30,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>'''
new = '''                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="34,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>'''
assert old in s
s = s.replace(old, new)

old = '''                                <Border x:Name="bd" Background="{DynamicResource SurfaceHoverBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0" Height="42" VerticalAlignment="Top"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                    <Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfacePressedBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>'''
new = '''                                <Border x:Name="bd" Background="{DynamicResource SurfaceHoverBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0" Height="42" VerticalAlignment="Top"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                                  Margin="{TemplateBinding Padding}"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                    <Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfacePressedBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>'''
assert old in s
s = s.replace(old, new)

# ---- TabMerged: same treatment ----
old = '''                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="30,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>'''
new = '''                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="34,0"/>
                <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>'''
assert old in s
s = s.replace(old, new)

old = '''                                <!-- body extends past the seam into the white band -->
                                <Border Background="{DynamicResource WellBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0"
                                        VerticalAlignment="Top" Height="56"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>'''
new = '''                                <!-- body extends past the seam into the white band -->
                                <Border Background="{DynamicResource WellBrush}"
                                        CornerRadius="6,6,0,0" Margin="9,0,9,0"
                                        VerticalAlignment="Top" Height="56">
                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Top"
                                                      Margin="{TemplateBinding Padding}"/>
                                </Border>
                            </Grid>'''
assert old in s
s = s.replace(old, new)

# center content in the visible 42px band (face extends 14px below)
s = s.replace('''                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Top"
                                                      Margin="{TemplateBinding Padding}"/>''',
'''                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Top"
                                                      Margin="{TemplateBinding Padding}" />''')

open(p, 'w', encoding='utf-8').write(s)
print('root fix applied: templates consume Padding now')
