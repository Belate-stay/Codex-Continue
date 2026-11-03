# -*- coding: utf-8 -*-
# very faint lavender canvas + faint cream band — barely distinguishable, nothing vivid
p = r'D:\Desktop\Codex-Continue\src\Themes\Light.xaml'
s = open(p, encoding='utf-8').read()
s = s.replace('<Color x:Key="Canvas">#EEEBF5</Color>', '<Color x:Key="Canvas">#F4F3F7</Color>')
s = s.replace('<Color x:Key="Well">#FFFFFF</Color>', '<Color x:Key="Well">#FAF8F2</Color>')
s = s.replace('<Color x:Key="Pill">#EEEBF5</Color>', '<Color x:Key="Pill">#F4F3F7</Color>')
s = s.replace('<Color x:Key="Hairline">#DFDAEE</Color>', '<Color x:Key="Hairline">#E7E4ED</Color>')
s = s.replace('<Color x:Key="SurfaceHover">#E7E2F3</Color>', '<Color x:Key="SurfaceHover">#EEECF3</Color>')
s = s.replace('<Color x:Key="SurfacePressed">#DDD7EC</Color>', '<Color x:Key="SurfacePressed">#E4E1EC</Color>')
s = s.replace('<Color x:Key="CaptionHover">#E4DFF2</Color>', '<Color x:Key="CaptionHover">#ECEAF2</Color>')
s = s.replace('<Color x:Key="NeuDark">#C9C2DC</Color>', '<Color x:Key="NeuDark">#D6D3E0</Color>')
s = s.replace('<Color x:Key="Track">#E8E8E8</Color>', '<Color x:Key="Track">#EAE7DF</Color>')
open(p, 'w', encoding='utf-8').write(s)

p = r'D:\Desktop\Codex-Continue\src\Themes\Dark.xaml'
s = open(p, encoding='utf-8').read()
s = s.replace('<Color x:Key="Canvas">#211E2B</Color>', '<Color x:Key="Canvas">#24222C</Color>')
s = s.replace('<Color x:Key="Well">#2B2836</Color>', '<Color x:Key="Well">#2C2A26</Color>')
s = s.replace('<Color x:Key="Pill">#211E2B</Color>', '<Color x:Key="Pill">#24222C</Color>')
s = s.replace('<Color x:Key="Hairline">#3A3548</Color>', '<Color x:Key="Hairline">#3B3844</Color>')
s = s.replace('<Color x:Key="SurfaceHover">#2A2635</Color>', '<Color x:Key="SurfaceHover">#2D2B36</Color>')
s = s.replace('<Color x:Key="SurfacePressed">#342F42</Color>', '<Color x:Key="SurfacePressed">#37343F</Color>')
s = s.replace('<Color x:Key="CaptionHover">#2E2A3A</Color>', '<Color x:Key="CaptionHover">#2F2D38</Color>')
s = s.replace('<Color x:Key="NeuDark">#14121C</Color>', '<Color x:Key="NeuDark">#17151E</Color>')
s = s.replace('<Color x:Key="NeuLight">#4E4960</Color>', '<Color x:Key="NeuLight">#504C5E</Color>')
s = s.replace('<Color x:Key="Track">#3A3548</Color>', '<Color x:Key="Track">#3A3733</Color>')
open(p, 'w', encoding='utf-8').write(s)
print('soft faint palette applied')
