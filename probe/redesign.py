# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\MainWindow.xaml'
s = open(p, encoding='utf-8').read()

# ---------- 1) quota band: texture + artistic numerals ----------
start = s.index('        <!-- ==================== quota well')
end = s.index('        <!-- ==================== content:')
new_well = '''        <!-- ==================== quota well (pure white, recessed, textured) ==================== -->
        <Grid Grid.Row="2" Background="{DynamicResource WellBrush}">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <!-- ambient glow behind the hero numeral -->
            <Ellipse Width="620" Height="280" HorizontalAlignment="Left" Margin="-60,4,0,0"
                     VerticalAlignment="Top" IsHitTestVisible="False">
                <Ellipse.Effect>
                    <BlurEffect Radius="80"/>
                </Ellipse.Effect>
                <Ellipse.Fill>
                    <RadialGradientBrush>
                        <GradientStop Color="#182F7FD5" Offset="0"/>
                        <GradientStop Color="#002F7FD5" Offset="1"/>
                    </RadialGradientBrush>
                </Ellipse.Fill>
            </Ellipse>
            <Grid Margin="36,16,36,4">
                <StackPanel Orientation="Horizontal" VerticalAlignment="Bottom">
                    <TextBlock x:Name="QuotaPct" Text="—" TextOptions.TextFormattingMode="Ideal"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="96"
                               Typography.NumeralAlignment="Tabular"
                               VerticalAlignment="Bottom">
                        <TextBlock.Effect>
                            <DropShadowEffect x:Name="QuotaNumGlow" Color="#2F7FD5" Direction="315"
                                              ShadowDepth="4" BlurRadius="22" Opacity="0.38"/>
                        </TextBlock.Effect>
                    </TextBlock>
                    <TextBlock x:Name="QuotaSign" Text="%" TextOptions.TextFormattingMode="Ideal"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="50"
                               VerticalAlignment="Bottom" Margin="6,0,0,18"/>
                    <TextBlock x:Name="QuotaLabel" Text="五小时窗口额度" FontSize="18.5"
                               FontFamily="{DynamicResource UiFont}"
                               Foreground="{DynamicResource TextSecondaryBrush}"
                               VerticalAlignment="Bottom" Margin="26,0,0,20"/>
                </StackPanel>
            </Grid>
            <Grid Grid.Row="1" Margin="36,4,36,30">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <!-- sunken track + glossy gradient fill -->
                <Grid x:Name="QuotaTrack" Grid.Column="0" Height="13" VerticalAlignment="Center"
                      SizeChanged="QuotaTrack_SizeChanged">
                    <Border Background="{DynamicResource SurfaceHoverBrush}" CornerRadius="6.5">
                        <Border.Effect>
                            <DropShadowEffect Color="{DynamicResource ShadowDark}" ShadowDepth="0" BlurRadius="5" Opacity="0.55"/>
                        </Border.Effect>
                    </Border>
                    <Grid x:Name="QuotaFill" HorizontalAlignment="Left" Width="0">
                        <Border x:Name="QuotaFillBar" CornerRadius="6.5">
                            <Border.Effect>
                                <DropShadowEffect x:Name="QuotaGlow" Color="#2F7FD5" Direction="315" ShadowDepth="2" BlurRadius="9" Opacity="0.5"/>
                            </Border.Effect>
                        </Border>
                        <!-- gloss strip -->
                        <Border CornerRadius="6.5" VerticalAlignment="Top" Height="5">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                    <GradientStop Color="#66FFFFFF" Offset="0"/>
                                    <GradientStop Color="#00FFFFFF" Offset="1"/>
                                </LinearGradientBrush>
                            </Border.Background>
                        </Border>
                    </Grid>
                </Grid>
                <StackPanel Grid.Column="1" Margin="40,0,0,0" VerticalAlignment="Center">
                    <TextBlock x:Name="QuotaTimeCaption" Text="检测时刻" FontSize="11.5"
                               FontFamily="{DynamicResource UiFont}"
                               Foreground="{DynamicResource TextTertiaryBrush}" Margin="4,0,0,3"/>
                    <TextBlock x:Name="QuotaTime" Text="--:--" TextOptions.TextFormattingMode="Ideal"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="48"
                               Foreground="{DynamicResource TextPrimaryBrush}"
                               Typography.NumeralAlignment="Tabular"/>
                </StackPanel>
            </Grid>
            <!-- recessed edge shadows (no border lines) + subtle bottom vignette -->
            <Rectangle Grid.RowSpan="2" Height="18" VerticalAlignment="Top" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                        <GradientStop Color="#2E232323" Offset="0"/>
                        <GradientStop Color="#00232323" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
            <Rectangle Grid.RowSpan="2" Height="14" VerticalAlignment="Bottom" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,1" EndPoint="0,0">
                        <GradientStop Color="#14232323" Offset="0"/>
                        <GradientStop Color="#00232323" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
            <Rectangle Grid.RowSpan="2" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                        <GradientStop Color="#00FFFFFF" Offset="0.55"/>
                        <GradientStop Color="#08000000" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
        </Grid>

'''
s = s[:start] + new_well + s[end:]

# ---------- 2) status rows: flat editorial list, no pill boxes ----------
start = s.index('                <StackPanel Grid.Row="1" Margin="0,18,0,0">')
end = s.index('            </Grid>\n\n            <!-- faint divider')
rows = [
    ('LblWindow', 'Codex 窗口', True),
    ('LblAccount', '账号名称', False),
    ('LblConvos', '运行对话', False),
    ('LblQuota', '额度状态', False),
    ('LblReset', '恢复时间', False),
    ('LblSource', '检测来源', False),
]
val_names = ['ValWindow', 'ValAccount', 'ValConvos', 'ValQuota', 'ValReset', 'ValSource']
parts = ['                <StackPanel Grid.Row="1" Margin="0,22,0,0">\n']
for i, ((lbl, txt, dot), val) in enumerate(zip(rows, val_names)):
    last = i == len(rows) - 1
    if dot:
        valcell = f'''<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                            <Ellipse x:Name="WindowDot" Width="9" Height="9" Fill="{{DynamicResource OkBrush}}" VerticalAlignment="Center"/>
                            <TextBlock x:Name="{val}" Text="在线" FontSize="15" Margin="10,0,0,0"
                                       FontFamily="{{DynamicResource BodyFont}}" FontWeight="SemiBold"
                                       Foreground="{{DynamicResource TextPrimaryBrush}}"/>
                        </StackPanel>'''
    else:
        valcell = f'''<TextBlock x:Name="{val}" Grid.Column="1" Text="-- --" FontSize="15"
                                   FontFamily="{{DynamicResource BodyFont}}" FontWeight="SemiBold"
                                   Foreground="{{DynamicResource TextPrimaryBrush}}" VerticalAlignment="Center"/>'''
    row = f'''                    <Grid{'>' if last else ' Margin="0,0,0,16">'}>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="120"/><ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock x:Name="{lbl}" Text="{txt}" FontSize="13.5" FontFamily="{{DynamicResource UiFont}}"
                                   Foreground="{{DynamicResource TextSecondaryBrush}}" VerticalAlignment="Center"/>
                        {valcell}
                    </Grid>'''
    parts.append(row)
    if not last:
        parts.append('                    <Rectangle Height="1" Fill="{DynamicResource DividerBrush}" Opacity="0.5" Margin="0,0,0,16"/>')
parts.append('                </StackPanel>\n')
new_status = '\n'.join(parts)
s = s[:start] + new_status + s[end:]

open(p, 'w', encoding='utf-8').write(s)
print('xaml redesigned')
