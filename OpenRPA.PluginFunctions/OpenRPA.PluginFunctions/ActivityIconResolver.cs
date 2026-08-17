using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Toolbox;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Resolves toolbox icons:
    /// live ToolboxItemWrapper → ToolboxBitmap → ToolboxItem.Bitmap → WF DrawingBrush → Designer.Icon.
    /// </summary>
    internal static class ActivityIconResolver
    {
        private static readonly Dictionary<Type, ImageSource> Cache = new Dictionary<Type, ImageSource>();
        private static readonly object Gate = new object();

        private static Dictionary<string, ImageSource> _toolboxIndex;
        private static int _toolboxIndexCount = -1;

        public static ImageSource Resolve(Type activityType)
        {
            if (activityType == null)
            {
                return null;
            }

            lock (Gate)
            {
                ImageSource cached;
                if (Cache.TryGetValue(activityType, out cached) && cached != null)
                {
                    return cached;
                }

                ImageSource created = Create(activityType);
                if (created != null)
                {
                    Cache[activityType] = created;
                }

                return created;
            }
        }

        private static ImageSource Create(Type activityType)
        {
            ImageSource fromToolbox = FromLiveToolbox(activityType);
            if (fromToolbox != null)
            {
                return fromToolbox;
            }

            ImageSource fromAttr = FromToolboxBitmapAttribute(activityType);
            if (fromAttr != null)
            {
                return fromAttr;
            }

            ImageSource fromToolboxItem = FromToolboxItem(activityType);
            if (fromToolboxItem != null)
            {
                return fromToolboxItem;
            }

            ImageSource fromBrush = FromWorkflowDesignerBrush(activityType);
            if (fromBrush != null)
            {
                return fromBrush;
            }

            return FromActivityDesignerIcon(activityType);
        }

        private static ImageSource FromLiveToolbox(Type activityType)
        {
            try
            {
                EnsureToolboxIndex();
                if (_toolboxIndex == null || _toolboxIndex.Count == 0)
                {
                    return null;
                }

                string fullName = activityType.FullName;
                ImageSource image;
                if (!string.IsNullOrEmpty(fullName) && _toolboxIndex.TryGetValue(fullName, out image))
                {
                    return image;
                }

                string aq = activityType.AssemblyQualifiedName;
                if (!string.IsNullOrEmpty(aq) && _toolboxIndex.TryGetValue(aq, out image))
                {
                    return image;
                }

                // Display-name fallback within same assembly (rare duplicate names).
                string displayKey = BuildDisplayKey(activityType.Assembly.GetName().Name, activityType.Name);
                if (_toolboxIndex.TryGetValue(displayKey, out image))
                {
                    return image;
                }
            }
            catch
            {
            }

            return null;
        }

        private static void EnsureToolboxIndex()
        {
            ToolboxControl toolbox = ToolboxAccess.FindToolboxControl();
            if (toolbox == null)
            {
                return;
            }

            int count = 0;
            try
            {
                if (toolbox.Categories != null)
                {
                    foreach (ToolboxCategory category in toolbox.Categories)
                    {
                        if (category == null)
                        {
                            continue;
                        }

                        count += category.Tools != null ? category.Tools.Count : 0;
                    }
                }
            }
            catch
            {
                return;
            }

            if (_toolboxIndex != null && _toolboxIndexCount == count && count > 0)
            {
                return;
            }

            var index = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (ToolboxCategory category in toolbox.Categories)
                {
                    if (category == null || category.Tools == null)
                    {
                        continue;
                    }

                    foreach (ToolboxItemWrapper wrapper in category.Tools)
                    {
                        if (wrapper == null)
                        {
                            continue;
                        }

                        ImageSource image = null;
                        try
                        {
                            Bitmap bitmap = wrapper.Bitmap;
                            if (bitmap != null)
                            {
                                image = ToImageSource(bitmap);
                            }
                        }
                        catch
                        {
                        }

                        if (image == null)
                        {
                            continue;
                        }

                        Type type = wrapper.Type;
                        if (type != null)
                        {
                            if (!string.IsNullOrEmpty(type.FullName))
                            {
                                index[type.FullName] = image;
                            }

                            if (!string.IsNullOrEmpty(type.AssemblyQualifiedName))
                            {
                                index[type.AssemblyQualifiedName] = image;
                            }

                            string asm = type.Assembly != null ? type.Assembly.GetName().Name : null;
                            index[BuildDisplayKey(asm, type.Name)] = image;
                        }

                        if (!string.IsNullOrEmpty(wrapper.ToolName))
                        {
                            index[wrapper.ToolName] = image;
                        }
                    }
                }
            }
            catch
            {
            }

            _toolboxIndex = index;
            _toolboxIndexCount = count;
        }

        private static string BuildDisplayKey(string assemblyName, string typeName)
        {
            return (assemblyName ?? string.Empty) + "|" + (typeName ?? string.Empty);
        }

        private static ImageSource FromToolboxBitmapAttribute(Type activityType)
        {
            try
            {
                ToolboxBitmapAttribute tba = null;
                object[] attrs = activityType.GetCustomAttributes(typeof(ToolboxBitmapAttribute), true);
                if (attrs != null && attrs.Length > 0)
                {
                    tba = attrs[0] as ToolboxBitmapAttribute;
                }

                if (tba == null)
                {
                    AttributeCollection collection = TypeDescriptor.GetAttributes(activityType);
                    tba = collection[typeof(ToolboxBitmapAttribute)] as ToolboxBitmapAttribute;
                }

                if (tba == null)
                {
                    return null;
                }

                using (Image image = tba.GetImage(activityType, false) ?? tba.GetImage(activityType, true))
                {
                    return ToImageSource(image);
                }
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource FromToolboxItem(Type activityType)
        {
            try
            {
                var item = new ToolboxItem(activityType);
                Bitmap bitmap = item.Bitmap;
                if (bitmap == null)
                {
                    return null;
                }

                return ToImageSource(bitmap);
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource FromActivityDesignerIcon(Type activityType)
        {
            try
            {
                var designerAttr = TypeDescriptor.GetAttributes(activityType)[typeof(DesignerAttribute)] as DesignerAttribute;
                if (designerAttr == null || string.IsNullOrWhiteSpace(designerAttr.DesignerTypeName))
                {
                    return null;
                }

                Type designerType = Type.GetType(designerAttr.DesignerTypeName, false);
                if (designerType == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            designerType = assembly.GetType(designerAttr.DesignerTypeName.Split(',')[0].Trim(), false);
                        }
                        catch
                        {
                            continue;
                        }

                        if (designerType != null)
                        {
                            break;
                        }
                    }
                }

                if (designerType == null || !typeof(ActivityDesigner).IsAssignableFrom(designerType))
                {
                    return null;
                }

                if (designerType.GetConstructor(Type.EmptyTypes) == null)
                {
                    return null;
                }

                var designer = Activator.CreateInstance(designerType) as ActivityDesigner;
                if (designer == null)
                {
                    return null;
                }

                object icon = designer.Icon;
                var brush = icon as DrawingBrush;
                if (brush != null)
                {
                    return RenderBrush(brush, 16, 16);
                }

                return icon as ImageSource;
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource FromWorkflowDesignerBrush(Type activityType)
        {
            try
            {
                DrawingBrush brush = FindDesignerBrush(activityType);
                if (brush == null)
                {
                    return null;
                }

                return RenderBrush(brush, 16, 16);
            }
            catch
            {
                return null;
            }
        }

        private static DrawingBrush FindDesignerBrush(Type activityType)
        {
            string[] keys =
            {
                activityType.Name + "Icon",
                activityType.Name + "IconBrush",
                GetMappedIconKey(activityType)
            };

            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                DrawingBrush brush = TryGetBrush(key);
                if (brush != null)
                {
                    return brush;
                }
            }

            return null;
        }

        private static string GetMappedIconKey(Type activityType)
        {
            try
            {
                Type helper = Type.GetType(
                    "System.Activities.Presentation.Utility.IconHelper, System.Activities.Presentation",
                    false);
                if (helper == null)
                {
                    return null;
                }

                MethodInfo method = helper.GetMethod(
                    "GetIconResourceKey",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (method == null)
                {
                    return null;
                }

                return method.Invoke(null, new object[] { activityType.FullName }) as string;
            }
            catch
            {
                return null;
            }
        }

        private static DrawingBrush TryGetBrush(string key)
        {
            DrawingBrush brush = LookupBrush(Application.Current != null ? Application.Current.Resources : null, key);
            if (brush != null)
            {
                return brush;
            }

            try
            {
                Type iconsType = typeof(WorkflowDesignerIcons);
                PropertyInfo dictProp = iconsType.GetProperty(
                    "IconResourceDictionary",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (dictProp != null)
                {
                    var dict = dictProp.GetValue(null, null) as ResourceDictionary;
                    brush = LookupBrush(dict, key);
                    if (brush != null)
                    {
                        return brush;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static DrawingBrush LookupBrush(ResourceDictionary dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            if (dictionary.Contains(key))
            {
                return dictionary[key] as DrawingBrush;
            }

            foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
            {
                DrawingBrush nested = LookupBrush(merged, key);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static ImageSource RenderBrush(DrawingBrush brush, int width, int height)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }

            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            return target;
        }

        private static ImageSource ToImageSource(Image image)
        {
            if (image == null)
            {
                return null;
            }

            // Normalize to 32bpp ARGB — some toolbox bitmaps fail GetHbitmap / look blank otherwise.
            using (var normalized = new Bitmap(
                Math.Max(1, image.Width),
                Math.Max(1, image.Height),
                System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(normalized))
                {
                    g.Clear(System.Drawing.Color.Transparent);
                    g.DrawImage(image, 0, 0, normalized.Width, normalized.Height);
                }

                IntPtr hBitmap = normalized.GetHbitmap();
                try
                {
                    BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
