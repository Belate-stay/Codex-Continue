# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\MainWindow.xaml'
s = open(p, encoding='utf-8').read()

# 1) quota band: [% label] side by side, both hugging the bar; bigger numerals
start = s.index('        <!-- ============ quota band')
end = s.index('        <!-- ============ content: status | command')
new_band = '''        <!-- ============ quota band (pure white, recessed) ============ -->
        <Grid Grid.Row="2" Height="150" ClipToBounds="True" Background="{DynamicResource WellBrush}">
            <StackPanel Margin="36,10,36,10" VerticalAlignment="Center">
                <!-- big numeral + label side by side, hugging each other and the bar below -->
                <StackPanel Orientation="Horizontal" VerticalAlignment="Bottom">
                    <TextBlock x:Name="QuotaPct" Text="—"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="76"
                               LineHeight="72" LineStackingStrategy="BlockLineHeight"
                               Typography.NumeralAlignment="Tabular"
                               Foreground="{DynamicResource QuotaBlueBrush}"
                               VerticalAlignment="Bottom"/>
                    <TextBlock x:Name="QuotaSign" Text="%"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="34"
                               Foreground="{DynamicResource QuotaBlueBrush}"
                               VerticalAlignment="Bottom" Margin="3,0,0,11"/>
                    <TextBlock x:Name="QuotaLabel" Text="五小时窗口额度" FontSize="13"
                               FontFamily="{DynamicResource BodyFont}"
                               Foreground="{DynamicResource TextSecondaryBrush}"
                               VerticalAlignment="Bottom" Margin="14,0,0,9"/>
                </StackPanel>
                <!-- bar row: time MIDDLE-aligned with the bar -->
                <Grid Height="42" Margin="0,2,0,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <Grid x:Name="QuotaTrack" Grid.Column="0" Height="10" VerticalAlignment="Center"
                          SizeChanged="QuotaTrack_SizeChanged">
                        <Border Background="{DynamicResource TrackBrush}" CornerRadius="5">
                            <Border.Effect>
                                <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                  BlurRadius="3" Opacity="0.55"/>
                            </Border.Effect>
                        </Border>
                        <Border x:Name="QuotaFill" HorizontalAlignment="Left" Width="0"
                                Background="{DynamicResource QuotaBlueBrush}" CornerRadius="5"/>
                    </Grid>
                    <TextBlock Grid.Column="1" x:Name="QuotaTime" Text="--:--"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="34"
                               Foreground="{DynamicResource TextPrimaryBrush}"
                               Typography.NumeralAlignment="Tabular"
                               VerticalAlignment="Center" Margin="32,0,0,0"
                               ToolTip="检测时刻"/>
                </Grid>
            </StackPanel>
            <!-- recessed seam: soft inner shading, no border lines -->
            <Rectangle Height="8" VerticalAlignment="Top" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                        <GradientStop Color="#1F000000" Offset="0"/>
                        <GradientStop Color="#00000000" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
            <Rectangle Height="6" VerticalAlignment="Bottom" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,1" EndPoint="0,0">
                        <GradientStop Color="#16000000" Offset="0"/>
                        <GradientStop Color="#00000000" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
        </Grid>

'''
s = s[:start] + new_band + s[end:]

# 2) status rows: NO cards, plain rows, light font
start = s.index('                <Grid Grid.Row="1" Margin="0,10,0,0">')
end = s.index('            </Grid>\n\n            <!-- hairline divider between panels -->')
rows = [
    ('LblWindow', 'Codex 窗口', True, 'ValWindow', '在线'),
    ('LblAccount', '账号名称', False, 'ValAccount', '-- --'),
    ('LblConvos', '运行对话', False, 'ValConvos', '0'),
    ('LblQuota', '额度状态', False, 'ValQuota', '正常'),
    ('LblReset', '恢复时间', False, 'ValReset', '-- --'),
    ('LblSource', '检测来源', False, 'ValSource', '-- --'),
]
parts = ['                <Grid Grid.Row="1" Margin="0,10,0,0">',
         '                    <Grid.RowDefinitions>',
         '                        <RowDefinition Height="*"/>',
         '                        <RowDefinition Height="Auto"/>',
         '                        <RowDefinition Height="*"/>',
         '                        <RowDefinition Height="Auto"/>',
         '                        <RowDefinition Height="*"/>',
         '                        <RowDefinition Height="Auto"/>',
         '                        <RowDefinition Height="*"/>',
         '                        <RowDefinition Height="Auto"/>',
         '                        <RowDefinition Height="*"/>',
         '                        <RowDefinition Height="Auto"/>',
         '                        <RowDefinition Height="*"/>',
         '                    </Grid.RowDefinitions>']
for i, (lbl, txt, dot, val, vtxt) in enumerate(rows):
    row_idx = i * 2
    if dot:
        valcell = f'''<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                            <Ellipse x:Name="WindowDot" Width="8" Height="8" Fill="{{DynamicResource OkBrush}}" VerticalAlignment="Center"/>
                            <TextBlock x:Name="{val}" Text="{vtxt}" FontSize="14" Margin="9,0,0,0"
                                       FontFamily="{{DynamicResource BodyFont}}"
                                       Foreground="{{DynamicResource TextPrimaryBrush}}"/>
                        </StackPanel>'''
    else:
        valcell = f'''<TextBlock x:Name="{val}" Grid.Column="1" Text="{vtxt}" FontSize="14"
                                   FontFamily="{{DynamicResource BodyFont}}"
                                   Foreground="{{DynamicResource TextPrimaryBrush}}" VerticalAlignment="Center"/>'''
    row = f'''                    <Grid Grid.Row="{row_idx}">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="112"/><ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock x:Name="{lbl}" Text="{txt}" FontSize="13"
                                   FontFamily="{{DynamicResource BodyFont}}"
                                   Foreground="{{DynamicResource TextSecondaryBrush}}" VerticalAlignment="Center"/>
                        {valcell}
                    </Grid>'''
    parts.append(row)
    if i < len(rows) - 1:
        parts.append(f'                    <Rectangle Grid.Row="{row_idx + 1}" Height="1" Fill="{{DynamicResource HairlineBrush}}" Opacity="0.6" Margin="0,5,0,5"/>')
parts.append('                </Grid>')
new_status = '\n'.join(parts)
s = s[:start] + new_status + '\n' + s[end:]

# 3) panels: generous margins (left/right/bottom breathing)
s = s.replace('<Grid Grid.Column="0" Margin="28,26,24,28">', '<Grid Grid.Column="0" Margin="32,26,28,36">')
s = s.replace('<Grid Grid.Column="1" Margin="28,26,36,28">', '<Grid Grid.Column="1" Margin="32,26,36,36">')

# 4) action row UP: spacer goes below everything, action row follows attachments with fixed gap
s = s.replace('''                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" x:Name="TitleCommand"''',
'''                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <TextBlock Grid.Row="0" x:Name="TitleCommand"''')
s = s.replace('<!-- conversation + action buttons -->\n                <Grid Grid.Row="4">',
              '<!-- conversation + action buttons -->\n                <Grid Grid.Row="3" Margin="0,22,0,0">')
s = s.replace('''                <!-- running status (red, at the very bottom right, below the buttons) -->
                <TextBlock Grid.Row="5" x:Name="RunStatus" Text="" FontSize="12.5" TextWrapping="Wrap"
                           FontFamily="{DynamicResource BodyFont}" MinHeight="22"
                           Foreground="{DynamicResource ErrBrush}" HorizontalAlignment="Right"
                           TextAlignment="Right" Margin="0,10,0,0" Visibility="Collapsed"/>''',
'''                <!-- running status (red, below the buttons) -->
                <TextBlock Grid.Row="4" x:Name="RunStatus" Text="" FontSize="12.5" TextWrapping="Wrap"
                           FontFamily="{DynamicResource BodyFont}" MinHeight="22"
                           Foreground="{DynamicResource ErrBrush}" HorizontalAlignment="Right"
                           TextAlignment="Right" Margin="0,10,0,0" Visibility="Collapsed"/>''')

# 5) value/convo weights: drop the heavy SemiBold everywhere in main layout
s = s.replace(' FontWeight="SemiBold"\n                                       Foreground="{DynamicResource TextPrimaryBrush}"/>',
              '\n                                       Foreground="{DynamicResource TextPrimaryBrush}"/>')
s = s.replace('''                        <TextBlock x:Name="ConvoName" Text="-- --" FontSize="13.5" Margin="12,0,0,0"
                                   FontFamily="{DynamicResource BodyFont}" FontWeight="SemiBold"''',
'''                        <TextBlock x:Name="ConvoName" Text="-- --" FontSize="13.5" Margin="12,0,0,0"
                                   FontFamily="{DynamicResource BodyFont}"''')
s = s.replace('VerticalAlignment="Center" Margin="12,0,0,0"\n                                   FontFamily="{DynamicResource BodyFont}" FontWeight="SemiBold"',
              'VerticalAlignment="Center" Margin="12,0,0,0"\n                                   FontFamily="{DynamicResource BodyFont}"')

open(p, 'w', encoding='utf-8').write(s)
print('main layout final fixes done')
