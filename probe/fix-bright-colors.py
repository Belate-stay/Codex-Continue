# -*- coding: utf-8 -*-
# brighten quota red/blue + accent per user: 鲜红 not deep red, 亮蓝 not deep blue
p = r'D:\Desktop\Codex-Continue\src\Themes\Light.xaml'
s = open(p, encoding='utf-8').read()
s = s.replace('<Color x:Key="Accent">#2F7FD5</Color>', '<Color x:Key="Accent">#3B92FF</Color>')
s = s.replace('<Color x:Key="AccentHover">#256EB8</Color>', '<Color x:Key="AccentHover">#5AA4FF</Color>')
s = s.replace('<Color x:Key="AccentPressed">#1F5FA3</Color>', '<Color x:Key="AccentPressed">#2E7FE0</Color>')
s = s.replace('<Color x:Key="QuotaBlue">#2F7FD5</Color>', '<Color x:Key="QuotaBlue">#3B92FF</Color>')
s = s.replace('<Color x:Key="QuotaYellow">#E3A008</Color>', '<Color x:Key="QuotaYellow">#F5B301</Color>')
s = s.replace('<Color x:Key="QuotaRed">#FF3B30</Color>', '<Color x:Key="QuotaRed">#FF2D2D</Color>')
s = s.replace('<Color x:Key="Err">#FF3B30</Color>', '<Color x:Key="Err">#FF2D2D</Color>')
open(p, 'w', encoding='utf-8').write(s)

p = r'D:\Desktop\Codex-Continue\src\Themes\Dark.xaml'
s = open(p, encoding='utf-8').read()
s = s.replace('<Color x:Key="Accent">#4C8DDA</Color>', '<Color x:Key="Accent">#5CA8FF</Color>')
s = s.replace('<Color x:Key="AccentHover">#5B9BE6</Color>', '<Color x:Key="AccentHover">#78B7FF</Color>')
s = s.replace('<Color x:Key="AccentPressed">#3D7BC8</Color>', '<Color x:Key="AccentPressed">#4A93EC</Color>')
s = s.replace('<Color x:Key="QuotaBlue">#4C8DDA</Color>', '<Color x:Key="QuotaBlue">#5CA8FF</Color>')
s = s.replace('<Color x:Key="QuotaYellow">#E8B04B</Color>', '<Color x:Key="QuotaYellow">#F5C55C</Color>')
s = s.replace('<Color x:Key="QuotaRed">#FF6961</Color>', '<Color x:Key="QuotaRed">#FF5F56</Color>')
s = s.replace('<Color x:Key="Err">#FF6961</Color>', '<Color x:Key="Err">#FF5F56</Color>')
open(p, 'w', encoding='utf-8').write(s)
print('colors brightened')
