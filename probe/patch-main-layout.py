# -*- coding: utf-8 -*-
p = r'D:\Desktop\Codex-Continue\src\MainWindow.xaml'
s = open(p, encoding='utf-8').read()

# 1) tab bar taller (generous tabs breathe)
s = s.replace('<Grid Grid.Row="1" Height="52" Panel.ZIndex="5">', '<Grid Grid.Row="1" Height="58" Panel.ZIndex="5">')

# 2) quota band: tight stack — % hugs the bar, label sits right above the bar,
#    time MIDDLE-aligned with the bar (no floating caption)
start = s.index('        <!-- ============ quota band')
end = s.index('        <!-- ============ content: status | command')
new_band = '''        <!-- ============ quota band (pure white, recessed) ============ -->
        <Grid Grid.Row="2" Height="184" ClipToBounds="True" Background="{DynamicResource WellBrush}">
            <StackPanel Margin="36,12,36,12" VerticalAlignment="Center">
                <!-- hero numeral hugs the label + bar below -->
                <StackPanel Orientation="Horizontal">
                    <TextBlock x:Name="QuotaPct" Text="—"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="68"
                               Typography.NumeralAlignment="Tabular"
                               Foreground="{DynamicResource QuotaBlueBrush}"
                               VerticalAlignment="Bottom"/>
                    <TextBlock x:Name="QuotaSign" Text="%"
                               FontFamily="{DynamicResource DisplayFont}" FontSize="30"
                               Foreground="{DynamicResource QuotaBlueBrush}"
                               VerticalAlignment="Bottom" Margin="3,0,0,14"/>
                </StackPanel>
                <!-- caption sits directly above the bar -->
                <TextBlock x:Name="QuotaLabel" Text="五小时窗口额度" FontSize="13"
                           FontFamily="{DynamicResource UiFont}"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           Margin="2,2,0,4"/>
                <!-- bar row: time centered on the bar's middle -->
                <Grid Height="42">
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

# 3) panels: more breathing room at right + bottom
s = s.replace('<Grid Grid.Column="0" Margin="28,24,24,24">', '<Grid Grid.Column="0" Margin="28,26,24,28">')
s = s.replace('<Grid Grid.Column="1" Margin="24,24,28,24">', '<Grid Grid.Column="1" Margin="28,26,36,28">')

# 4) action row up + status below it (room for the running status line)
s = s.replace('''                <!-- running status (red, above buttons) -->
                <TextBlock Grid.Row="4" x:Name="RunStatus" Text="" FontSize="12.5" TextWrapping="Wrap"
                           FontFamily="{DynamicResource BodyFont}"
                           Foreground="{DynamicResource ErrBrush}" HorizontalAlignment="Right"
                           TextAlignment="Right" Margin="0,0,0,10" Visibility="Collapsed"/>

                <!-- conversation + action buttons -->
                <Grid Grid.Row="5">''',
'''                <!-- conversation + action buttons -->
                <Grid Grid.Row="4">''')
s = s.replace('''                    <Button Grid.Column="2" x:Name="BtnRun" Style="{StaticResource NeuBtnPrimary}" Content="运行"
                            MinWidth="104" Click="BtnRun_Click"/>
                </Grid>
            </Grid>
        </Grid>''',
'''                    <Button Grid.Column="2" x:Name="BtnRun" Style="{StaticResource NeuBtnPrimary}" Content="运行"
                            MinWidth="104" Click="BtnRun_Click"/>
                </Grid>

                <!-- running status (red, at the very bottom right, below the buttons) -->
                <TextBlock Grid.Row="5" x:Name="RunStatus" Text="" FontSize="12.5" TextWrapping="Wrap"
                           FontFamily="{DynamicResource BodyFont}" MinHeight="22"
                           Foreground="{DynamicResource ErrBrush}" HorizontalAlignment="Right"
                           TextAlignment="Right" Margin="0,10,0,0" Visibility="Collapsed"/>
            </Grid>
        </Grid>''')

# 5) ComboBox: same-surface raised (drop the hairline border for the borderless soft look)
s = s.replace('''                                        <Border x:Name="bd" Background="{DynamicResource WellBrush}" CornerRadius="10"
                                                BorderBrush="{DynamicResource InputBorderBrush}" BorderThickness="1"
                                                Padding="12,9">
                                            <Grid>''',
'''                                        <Border x:Name="bd" Background="{DynamicResource CanvasBrush}" CornerRadius="10"
                                                Padding="12,9">
                                            <Grid>''')
s = s.replace('''                                    <ControlTemplate.Triggers>
                                        <Trigger Property="IsMouseOver" Value="True">
                                            <Setter TargetName="bd" Property="BorderBrush" Value="{DynamicResource TextTertiaryBrush}"/>
                                        </Trigger>
                                    </ControlTemplate.Triggers>
                                </ControlTemplate>
                            </ComboBox.Template>''',
'''                                </ControlTemplate>
                            </ComboBox.Template>''')

open(p, 'w', encoding='utf-8').write(s)
print('main layout patched')
