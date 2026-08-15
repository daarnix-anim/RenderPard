using System.Windows;
using System;
using System.Globalization;
using System.Windows.Data;
using RenderPard.Core.Models;

namespace RenderPard.UI
{
    public class EnumToLocalizedStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var key = $"Enum_{value.GetType().Name}_{value}";
            var localized = System.Windows.Application.Current.TryFindResource(key) as string;
            return localized ?? value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return true;
        }
    }
    
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseXdcamVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenderPard.Core.Models.VideoCodec codec && codec == RenderPard.Core.Models.VideoCodec.XdcamHd422)
            {
                return System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class VideoOnlyAndNotXdcamVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenderPard.Core.Models.Preset preset)
            {
                bool isVideo = preset.Container != RenderPard.Core.Models.ContainerFormat.Gif && !preset.IsImagePreset;
                bool isXdcam = preset.VideoCodec == RenderPard.Core.Models.VideoCodec.XdcamHd422;
                return (isVideo && !isXdcam) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class MaxLongSideVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenderPard.Core.Models.Preset preset)
            {
                if (preset.IsImagePreset || preset.IsMXF || preset.VideoCodec == RenderPard.Core.Models.VideoCodec.XdcamHd422)
                {
                    return System.Windows.Visibility.Collapsed;
                }
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class VideoSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVideo)
            {
                return isVideo ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            if (value is RenderPard.Core.Models.ContainerFormat format)
            {
                if (format == RenderPard.Core.Models.ContainerFormat.Gif || 
                    format == RenderPard.Core.Models.ContainerFormat.MXF ||
                    format == RenderPard.Core.Models.ContainerFormat.Jpeg ||
                    format == RenderPard.Core.Models.ContainerFormat.Png ||
                    format == RenderPard.Core.Models.ContainerFormat.Webp ||
                    format == RenderPard.Core.Models.ContainerFormat.Mp3 ||
                    format == RenderPard.Core.Models.ContainerFormat.Wav ||
                    format == RenderPard.Core.Models.ContainerFormat.Ogg ||
                    format == RenderPard.Core.Models.ContainerFormat.Flac ||
                    format == RenderPard.Core.Models.ContainerFormat.Aac)
                {
                    return System.Windows.Visibility.Collapsed;
                }
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class GifVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenderPard.Core.Models.ContainerFormat format && format == RenderPard.Core.Models.ContainerFormat.Gif)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class PresetTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isImage && isImage)
            {
                // Material Design Image Icon
                return "M21,19V5c0-1.1-0.9-2-2-2H5C3.9,3,3,3.9,3,5v14c0,1.1,0.9,2,2,2h14C20.1,21,21,20.1,21,19z M8.5,13.5l2.5,3.01L14.5,12l4.5,6H5L8.5,13.5z";
            }
            // Material Design Movie Icon
            return "M18,4l2,4h-3l-2-4h-2l2,4h-3l-2-4H8l2,4H7L5,4H4c-1.1,0-1.99,0.9-1.99,2L2,18c0,1.1,0.9,2,2,2h16c1.1,0,2-0.9,2-2V4H18z";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ImageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isImage && isImage) return System.Windows.Visibility.Visible;
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseImageVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isImage && isImage) return System.Windows.Visibility.Collapsed;
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class VideoOnlyVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RenderPard.Core.Models.ContainerFormat format)
            {
                if (format == RenderPard.Core.Models.ContainerFormat.Mp4 || format == RenderPard.Core.Models.ContainerFormat.WebM)
                    return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class IconNameToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconName && !string.IsNullOrEmpty(iconName))
            {
                if (System.IO.Path.IsPathRooted(iconName) && System.IO.File.Exists(iconName))
                {
                    try 
                    { 
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(iconName, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    } 
                    catch { }
                }

                // Check custom icons directory in AppData
                string customIconsDir = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "RenderPard", "CustomIcons");
                string customIco = System.IO.Path.Combine(customIconsDir, iconName + ".ico");
                if (System.IO.File.Exists(customIco))
                {
                    try 
                    { 
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(customIco, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    } 
                    catch { }
                }

                // Check built-in Icons directory
                string iconsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Icons");
                string icoPath = System.IO.Path.Combine(iconsDir, iconName + ".ico");
                if (System.IO.File.Exists(icoPath))
                {
                    try 
                    { 
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(icoPath, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    } 
                    catch { }
                }

                // Fallback to video.ico
                string fallback = System.IO.Path.Combine(iconsDir, "video.ico");
                if (System.IO.File.Exists(fallback))
                {
                    try 
                    { 
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.UriSource = new Uri(fallback, UriKind.Absolute);
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    } 
                    catch { }
                }

                return RenderPard.UI.IconGenerator.GetIconImageSource(iconName);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImageSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isImage && isImage)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class AudioSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isAudio && isAudio)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class AudioEncodeSettingsVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AudioMode mode && mode == AudioMode.Encode)
            {
                return System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BooleanToPlayPauseTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPlaying && isPlaying)
                return "❚❚";
            return "▶";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}


