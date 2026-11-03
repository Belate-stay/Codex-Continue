# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\MainWindow.xaml'
s = open(p, encoding='utf-8').read()

# ---- status rows: hairline list -> raised white strips ----
start = s.index('                <Grid Grid.Row="1" Margin="0,12,0,0">')
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
                                       FontFamily="{{DynamicResource BodyFont}}" FontWeight="SemiBold"
                                       Foreground="{{DynamicResource TextPrimaryBrush}}"/>
                        </StackPanel>'''
    else:
        valcell = f'''<TextBlock x:Name="{val}" Grid.Column="1" Text="{vtxt}" FontSize="14"
                                   FontFamily="{{DynamicResource BodyFont}}" FontWeight="SemiBold"
                                   Foreground="{{DynamicResource TextPrimaryBrush}}" VerticalAlignment="Center"/>'''
    row = f'''                    <Grid Grid.Row="{row_idx}">
                        <ContentControl Style="{{StaticResource NeuPill}}" Padding="16,11">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="112"/><ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <TextBlock x:Name="{lbl}" Text="{txt}" FontSize="13"
                                           FontFamily="{{DynamicResource UiFont}}"
                                           Foreground="{{DynamicResource TextSecondaryBrush}}" VerticalAlignment="Center"/>
                                {valcell}
                            </Grid>
                        </ContentControl>
                    </Grid>'''
    parts.append(row)
    if i < len(rows) - 1:
        parts.append(f'                    <Grid Grid.Row="{row_idx + 1}" Height="10"/>')
parts.append('                </Grid>')
new_status = '\n'.join(parts)
s = s[:start] + new_status + '\n' + s[end:]

# ---- well seams: whisper -> readable recession ----
s = s.replace('''            <Rectangle Height="3" VerticalAlignment="Top" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                        <GradientStop Color="#12000000" Offset="0"/>
                        <GradientStop Color="#00000000" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>
            <Rectangle Height="3" VerticalAlignment="Bottom" IsHitTestVisible="False">
                <Rectangle.Fill>
                    <LinearGradientBrush StartPoint="0,1" EndPoint="0,0">
                        <GradientStop Color="#0E000000" Offset="0"/>
                        <GradientStop Color="#00000000" Offset="1"/>
                    </LinearGradientBrush>
                </Rectangle.Fill>
            </Rectangle>''',
'''            <Rectangle Height="8" VerticalAlignment="Top" IsHitTestVisible="False">
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
            </Rectangle>''')

# ---- progress track: flat gray -> sunken feel (darker inset + white lip) ----
s = s.replace('''                        <Border Background="{DynamicResource TrackBrush}" CornerRadius="5"/>''',
'''                        <Border Background="{DynamicResource TrackBrush}" CornerRadius="5">
                            <Border.Effect>
                                <DropShadowEffect Color="{DynamicResource NeuDark}" ShadowDepth="0"
                                                  BlurRadius="3" Opacity="0.6"/>
                            </Border.Effect>
                        </Border>''')

open(p, 'w', encoding='utf-8').write(s)
print('status strips + well recession done')
