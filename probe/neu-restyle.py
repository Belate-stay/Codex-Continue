# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\App.xaml'
s = open(p, encoding='utf-8').read()

# ---- NeuBtn: raised neumorphic (soft dual shadow, depth 3/blur 12) ----
start = s.index('            <!-- ============ buttons')
end = s.index('            <!-- ============ input')
new_btns = '''            <!-- ============ buttons: neumorphic raised (soft dual shadow, depth 3/blur 12) ============ -->
            <Style x:Key="NeuBtn" TargetType="Button">
                <Setter Property="Height" Value="42"/>
                <Setter Property="Padding" Value="18,0"/>
                <Setter Property="Background" Value="{DynamicResource PillBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource UiFont}"/>
                <Setter Property="FontSize" Value="13.5"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Grid>
                                <Border x:Name="dark" CornerRadius="10" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                          ShadowDepth="3" BlurRadius="12" Opacity="0.9"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="light" CornerRadius="10" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                          ShadowDepth="3" BlurRadius="12" Opacity="0.95"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="face" CornerRadius="10" Background="{TemplateBinding Background}"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="dark" Property="Effect">
                                        <Setter.Value>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                              ShadowDepth="4" BlurRadius="14" Opacity="1"/>
                                        </Setter.Value>
                                    </Setter>
                                </Trigger>
                                <Trigger Property="IsPressed" Value="True">
                                    <Setter TargetName="dark" Property="Effect">
                                        <Setter.Value>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                              ShadowDepth="1" BlurRadius="4" Opacity="0.7"/>
                                        </Setter.Value>
                                    </Setter>
                                    <Setter TargetName="light" Property="Effect">
                                        <Setter.Value>
                                            <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                              ShadowDepth="1" BlurRadius="4" Opacity="0.7"/>
                                        </Setter.Value>
                                    </Setter>
                                </Trigger>
                                <Trigger Property="IsEnabled" Value="False">
                                    <Setter Property="Foreground" Value="{DynamicResource TextDisabledBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

            <!-- primary: raised + accent face (the ONE saturated block) -->
            <Style x:Key="NeuBtnPrimary" TargetType="Button" BasedOn="{StaticResource NeuBtn}">
                <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource AccentTextBrush}"/>
                <Setter Property="FontWeight" Value="SemiBold"/>
            </Style>

            <Style x:Key="NeuBtnDanger" TargetType="Button" BasedOn="{StaticResource NeuBtn}">
                <Setter Property="Background" Value="{DynamicResource ErrBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource AccentTextBrush}"/>
                <Setter Property="FontWeight" Value="SemiBold"/>
            </Style>

'''
s = s[:start] + new_btns + s[end:]

# ---- NeuInput: sunken (inner shadow via clipped outline trick) ----
start = s.index('            <!-- ============ input')
end = s.index('            <!-- ============ attachment "+')
new_input = '''            <!-- ============ input: neumorphic sunken (inner shadow, no borders) ============ -->
            <Style x:Key="NeuInput" TargetType="TextBox">
                <Setter Property="Background" Value="{DynamicResource WellBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                <Setter Property="CaretBrush" Value="{DynamicResource AccentBrush}"/>
                <Setter Property="FontFamily" Value="{DynamicResource BodyFont}"/>
                <Setter Property="FontSize" Value="13.5"/>
                <Setter Property="Padding" Value="12,9"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="TextBox">
                            <Border x:Name="bd" CornerRadius="12" Background="{TemplateBinding Background}" ClipToBounds="True">
                                <Grid>
                                    <!-- inner shadow: dark bleeding from top-left -->
                                    <Border BorderThickness="2,2,0,0" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource WellBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.85"/>
                                        </Border.Effect>
                                    </Border>
                                    <!-- inner highlight: light bleeding from bottom-right -->
                                    <Border BorderThickness="0,0,2,2" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource WellBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuLight}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.9"/>
                                        </Border.Effect>
                                    </Border>
                                    <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}"
                                                  VerticalContentAlignment="{TemplateBinding VerticalContentAlignment}"/>
                                </Grid>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsKeyboardFocusWithin" Value="True">
                                    <Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfaceHoverBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_input + s[end:]

# ---- AttachBtn: sunken per original spec ----
start = s.index('            <!-- ============ attachment "+')
end = s.index('            <!-- ============ attachment chip')
new_attach = '''            <!-- ============ attachment "+" : neumorphic sunken (spec: 凹进去) ============ -->
            <Style x:Key="AttachBtn" TargetType="Button">
                <Setter Property="Width" Value="56"/>
                <Setter Property="Height" Value="56"/>
                <Setter Property="Background" Value="{DynamicResource CanvasBrush}"/>
                <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
                <Setter Property="FontFamily" Value="Segoe Fluent Icons, Segoe UI"/>
                <Setter Property="FontSize" Value="18"/>
                <Setter Property="Cursor" Value="Hand"/>
                <Setter Property="Focusable" Value="False"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="bd" CornerRadius="12" Background="{TemplateBinding Background}" ClipToBounds="True">
                                <Grid>
                                    <Border BorderThickness="2,2,0,0" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource CanvasBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.9"/>
                                        </Border.Effect>
                                    </Border>
                                    <Border BorderThickness="0,0,2,2" Background="Transparent" Margin="-2" CornerRadius="11"
                                            BorderBrush="{DynamicResource CanvasBrush}">
                                        <Border.Effect>
                                            <DropShadowEffect Color="{DynamicResource NeuLight}" ShadowDepth="0"
                                                              BlurRadius="6" Opacity="0.8"/>
                                        </Border.Effect>
                                    </Border>
                                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                </Grid>
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_attach + s[end:]

# ---- NeuPill: raised white (status rows / chips) ----
start = s.index('            <!-- ============ attachment chip')
end = s.index('            <!-- ============ floating card')
new_pill = '''            <!-- ============ raised white strip (status rows, chips) ============ -->
            <Style x:Key="NeuPill" TargetType="ContentControl">
                <Setter Property="Background" Value="{DynamicResource PillBrush}"/>
                <Setter Property="Template">
                    <Setter.Value>
                        <ControlTemplate TargetType="ContentControl">
                            <Grid>
                                <Border x:Name="dark" CornerRadius="10" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.85"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="light" CornerRadius="10" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.9"/>
                                    </Border.Effect>
                                </Border>
                                <Border CornerRadius="10" Background="{TemplateBinding Background}">
                                    <ContentPresenter Margin="{TemplateBinding Padding}" VerticalAlignment="Center"/>
                                </Border>
                            </Grid>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>

'''
s = s[:start] + new_pill + s[end:]

# ---- NeuCard: raised neumorphic (overlays) ----
old_card = '''                            <Border CornerRadius="16" Background="{TemplateBinding Background}">
                                <Border.Effect>
                                    <DropShadowEffect Color="{DynamicResource SoftShadow}" Direction="270"
                                                      ShadowDepth="2" BlurRadius="8" Opacity="1"/>
                                </Border.Effect>
                                <ContentPresenter Margin="{TemplateBinding Padding}"/>
                            </Border>'''
new_card = '''                            <Grid>
                                <Border x:Name="dark" CornerRadius="16" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                          ShadowDepth="6" BlurRadius="20" Opacity="0.9"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="light" CornerRadius="16" Background="{TemplateBinding Background}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                          ShadowDepth="6" BlurRadius="20" Opacity="0.9"/>
                                    </Border.Effect>
                                </Border>
                                <Border CornerRadius="16" Background="{TemplateBinding Background}">
                                    <ContentPresenter Margin="{TemplateBinding Padding}"/>
                                </Border>
                            </Grid>'''
assert old_card in s
s = s.replace(old_card, new_card)

# ---- TabRaised: raised pill (neumorphic) ----
old_tab = '''                            <Border x:Name="bd" Background="Transparent" CornerRadius="9">
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="bd" Property="Background" Value="{DynamicResource SurfaceHoverBrush}"/>
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>'''
new_tab = '''                            <Grid>
                                <Border x:Name="dark" CornerRadius="9" Background="{DynamicResource CanvasBrush}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuDark}" Direction="315"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.85"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="light" CornerRadius="9" Background="{DynamicResource CanvasBrush}">
                                    <Border.Effect>
                                        <DropShadowEffect Color="{DynamicResource NeuLight}" Direction="135"
                                                          ShadowDepth="2.5" BlurRadius="9" Opacity="0.9"/>
                                    </Border.Effect>
                                </Border>
                                <Border x:Name="bd" Background="{DynamicResource CanvasBrush}" CornerRadius="9"/>
                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Grid>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>'''
assert old_tab in s
s = s.replace(old_tab, new_tab)

open(p, 'w', encoding='utf-8').write(s)
print('neumorphic restyle done')
