---
name: "WPF UI Carbon Tech Theming"
description: "Best practices for UI styling in WPF, specifically Carbon Tech dark themes, custom buttons, checkboxes, and perfect SVG icon rendering via Viewbox."
---

# Успешные методы UI (WPF) в проекте RenderPard

В проекте RenderPard была выработана единая стратегия построения премиального, современного интерфейса на базе WPF. При любой модификации UI следует придерживаться этих принципов.

## 1. Отрисовка SVG-иконок без искажений
Обычное использование свойства `Stretch="Uniform"` напрямую на теге `<Path>` в WPF может привести к сжатию или искажению пропорций иконки, если ее внутренние координаты не центрированы.

**Идеальный метод (Используется в RenderPard):**
Всегда оборачивайте SVG-путь в `Viewbox` + `Canvas` с фиксированным размером холста (обычно 24x24).
```xml
<Viewbox Width="16" Height="16" Stretch="Uniform">
    <Canvas Width="24" Height="24">
        <Path Data="M..." Fill="{StaticResource AppTextBrush}"/>
    </Canvas>
</Viewbox>
```

## 2. Кнопки-призраки (Ghost Buttons) и Анимации
Вместо стандартных серых кнопок Windows WPF, мы используем прозрачные кнопки с кастомным шаблоном. Эффект наведения достигается с помощью ColorAnimation и EventTrigger.

**Пример стилизации кнопки (Carbon Tech):**
- Фон: `#0f1012` или `Transparent`
- Рамка: `#2a2d35`
- Акцент: `#f39c12` (Amber/Оранжевый)
```xml
<Style TargetType="Button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="{StaticResource AppBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <!-- Плавное появление -->
                    <EventTrigger RoutedEvent="MouseEnter">
                        <BeginStoryboard>
                            <Storyboard>
                                <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)" To="#1AFFFFFF" Duration="0:0:0.2"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                    <!-- Плавное затухание -->
                    <EventTrigger RoutedEvent="MouseLeave">
                        <BeginStoryboard>
                            <Storyboard>
                                <ColorAnimation Storyboard.TargetProperty="(Border.Background).(SolidColorBrush.Color)" Duration="0:0:0.2"/>
                            </Storyboard>
                        </BeginStoryboard>
                    </EventTrigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

## 3. Стилизация Чекбоксов
Стандартные CheckBox в WPF выглядят устаревшими. Мы переопределяем их `Template`, заменяя галочку на кастомный `<Path>`, который плавно меняет непрозрачность (`Opacity`).
- Фон пустого квадрата: `#16181c`
- Внутренняя заливка при активации: `rgba(243, 156, 18, 0.2)`
- Галочка: `#f39c12`
Это позволяет создавать современные премиальные интерфейсы без использования тяжелых UI-фреймворков.
